<#
.SYNOPSIS
    打包 Smart Input 的 SSMS 发布载荷，供 GitHub Release 直接上传。

.DESCRIPTION
    在运行 build.ps1 完成 Release 构建之后，本脚本生成两个互不依赖、可直接交付最终用户的压缩包：

      SmartInput-SSMS22.zip        面向 SSMS 22（2025，64 位）
          - SmartInput.VisualStudio.vsix   现代包（VSSDK 17，x64），SSMS 22 与 Visual Studio 共用
          - Deploy-SSMS22.ps1              兜底部署 / 卸载脚本
          - 安装说明.txt

      SmartInput-SSMS-Legacy.zip   面向 SSMS 18 / 19 / 20（32 位，VSSDK 15）
          - SmartInput.VisualStudio.dll
          - SmartInput.Core.dll
          - SmartInput.VisualStudio.pkgdef
          - extension.vsixmanifest
          - Deploy-SSMS-Legacy.ps1         部署 / 卸载脚本
          - 安装说明.txt

    SmartInput.VisualStudio.vsix 是 Visual Studio 2022/2026 与 SSMS 22 共用的安装包：SSMS 22
    自带 VSIX Installer，通常直接运行该 .vsix 即可安装。SmartInput-SSMS22.zip 只是把同一个
    VSIX 与兜底部署脚本、安装说明放在一起，便于离线或安装器未列出 SSMS 22 时使用。

.PARAMETER Configuration
    收集哪个配置的构建产物，默认 Release。

.PARAMETER OutputDir
    zip 输出目录，默认仓库根目录下的 artifacts（已被 .gitignore 忽略）。

.PARAMETER Build
    打包前先运行 build.ps1（含还原、构建、测试与双包校验）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
    powershell -ExecutionPolicy Bypass -File .\tools\Pack-Release.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Pack-Release.ps1 -Build
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'artifacts' }

if ($Build) {
    Write-Host '先运行 build.ps1 ...'
    $buildScript = Join-Path $repoRoot 'build.ps1'
    & $buildScript -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 失败，退出码: $LASTEXITCODE" }
}

$vsix = Join-Path $repoRoot "src\SmartInput.VisualStudio\bin\$Configuration\SmartInput.VisualStudio.vsix"
$legacyDir = Join-Path $repoRoot "src\SmartInput.Ssms.Legacy\bin\$Configuration"
$legacyFiles = @(
    'SmartInput.VisualStudio.dll',
    'SmartInput.Core.dll',
    'SmartInput.VisualStudio.pkgdef',
    'extension.vsixmanifest'
)
$deploy22 = Join-Path $PSScriptRoot 'Deploy-SSMS22.ps1'
$deployLegacy = Join-Path $PSScriptRoot 'Deploy-SSMS-Legacy.ps1'

function Require-File {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "缺少打包所需文件: $Path；请先运行 build.ps1 -Configuration $Configuration 完成构建。"
    }
}

# 只读校验：即使 -WhatIf 也能提前发现“尚未构建”。
Require-File $vsix
Require-File $deploy22
Require-File $deployLegacy
foreach ($name in $legacyFiles) { Require-File (Join-Path $legacyDir $name) }

$readme22 = @'
Smart Input —— SSMS 22（SQL Server Management Studio 2025，64 位）安装包
=====================================================================

包含文件：
  SmartInput.VisualStudio.vsix   现代扩展包（VSSDK 17，x64），SSMS 22 与 Visual Studio 共用。
  Deploy-SSMS22.ps1             兜底部署 / 卸载脚本。

推荐安装：直接运行 VSIX（SSMS 22 自带 VSIX Installer）
  1. 完全关闭 SSMS 22（包括所有查询窗口）。
  2. 双击（或右键“打开”）SmartInput.VisualStudio.vsix；若安装器列出多个产品目标，
     选择 SSMS 22 后安装。也可在“以管理员身份运行”的 PowerShell 中执行：
         & "<SSMS 22 安装目录>\Common7\IDE\VSIXInstaller.exe" ".\SmartInput.VisualStudio.vsix"
  3. 启动 SSMS 22 并打开一个 .sql 查询窗口：“工具”菜单出现 Smart Input 暂停命令、
     左下角显示状态栏即成功。

兜底安装（仅当 VSIX Installer 未列出 SSMS 22、安装失败，或安装后未加载时）
  1. 完全关闭 SSMS 22，右键“以管理员身份运行 PowerShell”，cd 到本解压目录。
  2. 建议先演练（只显示将要做什么，不改动系统）：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -WhatIf
  3. 正式部署（脚本会自动探测标准安装路径，并自动找到同目录的 VSIX）：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1
     若 SSMS 装在非系统盘 / 自定义目录，请显式指定 IDE 目录：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -SsmsIdeDir "D:\SQL Server Studio 22\Common7\IDE"

