<#
.SYNOPSIS
    将 Smart Input 手动部署到 32 位的 SQL Server Management Studio 18 / 19 / 20（即“SSMS 2022”这一代）。

.DESCRIPTION
    SSMS 18、19、20 基于 Visual Studio 2017 独立 Shell（VSSDK 15，32 位），不支持通过现代 VSIX 安装。
    需要把为 VSSDK 15 编译的 AnyCPU 程序集、手写的 .pkgdef 与 v1(2010) 清单平铺到
    Common7\IDE\Extensions 下的独立子目录，并清理私有注册表 / MEF 缓存后才会被发现。

    本脚本部署的文件来自 src\SmartInput.Ssms.Legacy\bin\Release（请先运行 build.ps1）。
    SSMS 22（2025，64 位）请改用 Deploy-SSMS22.ps1。

    脚本支持 -WhatIf 演练：加上 -WhatIf 时只打印将要执行的操作，不会删除、覆盖或写入
    任何文件（包括私有注册表与 MEF 缓存），也不要求管理员权限或关闭 SSMS。

    删除私有注册表 privateregistry.bin 之前，脚本会先把它自动备份到该版本缓存目录下的
    SmartInput-PrivateRegistry-Backup 子目录（文件名带时间戳），以便需要时恢复 Shell 设置。

    实际部署到 Program Files (x86) 需要管理员权限，请用“以管理员身份运行”的 PowerShell 执行：
        powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1
    先演练（不改任何内容）：
        powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -WhatIf

.PARAMETER Version
    要部署的 SSMS 主版本：18、19、20 或 All（默认 All，自动部署到所有检测到的版本）。

.PARAMETER Uninstall
    删除已部署扩展并清理缓存。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Version 20
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -WhatIf
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Uninstall
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('18', '19', '20', 'All')]
    [string]$Version = 'All',
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ExtensionName = 'SmartInput'
$ProgramFilesX86 = ${env:ProgramFiles(x86)}
if (-not $ProgramFilesX86) { $ProgramFilesX86 = 'C:\Program Files (x86)' }

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '部署到 Program Files (x86) 需要管理员权限。请用“以管理员身份运行”的 PowerShell 重新执行本脚本。'
    }
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
            $dir = Split-Path -Parent $Path
            if ($dir -and -not (Test-Path -LiteralPath $dir)) {
                New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop | Out-Null
            }
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

function Get-LegacyCatalog {
    $all = @('18', '19', '20')
    $versions = if ($Version -eq 'All') { $all } else { @($Version) }
    foreach ($major in $versions) {
        $ide = Join-Path $ProgramFilesX86 "Microsoft SQL Server Management Studio\$major\Common7\IDE"
        [pscustomobject]@{
            Major       = $major
            IdePath     = $ide
            Present     = Test-Path (Join-Path $ide 'Extensions')
            DataBase    = Join-Path $env:LOCALAPPDATA 'Microsoft\SQL Server Management Studio'
            DataPattern = "^$major\."
        }
    }
}

function Clear-LegacyCache {
    param(
        [Parameter(Mandatory = $true)][string]$DataBase,
        [Parameter(Mandatory = $true)][string]$DataPattern,
        [Parameter(Mandatory = $true)][string]$IdePath,
        [Parameter(Mandatory = $true)][System.Management.Automation.PSCmdlet]$Cmdlet
    )
    if (Test-Path $DataBase) {
        $dataDir = Get-ChildItem $DataBase -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match $DataPattern } | Select-Object -First 1
        if ($dataDir) {
            Write-Host "  处理缓存目录: $($dataDir.FullName)"
            Backup-PrivateRegistry -DataDir $dataDir.FullName -Cmdlet $Cmdlet
            foreach ($f in @('privateregistry.bin', 'privateregistry.bin.LOG1', 'privateregistry.bin.LOG2')) {
                Remove-PathSafe -Path (Join-Path $dataDir.FullName $f) -Description '删除私有注册表缓存' -Cmdlet $Cmdlet
            }
            Remove-PathSafe -Path (Join-Path $dataDir.FullName 'ComponentModelCache') -Recurse -Description '清理 MEF 组件缓存' -Cmdlet $Cmdlet
            Write-SignalSafe -Path (Join-Path $dataDir.FullName 'extensions.configurationchanged') -Description '写入用户目录扩展变更标记' -Cmdlet $Cmdlet
        } else {
            Write-Host "  未发现匹配 $DataPattern 的用户缓存目录，跳过私有注册表/MEF 清理。"
        }
    }
    Write-SignalSafe -Path (Join-Path $IdePath 'Extensions\extensions.configurationchanged') -Description '写入 IDE 目录扩展变更标记' -Cmdlet $Cmdlet
}

