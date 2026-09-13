# Read-only interop smoke test. Does NOT invoke TrySet or activate/install any input method.
# Use Windows PowerShell 5.1 (STA, .NET Framework), not PowerShell 7.
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Please run with Windows PowerShell 5.1 -STA.' }
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
$workspacePath = Split-Path -Parent $PSScriptRoot
$binaryPath = Join-Path $workspacePath "src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.dll"
$assembly = [System.Reflection.Assembly]::LoadFrom($binaryPath)
$type = $assembly.GetType('SmartInput.VisualStudio.PinyinInputMethod', $true)
$adapterType = $assembly.GetType('SmartInput.VisualStudio.TsfInputMethodAdapter', $true)
$nativeType = $adapterType.GetNestedType('InputProcessorProfile', [System.Reflection.BindingFlags]::NonPublic)
$size = [System.Runtime.InteropServices.Marshal]::SizeOf([type]$nativeType)
if ([IntPtr]::Size -eq 8 -and $size -ne 88) { throw "Unexpected x64 TF_INPUTPROCESSORPROFILE layout: $size" }
$instance = [Activator]::CreateInstance($type, $true)
try {
    $property = $type.GetProperty('IsMicrosoftPinyin')
    $pinyin = $property.GetValue($instance, $null)
    [pscustomobject]@{
        StructureBytes = $size
        MicrosoftPinyinActiveForProbe = $pinyin
        ModeChanged = $false
        Note = 'Read-only probe context; does not validate the VS editor input context.'
    }
}
finally { ([IDisposable]$instance).Dispose() }
