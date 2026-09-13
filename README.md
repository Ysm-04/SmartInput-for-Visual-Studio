# Smart Input for Visual Studio

作者：**我在人间做废物**

面向中文开发者的本地智能输入态切换扩展，项目名为 **Smart Input**。本项目独立实现，参考 Smart Input Pro 的交互思路，不包含其代码、资源、授权或收费系统。

**当前版本为 0.2.0，已完成当前支持范围内的实机验证。** 扩展提供 `工具 > 选项 > Smart Input > 常规` 设置页、左下角状态栏和 `工具` 菜单暂停/恢复命令，并支持微软拼音和搜狗拼音。构建、自动化测试、VSIX 包结构校验以及 Windows 10/11、Visual Studio 2022/2026 x64 实机验证已通过。

## 支持范围

- 宿主：Visual Studio 2022 **17.14+** 和 Visual Studio 2026，x64。
- 系统：Windows 10 / Windows 11 x64。宿主 VS 和 .NET Framework 4.8 也必须满足各自要求。
- 语言：C、C++、C# 的可编辑文档视图。
- 输入法：当前活动的**微软拼音或搜狗拼音**。扩展只切换其内部中英文状态，不安装、添加或主动切换键盘布局，也不控制其他输入法。

## 交互规则

| 场景 | 自动输入态 |
| --- | --- |
| 代码、字符字面量 | 英文 |
| 单行、多行、三斜线注释正文 | 中文 |
| 包含汉字的字符串文本段 | 中文 |
| 空字符串、纯英文字符串 | 英文 |
| C# 插值表达式 | 英文；表达式内部的注释/字符串单独判断 |

启动时不修改系统默认输入法。第一次进入受支持编辑器时按当前场景初始化：代码为英文，注释为中文。启动页、搜索框、终端等不接管。

手动切换（包括 Shift）以**实际读到的输入态变化**为准，不拦截或模拟 Shift。偏离当前自动规则时保留手动选择并显示配置颜色光标；在同一文本区域持续输入不会立即被切回。切回推荐状态、离开该区域、切换文档或失焦后恢复自动规则。手动覆盖按文本区域而非固定秒数保持。

**不固定要求使用 Shift。** 请使用输入法设置中实际配置的中英文切换方式，例如微软拼音的 Ctrl＋空格或已启用的 Shift。如果系统没有启用 Shift，单按 Shift 不会切换，扩展也不会将它误判为手动切换。快捷键与 VS 命令冲突时，以是否真正改变输入法状态为准。扩展不自动修改系统快捷键设置。

0.2.0 在手动覆盖时使用当前视图的独立光标装饰，临时隐藏原生光标；退出覆盖后移除装饰并恢复原生光标。光标颜色可在 `工具 > 选项 > Smart Input > 常规` 中设置。不会修改正文颜色或主题格式，闪烁间隔读取系统配置。

编辑器底部的 `Smart Input` 按钮显示状态；单击暂停/恢复所有文档的自动切换。暂停也可通过 `工具 > Smart Input: 暂停自动切换/恢复自动切换` 操作，便于隐藏状态栏后使用。暂停仅保存在当前 VS 进程内，重启 VS 后恢复为未暂停。

长期设置位于 `工具 > 选项 > Smart Input > 常规`：

- 启用自动切换。
- 显示左下角状态栏。
- 手动覆盖光标颜色。
- 是否自定义状态栏文字颜色。
- 状态栏文字颜色。

安全边界：

- 组词期间不切换；有选区或多个光标时暂不切换。
- 只有本进程的编辑器获得键盘焦点时才发出切换请求。
- 每次切换后读取状态确认，600ms 未确认则停止当前区域内的重试。
- 未识别出微软拼音/搜狗拼音或无法读取状态时不猜测、不操作。
- 超过 2M 字符的文件不自动分析；分析在后台进行，旧快照会取消。
- 不修改源文本，不自动替换标点；不联网、不采集源码、按键或候选词。
- 不写入“字体和颜色”或正文画刷；失焦、暂停、关闭文档时移除自定义光标装饰并恢复原生光标。

## 构建与测试

需要 MSBuild（VS 2022 17.14+ 或兼容版本）和 .NET Framework 4.8 目标包。**不需要 .NET SDK，也不需要先安装 VS 扩展开发工作负载**：编辑器引用和 VSIX 构建任务从 NuGet 项目依赖获得。

```powershell
.\build.ps1
# 已还原依赖时可离线构建
.\build.ps1 -SkipRestore
# 也可显式指定 MSBuild
.\build.ps1 -MSBuildPath 'C:\Path\To\MSBuild.exe'
```