卸载：
  - 通过 VSIX Installer 安装的：在 VSIX Installer 的已安装列表卸载，或执行：
         & "<SSMS 22 安装目录>\Common7\IDE\VSIXInstaller.exe" /uninstall:SmartInput.VisualStudio.943daace-d510-4e75-b17c-715cf0602c01
  - 通过脚本手动部署的：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -Uninstall
  注意：VSIX 安装与手动部署二选一，不要对同一台机器重复部署。

说明：脚本在删除私有注册表缓存（privateregistry.bin）前会先自动备份到该缓存目录下的
SmartInput-PrivateRegistry-Backup 子目录（文件名带时间戳）。清缓存后首次启动可能重建 MEF 缓存，
稍慢属正常现象，极少数情况下窗口布局会被重置，可用上述备份恢复。
'@

$readmeLegacy = @'
Smart Input —— SSMS 18 / 19 / 20（“SSMS 2022”这一代，32 位，VSSDK 15）安装包
=====================================================================

包含文件（直接平铺，无需 VSIXInstaller）：
  SmartInput.VisualStudio.dll
  SmartInput.Core.dll
  SmartInput.VisualStudio.pkgdef
  extension.vsixmanifest
  Deploy-SSMS-Legacy.ps1       部署 / 卸载脚本。

安装步骤：
  1. 完全关闭对应版本的 SSMS。
  2. 右键“以管理员身份运行 PowerShell”，cd 到本解压目录。
  3. 建议先演练（只显示将要做什么，不改动系统）：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -WhatIf
  4. 正式部署（自动检测并部署到所有已安装的 18/19/20，脚本会自动找到同目录的载荷文件）：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1
     只部署某个版本（例如 20）：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Version 20
  5. 启动 SSMS 并打开 .sql 查询窗口：编辑器底部出现 “Smart Input · …” 状态栏即成功。
     这一代没有“工具”菜单命令，请用底部状态栏按钮或选项页暂停/恢复。

卸载：
         powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Uninstall

说明：脚本在删除私有注册表缓存（privateregistry.bin）前会先自动备份到该缓存目录下的
SmartInput-PrivateRegistry-Backup 子目录（文件名带时间戳）。
SSMS 22（2025，64 位）请改用 SmartInput-SSMS22.zip。
'@

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ReleaseZip {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Files,
        [Parameter(Mandatory = $true)][string]$ReadmeText,
        [Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet
    )
    if (-not $Cmdlet.ShouldProcess($ZipPath, '生成发布压缩包')) { return }
    $dir = Split-Path -Parent $ZipPath
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $fs = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::Create)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entryName in $Files.Keys) {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $Files[$entryName], $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
            $entry = $zip.CreateEntry('安装说明.txt')
            $stream = $entry.Open()
            try {
                $writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($true)))
                try { $writer.Write($ReadmeText) } finally { $writer.Dispose() }
            } finally { $stream.Dispose() }
        } finally { $zip.Dispose() }
    } finally { $fs.Dispose() }
}

$zip22 = Join-Path $OutputDir 'SmartInput-SSMS22.zip'
$zipLegacy = Join-Path $OutputDir 'SmartInput-SSMS-Legacy.zip'

New-ReleaseZip -ZipPath $zip22 -Cmdlet $PSCmdlet -ReadmeText $readme22 -Files ([ordered]@{
    'SmartInput.VisualStudio.vsix' = $vsix
    'Deploy-SSMS22.ps1'            = $deploy22
})

New-ReleaseZip -ZipPath $zipLegacy -Cmdlet $PSCmdlet -ReadmeText $readmeLegacy -Files ([ordered]@{
    'SmartInput.VisualStudio.dll'      = (Join-Path $legacyDir 'SmartInput.VisualStudio.dll')
    'SmartInput.Core.dll'              = (Join-Path $legacyDir 'SmartInput.Core.dll')
    'SmartInput.VisualStudio.pkgdef'   = (Join-Path $legacyDir 'SmartInput.VisualStudio.pkgdef')
    'extension.vsixmanifest'           = (Join-Path $legacyDir 'extension.vsixmanifest')
    'Deploy-SSMS-Legacy.ps1'           = $deployLegacy
})

if (-not $WhatIfPreference) {
    Write-Host ''
    Write-Host '发布载荷已生成：'
    foreach ($z in @($zip22, $zipLegacy)) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($z)
        try {
            Write-Host ("  {0}  ({1:N0} 字节, {2} 个条目)" -f $z, (Get-Item -LiteralPath $z).Length, $archive.Entries.Count)
            foreach ($e in $archive.Entries) { Write-Host "      - $($e.FullName)" }
        } finally { $archive.Dispose() }
    }
    Write-Host '请把这两个 zip 作为对应版本 GitHub Release 的资产上传。'
} else {
    Write-Host '演练完成：未生成任何压缩包。去掉 -WhatIf 重新执行以实际打包。'
}
