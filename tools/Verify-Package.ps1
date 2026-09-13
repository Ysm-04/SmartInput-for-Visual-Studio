param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-ZipXml {
    param($Archive, [string]$EntryName)
    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) { throw "VSIX is missing required file: $EntryName" }
    $stream = $entry.Open()
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.Load($stream)
        return $document
    }
    finally { $stream.Dispose() }
}

function Read-ZipText {
    param($Archive, [string]$EntryName)
    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) { throw "VSIX is missing required file: $EntryName" }
    $stream = $entry.Open()
    $reader = [System.IO.StreamReader]::new($stream)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose(); $stream.Dispose() }
}

function Read-FileXml {
    param([string]$Path)
    $document = [System.Xml.XmlDocument]::new()
    $document.Load($Path)
    return $document
}

function Select-RequiredNode {
    param($Document, [string]$Query, $NamespaceManager, [string]$Name)
    $node = $Document.SelectSingleNode($Query, $NamespaceManager)
    if ($null -eq $node) { throw "Manifest node was not found: $Name" }
    return $node
}

function Test-AssemblyResourceKey {
    param([string]$AssemblyPath, [string]$Key)
    $assembly = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
    foreach ($resourceName in $assembly.GetManifestResourceNames()) {
        $stream = $assembly.GetManifestResourceStream($resourceName)
        if ($null -eq $stream) { continue }
        $reader = $null
        try {
            $reader = [System.Resources.ResourceReader]::new($stream)
            foreach ($entry in $reader) {
                if ($entry.Key -eq $Key) { return $true }
            }
        }
        catch [System.ArgumentException] {
        }
        finally {
            if ($null -ne $reader) { $reader.Dispose() }
            $stream.Dispose()
        }
    }
    return $false
}

$workspacePath = Split-Path -Parent $PSScriptRoot
$packagePath = Join-Path $workspacePath "src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.vsix"
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName })
    foreach ($required in @('extension.vsixmanifest', 'SmartInput.VisualStudio.dll', 'SmartInput.Core.dll', 'SmartInput.VisualStudio.pkgdef')) {
        if ($names -notcontains $required) { throw "VSIX is missing required file: $required" }
    }
    $allowedLibraries = @('SmartInput.VisualStudio.dll', 'SmartInput.Core.dll')
    $unexpectedLibraries = @($names | Where-Object { $_ -like '*.dll' -and $allowedLibraries -notcontains $_ })
    if ($unexpectedLibraries.Count -gt 0) { throw "VSIX contains unexpected DLLs: $unexpectedLibraries" }
    $manifest = Read-ZipXml -Archive $archive -EntryName 'extension.vsixmanifest'
    $ns = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $ns.AddNamespace('v', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
    $target = Select-RequiredNode -Document $manifest -Query '//v:InstallationTarget' -NamespaceManager $ns -Name 'InstallationTarget'
    if ($target.Version -ne '[17.14,)' -or $target.ProductArchitecture -ne 'amd64') { throw 'VSIX target version or architecture is invalid.' }
    $asset = Select-RequiredNode -Document $manifest -Query "//v:Asset[@Type='Microsoft.VisualStudio.MefComponent']" -NamespaceManager $ns -Name 'MEF asset'
    if ($asset.Path -ne 'SmartInput.VisualStudio.dll') { throw 'MEF entry is invalid.' }
    $packageAsset = Select-RequiredNode -Document $manifest -Query "//v:Asset[@Type='Microsoft.VisualStudio.VsPackage']" -NamespaceManager $ns -Name 'VS Package asset'
    if ($packageAsset.Path -ne 'SmartInput.VisualStudio.pkgdef') { throw 'VS Package entry is invalid.' }
    $pkgdef = Read-ZipText -Archive $archive -EntryName 'SmartInput.VisualStudio.pkgdef'
    $menuResourceName = 'SmartInputCommands.CTMENU'
    if (-not $pkgdef.Contains('[$RootKey$\Menus]') -or -not $pkgdef.Contains($menuResourceName)) { throw 'VS Package menu resource is missing.' }
    $generalPageName = [string]([char]0x5E38) + [string]([char]0x89C4)
    $generalOptionsKey = '[$RootKey$\ToolsOptionsPages\Smart Input\' + $generalPageName + ']'
    if (-not $pkgdef.Contains($generalOptionsKey)) { throw 'Smart Input options page is missing.' }
    $vsctPath = Join-Path $workspacePath 'src\SmartInput.VisualStudio\SmartInputCommands.vsct'
    $vsct = Read-FileXml -Path $vsctPath
    $vsctNs = [System.Xml.XmlNamespaceManager]::new($vsct.NameTable)
    $vsctNs.AddNamespace('c', 'http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable')
    $toolsGroupParent = Select-RequiredNode -Document $vsct -Query "//c:Group[@id='SmartInputToolsGroup']/c:Parent" -NamespaceManager $vsctNs -Name 'Smart Input tools group parent'
    if ($toolsGroupParent.guid -ne 'guidSHLMainMenu' -or $toolsGroupParent.id -ne 'IDM_VS_MENU_TOOLS') { throw 'Smart Input tools command group is not attached to the Tools menu.' }
    $identity = Select-RequiredNode -Document $manifest -Query '//v:Identity' -NamespaceManager $ns -Name 'Identity'
    $sourceManifestPath = Join-Path $workspacePath 'src\SmartInput.VisualStudio\source.extension.vsixmanifest'
    $sourceManifest = Read-FileXml -Path $sourceManifestPath
    $sourceNs = [System.Xml.XmlNamespaceManager]::new($sourceManifest.NameTable)
    $sourceNs.AddNamespace('v', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
    $expectedIdentity = Select-RequiredNode -Document $sourceManifest -Query '//v:Identity' -NamespaceManager $sourceNs -Name 'source Identity'
    if ($identity.Version -ne $expectedIdentity.Version -or $identity.Id -ne $expectedIdentity.Id) { throw 'VSIX version or identity does not match source.extension.vsixmanifest.' }
    foreach ($library in @('SmartInput.VisualStudio.dll', 'SmartInput.Core.dll')) {
        $stream = $archive.GetEntry($library).Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { $packedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        $builtPath = Join-Path $workspacePath "src\SmartInput.VisualStudio\bin\$Configuration\$library"
        if ($packedHash -ne (Get-FileHash -LiteralPath $builtPath -Algorithm SHA256).Hash) { throw "VSIX contains a stale assembly: $library" }
    }
    $pluginPath = Join-Path $workspacePath "src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.dll"
    $fileVersion = (Get-Item -LiteralPath $pluginPath).VersionInfo.FileVersion
    if ($fileVersion -ne "$($identity.Version).0") { throw 'Plugin assembly version does not match VSIX version.' }
    if (-not (Test-AssemblyResourceKey -AssemblyPath $pluginPath -Key $menuResourceName)) { throw 'Compiled VSCT menu resource is not embedded in the package assembly.' }
    Write-Host "PASS VSIX $($identity.Version): structure, MEF entry, VS Package/menu entries, options page, target, dependency isolation, and assemblies match ($($names.Count) entries)"
    Get-FileHash -LiteralPath $packagePath -Algorithm SHA256 | Select-Object Hash,Path
}
finally { $archive.Dispose() }
