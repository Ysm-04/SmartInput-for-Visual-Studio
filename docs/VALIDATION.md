# 验证报告

更新日期：2026-10-18（0.3.4 版本发布）。当前版本：0.3.4（缺陷修复版：监视 / 即时 / 快速监视窗口不再被状态栏遮挡、新增 XAML 支持、修复微软拼音→搜狗拼音手动切换后未被识别 / 驱动；并新增可配置的状态栏背景色、核对 SSMS 22 直接 VSIX 安装方式；同时让只安装 VS 2026 的机器也能正常构建 VSIX）。0.3.3 为发布一致性修正版；0.3.2 为 Pull Request 评审四项修复版（自动化测试 138→143）；0.3.1 为工程修复版；0.3.0 新增 Transact-SQL 与 SSMS 支持。

## 0.3.4 结果摘要

- 缺陷修复版：自动化测试由 143 项增至 **177 项**（新增 28 项 XAML 词法用例、6 项输入法切换重同步用例），Release 干净重建 0 错误、**177 项全部通过、0 失败**。
- **问题 1（调试器表达式窗口被状态栏遮挡）修复**：编辑器接入层（`EditorIntegration.cs`）与状态栏提供器只对真正的文档编辑器生效——通过文本缓冲区上的 `ITextDocument` 文档属性区分；监视 / 即时 / 快速监视窗口的表达式文本视图是工具窗口临时缓冲区，不再创建 Smart Input 会话与底部状态按钮。普通 C/C++、C#、XAML、SQL 文档编辑器行为不变。
- **问题 2（微软拼音→搜狗拼音手动切换后未识别 / 未实际切换）修复**：搜狗 IMM32 读写改为使用获得键盘焦点的窗口（`GetFocus` → 跨线程 `GetGUIThreadInfo` → 前台窗口兜底）的输入上下文；检测到活动输入法（TSF Profile）变化后由新增的 `ImeResyncPolicy` 强制执行一次目标态同步写入（即使读回值已等于目标值）；新增 `WM_INPUTLANGCHANGE` 立即重评估；600ms 读回确认与搜狗一次性 Shift 兜底不变。
- **新增XAML语言支持 **：新增 `SourceLanguage.Xaml` 与 XML 标记扫描（`<!-- -->` 注释 → 中文；引号属性值、元素文本、`<![CDATA[]]>` 按内容含汉字判中文；标签 / 属性名 / XML 声明 → 英文；标签间纯空白保持代码）。`ViewListener` / `MarginProvider` 新增 `ContentType("XAML")`，新增 `samples/Contexts.xaml` 与 28 项用例。
- **工程修复**：现代包按当前 MSBuild 所在 Visual Studio 自动选择 VSSDK 目标目录（v17.0 / v18.0），解决方案中现代包映射改为 Any CPU，解决只装 VS 2026 时 `CreatePkgDef`（64 位工具、无 x86）失败或静默不产 VSIX 的问题；`tools/Verify-Package.ps1` 在程序集带“网络位置”标记时回退内存加载。
- **新增状态栏背景色设置**：选项页 `工具 > 选项 > Smart Input > 常规` 的“外观”分类新增“自定义状态栏背景色”（布尔，默认关闭）与“状态栏背景色”（颜色，默认深灰 `#1E1E1E`，即 RGB 30,30,30）；关闭时状态栏按钮背景跟随 VS / SSMS 主题，开启后使用所选颜色；与状态栏文字颜色相互独立、可分别设置。设置写入宿主用户设置，VS 与 SSMS 22、SSMS 18/19/20 共用同一套源码，行为一致；状态栏按钮仍可单击暂停 / 恢复。
- **SSMS 22 安装方式核对结论**：SSMS 22（2025，64 位）自带 `Common7\IDE\VSIXInstaller.exe`（与 VS 2026 同一代安装器），本扩展 VSIX 清单声明了 `Microsoft.VisualStudio.Ssms [21.0,23.0)` amd64 安装目标，双击 / 运行 `SmartInput.VisualStudio.vsix` 即可由 VSIX Installer 完成安装——**直接 VSIX 安装为一等安装方式**，`tools\Deploy-SSMS22.ps1` 保留为兜底 / 集中部署 / 缓存修复；SSMS 18 / 19 / 20（32 位）仍需 `Deploy-SSMS-Legacy.ps1` 手动平铺部署。当前包未启用 AllUsers，安装器按当前用户写入 `%LocalAppData%\Microsoft\SSMS\22.0_<实例>\Extensions\<随机目录>`、无需管理员权限；本机用户级安装记录（`0.3.4-验证证据/ssms/`）显示安装器同时列出 VS 2026 与 SSMS 22，SSMS 22 装入的 0.3.4 载荷被 MEF 正常加载。
- 问题 2 已在**真实输入法的 WPF 测试宿主**（顶层 Win32 框架 + 子 HwndSource，与 VS/SSMS 的窗口结构一致；微软拼音 16.x 与搜狗拼音均为系统已安装的真实 TIP）中用旧 / 新适配器对照验证：
  - 旧 0.3.3 逻辑：微软拼音（英文）手动切到搜狗拼音（搜狗自身中文）后，IMM32 读到的是上一输入法残留的“英文”，`actual == desired` 导致**不写**；700ms 后读回仍为英文、**实际键入仍为中文**——`BUG_SIGNATURE` 在有效样本中 **4/4 复现**（只观察阶段 5/5 读到残留值）；
  - 0.3.4：`ImeResyncPolicy` 在切换后的短时窗口内进行最多 3 次幂等重同步写入 → 700ms 读回目标态且**实际键入正确，无需按 Shift**，**3/3 通过**；连续 5 次切换 5/5 通过，`Unknown` 不消耗标记、真实搜狗环境写入结束后不重复写均已实测；反向“搜狗→微软拼音”无该残留问题（旧 2/2、新 2/2 正常）。
  - 该测试宿主中框架窗口与编辑器子窗口共享同一 HIMC，因此“焦点窗口优先”这一改动的独立效果仍需在真实 VS/SSMS 宿主补测（判据见 `docs/MANUAL-TESTS.md`）。
