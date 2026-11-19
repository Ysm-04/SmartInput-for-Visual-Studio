# Smart Input for Visual Studio and SSMS

面向中文开发者的本地智能输入态切换扩展：在代码、注释、字符串和 XAML 标记之间移动光标时，自动调整微软拼音或搜狗拼音的中英文状态。

作者：**我在人间做废物**（[@Ysm-04](https://github.com/Ysm-04)）

[项目主页](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS) · [最新版本](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/latest) · [全部版本](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases) · [问题反馈](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/issues) · [提交贡献](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/pulls)

当前稳定版本为 **[v0.3.4](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/tag/0.3.4)**。扩展完全在本地运行，不联网，不采集源码、按键或候选词。

v0.3.4 为缺陷修复版，主要变化：

- **修复调试器窗口被状态按钮覆盖**：调试时的**监视 / 即时 / 快速监视**窗口表达式输入框不再被 Smart Input 状态按钮遮挡——扩展只接管真正的文件文档编辑器，不接管调试器表达式文本视图。
- **修复微软拼音手动切换到搜狗拼音后的识别问题**：此前状态栏显示正确但输入法未实际切换；搜狗的 IMM32 状态改为通过焦点窗口读写，并在输入法（Profile）切换后强制一次重同步写入。
- **新增 XAML 语言支持**：C# WPF 项目的 `.xaml` 文件按注释、属性值、元素文本内容自动切换中英文态。
- **新增可配置的状态栏背景色**：位于选项页“外观”分类，默认关闭、关闭时跟随 VS / SSMS 主题，与状态栏文字颜色相互独立、可分别设置。
- 构建脚本自动识别 VS 2022（v17.0）与 VS 2026（v18.0）的 VSSDK 目标目录，在只安装 VS 2026 的机器上也能正常产出 VSIX；自动化测试由 143 项增至 **177 项**。

## 快速下载

普通用户无需自行构建。请从 [最新版本页面](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/latest) 按宿主下载对应资产：

| 宿主 | 下载资产 | 安装方式 |
| --- | --- | --- |
| Visual Studio 2022 / 2026 | `SmartInput.VisualStudio.vsix` | 双击并通过 VSIX Installer 安装 |
| SSMS 22（2025，64 位） | `SmartInput-SSMS22.zip` | 解压后双击其中的 `SmartInput.VisualStudio.vsix`（SSMS 22 自带 VSIX Installer）；失败时运行 `Deploy-SSMS22.ps1` 兜底 |
| SSMS 18 / 19 / 20（32 位） | `SmartInput-SSMS-Legacy.zip` | 解压后运行其中的 `Deploy-SSMS-Legacy.ps1` |

> 两套 SSMS 安装包不能混用。SSMS 22 可直接用自带的 VSIX Installer 安装；SSMS 18/19/20 不通过 VSIX Installer 安装第三方扩展，部署前请先阅读下文的安装说明，并优先使用 `-WhatIf` 进行只读演练。

## 导航

- [功能演示](#功能演示)
- [主要功能](#主要功能)
- [支持范围](#支持范围)
- [交互规则](#交互规则)
- [安装与使用](#安装与使用)
- [代码结构](#代码结构)
- [构建与测试](#构建与测试)
- [兼容性与已知限制](#兼容性与已知限制)
- [项目由来与贡献者](#项目由来与贡献者)
- [参与贡献](#参与贡献)
- [许可证](#许可证)

## 功能演示

![Visual Studio Smart Input 自动切换输入法演示](docs/assets/smart-input-demo.gif)

![SSMS Smart Input 自动切换输入法演示](docs/assets/SSMS.gif)

![SSMS Smart Input 自动切换输入法演示](docs/assets/XAML.gif)

## 主要功能

- 根据代码、注释、字符串、XAML 标记等文本上下文自动选择中文或英文输入态。
- 支持 C、C++、C#、XAML 和 Transact-SQL（T-SQL）。
- 支持当前活动的微软拼音和搜狗拼音，不主动切换键盘布局或其他输入法。
- 用户手动切换后进入临时覆盖状态，不会在同一区域立即抢回输入态。
- 提供编辑器状态按钮、暂停/恢复命令、光标提示颜色、可配置的状态栏文字 / 背景色和可持久化选项。
- 有选区、多光标、输入法组词或编辑器失焦时暂停自动切换；调试器的监视 / 即时 / 快速监视表达式窗口不接管。
- 不修改源代码、不自动替换标点，不联网、不采集源码、按键或候选词。


## 支持范围

| 宿主 | 位数 / Shell | 安装方式 |
| --- | --- | --- |
| Visual Studio 2022 **17.14+** / Visual Studio 2026 | x64，VSSDK 17 | 安装 `SmartInput.VisualStudio.vsix` |
| **SSMS 22（即 SSMS 2025）** | x64，基于 VS 2026 Shell | 双击 `SmartInput.VisualStudio.vsix`（SSMS 22 自带 VSIX Installer，按用户安装、无需管理员）；失败时改用 `tools\Deploy-SSMS22.ps1` 手动部署（需管理员） |
| **SSMS 18 / 19 / 20（即“SSMS 2022”这一代 32 位版本）** | x86，基于 VS 2017 Shell（VSSDK 15） | Legacy 包手动部署，`tools\Deploy-SSMS-Legacy.ps1` |

- 系统：Windows 10 / Windows 11；需要 .NET Framework 4.8（VS、SSMS 19/20/22 的现代安装均已满足）。
- 语言：C、C++、C#、**XAML** 以及 **Transact-SQL（T-SQL）** 的可编辑文档 / 查询视图。
- 输入法：当前活动的**微软拼音或搜狗拼音**。扩展只切换其内部中英文状态，不安装、添加或主动切换键盘布局，也不控制其他输入法。

> **SSMS 22 可直接用自带的 VSIX Installer 安装**：现代 VSIX 清单已声明 `Microsoft.VisualStudio.Ssms [21.0,23.0)` amd64 安装目标，双击 / 运行 `SmartInput.VisualStudio.vsix` 即可由 SSMS 22 自带的 `VSIXInstaller.exe` 完成安装（安装器列出多个产品目标时选择 SSMS 22）。当前包按**当前用户**安装到 `%LocalAppData%\Microsoft\SSMS\22.0_<实例>\Extensions\<随机目录>`，**无需管理员权限**；建议安装前关闭 SSMS、安装后重启，通常无需手动清缓存。**SSMS 18 / 19 / 20（32 位，旧隔离 Shell）仍需手动平铺部署**：这些版本沿用旧隔离 Shell，微软对第三方 SSMS 扩展也不提供官方安装入口，本项目按社区通行做法，把扩展文件平铺到 SSMS 的 `Common7\IDE\Extensions` 目录并清理缓存完成安装（见下文“安装与使用”）。

## 交互规则

通用规则：

| 场景 | 自动输入态 |
| --- | --- |
| 代码、字符字面量 | 英文 |
| 单行、多行、三斜线注释正文 | 中文 |
| 包含汉字的字符串文本段 | 中文 |
| 空字符串、纯英文字符串 | 英文 |
| C# 插值表达式 | 英文；表达式内部的注释/字符串单独判断 |

Transact-SQL 规则（0.3.0 新增）：

| T-SQL 场景 | 自动输入态 |
| --- | --- |
| 关键字、表名 / 列名、负数、运算符等代码 | 英文 |
| `--` 单行注释、`/* ... */` 块注释正文（块注释**支持嵌套**） | 中文 |
| 含汉字的单引号字符串 `'...'` / `N'...'`（可跨多行） | 中文 |
| 字符串内的 `''` 转义引号按同一字符串处理 | 中文 / 英文随整体内容 |
| 空字符串、纯英文字符串、仅 emoji 的字符串 | 英文 |
| `[方括号标识符]`（含 `]]` 转义），即使其中是汉字 | 英文 |
| `"双引号标识符"`：`SET QUOTED_IDENTIFIER ON`（默认）时双引号界定标识符（含 `""` 转义），即使其中是汉字 | 英文 |
| `SET QUOTED_IDENTIFIER OFF` 之后的 `"双引号字符串"`：按字符串字面量处理，含汉字推荐中文、纯英文推荐英文 | 中文 / 英文随内容 |
| 字符串内部的 `--`、`/*` 不当作注释；括号标识符内部的引号 / `--` 不当作字符串 / 注释 | 随外层区域 |

XAML 规则（0.3.4 新增）：

| XAML 场景 | 自动输入态 |
| --- | --- |
| 元素名、属性名、命名空间、`<?xml ... ?>` 声明等标记代码 | 英文 |
| `<!-- ... -->` 注释正文（含注释内的标签文本） | 中文 |
| 双引号 / 单引号属性值，如 `Text="加载成功"`、`Tag="{Binding 显示名称}"` 等含汉字的值 | 中文 |
| 纯英文、数字、emoji 或实体转义（如 `&amp;`）属性值 | 英文 |
| 元素文本内容（如 `<TextBlock>纯文本内容</TextBlock>`）含汉字 | 中文 |
| `<![CDATA[...]]>` 文本段 | 中文 / 英文随内容 |
| 标签之间的缩进、换行等纯空白文本 | 保持代码（英文），不误报字符串 |

可打开仓库内的 `samples/Contexts.sql`、`samples/Contexts.xaml`（以及 `Contexts.cs`、`Contexts.cpp`）逐区域体验。T-SQL 没有字符字面量与字符串插值，反斜杠也不是转义符，因此单引号字符串可一直延续到闭合引号（允许跨行）。

启动时不修改系统默认输入法。第一次进入受支持编辑器时按当前场景初始化：代码为英文，注释为中文。启动页、搜索框、终端、调试器表达式窗口等不接管。

手动切换（包括 Shift）以**实际读到的输入态变化**为准，不拦截用户按键。搜狗输入法在普通状态写入无法确认时，仅发送一次已配置的 Shift 切换键作为兼容性恢复，并再次读取状态；微软拼音不模拟按键。偏离当前自动规则时保留手动选择并显示配置颜色光标；在同一文本区域持续输入不会立即被切回。切回推荐状态、离开该区域、切换文档或失焦后恢复自动规则。手动覆盖按文本区域而非固定秒数保持。

**不固定要求使用 Shift。** 请使用输入法设置中实际配置的中英文切换方式，例如微软拼音的 Ctrl＋空格或已启用的 Shift。如果系统没有启用 Shift，单按 Shift 不会切换，扩展也不会将它误判为手动切换。快捷键与宿主命令冲突时，以是否真正改变输入法状态为准。扩展不自动修改系统快捷键设置。

编辑器底部的 `Smart Input` 按钮显示状态；单击暂停/恢复所有文档的自动切换。在 Visual Studio 与 SSMS 22 中，暂停也可通过 `工具 > Smart Input: 暂停自动切换/恢复自动切换` 操作。**SSMS 18/19/20 的 Legacy 包不注册“工具”菜单命令**（该宿主不编译菜单资源），请使用底部状态栏按钮或选项页。暂停仅保存在当前宿主进程内，重启后恢复为未暂停。

长期设置位于 `工具 > 选项 > Smart Input > 常规`：

- 启用自动切换。
- 显示左下角状态栏。
- 手动覆盖光标颜色。
- 是否自定义状态栏文字颜色。
- 状态栏文字颜色。
- 是否自定义状态栏背景色（默认关闭）。
- 状态栏背景色（默认深灰 `#1E1E1E`；关闭自定义时跟随 VS / SSMS 主题）。

状态栏文字颜色与背景色相互独立，可分别设置；两项自定义都关闭时，状态按钮的文字与背景均跟随宿主主题。

安全边界：

- 组词期间不切换；有选区或多个光标时暂不切换。
- 只有本进程的编辑器获得键盘焦点时才发出切换请求。
- 每次切换后读取状态确认，600ms 未确认最多再尝试一次，仍未确认则停止当前区域内的重试。
- 搜狗输入法仅在一次状态确认失败时发送一次 Shift 恢复按键，不连续模拟，也不修改系统快捷键设置。
- 未识别出微软拼音/搜狗拼音或无法读取状态时不猜测、不操作。
- 超过 2M 字符的文件不自动分析；分析在后台进行，旧快照会取消。
- 不修改源文本，不自动替换标点；不联网、不采集源码、按键或候选词。
- 不写入“字体和颜色”或正文画刷；失焦、暂停、关闭文档时移除自定义光标装饰并恢复原生光标。
- 输入法（Profile）切换后会进行短时、幂等的重同步写入（约 3 次），并同时写入焦点编辑器窗口与宿主框架窗口的 IMM32 上下文，避免“状态栏正确但输入法未切换”；普通输入过程中不会重复写入。

## 安装与使用

所有公开安装包均以 [Di-Y](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases)  和 [我在人间做废物](https://github.com/Ysm-04/SmartInput-for-Visual-Studio/releases) 为准。两个 SSMS 压缩包均已包含部署脚本和对应载荷，解压后无需保留源码目录结构。

### Visual Studio 2022 / 2026

1. 从 [最新版本页面](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/latest) 下载 `SmartInput.VisualStudio.vsix`。
2. 双击文件并通过 VSIX Installer 选择目标 Visual Studio 实例。
3. 安装完成后重启 Visual Studio。

从源码构建时，VSIX 位于 `src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix`。

### SSMS 22（SSMS 2025，64 位）

**主路径：直接运行 / 双击 VSIX 安装。** SSMS 22 自带 `Common7\IDE\VSIXInstaller.exe`（与 VS 2026 同一代安装器），本扩展 VSIX 清单已声明 `Microsoft.VisualStudio.Ssms [21.0,23.0)` amd64 安装目标，因此可以直接用 VSIX Installer 安装（已在 SSMS 22.10.12210.168 实机核对）：

1. 建议先完全关闭 SSMS（任务管理器确认没有 `Ssms.exe`；安装器会提示重启目标实例后生效）。
2. 从 [最新版本页面](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/latest) 下载并解压 `SmartInput-SSMS22.zip`，双击其中的 `SmartInput.VisualStudio.vsix`；也可在普通 PowerShell 中直接调用 SSMS 自带的安装器（当前包按用户安装，**无需管理员权限**）：

```powershell
# 原样运行 SSMS 22 自带的 VSIX Installer（路径按实际安装位置调整）
& 'C:\Program Files\Microsoft SQL Server Management Studio\22\Release\Common7\IDE\VSIXInstaller.exe' '.\SmartInput.VisualStudio.vsix'
```

3. 若安装器列出了多个产品目标，选择 **SSMS 22**。
4. 安装完成后重启 SSMS，扩展与底部 `Smart Input` 状态按钮即会加载，一般无需再手动清理缓存。

安装位置与权限：当前包未启用 AllUsers，安装器按**当前用户**把文件放到 `%LocalAppData%\Microsoft\SSMS\22.0_<实例>\Extensions\<随机目录>`，**不需要管理员权限**；安装器通常同时列出 Visual Studio 2026 与 SSMS 22，按需勾选即可。建议安装前关闭 SSMS、安装后重启；VSIX 安装无需再手动清理 MEF / 私有注册表缓存。

卸载（三选一）：
- 在 VSIX Installer 的卸载入口中卸载 SSMS 22 下的本扩展；
- 命令行运行 SSMS 22 自带的卸载入口：`"<SSMS 22 安装目录>\Common7\IDE\VSIXInstaller.exe" /uninstall:SmartInput.VisualStudio.943daace-d510-4e75-b17c-715cf0602c01`（安装器同时装有 VS 2026 和 SSMS 22 时，可加 `/appName:"SQL Server Management Studio 22"` 限定目标；卸载同样无需管理员权限）；
- 用手动部署脚本 `Deploy-SSMS22.ps1 -Uninstall` 清理脚本平铺的副本。

**兜底路径：手动平铺部署。** 当 VSIX Installer 未列出 SSMS 22、安装失败，或扩展装了但未加载（缓存未刷新）时，先完全关闭 SSMS，然后用**“以管理员身份运行”**的 PowerShell 执行脚本；它也适合集中部署多台机器与缓存修复：

```powershell
# 在 Release 解压目录中只读演练（不改动系统，也无需管理员权限或关闭 SSMS）
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -WhatIf

# 正式安装（管理员 PowerShell）
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1

# 卸载（清理手动部署的副本；VSIX 安装的卸载见上文）
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -Uninstall
```

从源码部署时，将脚本路径改为 `.\tools\Deploy-SSMS22.ps1`；脚本会自动使用构建产生的现代 VSIX。

脚本会把现代 VSIX（本质是 zip）解压、平铺为 SSMS 22 需要的文件到
`C:\Program Files\Microsoft SQL Server Management Studio\22\Release\Common7\IDE\Extensions\SmartInput\`，
删除 `%LocalAppData%\Microsoft\SSMS\22.0_*\` 下的 `privateregistry.bin`（含 `.LOG1/.LOG2`）与 `ComponentModelCache`，并写入 `extensions.configurationchanged` 标记。删除 `privateregistry.bin` 前会先把它自动备份到该缓存目录下的 `SmartInput-PrivateRegistry-Backup` 子目录（文件名带时间戳），需要时可用于恢复 Shell 布局。

实机部署补充（SSMS 22，已在 22.10 上验证）：

- 即使 SSMS 安装在非系统盘（如自定义的 `D:\...`），其 `Common7\IDE\Extensions` 目录仍需管理员权限写入，请始终用“以管理员身份运行”的 PowerShell 执行脚本；它写入的是 `Program Files` 全局目录，因此需要管理员。**推荐路径的 VSIX 安装按当前用户写入 `%LocalAppData%\...`，无需管理员权限**。
- 脚本默认只自动探测 `C:\Program Files\...` 下的标准安装路径；若 SSMS 安装在自定义目录，请显式传入 IDE 目录，例如：

```powershell
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS22.ps1 -SsmsIdeDir 'D:\SQL Server Studio 22\Common7\IDE'
```

- 部署会清理 SSMS 的私有注册表与 MEF 缓存，首次重启可能需要重新确认一次界面布局；使用本地“Windows 身份验证”连接数据库引擎不需要任何 Microsoft / Azure 账号登录。
- VSIX 安装与手动平铺部署**二选一**，不要重复部署；重复部署会留下两份扩展副本，可能触发“包未正确加载”。

### SSMS 18 / 19 / 20（SSMS 2022 代，32 位）

1. 从 [最新版本页面](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/releases/latest) 下载并解压 `SmartInput-SSMS-Legacy.zip`。
2. 可先在普通 PowerShell 中运行 `-WhatIf`，确认本机检测到的 SSMS 版本和目标路径。
3. 正式部署前完全关闭对应版本的 SSMS，再用**“以管理员身份运行”**的 PowerShell 执行安装命令。

```powershell
# 在 Release 解压目录中只读演练
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -WhatIf

# 部署到检测到的所有 18/19/20 版本（管理员 PowerShell）
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1

# 只装某一版（18 / 19 / 20）
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Version 20

# 卸载
powershell -ExecutionPolicy Bypass -File .\Deploy-SSMS-Legacy.ps1 -Uninstall
```

从源码部署时，将脚本路径改为 `.\tools\Deploy-SSMS-Legacy.ps1`。

脚本把 Legacy 负载平铺到
`C:\Program Files (x86)\Microsoft SQL Server Management Studio {18|19|20}\Common7\IDE\Extensions\SmartInput\`，
并清理 `%LocalAppData%\Microsoft\SQL Server Management Studio\{版本}.0_IsoShell\` 下的私有注册表与 MEF 缓存。SSMS 只扫描 Program Files 下的全局扩展目录，不扫描当前用户目录；从 Release 的 `SmartInput-SSMS-Legacy.zip` 安装时，脚本与四个载荷文件在同一解压目录，把命令中的脚本路径改为 `.\Deploy-SSMS-Legacy.ps1` 即可；删除私有注册表前同样会先自动备份到 `SmartInput-PrivateRegistry-Backup`。

### 安装后检查

- `工具 > 选项 > Smart Input > 常规` 是否存在。
- Visual Studio / SSMS 22 的 `工具` 菜单中是否存在 Smart Input 暂停命令（SSMS 18-20 无此菜单，属预期）。
- 在 `工具 > 选项 > Smart Input > 常规` 的“外观”分类分别设置状态栏文字颜色与背景色：启用“自定义状态栏文字颜色”/“自定义状态栏背景色”后，底部 `Smart Input` 按钮的文字 / 背景色随之变化；关闭两项自定义后，文字与背景跟随 VS / SSMS 主题。
- 打开 C/C++、C#、`.xaml` 或 `.sql` 文件后，编辑器底部是否出现 `Smart Input` 状态按钮；调试器的监视 / 即时 / 快速监视窗口中不应出现该按钮。
- 在 `.sql` 窗口中，左下角状态条随光标位置变化：代码与 `[标识符]` / `"标识符"` 区域显示“英文 · 代码”，`--`、`/* */` 注释与含汉字字符串显示“中文 · 注释 / 字符串”。在 SSMS 22 + 搜狗拼音下实测，移动光标即会自动切换到对应中 / 英文态，手动按 Shift 也可正常切换，与推荐态不符时输入光标变为提示色。

若扩展未出现，请确认部署前 SSMS 已完全关闭、缓存文件确实被删除；也可用 `Ssms.exe -log` 启动并在 `%AppData%\Microsoft\SSMS\<版本>\ActivityLog.xml` 中检索错误。从浏览器下载的压缩包解压后如遇“已阻止加载网络位置程序集”，对扩展目录执行 `Get-ChildItem <扩展目录> -Recurse | Unblock-File`（部署脚本已自动处理）。

## 代码结构

- `src/SmartInput.Core`：无外部依赖的词法边界分析与手动覆盖状态机。`ContextAnalysis.cs` 内按语言分流，XAML 的 XML 标记扫描与 T-SQL、C/C++、C# 规则并列；`ImeResyncPolicy.cs` 负责输入法切换后的一次性重同步判定。
- `src/SmartInput.VisualStudio`：现代包（VSSDK 17，x64）。MEF 编辑器接入（只对真正的文档编辑器建会话，调试器表达式视图不接管）、选项页、工具菜单命令、后台快照分析、微软拼音/搜狗拼音状态读写、临时光标颜色、状态栏按钮；同时目标 Visual Studio 与 SSMS 22。
- `src/SmartInput.Ssms.Legacy`：SSMS 18/19/20 的 32 位包（VSSDK 15，net48，AnyCPU）。用 `<Compile Link>` 共享 `SmartInput.VisualStudio` 的全部源码，仅引用 VSSDK 15 程序集；`SmartInput.VisualStudio.pkgdef` 与 v1 `extension.vsixmanifest` 为手写维护。
- `tests/SmartInput.Tests`：可直接运行的控制台测试程序，失败返回非零退出码。
- `samples/Contexts.sql`、`Contexts.xaml`、`Contexts.cs`、`Contexts.cpp`：编辑器交互检查样例（不参与构建）。
- `tools/Deploy-SSMS22.ps1` / `tools/Deploy-SSMS-Legacy.ps1`：SSMS 手动部署 / 卸载与缓存清理；支持 `-WhatIf` 只读演练，删除私有注册表前自动备份。
- `tools/Pack-Release.ps1`：构建后收集产物，生成供 GitHub Release 上传的 `SmartInput-SSMS22.zip` 与 `SmartInput-SSMS-Legacy.zip`。
- `tools/Verify-Package.ps1` / `tools/Verify-LegacyPackage.ps1`：分别校验现代 VSIX 与 Legacy 负载，不加载或安装扩展。
- `docs/MANUAL-TESTS.md`：安装前提、实机验证步骤和回归项目。
- `docs/VALIDATION.md`：验证结果、兼容性和已知边界。
- `docs/RELEASE-NOTES.md`：发布说明和版本变化。
- `docs/PRIVACY.md`：本地处理与隐私边界说明。
- `tools/Inspect-Pinyin.ps1`、`tools/Inspect-InputMethods.ps1`：Windows PowerShell 5.1 下的只读检查，不执行输入态切换。
- `docs/CARET-FIX.md`：手动覆盖光标装饰层的技术原因、实现方式和验证边界。

后台词法扫描器负责引号、注释和插值的边界分析，VS 语法分类辅助排除非活动代码；它不是完整编译器。XAML 部分按 XML 标记扫描：`<!-- -->` 注释、引号属性值、元素文本与 `<![CDATA[]]>`，标签名 / 属性名按代码推荐英文；XAML 标记扩展（`{Binding ...}`）整体按属性值内容判断，复杂多绑定语法可能按普通文本处理。T-SQL 部分覆盖注释、单引号字符串、括号标识符与双引号定界内容的常见写法；双引号按文本中出现的 `SET QUOTED_IDENTIFIER ON/OFF` 顺序判定（默认 ON：双引号为标识符；OFF：双引号为字符串）。由于 `QUOTED_IDENTIFIER` 本质是连接 / 批处理 / 运行时设置，存储过程、触发器内或跨 `GO` 批处理中由运行时决定、静态文本无法确定的取值，仍按默认 ON 处理；非常见的宿主专有语法也可能存在识别偏差。分类信息尚未更新时，可能暂时按词法结果判断。

## 构建与测试

构建需要 MSBuild（Visual Studio 2022 17.14+ 或兼容版本）和 .NET Framework 4.8 目标包。**不需要单独安装 .NET SDK，也不要求预装 Visual Studio 扩展开发工作负载**；VSSDK 17、VSSDK 15 和 VSIX 构建依赖由 NuGet 还原到项目的 `.packages` 目录。

工程会自动使用当前 MSBuild 所在 Visual Studio 的 VSSDK 目标目录：VS 2022 为 `Microsoft\VisualStudio\v17.0`，VS 2026 为 `v18.0`（0.3.4 起自动识别）。因此在只安装 VS 2026 的机器上同样可以产出 VSIX；若两者都不存在，VSSDK 导入会被跳过且**不会生成 VSIX**，请改用带有 VSSDK 的 MSBuild 或安装对应工作负载。

```powershell
# 还原、Release 构建、运行测试并校验两套包
.\build.ps1 -Configuration Release

# 已还原依赖时可离线构建
.\build.ps1 -Configuration Release -SkipRestore

# 也可显式指定 MSBuild
.\build.ps1 -Configuration Release -MSBuildPath 'C:\Path\To\MSBuild.exe'
```

脚本不会安装扩展、启动 Visual Studio / SSMS 或修改系统输入法。一次构建会产生两套不可互换的二进制：

- 现代包（x64，VSSDK 17）：`src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix`，用于 Visual Studio 2022/2026 与 SSMS 22。
- Legacy 包（AnyCPU，绑定 VSSDK 15）：`src/SmartInput.Ssms.Legacy/bin/Release/`，用于 32 位 SSMS 18/19/20。

`tools/Verify-Package.ps1` 校验现代 VSIX，`tools/Verify-LegacyPackage.ps1` 校验 Legacy 负载（文件齐全、AnyCPU、引用程序集确为 15.x、pkgdef 与 v1 清单要素完整）。制作发布附件时运行：

```powershell
.\tools\Pack-Release.ps1 -Build
```

生成的 `SmartInput-SSMS22.zip`（现代 VSIX + `Deploy-SSMS22.ps1` + 安装说明，其中 VSIX 在 SSMS 22 上可直接双击安装）和 `SmartInput-SSMS-Legacy.zip`（Legacy 四个平铺文件 + `Deploy-SSMS-Legacy.ps1` + 安装说明）位于 `artifacts/`。完整验证记录见 [docs/VALIDATION.md](docs/VALIDATION.md)，实机回归项目见 [docs/MANUAL-TESTS.md](docs/MANUAL-TESTS.md)。

## 构建与测试

构建需要 MSBuild（Visual Studio 2022 17.14+ 或兼容版本）和 .NET Framework 4.8 目标包。**不需要单独安装 .NET SDK，也不要求预装 Visual Studio 扩展开发工作负载**；VSSDK 17、VSSDK 15 和 VSIX 构建依赖由 NuGet 还原到项目的 `.packages` 目录。

```powershell
# 还原、Release 构建、运行测试并校验两套包
.\build.ps1 -Configuration Release

# 已还原依赖时可离线构建
.\build.ps1 -Configuration Release -SkipRestore

# 也可显式指定 MSBuild
.\build.ps1 -Configuration Release -MSBuildPath 'C:\Path\To\MSBuild.exe'
```

脚本不会安装扩展、启动 Visual Studio / SSMS 或修改系统输入法。一次构建会产生两套不可互换的二进制：

- 现代包（x64，VSSDK 17）：`src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix`，用于 Visual Studio 2022/2026 与 SSMS 22。
- Legacy 包（AnyCPU，绑定 VSSDK 15）：`src/SmartInput.Ssms.Legacy/bin/Release/`，用于 32 位 SSMS 18/19/20。

`tools/Verify-Package.ps1` 校验现代 VSIX，`tools/Verify-LegacyPackage.ps1` 校验 Legacy 负载。制作发布附件时运行：

```powershell
.\tools\Pack-Release.ps1 -Build
```

生成的 `SmartInput-SSMS22.zip` 和 `SmartInput-SSMS-Legacy.zip` 位于 `artifacts/`。完整验证记录见 [docs/VALIDATION.md](docs/VALIDATION.md)，实机回归项目见 [docs/MANUAL-TESTS.md](docs/MANUAL-TESTS.md)。

## 兼容性与已知限制

截至 v0.3.4，**177 项自动化测试全部通过**（0.3.0 在 0.2.1 的 104 项基础上新增 34 项 T-SQL 用例；0.3.2 再补 5 项 `SET QUOTED_IDENTIFIER ON/OFF` 用例；0.3.4 补 28 项 XAML 用例与 6 项输入法切换重同步用例），包含四语言随机文本边界安全、Profile 匹配、23 项光标绘制与生命周期检查，以及离屏 WPF 渲染的颜色像素检查。现代 VSIX 与 Legacy 负载的结构、目标版本、依赖隔离、AnyCPU / VSSDK 15 绑定均通过脚本校验。该结果是对应发布提交的验证快照；仓库当前未配置 GitHub Actions，因此不能代替每次提交后的持续集成检查。

| 环境 | 当前验证状态 |
| --- | --- |
| Visual Studio 2022 / 2026，C/C++/C#/XAML，微软拼音与搜狗拼音 | 已完成既有功能实机验证 |
| SSMS 22.10 / Windows 11 / 搜狗拼音 | 部署、MEF 加载、菜单、状态栏、T-SQL 上下文、自动切换、手动 Shift 覆盖及 VSIX 直装均已实测通过 |
| SSMS 22 / 微软拼音 | 尚未完成同项实机验证（预期可用，欢迎 Issue 反馈） |
| SSMS 18 / 19 / 20 Legacy 包 | 包结构已校验，宿主加载和输入法行为尚待实机验证 |
| Visual Studio 的 T-SQL / XAML 编辑器 | 建议继续补充实机验证（XAML 设计器预览界面不接管） |

实机验证细节（SSMS 22，环境：Windows 11、SSMS 22.10.12210.168 / x64、本地 SQL Server、Windows 身份验证、搜狗拼音）：

- 管理员部署脚本可把 6 个文件平铺到 `Common7\IDE\Extensions\SmartInput` 并清理私有注册表 / MEF 缓存；重启后扩展被 MEF 成功加载（`Ssms.exe -log` 的 `ActivityLog.xml` 记录了 `.pkgdef` 扫描），`工具` 菜单出现 `Smart Input: 暂停/恢复自动切换` 命令。
- 打开 `.sql` 查询后左下角出现 `Smart Input` 状态条，并随光标位置正确显示区域与目标输入态——普通代码 / 空行为“自动 · 英文 · 代码”，`/* … */` 块注释为“自动 · 中文 · 注释”；有选区时显示“选区中，暂不切换”；手动按 Shift 进入“手动覆盖”、与推荐态不符时输入光标变为配置的提示色，移动到其它区域后自动恢复自动模式。
- 用物理键盘实测，鼠标在代码区与注释区（及含汉字字符串）之间移动时，搜狗拼音会自动切换到对应中 / 英文态（代码区直接上屏英文、注释区可输入中文），手动按 Shift 切换同样正常，未出现“状态条正确但输入法不跟随”；连接、编辑与反复移动光标全程无崩溃或挂起。
- SSMS 22 自带的 `VSIXInstaller.exe` 双击安装已实机核对，安装器会列出 SSMS 22 目标；`tools\Deploy-SSMS22.ps1` 保留为兜底 / 集中部署 / 缓存修复方式。

完整环境、证据与验证边界见 [验证记录](docs/VALIDATION.md)，复测时请使用 [实机回归清单](docs/MANUAL-TESTS.md)。

需要如实说明的其他验证边界：

- 微软拼音在 SSMS 22 下走 WPF 原生 TSF 状态通道、不模拟按键，尚未单独做同项实机验证；鉴于第三方 TSF 的搜狗拼音已实测可正常自动 / 手动切换，微软拼音预期同样可用。
- SSMS 18 / 19 / 20（32 位 Legacy 包）本机未安装，仍未经实机验证：其实际加载、T-SQL 编辑器 ContentType 的运行时名称与输入法切换表现需在装有对应版本的机器上确认。为降低风险，SQL 编辑器同时以精确的 `SQL` ContentType 和运行时按类型名兜底（类型名含 SQL 但非 `SQL` 派生时也接管）两种方式接入。
- 0.3.4 的搜狗重同步依赖 TSF 活动 Profile 与 IMM32 焦点窗口状态；在“每个应用窗口使用不同输入法”等系统选项、或宿主未把输入上下文交给焦点子窗口时，仍可能出现读回与实际的短暂差异。
- 搜狗拼音的 Shift 兜底在 32 位与 64 位进程中的 Win32 结构体布局沿用既有实现；该兜底在 Visual Studio 与 SSMS 22（WPF 编辑器）中均已实测。

Windows 的输入态可能受“每个应用窗口使用不同输入法”等系统选项影响。扩展会限制自己的调用范围，但无法保证离开宿主后 Windows 不继承原输入态。SSMS 18/19/20 的 Legacy 包不提供“工具”菜单暂停命令，请使用状态栏按钮或选项页。

以下范围不属于当前支持承诺：

- SSMS 22 的 ARM64 原生扩展（当前为 amd64 目标，可在 x64 / x64 仿真环境使用）。
- 微软拼音旧版兼容模式。
- C++ 预处理器复杂分支/拼接、C# 插值格式段、T-SQL 宿主专有方言和不完整语法的全部边界。
- XAML 的宿主专有标记扩展语法、混排 HTML/脚本的扩展内容，以及仅在运行时才确定的绑定表达式内容。

## 项目由来与贡献者

Smart Input 源于中文开发者在代码、注释和字符串之间频繁切换输入法的实际痛点：手动切换容易打断思路、拖慢编码效率，也可能把半角标点误输为全角标点——例如把 `;`、`()`、`""` 误写成 `；`、`（）`、`“”`，带来不必要的编译错误和返工。项目由 [Di-Y](https://github.com/Di-Y) 提出产品设想和功能构想，希望在 Visual Studio 与 SSMS 上获得类似 JetBrains IDE 插件 Smart Input Pro (Chinese) 的体验。本项目独立设计与实现，只参考 Smart Input Pro (Chinese) 的交互思路，不包含对方的代码、资源、授权或收费系统。

需要特别说明的是，**项目早期（0.1.x – 0.2.x）Di-Y 仅提出需求意见与功能构想，并未参与编码实现**；自 **0.3.0 起 Di-Y 才亲自承担开发与维护**。版本分工如下：

| 版本 | 主要贡献 |
| --- | --- |
| 0.1.x – 0.2.x | **我在人间做废物**：实现 Visual Studio 上 C/C++/C# 的自动中英文态切换；Di-Y 提供需求意见与功能构想、未参与编码 |
| 0.3.0 | **Di-Y**：新增 T-SQL、SSMS 18/19/20/22 支持及现代包 + Legacy 双包结构 |
| 0.3.1 | **Di-Y**：修复无独立 .NET SDK 环境下的构建流程（仅 MSBuild + NuGet 即可构建），完善 SSMS 部署与加载兼容性 |
| 0.3.2 | **Di-Y**：按 Pull Request 评审意见完成四项修复——补齐 SSMS 发布载荷打包、部署脚本 `-WhatIf` 安全与私有注册表备份、Release 测试配置与定位、T-SQL 双引号遵循 `SET QUOTED_IDENTIFIER`；测试由 138 增至 143 项 |
| 0.3.3 | **Di-Y**：统一发布标签、构建提交与三份发布资产，无功能行为变更 |
| 0.3.4 | **Di-Y**：调试器监视 / 即时 / 快速监视窗口不再被状态栏遮挡（只接管文档编辑器）、新增 XAML 支持、修复微软拼音→搜狗拼音手动切换后的识别 / 驱动问题、新增可配置状态栏背景色、确认 SSMS 22 可 VSIX 直装、构建适配 VS 2026 的 VSSDK 18 目标；测试由 143 增至 177 项 |

具体变更以 Git 提交记录和 [发布说明](docs/RELEASE-NOTES.md) 为准。

## 参与贡献

1. 通过本仓库的 [Issues](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/issues) 报告缺陷或提出建议，并附上系统、宿主、扩展和输入法版本，以及复现步骤与实际表现；请勿上传私人源码、候选词或完整键盘记录。
2. Fork 仓库后在独立分支修改，通过 [Pull Requests](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/pulls) 提交，并说明动机、改动和验证方式。
3. 提交前运行 `.\build.ps1 -Configuration Release`，确认当前 **177 项自动化测试**、现代 VSIX 和 Legacy 负载校验全部通过。
4. 不要提交 `.packages`、`bin`、`obj`、`.vs` 等还原或构建产物；新增第三方依赖时须注明来源与许可证。
5. 遵守项目的[隐私边界](docs/PRIVACY.md)：不联网、不采集源码或按键、不自动替换标点。

署名或贡献描述需要调整时，也请通过本仓库的 Issue 或 Pull Request 提出。

## 许可证

作者：**我在人间做废物**（[@Ysm-04](https://github.com/Ysm-04)）。

本项目采用 [MIT License](LICENSE)。除许可证文本中规定的条件外，使用、修改、分发本项目不附加额外限制。项目依赖遵循各自许可证，不代表本项目许可证覆盖第三方代码、商标或资源。

提交代码或文档时，请确保提交内容由提交者拥有相应权利，或已取得必要授权；新增第三方依赖时，请同时说明其来源和许可证。

问题反馈请提交到本仓库的 [Issues](https://github.com/Di-Y/SmartInput-for-Visual-Studio-and-SSMS/issues)，并提供系统版本、Visual Studio / SSMS 版本、扩展版本、输入法版本、操作步骤和实际表现。


参考：[SSMS 版本与扩展兼容说明](https://learn.microsoft.com/sql/ssms/sql-server-management-studio-changelog-ssms)、[VS 2026 扩展兼容模型](https://learn.microsoft.com/zh-cn/visualstudio/extensibility/migration/extension-compatibility?view=visualstudio)、[VS 光标格式接口](https://learn.microsoft.com/en-us/visualstudio/extensibility/walkthrough-customizing-the-text-view?view=vs-2022)、[WPF InputMethod](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.inputmethod?view=netframework-4.8)。SSMS 手动部署与缓存清理的实现参考了开源项目 [mourier/sql-pilot](https://github.com/mourier/sql-pilot) 的集成笔记。