脚本只还原、构建、测试和检查打包内容，不安装扩展，不启动 VS。依赖下载到项目 `.packages` 目录。

输出：`src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix`。

0.2.0 Release VSIX SHA256：

```text
79C25E4FB6F0BD0C1DB7ED3D1F01267421664E69108612E5CF8EB173F237480B
```

## 安装与使用

当前 VSIX 未做代码签名，发布者显示为“我在人间做废物”。安装时如果 VSIX Installer 或 Windows 显示未知发布者提示，请先核对 VSIX 来源和 SHA256，再决定是否继续。首次部署或排查问题时，建议优先使用 VS 实验实例或其他非日常实例。

安装后重启 Visual Studio，并检查：

- `工具 > 选项 > Smart Input > 常规` 是否存在。
- `工具` 菜单中是否存在 `Smart Input: 暂停自动切换` 或 `Smart Input: 恢复自动切换`。
- 打开 C/C++ 或 C# 文件后，编辑器底部是否出现 `Smart Input` 状态按钮，除非已在设置中隐藏。

## 代码结构

- `src/SmartInput.Core`：无外部依赖的词法边界分析与手动覆盖状态机。
- `src/SmartInput.VisualStudio`：MEF 编辑器接入、VS 设置页和工具菜单命令、后台快照分析、微软拼音/搜狗拼音状态读写、临时光标颜色、状态栏按钮。
- `tests/SmartInput.Tests`：可直接运行的控制台测试程序，失败返回非零退出码。
- `tools/Verify-Package.ps1`：验证 VSIX 内容，不加载或安装扩展。
- `docs/MANUAL-TESTS.md`：安装前提、实机验证步骤和回归项目。
- `docs/VALIDATION.md`：验证结果、兼容性和已知边界。
- `docs/RELEASE-NOTES.md`：发布说明和版本变化。
- `docs/PRIVACY.md`：本地处理与隐私边界说明。
- `tools/Inspect-Pinyin.ps1`：Windows PowerShell 5.1 下的只读 COM 互操作检查，不执行输入态切换。
- `tools/Inspect-InputMethods.ps1`：Windows PowerShell 5.1 下的微软拼音/搜狗拼音 Profile 只读检查，不执行输入态切换。
- `docs/CARET-FIX.md`：手动覆盖光标装饰层的技术原因、实现方式和验证边界。

后台词法扫描器负责引号、注释和插值的边界分析，VS 语法分类辅助排除非活动代码；它不是完整编译器。当前实现已覆盖常见原始字符串和插值结构，但 C++ 预处理器复杂分支/拼接、C# 插值格式段和不完整语法仍可能存在识别偏差。分类信息尚未更新时，可能暂时按词法结果判断。

## 兼容性与已知限制

0.2.0 共 **104 项自动化测试通过**，包含 Profile 匹配、23 项光标绘制与生命周期检查，以及离屏 WPF 渲染的颜色像素检查。VSIX 校验覆盖 MEF 入口、VS Package 入口、菜单资源、设置页注册、目标版本、依赖隔离和程序集版本一致性。

已验证范围包括 Windows 10、Windows 11、Visual Studio 2022、Visual Studio 2026，以及微软拼音和搜狗拼音的代码、注释、字符串、候选窗、Escape、手动覆盖、连续输入、切换文档和共存场景。

Windows 的输入态可能受“每个应用窗口使用不同输入法”等系统选项影响。本扩展限制自己的调用范围，但不能仅凭这一点保证切出 VS 后系统完全不会继承原输入态。

以下范围不属于当前支持承诺：

- ARM64。
- 微软拼音旧版兼容模式。
- C++ 预处理器复杂分支/拼接、C# 插值格式段和不完整语法的全部边界。

## 许可证与贡献

作者：**我在人间做废物**。

本项目采用 [MIT License](LICENSE)。除许可证文本中规定的条件外，使用、修改、分发本项目不附加额外限制。项目依赖遵循各自许可证，不代表本项目许可证覆盖第三方代码、商标或资源。

提交代码或文档时，请确保提交内容由提交者拥有相应权利，或已取得必要授权；新增第三方依赖时，请同时说明其来源和许可证。

问题反馈请提供系统版本、Visual Studio 版本、扩展版本、输入法版本、操作步骤和实际表现。提交问题时请避免上传私人源码、候选词或完整按键记录。

参考：[VS 2026 扩展兼容模型](https://learn.microsoft.com/zh-cn/visualstudio/extensibility/migration/extension-compatibility?view=visualstudio)、[VS 光标格式接口](https://learn.microsoft.com/en-us/visualstudio/extensibility/walkthrough-customizing-the-text-view?view=vs-2022)、[WPF InputMethod](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.inputmethod?view=netframework-4.8)。