- **问题 2 在真实 Visual Studio 2026 + 0.3.4 上完成三轮实机回执**（UIA 读状态按钮、Win+Space 真实手动切换输入法、SendInput 键入、截图）：
  - 代码区（期望英文）：微软拼音→搜狗、搜狗→微软拼音两个方向，切换后不按 Shift、不移动光标直接键入均为英文，状态按钮“自动 · 英文 · 代码”；
  - 注释区（期望中文）：切换后键入 `ab` 时 **Sogou 中文候选窗正常弹出**（候选“ab/阿爸/阿布…”），状态按钮显示“组词中，暂不切换”，组词结束/提交后回到“自动 · 中文 · 注释”，说明 Sogou 实际处于中文态；
  - 结论：三轮均符合预期（注释区首次自动判定曾被测试脚本的“提交字符是 ASCII ⇒ 英文”规则误报，截图复核后确认是 Sogou 中文候选窗，已修正判定说明）。测试截图见交付包 `0.3.4-验证证据/ime/logs/screenshots/vs-live-*`。
- **问题 1 已在真实 Visual Studio 2026（18.10.12201.205）中做基线 / 修复对照**（用户扩展目录手工部署，调试 C# 控制台样例，UIAutomation + 截图取证）：
  - 基线 0.3.3：**即时窗口**底部出现 `Smart Input` 状态按钮（1 个，位于窗口底部输入区域）；**快速监视**窗口的“监视表达式”ComboBox 下方出现同一按钮（UIA 重复暴露 165 次、去重后 1 个唯一位），即覆盖表达式输入框（与用户截图 2 一致）；监视 1 当时为空、未复现（该窗口需先添加监视项才会创建表达式文本视图）。
  - 修复版 0.3.4：同一调试中断态下，**监视 1 / 即时窗口 / 快速监视内 `Smart Input` 元素数均为 0**；普通 C# 文档编辑器底部仍显示 `Smart Input · 自动 · 英文 · 代码`。说明“只对真正的文档编辑器建会话/状态栏”的门控有效。