# -WhatIf 是只读演练：不需要管理员权限，也不必先关闭 SSMS。
if ($WhatIfPreference) {
    Write-Host '【-WhatIf 演练】仅显示将要执行的操作，不会修改/删除/写入任何文件，并跳过管理员与 SSMS 关闭检查。'
} else {
    Assert-Administrator
    Assert-SsmsClosed
}

$catalog = @(Get-LegacyCatalog | Where-Object { $_.Present })
if ($catalog.Count -eq 0) {
    if ($WhatIfPreference) {
        Write-Host "演练结束：未在 $ProgramFilesX86 下检测到 SSMS 18/19/20 安装，因此没有可展示的部署目标；未做任何更改。"
        return
    }
    throw "未在 $ProgramFilesX86 下找到 SSMS 18/19/20 安装。若安装在其它位置，请手动复制 src\SmartInput.Ssms.Legacy\bin\Release 下的文件到对应 Common7\IDE\Extensions\$ExtensionName。"
}

# Files to flatten (SSMS 18-20 needs the assembly(ies), a .pkgdef and a v1 extension.vsixmanifest).
$sourceDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\SmartInput.Ssms.Legacy\bin\Release'
# 从发布 zip 解压时，四个载荷文件与本脚本在同一目录，回退到脚本所在目录。
if (-not (Test-Path (Join-Path $sourceDir 'SmartInput.VisualStudio.dll')) -and
    (Test-Path (Join-Path $PSScriptRoot 'SmartInput.VisualStudio.dll'))) {
    $sourceDir = $PSScriptRoot
}
$requiredFiles = @(
    'SmartInput.VisualStudio.dll',
    'SmartInput.Core.dll',
    'SmartInput.VisualStudio.pkgdef',
    'extension.vsixmanifest'
)
if (-not $Uninstall -and -not $WhatIfPreference) {
    foreach ($name in $requiredFiles) {
        if (-not (Test-Path (Join-Path $sourceDir $name))) { throw "缺少部署文件: $name（请先运行 build.ps1 完成 Legacy 构建）。" }
    }
}

foreach ($entry in $catalog) {
    $targetDir = Join-Path $entry.IdePath "Extensions\$ExtensionName"
    Write-Host ""
    Write-Host "=== SSMS $($entry.Major) -> $targetDir ==="

    if ($Uninstall) {
        Remove-PathSafe -Path $targetDir -Recurse -Description '删除扩展目录' -Cmdlet $PSCmdlet
        Clear-LegacyCache -DataBase $entry.DataBase -DataPattern $entry.DataPattern -IdePath $entry.IdePath -Cmdlet $PSCmdlet
        continue
    }

    if ($PSCmdlet.ShouldProcess($targetDir, "重建扩展目录并平铺 $($requiredFiles.Count) 个文件")) {
        if (Test-Path $targetDir) { Remove-Item -LiteralPath $targetDir -Recurse -Force -ErrorAction Stop }
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        foreach ($name in $requiredFiles) {
            Copy-Item -LiteralPath (Join-Path $sourceDir $name) -Destination (Join-Path $targetDir $name) -Force -ErrorAction Stop
        }
        # Strip Mark-of-the-Web in case the build folder inherited it (SSMS refuses network-sourced DLLs).
        Get-ChildItem $targetDir -Recurse | Unblock-File
    }
    Write-Host '  将平铺以下文件：'
    $requiredFiles | ForEach-Object { Write-Host "    $_" }
    Clear-LegacyCache -DataBase $entry.DataBase -DataPattern $entry.DataPattern -IdePath $entry.IdePath -Cmdlet $PSCmdlet
}

Write-Host ""
if ($WhatIfPreference) {
    Write-Host '演练完成：未对 SSMS 18/19/20 做任何更改。确认无误后去掉 -WhatIf 重新执行以实际部署。'
} elseif ($Uninstall) {
    Write-Host '卸载完成。下次启动 SSMS 即生效。'
} else {
    Write-Host '部署完成。请启动 SSMS 并打开一个 .sql 查询窗口验证：'
    Write-Host '  - 代码 / [标识符] / "标识符" 区域应为英文；'
    Write-Host '  - -- 注释、/* 块注释 */ 与含汉字的字符串应为中文；'
    Write-Host '  - 编辑器底部状态栏可见 “Smart Input · …”，单击可暂停/恢复。'
    Write-Host '注：SSMS 2018-2022 这一代不提供“工具”菜单暂停命令，请使用底部状态栏按钮或选项页。'
}
