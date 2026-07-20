<p align="center">
  <img src="src/AssestBundleTools.Presentation.WinUI/Assets/AppLogo.png" width="96" alt="AssestBundleTools 图标">
</p>

# AssestBundleTools

[English](README.en.md) | 简体中文

AssestBundleTools 是一款面向 Unity AssetBundle 的 Windows 工作区工具，用于识别、浏览、预览、提取、替换并重新构建游戏资源包。项目重点处理加密 AB、资源关联、可恢复修改记录和原加密方式导出等实际模组工作流。

> 项目仍处于预览阶段。请保留原始资源备份，并在游戏内实际验证重新构建的资源包。

完整的新建工作区、拖拽导入、Texture2D / AudioClip / Spine 替换与导出演示见 [图文使用指南](docs/user-guide.md)。

## 主要功能

- 工作区管理：导入文件或目录、缓存索引、恢复浏览状态和增量刷新。
- AB 自动识别：依次匹配可用解密器，最后回退到有效 Unity 资源识别，排除普通图片、压缩包等无关文件。
- 内置解密器：包含 Arknights LZ4AK 与 CrossCore duplicated-header 加解密 CLI，并保留来源加密元数据用于导出回写。
- 资源浏览与预览：支持常见 Unity 资产信息、依赖关系、图片、音频、Mesh、文本、Spine 等预览链路。
- 提取与替换：支持单资源、批量资源和 Spine 组的提取、文件替换及拖放工作流。
- 保存与导出：按工作区修改重新构建 AB，可选择压缩策略并按来源 codec 重新加密。
- 扩展能力：支持外部 AssetBundle codec CLI 和资源处理器 CLI，并提供在线组件安装基础设施。
- 现代 Windows UI：支持浅色 / 深色主题、Windows 10 / 11 图标兼容以及 125% / 150% 高 DPI 布局。

## 下载与安装

请从 [GitHub Releases](https://github.com/laoxinH/AssestBundleTools/releases) 下载最新版本。

| 架构 | 适用设备 | 发行形式 |
| --- | --- | --- |
| `win-x64` | 大多数 Intel / AMD 64 位电脑 | Portable ZIP / Installer MSI |
| `win-x86` | 需要 32 位兼容的电脑 | Portable ZIP / Installer MSI |
| `win-arm64` | Windows on ARM 设备 | Portable ZIP / Installer MSI |

MSI 安装向导允许选择当前用户或所有用户、安装位置和桌面快捷方式。当前发布包尚未进行代码签名，因此 Windows SmartScreen 可能显示安全提示；请只从本仓库正式 Release 下载。

便携版解压后直接运行。不要把工作区数据保存在应用解压目录内，以免后续更新或重新解压时混淆应用文件和用户数据。

## 基本流程

1. 新建工作区并选择数据目录。
2. 选择自动识别或指定 AssetBundle 解密器。
3. 拖入 AB 文件或包含 AB 的目录，等待识别和索引完成。
4. 浏览资源、依赖与预览，执行提取、替换或编辑操作。
5. 使用“保存为新包”导出修改后的 AB，并在目标游戏环境中验证。

工作区默认保留原始输入，修改通过可恢复操作记录管理。涉及替换和导出时仍建议自行保存独立备份。

## 更新

发布版本会在启动后检查 GitHub Release，关于页面也提供手动检查入口。发现新版本时，确认窗口会显示对应版本的中文更新内容。安装版与便携版使用各自架构的独立更新通道；自动应用与重启链路仍在持续进行跨版本和多设备验证。

## 开发与构建

主要技术栈：

- .NET 8
- WinUI 3 / Windows App SDK
- AssetsTools.NET
- Velopack
- xUnit 与仓库内控制台测试套件

仓库采用分层结构：

```text
Presentation.WinUI -> Application -> Domain
Infrastructure     -> Application + Domain
```

推荐使用 Visual Studio 的 MSBuild 构建 WinUI 项目。环境准备、平台选择和测试命令见 [调试与构建说明](docs/design/debug-setup.md)，架构约束见 [代码架构规范](docs/design/code-architecture-guidelines.md)，设计文档入口见 [设计文档索引](docs/design/README.md)。

## 贡献

提交修改前请阅读 [AGENTS.md](AGENTS.md) 和相关设计文档。用户可见文本必须进入本地化资源，解析、缓存和文件 IO 不应直接放入 View 或 ViewModel。

报告问题时建议附上：

- 应用版本、Windows 版本和系统架构；
- 工作区诊断日志；
- 使用的解密器 / codec；
- 可公开的最小复现资源或错误堆栈。

## 项目信息

- GitHub：[laoxinH/AssestBundleTools](https://github.com/laoxinH/AssestBundleTools)
- 官网：[www.modwu.com](https://www.modwu.com)
- 图标绘制：青雷
- 许可协议：[MIT License](LICENSE.md)

仓库包含的第三方组件继续遵循各自的许可和版权声明。