- **XAML语言已在真实 VS 2026 中确认接管**：打开 WPF 样例 `MainWindow.xaml` 后，DTE 报告的文档语言为 **XAML**，XAML 标记文本视图底部出现 `Smart Input` 状态按钮（截图见验证证据）；编辑器未获得焦点时显示“等待编辑器焦点”，符合既有行为。XAML 注释 / 中文属性值 / 标签区域的三处上下文采样与完整回归见 `docs/MANUAL-TESTS.md` 复查项。
- 实机验证的完整证据（脚本、UIA 树、截图、原始日志、对照矩阵）见交付包中的 `0.3.4-验证证据/` 目录（VS 与 IME 两份报告）。
- 本轮 0.3.4 的 Release 重建与校验：`build.ps1` 退出码 0，**177 项自动化测试全部通过、0 失败**，现代 VSIX 与 Legacy 负载双包校验通过；`Deploy-SSMS22.ps1 -WhatIf` 与 `Deploy-SSMS-Legacy.ps1 -WhatIf` 只读演练退出码均为 0。本次补充（状态栏背景色、SSMS 22 安装方式核对）未新增自动化用例，测试总数保持 **177 项**；三份发布资产的 SHA-256 见 `docs/RELEASE-NOTES.md`。
- 实机验证结论（VS 2026 / SSMS 22）见下文与 `docs/MANUAL-TESTS.md`。

## 0.3.3 结果摘要

- 0.3.3 为**发布一致性修正版，无功能变更**；解决 0.3.2 发布标签与三份资产停留在早期提交 `4f5b9e8`、未包含打包脚本修复（U+2011 连字符、`build.ps1` 路径）的问题。
- 从干净检出执行 `.\tools\Pack-Release.ps1 -Build`：还原 → 四工程构建 → **143 项自动化测试全部通过、0 失败** → 现代 / Legacy 双包校验 → 生成两个发布 zip，**退出码 0**。
- 发布标签 0.3.3、构建提交、三份资产对应**同一提交**；版本号在现代 VSIX Identity、Legacy manifest / pkgdef、两个程序集、`InstalledProductRegistration` 统一为 0.3.3。
- 三份资产 SHA-256 见 `docs/RELEASE-NOTES.md`（构建后回填），功能与交互同 0.3.2。

## 0.3.2 结果摘要

- 0.3.2 针对 0.3.1 Pull Request 的自动化评审意见完成四项修复（不改变既有交互语义）；Release 干净重建 0 错误，**143 项自动化测试全部通过、0 失败**（在 0.3.1 的 138 项基础上新增 5 项 `SET QUOTED_IDENTIFIER ON/OFF` 用例）。
- **发布载荷补齐（P1）**：新增 `tools/Pack-Release.ps1`，生成 `artifacts/SmartInput-SSMS22.zip`（3 条目：现代 VSIX、`Deploy-SSMS22.ps1`、安装说明）与 `artifacts/SmartInput-SSMS-Legacy.zip`（6 条目：四个平铺文件、`Deploy-SSMS-Legacy.ps1`、安装说明），普通用户下载 Release 资产后无需源码即可按包安装。另修复 `-Build` 开关误调用不存在的 `tools\build.ps1` 的路径缺陷（改为仓库根 `build.ps1`），并实测 `.\tools\Pack-Release.ps1 -Build` 端到端跑通：还原 → 四工程构建 → 143 项测试 → 现代 / Legacy 双包校验 → 生成两个发布 zip，退出码 0。
- **部署脚本 `-WhatIf` 安全与备份（P1）**：`Deploy-SSMS22.ps1 -WhatIf`（本机 D 盘自定义 SSMS 22）实测备份、删除私有注册表 / MEF 缓存、写信号、平铺扩展均只打印 `WhatIf:` 而不执行，运行前后 `privateregistry.bin`、`ComponentModelCache`、扩展目录与信号文件大小 / 时间戳完全不变、也不创建备份目录（非管理员、SSMS 开启即可演练）；`Deploy-SSMS-Legacy.ps1 -WhatIf` 在未安装 18/19/20 时优雅退出（退出码 0）。删除 `privateregistry.bin` 前自动备份；发布 zip 解压到临时目录、不传载荷路径运行脚本，确认同目录载荷自动回退定位生效。
- **Release 测试配置与定位（P2）**：Tests 工程 Release 解决方案配置由 `Debug|x86` 改回 `Release|x86`，`build.ps1` 只运行精确路径 `tests/SmartInput.Tests/bin/Release/SmartInput.Tests.exe`，已无“取目录中最新 exe”兜底，Release 构建运行 **143 项测试 0 失败**；发布 DLL 经核对仍为 AnyCPU (ILOnly)、无 `Required32Bit`。
- **T-SQL 双引号遵循 `QUOTED_IDENTIFIER`（P2）**：词法器跟踪 `SET QUOTED_IDENTIFIER ON/OFF`，`OFF` 时双引号界定字符串字面量、含汉字切中文，新增 5 项用例。
- 现代 VSIX Identity、Legacy `extension.vsixmanifest` 与 `.pkgdef`、两个程序集版本（0.3.2.0）、`InstalledProductRegistration` 均为 0.3.2；`Verify-Package.ps1` 报告 VSIX 0.3.2（8 个条目、不捆绑宿主 DLL、AnyCPU / VSSDK 17），`Verify-LegacyPackage.ps1` 报告 AnyCPU / VSSDK 15 绑定、4 个文件。功能、交互、SSMS 实机结论与已知限制同 0.3.1。

