# 发布说明

作者：**我在人间做废物**（[@Ysm-04](https://github.com/Ysm-04)）

## 0.3.4

状态：缺陷修复版。修复用户反馈的两处问题，并新增XAML语言支持和状态栏背景色设置、核对 SSMS 22 直接 VSIX 安装方式，同时让构建在只安装 Visual Studio 2026 的机器上也能正常产出 VSIX；自动化测试由 143 项增至 **177 项**。

### 修复

- **调试器表达式窗口不再被状态栏遮挡（问题 1）**：在 C# 调试期间，监视（Watch）/ 即时（Immediate）/ 快速监视（QuickWatch）窗口的表达式输入框会复用 C#（或 SQL）ContentType 的 WPF 文本视图，扩展此前把这些视图也当成编辑器，在底部 MEF 边栏创建 `Smart Input` 状态按钮，按钮覆盖在表达式输入框上。0.3.4 起编辑器接入层只对“真正的文档编辑器”创建会话与状态栏（通过文本缓冲区上的 `ITextDocument` 文档属性识别）；调试器表达式视图等工具窗口临时文本视图不再接管，普通文档编辑器（C/C++、C#、XAML、SQL 查询窗口）不受影响。
- **修复微软拼音→搜狗拼音手动切换后未被识别 / 驱动（问题 2）**：此前搜狗状态通过顶层前台窗口的 IMM32 上下文读写，而编辑器实际输入上下文属于获得焦点的子窗口（WPF HwndSource）；手动切换输入法后可能出现“状态栏显示正确、输入法实际未切换”，需要按几次 Shift 或在中英文之间来回移动光标才恢复。0.3.4 起：
  - 搜狗读 / 写会遍历焦点编辑器窗口及其父窗口、宿主框架窗口（`GetForegroundWindow`）的多个 IMM32 上下文：读取取第一个可读值，写入则同时写入全部候选上下文（幂等），避免“写到了 Sogou 不监听的那个 HIMC”而状态栏看起来正确；
  - 检测到活动输入法（TSF Profile）变化后，执行短时（最多 3 次、约 0.3–0.6 秒）的目标输入态重同步写入（`ImeResyncPolicy`），即使读回值已经等于目标值——新激活的输入法因此被明确同步，而不是被“读回已一致”或“写入过早”跳过；
  - 通过 `WM_INPUTLANGCHANGE` 立即重新评估，不再等待轮询；
  - 原有 600ms 读回确认与搜狗一次性 Shift 兜底保持不变。新增 6 项重同步状态机用例。

### 新增与文档

- **状态栏背景色可配置**：`工具 > 选项 > Smart Input > 常规` 的“外观”分类新增两项设置——“自定义状态栏背景色”（布尔，默认关闭）与“状态栏背景色”（颜色，默认深灰 `#1E1E1E`）。关闭自定义时状态栏按钮背景跟随 VS / SSMS 主题；开启后使用所选颜色。它与既有的“自定义状态栏文字颜色”/“状态栏文字颜色”相互独立，可分别设置。设置写入宿主用户设置；Visual Studio 与 SSMS 22、SSMS 18/19/20 使用同一套源码，行为一致。状态栏按钮仍可单击暂停 / 恢复，暂停 / 恢复菜单与隐私边界均无变化；本轮未新增自动化用例，测试总数保持 177 项。
- **新增 XAML 语言支持**：C# WPF 项目的 `.xaml` 文件现在由扩展接管：`<!-- -->` 注释推荐中文；双引号 / 单引号属性值（含 `Text="加载成功"`、`Tag="{Binding 显示名称}"`）、元素文本内容与 `<![CDATA[...]]>` 按是否含汉字推荐中文 / 英文；元素名、属性名、命名空间与 `<?xml ... ?>` 声明按代码推荐英文；标签之间纯缩进空白保持代码，不误报字符串。新增 `ContentType("XAML")` 接入、`samples/Contexts.xaml` 样例与 28 项 XAML 词法用例。
- **SSMS 22 安装方式核对与文档修正**：实机核对确认 **SSMS 22 直接运行 / 双击 `SmartInput.VisualStudio.vsix` 即可安装**——SSMS 22 自带 `Common7\IDE\VSIXInstaller.exe`（与 VS 2026 同一代安装器），且本扩展 VSIX 清单已声明 `Microsoft.VisualStudio.Ssms [21.0,23.0)` amd64 安装目标；安装器列出多个产品目标时选择 SSMS 22；当前包未启用 AllUsers，安装器按**当前用户**把文件放到 `%LocalAppData%\Microsoft\SSMS\22.0_<实例>\Extensions\<随机目录>`，**无需管理员权限**。建议安装前关闭 SSMS、安装后重启即可加载，VSIX 安装一般无需再手动清缓存（实机安装记录见 `0.3.4-验证证据/ssms/`）。`tools\Deploy-SSMS22.ps1`（解压 VSIX、平铺到 `Common7\IDE\Extensions\SmartInput`、清理私有注册表 / MEF 缓存、写 `extensions.configurationchanged`）保留为**兜底 / 集中部署 / 缓存修复**方式：VSIX Installer 未列出 SSMS 22、安装失败，或扩展装了但未加载时使用。**SSMS 18 / 19 / 20（32 位，旧隔离 Shell）仍需 `SmartInput-SSMS-Legacy.zip` 中的 `Deploy-SSMS-Legacy.ps1` 手动平铺部署**，不能靠现代 VSIX。README、VALIDATION、MANUAL-TESTS 与交付说明中“SSMS 22 必须手动部署”的过时表述已一并改正（SSMS 22 可直接用自带 VSIX Installer 安装；SSMS 18/19/20 仍只用手动平铺部署）。
- 卸载入口补充：VSIX Installer 的卸载入口，或 `VSIXInstaller.exe /uninstall:SmartInput.VisualStudio.943daace-d510-4e75-b17c-715cf0602c01`，或 `Deploy-SSMS22.ps1 -Uninstall` 清理手动部署副本。VSIX 安装与手动部署二选一，不要重复部署。

### 构建与工程

- 现代包工程按当前 MSBuild 所在的 Visual Studio 自动选择 VSSDK 目标目录（VS 2022 `v17.0` / VS 2026 `v18.0`），并把解决方案中的现代包映射改为 Any CPU：VS 2026 的 VSSDK 构建任务只提供 64 位工具，原先映射到 x86 时 `CreatePkgDef` 会失败或静默跳过 VSIX。现在只安装 VS 2026 的机器也能一次 `build.ps1` 产出 VSIX。
- `tools/Verify-Package.ps1` 在程序集带有 Windows“网络位置”标记（下载压缩包解压 / 网络盘）时回退为内存加载，避免 `LoadFrom` 抛出 `0x80131515`。
- 本轮按 0.3.4 重新执行 Release 构建与打包校验：`build.ps1` 退出码 0，177 项自动化测试全部通过，现代 VSIX 与 Legacy 负载双包校验通过；版本号仍对外称 0.3.4，状态栏背景色与 SSMS 22 安装方式核对未新增自动化用例。

### 发布资产与 SHA-256

- `SmartInput.VisualStudio.vsix`：Visual Studio 2022 / 2026 安装。
  `SHA256: 31CE4B4489E3F2BD7E7804075181C76BF1D84609CB614092DD06682C306D4604`
- `SmartInput-SSMS22.zip`：SSMS 22（2025，64 位）。
  `SHA256: 1C4AAB3DB4467704616126D5DB6FBBB29614DBE682C9BB24F12AD598FA979B58`
- `SmartInput-SSMS-Legacy.zip`：SSMS 18 / 19 / 20（32 位）。
  `SHA256: F20ED175F7AF61CB959121D39283B5BEA9BFE9A365CEAE40E432E6FCA879E05F`

### 验证

Release 干净重建 0 错误，**177 项自动化测试全部通过、0 失败**；现代 VSIX 与 Legacy 负载双校验通过，程序集文件版本与 VSIX 版本一致；对外版本号仍为 0.3.4。

- **问题 1（VS 2026 18.10.12201.205 实机）**：0.3.3 基线在调试中断态的**即时窗口**底部、**快速监视**“监视表达式”输入框下方复现 `Smart Input` 状态按钮覆盖；0.3.4 部署后同一场景下监视 1 / 即时窗口 / 快速监视内按钮数均为 **0**，普通 C# 文档编辑器仍显示 `Smart Input · 自动 · 英文 · 代码`。
- **问题 2（真实输入法 WPF 宿主 + 真实微软拼音/搜狗拼音）**：旧 0.3.3 逻辑在“微软拼音(英文) → 搜狗拼音(中文)”后 4/4 跳过写入、实际仍中文（状态栏正确但没切）；0.3.4 重同步后 8/8 实际键入立即一致，无需 Shift；反向方向、一次性语义、Unknown 边界均验证通过。另在 **真实 VS 2026 + 0.3.4** 上完成三轮实机回执：代码区两个方向切换后直接键入英文正确；注释区切换后 **Sogou 中文候选窗正常弹出**、状态显示“组词中，暂不切换”→提交后“自动 · 中文 · 注释”，均无需手动按 Shift。
- **XAML语言支持（VS 2026 实机）**：打开 WPF 样例 `MainWindow.xaml` 后 DTE 报告 Language=**XAML**，XAML 标记文本视图底部出现 `Smart Input` 状态按钮，点击正文获得焦点后显示“自动 · 英文 · 代码”，并可随光标所在 XAML 上下文切换。

完整证据（UIA 树、截图、对照矩阵、WPF 输入法宿主日志）见交付包 `0.3.4-验证证据/`。

---
## 0.3.3

状态：**发布一致性修正版，无功能变更**。修复 0.3.2 的发布标签与三份资产停留在早期提交 `4f5b9e8`、未包含 `tools/Pack-Release.ps1` 打包脚本修复（U+2011 非 ASCII 连字符、`build.ps1` 根目录路径）的问题。本版本在**同一提交**上完成干净构建、打标签与发布，确保“发布标签 = 构建提交 = 三份资产”三者完全对应、可追溯。

### 变更

- 版本号统一为 **0.3.3**：现代 VSIX Identity、Legacy `extension.vsixmanifest` 与手写 `.pkgdef`、`SmartInput.Core` / `SmartInput.VisualStudio` 程序集版本、`InstalledProductRegistration`。
- 打包脚本 `Pack-Release.ps1` 中 `-Build` 正确调用仓库根目录 `build.ps1`，全部参数连字符为 ASCII；已在干净检出实测 `.\tools\Pack-Release.ps1 -Build` 端到端跑通（还原 → 四工程构建 → 测试 → 双包校验 → 生成两个 zip），退出码 0。
- 功能、交互规则与 0.3.2 完全一致，未做任何行为改动。

### 发布资产与 SHA-256

- `SmartInput.VisualStudio.vsix`：Visual Studio 2022 / 2026 安装。
  `SHA256: D33D57B78060F26C7CD9D6D9E6C11D7F94B5A6ED4319C72954D25B257FD95814`
- `SmartInput-SSMS22.zip`：SSMS 22（2025，64 位）。
  `SHA256: 27502A0F7A21D4F5EC39F004B7AA736029819549671DD56C48A926E6A6682FD8`
- `SmartInput-SSMS-Legacy.zip`：SSMS 18 / 19 / 20（32 位）。
  `SHA256: 833EF74EC2E85E8D4A7AF0885B4E8113FAF9AD7CF4E211346C6310A345279B47`

### 验证

干净重建 0 错误，**143 项自动化测试全部通过、0 失败**；现代 VSIX（0.3.3）与 Legacy 负载双校验通过，程序集文件版本与 VSIX 版本一致为 0.3.3。

---
## 0.3.2

状态：Pull Request 评审修复版。针对 0.3.1 Pull Request 的自动化评审意见，在不改变既有交互语义的前提下完成**四项修复**（补齐发布载荷、部署脚本 `-WhatIf` 安全与私有注册表备份、Release 测试配置与定位、T-SQL 双引号遵循 `SET QUOTED_IDENTIFIER`），并补齐可直接交付最终用户的发布载荷；自动化测试由 0.3.1 的 138 项增至 **143 项**。版本号同步统一为 0.3.2（现代 VSIX Identity、Legacy `extension.vsixmanifest` 与手写 `.pkgdef`、`SmartInput.Core` / `SmartInput.VisualStudio` 程序集版本、`InstalledProductRegistration`）。

### 修复

- **发布载荷补齐（P1）**：新增 `tools/Pack-Release.ps1`，构建后一键生成两个可直接交付最终用户、互不依赖的压缩包：`SmartInput-SSMS22.zip`（现代 VSIX + `Deploy-SSMS22.ps1` + 安装说明）与 `SmartInput-SSMS-Legacy.zip`（Legacy 四个平铺文件 + `Deploy-SSMS-Legacy.ps1` + 安装说明）。两个包内脚本与载荷同目录，解压后即在该目录运行，脚本会自动回退定位同目录的 VSIX / 平铺文件，无需保留源码目录结构。另修复 `-Build` 开关误调用不存在的 `tools\build.ps1` 的路径缺陷（`$PSScriptRoot` 指向 `tools` 目录），改为调用仓库根目录的 `build.ps1`；已实测 `.\tools\Pack-Release.ps1 -Build` 可一次完成还原、构建、143 项测试、双包校验并生成两个压缩包（退出码 0）。
- **部署脚本 `-WhatIf` 安全与私有注册表自动备份（P1）**：`Deploy-SSMS22.ps1`、`Deploy-SSMS-Legacy.ps1` 中清理私有注册表 / MEF 缓存、写入 `extensions.configurationchanged` 的所有删除 / 写入操作全部纳入 `$PSCmdlet.ShouldProcess`；`-WhatIf` 时完全只读（实测不删除任何缓存 / 注册表、不生成备份、不改扩展目录），且无需管理员权限或关闭 SSMS。删除 `privateregistry.bin` 前先自动备份到缓存目录下 `SmartInput-PrivateRegistry-Backup\privateregistry.<时间戳>.bin`；删除 / 写入失败直接抛错，不再在 `-ErrorAction SilentlyContinue` 之后仍无条件报告“部署完成”；未检测到 SSMS 安装时 `-WhatIf` 优雅退出。
- **Release 测试配置与定位修正（P2）**：解决方案中 Tests 工程在 Release 解决方案配置下由原来的 `Debug|x86` 改为 `Release|x86`（Debug 解决方案仍为 `Debug|x86`）；`build.ps1` 只运行本次配置精确路径 `tests/SmartInput.Tests/bin/<配置>/SmartInput.Tests.exe`，删除“递归取目录中时间最新 exe”的兜底，避免误跑陈旧产物；已实测 Release 构建运行的是 `bin/Release/SmartInput.Tests.exe`，发布 DLL 仍为 AnyCPU (ILOnly)、无 `Required32Bit`。
- **T-SQL 双引号遵循 `QUOTED_IDENTIFIER`（P2）**：词法器默认 `QUOTED_IDENTIFIER ON`（双引号为标识符、推荐英文），并按文本中 `SET QUOTED_IDENTIFIER ON/OFF` 的出现顺序更新状态；`OFF` 时双引号界定字符串字面量，含汉字推荐中文、纯英文推荐英文，`""` 作为转义引号。存储过程 / 触发器内或跨 `GO` 批处理等运行时才确定、静态文本无法判定的取值按默认 ON 处理，已列入已知限制；新增 5 项自动化用例，测试总数由 138 增至 143。

### 发布资产

由 `tools/Pack-Release.ps1` 生成。0.3.2 未在当前主仓库单独发布，相关构建已由主仓库 `v0.3.3` 修正版取代：

- `SmartInput.VisualStudio.vsix`：Visual Studio 2022 / 2026 双击安装。
- `SmartInput-SSMS22.zip`：SSMS 22（2025，64 位），含现代 VSIX 与 `Deploy-SSMS22.ps1`。
- `SmartInput-SSMS-Legacy.zip`：SSMS 18 / 19 / 20（32 位），含四个平铺文件与 `Deploy-SSMS-Legacy.ps1`。

### 验证

Release 干净重建 0 错误，**143 项自动化测试全部通过**；现代 VSIX（版本 0.3.2，8 个条目）与 Legacy 负载（AnyCPU / VSSDK 15，4 个文件）双校验通过，程序集文件版本与 VSIX 版本一致为 0.3.2。功能、交互与 SSMS 实机结论同 0.3.1。

## 0.3.1

状态：工程修复版。不新增功能，不改变支持范围与交互规则；修复在 Visual Studio 中打开解决方案时，多个 `Microsoft.VisualStudio.*` 命名空间（Shell / Utilities / Text / Threading 等）在 IDE 设计时（智能提示 / 错误列表）报“类型或命名空间不存在”的问题。

包路径与 0.3.0 相同：

```text
Visual Studio 2022/2026 + SSMS 22（x64）：src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix
SSMS 18/19/20（32 位）平铺负载：src/SmartInput.Ssms.Legacy/bin/Release/
```

SHA256：构建完成后以本地 `tools/Verify-Package.ps1`、`tools/Verify-LegacyPackage.ps1` 的输出为准（未签名、本地构建）。

### 修复

- 彻底修复在 Visual Studio IDE 中打开解决方案时，`Microsoft.VisualStudio.*` 命名空间（Shell、Utilities、Text、Threading 等）在设计时（智能提示 / 错误列表）报“类型或命名空间不存在”的问题。
  - 真正根因：现代包（VSSDK 17）工程此前用硬编码相对 HintPath 引用 VSSDK.BuildTools 包内部 `tools\vssdk`（及 `tools\vssdk\bin\lib`）下的 Shell、Interop、Threading、Validation、Utilities 等程序集。这些路径写死在仓库本地的 `.packages` 目录，且依赖该包的内部目录布局；命令行按仓库 `NuGet.Config` 还原到 `.packages` 时能编译通过，但 Visual Studio 的设计时构建一旦尚未完成还原、或把包还原到全局缓存等其它位置，这些 HintPath 就会整体失效，于是所有相关命名空间集体报红。上一版仅补单个 Utilities 的 HintPath 仍保留了其余脆弱引用，未能根治。
  - 修复方式（只改工程、不改任何功能源码）：将现代工程的 Visual Studio 引用程序集全部改为官方 VSSDK 17 NuGet `PackageReference`（`Text.UI.Wpf 17.14.249`、`Shell.15.0 17.14.40264`、`Shell.Framework 17.14.40264`、`Threading 17.14.15`、`Imaging.Interop.14.0.DesignTime 17.14.40254`，均 `ExcludeAssets/PrivateAssets=runtime`），与 SSMS Legacy 工程使用 VSSDK 15 正规包的方式完全同构；Utilities、Interop、ComponentModelHost、Imaging 等同代程序集由 Shell.15.0 的 NuGet 依赖闭包自动带入。`VSSDK.BuildTools` 仅保留为打包工具（CreatePkgDef / VSIX 容器 / .vsct 编译）。
  - 由此删除了全部指向 VSSDK.BuildTools 内部路径的 HintPath，以及仅供 pkgdef 反射兜底、同样写死 `.packages` 相对路径的 `AddVssdkBinLibToCreatePkgDefReferences` 目标；NuGet 在命令行与 IDE 设计时都通过 `$(NuGetPackageRoot)` 绝对路径提供同一套程序集，与包被还原到哪个文件夹无关。
- 消除 `Microsoft.Bcl.AsyncInterfaces` 的程序集版本冲突警告（MSB3277）：VSSDK 17.14 的 Shell / Shell.Framework / Utilities / Imaging / Threading 以及 System.Text.Json 9.0 闭包均要求 `Microsoft.Bcl.AsyncInterfaces 9.0.0`，而工程此前显式钉在 8.0.0；NuGet 以 8.0.0 为主引用，VS 宿主 PublicAssemblies 与传递包却统一到 9.0.0.0，于是 IDE 与构建输出 MSB3277。现把显式引用对齐到 **9.0.0**（仍 `ExcludeAssets=runtime`，运行时由 VS 2022 17.14 / SSMS 22 宿主自带的 9.0.0.0 提供），命令行 Rebuild 与 IDE 设计时构建均不再出现该警告；该程序集仅为传递依赖、扩展并不直接引用，也不会拷入 VSIX。
- 版本号统一为 0.3.1：现代 VSIX Identity、Legacy `extension.vsixmanifest` 与手写 `.pkgdef`、`SmartInput.Core` / `SmartInput.VisualStudio` 程序集版本、`InstalledProductRegistration` 及相关文档。

### 说明

- 改用正规 NuGet 包闭包后，现代工程的 Visual Studio 引用在设计时与命令行完全一致，且不再依赖 VSSDK.BuildTools 的内部目录或仓库 `.packages` 的固定位置；干净构建不再出现此前 Shell 同代深层程序集（Utilities、ComponentModelHost、Imaging、ImageCatalog、StreamJsonRpc 等）未解析的提示。
- 现代 VSIX 仍为 8 个条目，不捆绑任何宿主程序集（所有 VS 引用程序集均不拷入输出 / VSIX）；现代 DLL 为 AnyCPU (ILOnly)、绑定 VSSDK 17（Shell / Text / Utilities 等为 17.0.0.0，Threading 为 17.14.0.0），可在 64 位 Visual Studio 2022/2026 与 SSMS 22 中加载。Legacy 负载仍为 ILOnly / AnyCPU，引用程序集全部绑定 VSSDK 15。

### 验证与已知边界

- Release 干净重建 0 错误，**138 项自动化测试全部通过**（与 0.3.0 相同；本版为工程修复，不新增功能与用例。四项 Pull Request 评审修复及新增的 5 项 `SET QUOTED_IDENTIFIER ON/OFF` 用例在 0.3.2 中提供）；`Verify-Package.ps1` 与 `Verify-LegacyPackage.ps1` 均通过，程序集文件版本与 VSIX 版本一致为 0.3.1。
- 额外完成两项工程级验证：① 以设计时构建（`DesignTimeBuild`）枚举 IDE 实际引用路径，Shell / Utilities / Interop / Text / Threading 等全套程序集均来自正规 NuGet 包，无一条 VSSDK.BuildTools 内部 HintPath；② 将包还原到与仓库 `.packages` 完全无关的全新全局文件夹、删除 bin/obj 重新还原后再做设计时构建，21 个 Visual Studio 引用仍全部正确解析，证明结果与还原位置无关。
- 在未安装 .NET SDK、仅 VS 2022 MSBuild + NuGet 还原的环境下完整跑通 Restore → Build → 测试 → 双校验。
- 0.3.1 阶段已在 SSMS 22（22.10.12210.168 / x64）完成首轮实机验证：手动部署、MEF 加载、`工具` 菜单命令、左下角状态栏、T-SQL 代码 / 注释上下文识别均通过、无崩溃；**SSMS 22 + 搜狗拼音下，移动光标自动中英文切换与手动 Shift 切换 / 手动覆盖均经物理键盘实测通过**；SSMS 22 + 微软拼音（未单独测）以及 SSMS 18/19/20 仍待实测。详见 `docs/VALIDATION.md` 与 `docs/MANUAL-TESTS.md`。

## 0.3.0

状态：功能版。新增 Transact-SQL 语言与 SQL Server Management Studio（SSMS）支持，采用现代包 + 32 位 Legacy 包双构建。

包路径：

```text
Visual Studio 2022/2026 + SSMS 22（x64）：src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix
SSMS 18/19/20（32 位）平铺负载：src/SmartInput.Ssms.Legacy/bin/Release/
```

SHA256：构建完成后以本地 `tools/Verify-Package.ps1`、`tools/Verify-LegacyPackage.ps1` 的输出为准（未签名、本地构建）。

### 新增

- 新增 Transact-SQL（T-SQL）词法分析：
  - `--` 单行注释、`/* ... */` 块注释（支持嵌套）正文推荐中文。
  - 单引号字符串 `'...'` / `N'...'`，支持 `''` 转义与跨行；含汉字推荐中文，空串 / 纯英文 / 仅 emoji 推荐英文。
  - `[方括号]`（`]]` 转义）与 `"双引号"`（`""` 转义）标识符一律按代码推荐英文，即使其中包含汉字。
  - 字符串内部的 `--`、`/*` 不视为注释，括号标识符内部的引号 / `--` 不视为字符串 / 注释；T-SQL 无字符字面量与插值，反斜杠非转义符。
- 编辑器接入层在精确 `SQL` ContentType 之外，新增运行时按类型名（含 “SQL”）兜底的监听器与状态栏提供器，兼容 SSMS 中改名 / 派生的 T-SQL 编辑器类型，且不接管其它代码编辑器。
- 现代包（VSSDK 17，x64）清单新增 `Microsoft.VisualStudio.Ssms [21.0,23.0)`（amd64）安装目标，覆盖 SSMS 22（2025）。
- 新增 `src/SmartInput.Ssms.Legacy`：以 VSSDK 15 引用程序集、net48、AnyCPU 编译同一份编辑器源码，绑定 `Microsoft.VisualStudio.* 15.0.0.0`，用于 32 位 SSMS 18/19/20；手写 `.pkgdef` 与 v1（2010 schema）`extension.vsixmanifest`。
- 新增手动部署 / 卸载脚本 `tools/Deploy-SSMS22.ps1`、`tools/Deploy-SSMS-Legacy.ps1`，自动平铺文件、清理私有注册表与 MEF 缓存、写入 `extensions.configurationchanged`、解除网络标记。
- 新增 `samples/Contexts.sql` 交互检查样例；新增 `tools/Verify-LegacyPackage.ps1` 校验 Legacy 负载。

### 变更

- 版本号统一升至 0.3.0（VSIX Identity、程序集、`InstalledProductRegistration`）。
- 显示名称更新为 `Smart Input for Visual Studio and SSMS`。
- 解决方案与 `build.ps1` 现一次性构建现代包、Legacy 包与测试，并分别校验。
- 构建可移植性修复：整套工程在**未安装 .NET SDK** 的机器上，仅靠 Visual Studio 2022 / Build Tools 的 MSBuild 加 NuGet 还原即可完整构建，兑现 README“不需要 .NET SDK”的承诺：
  - `SmartInput.Ssms.Legacy` 由 SDK-style 工程改写为传统非 SDK 的 .NET Framework 4.8 / AnyCPU 类库工程，消除仅安装 MSBuild 时的 `MSB4236：找不到 SDK "Microsoft.NET.Sdk"`。
  - 现代包工程在仅 NuGet 还原（无 VS SDK 扩展开发工作负载）时，向 CreatePkgDef 任务补入 VSSDK.BuildTools `tools\vssdk\bin\lib` 下的 VS 17 引用程序集，修复 `CreatePkgDef：未能加载文件或程序集 Microsoft.VisualStudio.Utilities 17.0.0.0`；这些程序集仅供生成 pkgdef 时反射读取，既不参与编译引用，也不会拷入 VSIX（VSIX 仍为 8 个条目、不捆绑宿主程序集）。
  - `build.ps1` 修正测试程序集定位：解决方案将 Tests 工程固定映射到 Debug|x86，现按实际输出查找本次最新构建的 `SmartInput.Tests.exe`，不再依赖恰好残留的 `bin\Release` 旧副本（全新清理后此前会报测试程序集不存在）。

### 验证与已知边界

- Release 构建、**138 项自动化测试**（较 0.2.1 新增 34 项 T-SQL 用例，含三语言随机文本边界安全）全部通过。
- 现代 VSIX 校验通过（含 SSMS 22 安装目标）；Legacy 负载校验通过（AnyCPU、ILOnly、引用程序集均为 VSSDK 15、pkgdef 与 v1 清单要素完整）。
- 构建机未安装 SSMS，SSMS 18/19/20/22 的实机加载与宿主内输入法切换尚未经实机验证，详见 `docs/VALIDATION.md` 与 `docs/MANUAL-TESTS.md`。
- SSMS 18/19/20 Legacy 包不注册“工具”菜单命令（该宿主不编译菜单资源），暂停 / 恢复请使用编辑器底部状态栏按钮。
- 已在“无 .NET SDK”条件下复现完整构建：从 PATH 移除 dotnet、清空 `MSBuildSDKsPath`/`DOTNET_ROOT` 并清空各工程 bin/obj 后运行 `build.ps1 -Configuration Release`，NuGet 还原 → 四个工程构建 → 138 项测试 → 现代 VSIX 与 Legacy 负载双校验全部通过。

## 0.2.1

状态：修复版。针对搜狗输入法在 Visual Studio 2026 中状态写入成功但实际输入态未切换的问题进行兼容性修复。

包路径：

```text
src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix
```

SHA256：`7A0679D76B1865B1590A3B2F7D31CD3C622A4005F95A556884EC3436F038E65D`（历史发布资产）。

### 修复

- 搜狗优先通过 IMM32 读取和写入实际中英文转换状态，避免状态栏显示与实际输入结果不一致。
- 搜狗状态确认失败时，最多自动发送一次已配置的 `Shift` 切换键并再次确认。
- 微软拼音继续使用原有 TSF/WPF 状态路径，不模拟键盘输入。
- 保留 600ms 确认窗口和一次性失败阻塞，避免连续抢夺用户输入法状态。

### 验证

- Release 构建、104 项自动化测试和 VSIX 结构校验通过。
- 已针对搜狗拼音 `Shift` 中英文切换配置完成 Visual Studio 2026 实机回归。

## 0.2.0

状态：正式版。已完成 Windows 10、Windows 11、Visual Studio 2022、Visual Studio 2026 x64 以及微软拼音/搜狗拼音的当前支持范围验证。

包路径：

```text
src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix
```

SHA256：

```text
79C25E4FB6F0BD0C1DB7ED3D1F01267421664E69108612E5CF8EB173F237480B
```

### 新增与修复

- 新增统一输入法适配器接口和工厂。
- 保留微软拼音原有 Profile 识别和标准 WPF 状态读写。
- 新增搜狗拼音 Profile `{E7EA138E-69F8-11D7-A6EA-00065B844310}` / `{E7EA138F-69F8-11D7-A6EA-00065B844311}` 适配。
- 搜狗和微软拼音都只通过标准 TSF Profile 查询与 WPF `InputMethod` 状态读写，不使用私有协议、进程注入或快捷键模拟。
- 增加 Profile 匹配自动化测试和只读检查脚本。
- VSIX 对外显示名称整理为 `Smart Input for Visual Studio`，发布者显示为 `我在人间做废物`。
- 手动覆盖时使用独立光标装饰层，支持在设置页配置光标颜色和状态栏文字颜色。

### 已验证

- 搜狗在 VS C++ / C# 代码、注释、字符串和插值字符串中的自动切换。
- 搜狗候选窗、组词、Escape、手动覆盖、连续输入和切换文档。
- 微软拼音与搜狗拼音共存时只控制当前活动 Profile，不主动激活另一个 Profile。
- Windows 10、Windows 11、Visual Studio 2022、Visual Studio 2026 x64 实机兼容性。
- 深色/浅色/高对比度主题、不同 DPI、插入/覆盖模式、多视图和快速切换。

## 0.1.5

状态：早期版本。

SHA256：

```text
EA07920DE6F3F1875C9B9AFA03FB0AEEFC4902E24867860DBBE88F7D684813A8
```

### 新增与修复

- 接入 `工具 > 选项 > Smart Input > 常规` 设置页。
- 设置页只保留长期设置：启用自动切换、状态栏显示、手动覆盖光标颜色、状态栏文字颜色。
- 临时暂停改为运行期状态，不写入 VS 用户设置，重启 VS 后恢复为未暂停。
- 状态栏点击可暂停/恢复自动切换。
- 新增 `工具 > Smart Input: 暂停自动切换/恢复自动切换` 命令，状态栏隐藏时也可操作。
- 修复工具菜单命令不显示的问题：菜单命令组直接挂载到 `IDM_VS_MENU_TOOLS`。
- 修复菜单资源注册名不一致的问题：`.pkgdef` 注册名与 DLL 内嵌资源键统一为 `SmartInputCommands.CTMENU`。
- VSIX 校验增强：检查 VS Package、菜单资源、设置页、目标版本、程序集版本一致性和依赖隔离。

## 历史摘要

- 0.1.4：修正 VSIX 菜单资源注册名与 DLL 资源键不一致的问题。
- 0.1.3：加入 VS Package、设置页、状态栏颜色、手动覆盖光标颜色和临时暂停命令初版。
- 0.1.1：修复手动覆盖时原生光标颜色不能变红的问题，改为独立 WPF 光标装饰。
- 0.1.0：支持基本微软拼音中英文切换、组词期间暂停、手动覆盖和连续输入保持。

## 分发说明

当前 VSIX 未签名。安装前请核对发布来源和 SHA256；Windows 或 VSIX Installer 显示未知发布者时，请结合来源判断是否继续安装。

本项目采用 [MIT License](../LICENSE)。Visual Studio、微软拼音、搜狗拼音及其相关名称和商标归各自权利人所有；兼容性支持不代表官方认可或参与。