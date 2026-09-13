# Read-only TSF profile probe. Does not activate, install, or change any input method.
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Please run with Windows PowerShell 5.1 -STA.' }

$workspacePath = Split-Path -Parent $PSScriptRoot
$binaryPath = Join-Path $workspacePath "src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.dll"
$assembly = [System.Reflection.Assembly]::LoadFrom($binaryPath)
$factoryType = $assembly.GetType('SmartInput.VisualStudio.InputMethodAdapterFactory', $true)
$factory = [Activator]::CreateInstance($factoryType, $true)
try {
    $active = $factoryType.GetMethod('GetActive').Invoke($factory, $null)
    if ($null -eq $active) {
        [pscustomobject]@{
            ActiveInputMethod = 'Unsupported or not available'
            IsForeground = $factoryType.GetProperty('IsForeground').GetValue($factory, $null)
            ModeChanged = $false
            Note = 'Read-only probe; no profile activation and no input-state write.'
        }
    }
    else {
        [pscustomobject]@{
            ActiveInputMethod = $active.DisplayName
            IsActive = $active.IsActive
            IsForeground = $active.IsForeground
            ModeChanged = $false
            Note = 'Profile detection only; no input-state write.'
        }
    }
}
finally {
    ([IDisposable]$factory).Dispose()
}