## 0.3.1 结果摘要

- 彻底修复 Visual Studio IDE 设计时（IntelliSense / 错误列表）对 `Microsoft.VisualStudio.*`（Shell、Utilities、Text、Threading 等）报“类型或命名空间不存在”的问题。
  - 真正根因：现代包工程此前用硬编码相对 HintPath 引用 VSSDK.BuildTools 包内部 `tools\vssdk` 与 `tools\vssdk\bin\lib` 下的 Shell、Interop、Threading、Validation、Utilities 等程序集，路径写死仓库本地 `.packages` 且依赖该包内部布局。命令行按仓库 `NuGet.Config` 还原到 `.packages` 时可编译，但 IDE 设计时在尚未还原完成、或把包还原到全局缓存等其它位置时，这些 HintPath 整体失效，相关命名空间集体报红。上一版仅补单个 Utilities 的 HintPath 未根治。
  - 修复方式（仅改工程，不改功能）：现代工程的 Visual Studio 引用全部改为官方 VSSDK 17 NuGet `PackageReference`（`Text.UI.Wpf 17.14.249`、`Shell.15.0 17.14.40264`、`Shell.Framework 17.14.40264`、`Threading 17.14.15`、`Imaging.Interop.14.0.DesignTime 17.14.40254`，均 `ExcludeAssets/PrivateAssets=runtime`），与 Legacy 工程的 VSSDK 15 正规包写法同构；Utilities、Interop、ComponentModelHost、Imaging 等由 Shell.15.0 的 NuGet 依赖闭包自动带入。同时删除全部 VSSDK.BuildTools 内部 HintPath 与写死 `.packages` 的 pkgdef 兜底 target；`VSSDK.BuildTools` 仅保留为打包工具。
- 工程级验证（本轮新增）：
  - 设计时构建（`DesignTimeBuild=true`）枚举 IDE 实际 `ReferencePath`：Shell / Shell.Framework / Utilities / Interop / Text.* / Threading / Validation 及 CreatePkgDef 反射所需的 ComponentModelHost / Imaging / RpcContracts / Telemetry 等共 21 个 Visual Studio 程序集全部来自正规 NuGet 包的 `lib` 目录，无一条 `tools\vssdk` 内部 HintPath；设计时与命令行引用集完全一致。
  - 将包还原到与仓库 `.packages` 完全无关的全新全局文件夹、删除 bin/obj 重新还原后再做设计时构建，21 个 Visual Studio 引用仍全部正确解析、关键程序集（Shell.15.0 / Utilities / Interop / Text.UI.Wpf / Threading / Shell.Framework）齐全，证明结果与 NuGet 包还原位置无关。
  - 移除 pkgdef 兜底 target 后，CreatePkgDef 仍正常完成、VSIX 正常生成，说明正规包闭包已提供 pkgdef 反射所需的全部宿主程序集。
