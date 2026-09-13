param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipRestore,
    [string]$MSBuildPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Push-Location $PSScriptRoot
try {
    if (-not $MSBuildPath) {
        $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path -LiteralPath $vswherePath)) { throw 'vswhere was not found. Pass MSBuild.exe with -MSBuildPath.' }
        $MSBuildPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
    if (-not $MSBuildPath -or -not (Test-Path -LiteralPath $MSBuildPath)) { throw 'MSBuild was not found. VS 2022 17.14+ or compatible build tools and the .NET Framework 4.8 targeting pack are required.' }
    if (-not $SkipRestore) {
        & $MSBuildPath 'SmartInput.sln' /t:Restore /p:RestoreConfigFile=NuGet.Config /nr:false /v:minimal /nologo
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    }
    & $MSBuildPath 'SmartInput.sln' "/p:Configuration=$Configuration" /nr:false /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    $testPath = Join-Path $PSScriptRoot "tests\SmartInput.Tests\bin\$Configuration\SmartInput.Tests.exe"
    & $testPath
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
    & (Join-Path $PSScriptRoot 'tools\Verify-Package.ps1') -Configuration $Configuration
    Write-Host "Done. Package: src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.vsix"
    Write-Host 'The extension was not installed, VS was not launched, and system input-method settings were not changed.'
}
finally { Pop-Location }
