<#
.SYNOPSIS
    将 Smart Input 手动部署（平铺）到 SQL Server Management Studio 22（即 SSMS 2025，64 位），
    或卸载早先由本脚本部署的副本。

.DESCRIPTION
    SSMS 22 自带 VSIX Installer，通常直接运行 SmartInput.VisualStudio.vsix 即可安装，本脚本不是
    首选安装方式。以下情况使用本脚本：
      - VSIX Installer 未列出 SSMS 22，或安装失败；
      - 需要离线 / 集中部署，或修复“已安装但未加载”的 MEF 缓存问题；
      - 卸载早先由本脚本平铺部署的副本（-Uninstall）。
    SSMS 18/19/20（32 位 Legacy 包）不使用本脚本，请改用 Deploy-SSMS-Legacy.ps1。

    脚本把现代包（VSSDK 17，x64）构建出的 .vsix（本质是 zip）解压并平铺为
    SSMS 22 所需的文件，然后清理 MEF / 私有注册表缓存。

    脚本支持 -WhatIf 演练：加上 -WhatIf 时只打印将要执行的操作，不会删除、覆盖或写入
    任何文件（包括私有注册表与 MEF 缓存），也不要求管理员权限或关闭 SSMS。

    删除私有注册表 privateregistry.bin 之前，脚本会先把它自动备份到该版本缓存目录下的
    SmartInput-PrivateRegistry-Backup 子目录（文件名带时间戳），以便需要时恢复 Shell 设置。

    实际部署到 Program Files 需要管理员权限。请用“以管理员身份运行”的 PowerShell 执行：
        powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1
    先演练（不改任何内容）：
        powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -WhatIf

.PARAMETER SsmsIdeDir
    SSMS 22 的 Common7\IDE 目录。默认自动探测常见安装位置。

.PARAMETER VsixPath
    现代包 VSIX 路径。默认使用 src\SmartInput.VisualStudio\bin\Release 下的产物。

.PARAMETER ExtensionName
    Extensions 下的子目录名，默认 SmartInput。

.PARAMETER Uninstall
    删除已部署的扩展并清理缓存。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -WhatIf
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -Uninstall
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SsmsIdeDir,
    [string]$VsixPath,
    [string]$ExtensionName = 'SmartInput',
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '部署到 Program Files 需要管理员权限。请用“以管理员身份运行”的 PowerShell 重新执行本脚本。'
    }
}

function Find-Ssms22IdeDir {
    $candidates = @(
        Join-Path ${env:ProgramFiles} 'Microsoft SQL Server Management Studio\22\Release\Common7\IDE',
        Join-Path ${env:ProgramFiles} 'Microsoft SQL Server Management Studio\22\Common7\IDE'
    )
    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate 'Extensions')) { return $candidate }
    }
    return $null
}

function Assert-SsmsClosed {
    $processes = Get-Process -Name 'ssms','SsmsApp' -ErrorAction SilentlyContinue
    if ($processes) {
        throw '检测到 SSMS 正在运行。请先完全关闭 SSMS（包括查询窗口），再执行部署。'
    }
}

# 在 ShouldProcess 保护下删除文件/目录；-WhatIf 时只由 PowerShell 打印“What if:”，不落盘。
# 不使用 -ErrorAction SilentlyContinue：删除失败会直接抛出，避免最后误报“部署完成”。
function Remove-PathSafe {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description,
        [switch]$Recurse,
        [Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet
    )
    if (-not (Test-Path -LiteralPath $Path)) { return }
    if ($Cmdlet.ShouldProcess($Path, $Description)) {
        try {
            if ($Recurse) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop }
            else { Remove-Item -LiteralPath $Path -Force -ErrorAction Stop }
        }
        catch { throw "删除失败: $Path；$($_.Exception.Message)" }
    }
}

# 在 ShouldProcess 保护下写入/触碰 extensions.configurationchanged 标记文件。
function Write-SignalSafe {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description,
        [Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet
    )
    if ($Cmdlet.ShouldProcess($Path, $Description)) {
        try {
            New-Item -ItemType File -Path $Path -Force -ErrorAction Stop | Out-Null
            (Get-Item -LiteralPath $Path).LastWriteTime = Get-Date
        }
        catch { throw "写入失败: $Path；$($_.Exception.Message)" }
    }
}