- 程序集版本统一（消除 MSB3277）：VSSDK 17.14 的 Shell / Utilities / Imaging / Threading 与 System.Text.Json 9.0 闭包要求 `Microsoft.Bcl.AsyncInterfaces 9.0.0`，工程此前显式钉 8.0.0，导致 NuGet 主引用 8.0.0 与宿主 PublicAssemblies / 传递包统一到的 9.0.0.0 冲突。已将显式引用对齐到 9.0.0（`ExcludeAssets=runtime`）；命令行 Rebuild（`/v:normal`）与 IDE 设计时构建均不再出现 MSB3277。该程序集仅为传递依赖、本扩展不直接引用，且不拷入 VSIX（运行时由 VS 2022 17.14 / SSMS 22 宿主提供 9.0.0.0），VSIX 仍为 8 个条目。
- Release 干净重建 0 错误；**138 项自动化测试全部通过**（与 0.3.0 相同，本版不新增功能与用例；5 项 `SET QUOTED_IDENTIFIER ON/OFF` 用例在 0.3.2 新增）；`Verify-Package.ps1` / `Verify-LegacyPackage.ps1` 均通过。现代 VSIX 仍为 8 个条目、不捆绑任何宿主 DLL；现代 `SmartInput.VisualStudio.dll` 经 PE / COR20 头核对为 AnyCPU (ILOnly)，绑定 VSSDK 17（Shell / Text / Utilities 等为 17.0.0.0，Threading 为 17.14.0.0）；Legacy 负载仍为 ILOnly / AnyCPU、引用程序集全部绑定 VSSDK 15。
- 已在“无 .NET SDK”条件下复现：从 PATH 移除 dotnet、清空 `MSBuildSDKsPath` / `DOTNET_ROOT`、清空各工程 bin/obj 后运行 `build.ps1 -Configuration Release`，Restore → 四工程构建 → 138 项测试 → 现代 VSIX 与 Legacy 负载双校验全部通过。
- 版本号统一为 0.3.1（现代 VSIX Identity、Legacy 清单与 pkgdef、两个程序集版本、`InstalledProductRegistration`、文档）。
- SSMS 22 已在 0.3.1 阶段完成首轮实机验证（2026-10-07，SSMS 22.10.12210.168 / x64 + 搜狗拼音）：部署、MEF 加载、菜单命令、状态栏、T-SQL 上下文识别，以及搜狗拼音的中英文**自动切换与手动 Shift 切换均通过**；微软拼音（未单独测）与 SSMS 18/19/20 仍待实测。详见下文“SSMS 22 实机验证记录”与 `MANUAL-TESTS.md`。

## 0.3.0 结果摘要

