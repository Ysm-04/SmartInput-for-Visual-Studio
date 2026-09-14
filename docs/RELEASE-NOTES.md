# 发布说明

作者：**我在人间做废物**

## 0.2.1

状态：修复版。针对搜狗输入法在 Visual Studio 2026 中状态写入成功但实际输入态未切换的问题进行兼容性修复。

包路径：

```text
src/SmartInput.VisualStudio/bin/Release/SmartInput.VisualStudio.vsix
```

SHA256：构建完成后以 `README.md` 和 `docs/VALIDATION.md` 中记录的值为准。

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