# 删除私有注册表前先自动备份 privateregistry.bin（可能保存 SSMS Shell 的布局/设置）。
function Backup-PrivateRegistry {
    param(
        [Parameter(Mandatory = $true)][string]$DataDir,
        [Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet
    )
    $bin = Join-Path $DataDir 'privateregistry.bin'
    if (-not (Test-Path -LiteralPath $bin)) { return }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupDir = Join-Path $DataDir 'SmartInput-PrivateRegistry-Backup'
    $backupFile = Join-Path $backupDir ("privateregistry.$stamp.bin")
    if ($Cmdlet.ShouldProcess($backupFile, "备份私有注册表（源: $bin）")) {
        try {
            if (-not (Test-Path -LiteralPath $backupDir)) {
                New-Item -ItemType Directory -Path $backupDir -Force -ErrorAction Stop | Out-Null
            }
            Copy-Item -LiteralPath $bin -Destination $backupFile -Force -ErrorAction Stop
            Write-Host "  已备份私有注册表: $backupFile"
        }
        catch { throw "备份私有注册表失败: $bin；$($_.Exception.Message)" }
    }
}

function Clear-Ssms22Cache {
    param([Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet)
    # SSMS 22 的私有注册表与 MEF 组件缓存按版本目录存放。
    $roots = @(Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA 'Microsoft\SSMS') -Directory -Filter '22.0_*' -ErrorAction SilentlyContinue)
    if ($roots.Count -eq 0) {
        Write-Host '未发现 SSMS 22 用户缓存目录，跳过缓存清理。'
        return
    }
    foreach ($root in $roots) {
        Write-Host "处理缓存目录: $($root.FullName)"
        Backup-PrivateRegistry -DataDir $root.FullName -Cmdlet $Cmdlet
        foreach ($name in @('privateregistry.bin', 'privateregistry.bin.LOG1', 'privateregistry.bin.LOG2')) {
            Remove-PathSafe -Path (Join-Path $root.FullName $name) -Description '删除私有注册表缓存' -Cmdlet $Cmdlet
        }
        Remove-PathSafe -Path (Join-Path $root.FullName 'ComponentModelCache') -Recurse -Description '清理 MEF 组件缓存' -Cmdlet $Cmdlet
    }
}

# -WhatIf 是只读演练：不需要管理员权限，也不必先关闭 SSMS。
if ($WhatIfPreference) {
    Write-Host '【-WhatIf 演练】仅显示将要执行的操作，不会修改/删除/写入任何文件，并跳过管理员与 SSMS 关闭检查。'
} else {
    Assert-Administrator
    Assert-SsmsClosed
}

if (-not $SsmsIdeDir) { $SsmsIdeDir = Find-Ssms22IdeDir }
if (-not $SsmsIdeDir -or -not (Test-Path $SsmsIdeDir)) {
    throw '未能自动定位 SSMS 22 的 Common7\IDE 目录，请用 -SsmsIdeDir 显式指定。'
}
$extensionsDir = Join-Path $SsmsIdeDir 'Extensions'
$targetDir = Join-Path $extensionsDir $ExtensionName
Write-Host "SSMS IDE 目录 : $SsmsIdeDir"
Write-Host "扩展目标目录 : $targetDir"

if ($Uninstall) {
    Remove-PathSafe -Path $targetDir -Recurse -Description '删除扩展目录' -Cmdlet $PSCmdlet
    Clear-Ssms22Cache -Cmdlet $PSCmdlet
    if ($WhatIfPreference) {
        Write-Host '演练完成：未卸载任何文件。确认无误后去掉 -WhatIf 重新执行。'
    } else {
        Write-Host '卸载完成。下次启动 SSMS 即生效。'
    }
    return
}

if (-not $VsixPath) {
    $workspace = Split-Path -Parent $PSScriptRoot
    $buildOutput = Join-Path $workspace 'src\SmartInput.VisualStudio\bin\Release\SmartInput.VisualStudio.vsix'
    $alongsideScript = Join-Path $PSScriptRoot 'SmartInput.VisualStudio.vsix'
    # 优先源码树构建产物；若是从发布 zip 解压（VSIX 与脚本同目录），则回退到同目录 VSIX。
    if (Test-Path $buildOutput) { $VsixPath = $buildOutput }
    elseif (Test-Path $alongsideScript) { $VsixPath = $alongsideScript }
    else { $VsixPath = $buildOutput }
}
if (-not (Test-Path $VsixPath)) { throw "找不到 VSIX：$VsixPath；请先运行 build.ps1 完成构建，或用 -VsixPath 显式指定。" }

# SSMS 22 需要平铺的文件（VSIX 本质为 zip，根目录已平铺，Asset Path 为实际文件名）。
$requiredFiles = @(
    'SmartInput.VisualStudio.dll',
    'SmartInput.Core.dll',
    'SmartInput.VisualStudio.pkgdef',
    'extension.vsixmanifest',
    'manifest.json',
    'catalog.json'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('smartinput-ssms22-' + [Guid]::NewGuid().ToString('N'))
try {
    if ($PSCmdlet.ShouldProcess($targetDir, "解压 VSIX 并平铺 $($requiredFiles.Count) 个文件到 SSMS 22")) {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($VsixPath, $staging)
        foreach ($name in $requiredFiles) {
            if (-not (Test-Path (Join-Path $staging $name))) { throw "VSIX 中缺少部署所需文件: $name" }
        }
        if (Test-Path $targetDir) { Remove-Item -LiteralPath $targetDir -Recurse -Force -ErrorAction Stop }
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        foreach ($name in $requiredFiles) {
            Copy-Item -LiteralPath (Join-Path $staging $name) -Destination (Join-Path $targetDir $name) -Force -ErrorAction Stop
        }
        # Strip Mark-of-the-Web if the build folder inherited it (SSMS refuses network-sourced DLLs).
        Get-ChildItem $targetDir -Recurse | Unblock-File
    }
    Write-Host '将平铺以下文件：'
    $requiredFiles | ForEach-Object { Write-Host "  $_" }
}
finally {
    Remove-PathSafe -Path $staging -Recurse -Description '清理临时解压目录' -Cmdlet $PSCmdlet
}

Clear-Ssms22Cache -Cmdlet $PSCmdlet

# 通知 SSMS 扩展目录发生变化，触发重新扫描。
$signal = Join-Path $extensionsDir 'extensions.configurationchanged'
Write-SignalSafe -Path $signal -Description '写入扩展变更标记 extensions.configurationchanged' -Cmdlet $PSCmdlet

Write-Host ''
if ($WhatIfPreference) {
    Write-Host '演练完成：未对 SSMS 22 做任何更改。确认无误后去掉 -WhatIf 重新执行以实际部署。'
} else {
    Write-Host '部署完成。请启动 SSMS 22 并打开一个 .sql 查询窗口验证：'
    Write-Host '  - 代码 / [标识符] / "标识符" 区域应为英文；'
    Write-Host '  - -- 注释、/* 块注释 */ 与含汉字的字符串应为中文；'
    Write-Host '  - “工具”菜单应出现 Smart Input 暂停命令。'
}