- MSBuild 17.14 / .NET Framework 4.8 Release 构建成功，解决方案包含 `SmartInput.Core`、`SmartInput.VisualStudio`（现代包）、`SmartInput.Ssms.Legacy`（32 位包）、`SmartInput.Tests` 四个工程。
- `SmartInput.Tests.exe`：**138 项测试通过，0 项失败**（在 0.2.1 的 104 项基础上新增 34 项 T-SQL 用例）。覆盖 C/C++、C#、T-SQL 三语言的语法边界、手动覆盖状态机、Profile 匹配、随机文本鲁棒性（三语言逐位置查询）、性能样本和光标绘制 / 生命周期测试。
- T-SQL 用例覆盖：`--` 行注释、可嵌套 `/* */` 块注释、单引号字符串（`N''`、`''` 转义、跨行）、空 / 英文 / 仅 emoji 字符串、`[括号]` 与 `"双引号"` 标识符、字符串 / 括号内部定界符不误判、负数单减号、扩展区汉字等。
- 性能样本：520,000 字符扫描约 8ms；扫描加 100,000 次位置查询约 12ms。此为核心算法样本，不代表宿主实际输入延迟。
- 现代 VSIX 检查通过（`tools/Verify-Package.ps1`）：目标含 Visual Studio `[17.14,)` / amd64 与 SSMS `Microsoft.VisualStudio.Ssms [21.0,23.0)` / amd64；MEF 入口、VS Package 入口、菜单资源、`工具 > 选项 > Smart Input > 常规` 设置页、程序集版本与依赖隔离均正确；安装包不捆绑宿主 DLL。
- Legacy 负载检查通过（`tools/Verify-LegacyPackage.ps1`）：
  - 输出含 `SmartInput.VisualStudio.dll`、`SmartInput.Core.dll`、`SmartInput.VisualStudio.pkgdef`、`extension.vsixmanifest`。
  - 两个 DLL 均为 **ILOnly / AnyCPU**，可被 32 位 SSMS 进程加载。
  - `SmartInput.VisualStudio.dll` 引用的 `Microsoft.VisualStudio.*` 程序集强名称版本全部为 **15.x**（Text.Data/Logic/UI/UI.Wpf、CoreUtility、Shell.15.0、Shell.Framework 为 15.0.0.0，Threading 为 15.8.0.0），不含 16/17 绑定。
  - 手写 pkgdef 含 Package 注册、`CodeBase` 与 NoSolution 自动加载项；v1 清单含 `<IsolatedShell>ssms</IsolatedShell>`、`MefComponent` 与 `VsPackage`。
- 部署脚本 `Deploy-SSMS22.ps1` / `Deploy-SSMS-Legacy.ps1` 的路径、文件清单与缓存清理逻辑对照参考实现（mourier/sql-pilot）编写，并通过静态检查；脚本支持 `-WhatIf` 与 `-Uninstall`。

## SSMS 22 实机验证记录（0.3.1，2026-10-07）

环境：Windows 11、SSMS 22.10.12210.168（stable，x64，基于 VS 2026 Shell，自定义安装于 D 盘）、本地 SQL Server 17.0 默认实例、Windows 身份验证连接、活动输入法为搜狗拼音（微软拼音已安装，但本轮未作为活动输入法测试）。

部署与加载（通过）：

- 以管理员 PowerShell 运行 `tools\Deploy-SSMS22.ps1 -SsmsIdeDir '<IDE 目录>'`，6 个文件平铺到 `Common7\IDE\Extensions\SmartInput`，私有注册表与 MEF 缓存被清理；`Ssms.exe -log` 的 `ActivityLog.xml` 记录了 `smartinput.visualstudio.pkgdef` 扫描，扩展被 MEF 成功加载，全程无崩溃。
- `工具` 菜单出现 `Smart Input: 暂停/恢复自动切换` 命令；新建查询后 T-SQL 编辑器左下角出现 `Smart Input` 状态条。

上下文识别与交互（通过）：

- 光标在普通代码 / 空行 / `SELECT` 行显示“自动 · 英文 · 代码”；在 `/* … */` 块注释内显示“自动 · 中文 · 注释”；存在文本选区时显示“选区中，暂不切换”。
- 手动按 Shift 进入“手动覆盖 · 中/英文”，与推荐态不符时输入光标变为配置的提示色；移动到其它文本区域后手动覆盖自动解除、恢复“自动”。暂停 / 恢复命令可用。

物理输入法自动 / 手动切换（搜狗拼音：通过）：

- 用物理键盘实测：鼠标在普通代码区与 `/* … */` 注释区（及含汉字字符串）之间移动时，搜狗拼音随上下文自动切换——代码区直接上屏英文，注释区可输入中文；移动光标即自动切换，无需额外按键。
- 手动按 Shift 的手动覆盖同样正常：进入与推荐态不符的输入态时显示提示色光标，移动到其它区域后自动恢复自动模式；暂停 / 恢复、选区与组词期间不切换等行为均符合预期，未出现“状态条正确但输入法不跟随”的情况。

仍待实测：

- SSMS 22 + **微软拼音**的自动切换未单独实测（本轮活动输入法为搜狗拼音；微软拼音走 WPF 原生 TSF 通道、不模拟按键，预期同样可用，欢迎反馈）。
- SSMS 18 / 19 / 20（32 位 Legacy 包）：本机未安装，加载、ContentType 运行时名称与输入法切换均未实测。
- SSMS 22 除深色主题 / 当前 DPI 外的其它主题、显示缩放组合。

## 0.3.0 发布时尚未覆盖的验证（历史记录）

- 以下为 0.3.0 发布时的验证边界。该构建机当时未安装任何版本 SSMS，因此下列内容在 0.3.0 阶段未经实机验证；后续已完成的 SSMS 22 + 搜狗拼音验证以本文前面的 0.3.1 实机记录为准：
  - SSMS 22 与 SSMS 18/19/20 是否成功发现并加载扩展（pkgdef 扫描、MEF 组合、私有注册表缓存重建）。
  - SSMS 查询编辑器 ContentType 的运行时实际名称（已用精确 `SQL` + 按类型名含 “SQL” 兜底双保险降低风险，但仍需实测）。
  - SSMS 宿主内微软拼音 / 搜狗拼音的状态读取与切换表现。
  - 选项页、状态栏在 SSMS 各版本中的显示。
- 现代包在 Visual Studio 中的 C/C++、C# 行为延续 0.2.1；新增的 T-SQL 路径在 Visual Studio SQL 编辑器中也建议实测。
- SSMS 18/19/20 Legacy 包不编译菜单资源，因此不提供“工具”菜单命令，预期通过状态栏按钮暂停 / 恢复；该差异未在实机确认。
- 部署脚本未在真实 SSMS 目录上执行过（构建机无该目录），仅做了逻辑与路径静态核对。

## 0.2.1 实机验证记录（历史，沿用）

0.2.1 Release VSIX SHA256：`7A0679D76B1865B1590A3B2F7D31CD3C622A4005F95A556884EC3436F038E65D`

- MSBuild 17.14 / .NET Framework 4.8 Release 构建成功，无警告、无错误；当时 104 项自动化测试通过。
- 只读 COM 互操作检查通过：x64 `TF_INPUTPROCESSORPROFILE` 布局为 88 字节；搜狗 16.8.0.4914 已注册为 TIP `{E7EA138E-69F8-11D7-A6EA-00065B844310}`，Profile `{E7EA138F-69F8-11D7-A6EA-00065B844311}`。
- 独立 WPF 探针验证搜狗 `On + Alphanumeric` 读取与 `Off` / `On + Native` 写入、进程结束后恢复；生产程序集工厂探针正确选中 `SogouInputMethod`；探针均未激活输入法或修改默认设置。
- 针对搜狗在 Visual Studio 2026 中状态栏与实际输出不一致的问题，0.2.1 增加 IMM32 状态读取并在确认失败时最多模拟一次已配置 `Shift`；微软拼音不走模拟按键路径。用户反馈环境完成回归，未复现原问题。

### 平台与输入法（0.2.1 实机）

| 环境 | 结果 |
| --- | --- |
| Windows 10 x64 / Visual Studio 2022 / 微软拼音 | 已通过 |
| Windows 11 x64 / Visual Studio 2022 17.14.21 / 微软拼音 | 已通过 |
| Windows 11 x64 / Visual Studio 2022 17.14.21 / 搜狗拼音 16.8.0.4914 | 已通过 |
| Windows 11 x64 / Visual Studio 2026 / 微软拼音 | 已通过 |
| 微软拼音与搜狗拼音共存 | 已通过；只控制当前活动 Profile |
| 主题、DPI、插入/覆盖、多视图和快速切换 | 已通过 |

实机验证覆盖 C++ / C# 代码、注释、字符串、插值字符串、候选窗、Escape、手动覆盖、连续输入、切换文档和失焦场景。

## 版本历史

- 0.3.4：缺陷修复版。三处修复（调试器监视 / 即时 / 快速监视窗口不再被状态栏遮挡、新增 XAML 支持、修复微软拼音→搜狗拼音手动切换后未被识别 / 驱动）；新增可配置的**状态栏背景色**（默认关闭、深灰 `#1E1E1E`、关闭时跟随主题、与文字颜色独立）；核对并改正 **SSMS 22 安装方式**（自带 VSIX Installer 直接安装为一等路径，`Deploy-SSMS22.ps1` 为兜底，SSMS 18/19/20 仍手动平铺）；现代包工程自动识别 VS 2022 / 2026 的 VSSDK 目标（v17.0 / v18.0）。自动化测试由 143 增至 **177 项**，现代 VSIX 与 Legacy 双包校验通过。
- 0.3.2：Pull Request 评审四项修复版。新增 `tools/Pack-Release.ps1` 打包两个可直接安装的 SSMS 发布载荷 zip；部署脚本 `-WhatIf` 全只读、删除私有注册表前自动备份、失败即报错；Release 解决方案测试配置改回 `Release|x86` 并使用精确测试路径（去除“最新 exe”兜底）；T-SQL 双引号支持 `SET QUOTED_IDENTIFIER OFF`（新增 5 项用例）。自动化测试由 138 增至 **143 项**，现代 VSIX（0.3.2，8 条目）与 Legacy 负载双校验通过；功能与交互同 0.3.1。
- 0.3.1：工程修复版，功能与 0.3.0 一致、测试仍为 138 项；现代工程的 Visual Studio 引用全部改为官方 VSSDK 17 NuGet `PackageReference`（删除写死 `.packages` 的 VSSDK.BuildTools 内部 HintPath），从根本上消除 IDE 设计时命名空间报红，并验证与 NuGet 包还原位置无关；另将 `Microsoft.Bcl.AsyncInterfaces` 对齐到 VSSDK 17.14 闭包 / 宿主要求的 9.0.0，消除 MSB3277；解决未安装 .NET SDK 时仅 MSBuild + NuGet 即可构建。另于 2026-10-07 完成 SSMS 22 首轮实机：部署、加载、菜单、状态栏、T-SQL 上下文识别，以及搜狗拼音自动 / 手动切换均通过；微软拼音（未单独测）与 SSMS 18/19/20 待实测。
- 0.3.0：新增 Transact-SQL 词法与 SSMS 18/19/20/22 双包支持；138 项自动化测试与双包结构校验通过；SSMS 实机验证待补。
- 0.2.1：修复搜狗输入法实际输入态与状态栏不一致，以及状态确认失败后无法自动恢复的问题。
- 0.2.0：加入搜狗拼音适配，并完成当前支持范围内的平台和实机验证。
- 0.1.0：验证基本微软拼音中英文切换、候选词、组词期间暂停、手动覆盖和连续输入保持。
- 0.1.1：将手动覆盖光标改为独立 WPF 装饰层，解决原生光标颜色无法稳定改变的问题。
- 0.1.5：加入正式设置页、状态栏设置、工具菜单暂停/恢复命令和增强版 VSIX 校验。

## 已知限制

- 现代包仅声明 amd64，不承诺 SSMS 22 的 ARM64 原生扩展（x64 / x64 仿真环境可用）。
- 微软拼音旧版兼容模式不属于当前支持范围。
- 后台词法分析器不是完整编译器；C++ 预处理器复杂分支/拼接、C# 插值格式段、T-SQL 宿主专有方言和不完整语法可能存在识别偏差。
- 编译、模拟宿主、COM 检查和离屏绘制测试不能替代所有宿主版本及显示缩放组合的实机验证。SSMS 22 已完成首轮实机（部署、加载、识别，以及搜狗拼音自动 / 手动切换均通过）；SSMS 22 + 微软拼音以及 SSMS 18/19/20 仍需在真实环境补测。
- 搜狗拼音 Shift 兜底在 32 位 / 64 位进程的 Win32 结构体布局沿用既有实现；在 Visual Studio 与 SSMS 22（WPF 编辑器）中均已实测，自动与手动切换正常。微软拼音在 SSMS 22 尚未单独实测，欢迎反馈。
- Windows 的输入态可能受“每个应用窗口使用不同输入法”等系统选项影响；扩展不能保证离开宿主后系统完全不会继承原输入态。

后续版本继续按 `MANUAL-TESTS.md` 执行回归验证。扩展不会修改默认输入法、微软拼音兼容设置、宿主工作负载或日常配置。