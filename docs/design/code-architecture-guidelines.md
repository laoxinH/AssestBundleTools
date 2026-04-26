# AssestBundleTools 代码设计规范

## 1. 文档目的

本文档用于约束项目后续实现阶段的代码设计方式，核心目标如下：

- 遵循 Clean Architecture，保证模块职责清晰
- 控制依赖方向，降低耦合
- 让功能扩展、替换实现、单元测试更容易
- 为多语言切换预留完整支持，避免后续返工

本文档是后续实现的基础规范，默认适用于：

- 解决方案结构
- 分层设计
- 模块职责
- UI 与业务交互方式
- 本地化与字符串管理

## 2. 核心原则

### 2.1 Clean Architecture 优先

代码按“业务规则向内、实现细节向外”的方向组织。

依赖方向必须满足：

- `Presentation` 依赖 `Application`
- `Infrastructure` 依赖 `Application` 和 `Domain`
- `Application` 依赖 `Domain`
- `Domain` 不依赖任何外层模块

禁止反向依赖：

- `Domain` 不允许引用 WinUI、文件系统、数据库、第三方 UI 库
- `Application` 不允许直接依赖具体文件读写、窗口对象、对话框对象
- `ViewModel` 不允许直接调用解析库、文件系统 API、序列化 API

### 2.2 业务逻辑不能放在 UI 层

UI 层只负责：

- 展示状态
- 收集用户输入
- 触发 Use Case
- 响应结果并刷新界面

以下逻辑不应放在 `View` 或 `ViewModel` 中：

- AB 解析规则
- 资源兼容性判断
- 替换校验
- 保存重建流程
- 工作区状态持久化策略

### 2.3 先抽象业务，再接具体实现

例如：

- 先定义 `IAssetBundleReader`
- 再在 `Infrastructure` 中实现具体 Unity/AB 读取适配器

例如：

- 先定义 `IWorkspaceRepository`
- 再决定是存本地 JSON、SQLite 还是别的形式

### 2.4 默认面向扩展设计

本项目后续大概率会继续增加：

- 新资源类型预览器
- 新替换规则
- 批处理任务
- 多工作区与标签页浏览能力
- 更多导出与重建方式

因此代码要优先支持“增加新实现”，而不是频繁修改既有核心流程。

## 3. 推荐的解决方案结构

当前解决方案已经演进为多项目结构，后续实现默认继续沿用这一边界，不再回退到单项目堆叠模式。

当前采用并继续推荐的结构如下：

```text
src/
  AssestBundleTools.Presentation.WinUI/
  AssestBundleTools.Application/
  AssestBundleTools.Domain/
  AssestBundleTools.Infrastructure/

tests/
  AssestBundleTools.Domain.Tests/
  AssestBundleTools.Application.Tests/
  AssestBundleTools.Infrastructure.Tests/
```

如果第一阶段暂时不拆多个项目，也必须先按相同边界划分命名空间和目录，避免后续迁移困难。

## 4. 各层职责

## 4.1 Domain 层

Domain 层负责项目最核心、最稳定的业务规则。

建议放置：

- 实体 `Entity`
- 值对象 `ValueObject`
- 领域枚举
- 领域服务
- 领域规则
- 与具体实现无关的业务异常或错误码定义

适合放进 Domain 的内容示例：

- `AssetBundleId`
- `AssetId`
- `AssetType`
- `WorkspaceSession`
- `AssetReplacementRule`
- `AssetCompatibilityResult`

不应放进 Domain 的内容：

- 文件路径选择器
- 窗口管理
- `ResourceLoader`
- WinUI 控件
- JSON 存储细节

## 4.2 Application 层

Application 层负责组织 Use Case，是业务流程编排中心。

建议放置：

- Commands / Queries
- Use Case Handler
- DTO
- 应用层接口 `Port`
- 输入校验
- 工作流编排
- 应用层返回结果模型

适合放进 Application 的内容示例：

- `OpenWorkspaceCommand`
- `OpenWorkspaceHandler`
- `LoadBundleTreeQuery`
- `ExtractAssetsCommand`
- `ReplaceAssetFromFileCommand`
- `ReplaceAssetFromWorkspaceCommand`
- `SaveWorkspaceChangesCommand`

Application 层可以依赖的接口示例：

- `IAssetBundleReader`
- `IAssetBundleWriter`
- `IWorkspaceStore`
- `ILogService`
- `IClock`

注意：

- `IFilePickerService`、`IDialogService`、`IWindowRegistry` 这类接口只应由 Presentation 调用，若 Use Case 需要用户交互，应由 UI 先收集好输入再调用 Use Case
- Application 更适合依赖纯业务或纯技术抽象，不要让 Use Case 主动弹窗

## 4.3 Infrastructure 层

Infrastructure 层负责所有外部依赖和技术实现。

建议放置：

- AB 文件读写实现
- 文件系统访问
- 设置持久化
- 工作区缓存
- 日志实现
- 第三方 Unity 解析库适配
- 序列化实现

示例：

- `UnityAssetBundleReader`
- `UnityAssetBundleWriter`
- `JsonWorkspaceStore`
- `FileSystemService`
- `SettingsRepository`

Infrastructure 的原则：

- 对外只暴露接口实现
- 不承载业务决策
- 不拼装 UI 文本
- 外部进程型解密器、脚本清单发现和命令行协议应放在 Infrastructure，而不是下沉到 Application 或散落到 Presentation

## 4.4 Presentation.WinUI 层

Presentation 层负责 WinUI 页面、交互和展示状态。

建议放置：

- Views
- ViewModels
- 页面导航
- 页面级标签页协调
- 拖拽事件桥接
- 对话框调用
- 本地化资源
- 资源格式化展示

示例：

- `ShellPage`
- `BundleExplorerPage`
- `AssetPaneViewModel`
- `InspectorPanelViewModel`
- `BundleTabCoordinator`
- `LocalizationService`

Presentation 层可持有的 UI 服务示例：

- `IFilePickerService`
- `IDialogService`
- `ILocalizationService`

UI 层特别注意：

- 不直接写业务规则
- 不直接 new 解析器
- 不直接操作保存流程
- 只通过 `Application` 暴露的 Use Case 完成功能
- `App` 或其他组合根负责组装 `Handler / Store / Service` 依赖
- `WorkspaceBrowserService`、`WorkspaceCatalogService` 这类 Presentation 门面可以保留，但不应在内部继续 `new Handler`

## 5. 推荐的目录组织方式

每层内部建议采用“按功能分目录”，而不是纯技术分堆。

示例：

```text
AssestBundleTools.Application/
  Workspace/
    Commands/
    Queries/
    Dtos/
  Bundles/
    Queries/
    Dtos/
  Assets/
    Commands/
    Queries/
    Dtos/
  Replacement/
    Commands/
    Services/
  Saving/
    Commands/

AssestBundleTools.Domain/
  Workspace/
  Bundles/
  Assets/
  Replacement/
  Shared/
```

这样做的好处是：

- 同一业务功能相关代码聚在一起
- 后续新增资源类型或用例时不容易散乱
- 拆分模块和编写测试更方便

## 6. 模块职责建议

建议先按下面这些核心业务模块来设计。

### Workspace 模块

负责：

- 工作区生命周期
- 已加载 Bundle 集合
- 当前变更队列
- 当前打开资源包标签页或窗格上下文

建议继续细分为两个子角色：

- `WorkspaceSession`
  负责运行时已加载数据和变更队列
- `WorkspaceCatalog`
  负责最近工作区、固定工作区和恢复元数据

### Bundle Browser 模块

负责：

- Bundle 树加载
- 依赖树构建
- 资产列表查询
- 过滤、搜索、排序

### Asset Extraction 模块

负责：

- 单个或批量资源导出
- 导出命名规则
- 导出冲突处理

### Asset Replacement 模块

负责：

- 外部文件替换
- 工作区内资源拖拽替换
- 替换兼容性校验
- 替换确认与撤销

### Save / Build 模块

负责：

- 变更汇总
- 输出前校验
- 另存为新包
- 保存日志与结果反馈

### Localization 模块

负责：

- 当前 UI 语言
- 文本资源读取
- 语言切换通知
- 格式化文本输出

## 7. 依赖约束

必须遵守以下规则：

- `Domain` 中不得出现任何 WinUI 命名空间
- `Domain` 中不得出现文件系统路径选择逻辑
- `Application` 中不得引用 `Window`、`Page`、`FrameworkElement`
- `Application` 中不得返回已本地化完成的 UI 文案
- `Infrastructure` 不得直接更新 ViewModel
- `ViewModel` 不得直接访问磁盘文件

推荐引入统一的结果模型，例如：

- `Result`
- `Result<T>`
- `ErrorCode`
- `MessageToken`

这样可以把“是否成功”“失败原因”“用户提示键”分离开。

## 8. MVVM 设计约定

WinUI 层建议统一采用 MVVM。

推荐规则：

- 每个页面或复合控件对应独立 ViewModel
- ViewModel 只暴露可绑定状态和命令
- ViewModel 通过 Use Case 获取数据
- 页面之间通过导航参数或工作区上下文传递状态

建议避免：

- 在 code-behind 中写复杂逻辑
- 在 ViewModel 中保留大量 UI 控件引用
- 在 ViewModel 中直接做文件系统遍历

## 9. 多语言与字符串规范

这是本项目必须从第一天就执行的约束。

### 9.1 禁止硬编码用户可见文本

以下内容不得硬编码在 XAML 或 C# 中：

- 按钮文字
- 页签标题
- 菜单项文字
- 对话框标题和内容
- 提示信息
- 错误信息
- 空状态文案
- 状态栏文案
- 占位符文本

例如不允许：

```xml
<Button Content="打开文件" />
```

例如不允许：

```csharp
StatusMessage = "保存成功";
```

### 9.2 资源来源

UI 文本统一来自资源文件，建议使用 WinUI 标准资源机制。

建议目录：

```text
Strings/
  zh-CN/Resources.resw
  en-US/Resources.resw
```

建议首批至少支持：

- `zh-CN`
- `en-US`

### 9.3 资源键命名规范

资源键必须稳定、可读、可搜索。

推荐命名方式：

- `Navigation.Home`
- `Navigation.BundleExplorer`
- `Command.OpenFile`
- `Command.SaveAs`
- `Inspector.Tab.Overview`
- `Dialog.SaveBundle.Title`
- `Dialog.SaveBundle.Confirm`
- `Status.WorkspaceLoaded`
- `Error.AssetTypeMismatch`

不要使用：

- 中文资源键
- 无意义编号键
- 依赖当前文案的键名

### 9.4 多语言切换规则

必须从架构上支持语言切换。

建议规则：

- 当前语言由统一的 `IAppLanguageService` 管理
- 语言设置持久化到配置中
- 默认跟随系统语言，用户可手动切换
- 语言切换后，当前已打开的页面与标签页承载视图都应收到刷新通知

如果 V1 不做完全实时热切换，至少也要保证：

- 语言设置可保存
- 下次启动能正确加载
- 所有文本都已资源化，不存在硬编码阻碍后续切换

### 9.5 应用层与本地化的边界

`Domain` 和 `Application` 不应直接拼装最终展示文本。

推荐做法：

- 应用层返回 `ErrorCode`
- 或返回 `MessageToken`
- UI 层根据 `Key + 参数` 再解析为本地化文本

例如：

```text
Application 返回:
  ErrorCode = AssetTypeMismatch
  Parameters = { SourceType: Texture2D, TargetType: TextAsset }

Presentation 根据资源键:
  Error.AssetTypeMismatch
生成最终提示文本
```

这样做有几个好处：

- Application 层不依赖语言
- 错误提示可统一翻译
- 后续新增语言不需要修改业务代码

### 9.6 字符串例外规则

以下字符串可以不走本地化资源：

- 代码常量名
- 日志类别名
- 协议名
- JSON 字段名
- 文件扩展名
- 资源键自身

但只要是直接面向用户显示的文本，就必须资源化。

### 9.7 日志与错误消息

建议把“诊断日志”和“用户提示”分开：

- 开发诊断日志可以使用统一技术语言，建议英文或稳定错误码
- 用户看到的提示必须本地化

例如：

- 日志写入：`Asset replacement validation failed`
- UI 提示显示：资源替换校验失败

## 10. 命名与代码风格建议

### 10.1 命名

建议规则：

- 类名使用清晰业务语义
- 命令使用 `Verb + Noun + Command`
- 查询使用 `Verb + Noun + Query`
- 处理器使用 `Handler`
- DTO 使用 `Dto`
- 接口使用 `I` 前缀

示例：

- `OpenBundleInPaneCommand`
- `ReplaceAssetFromWorkspaceCommand`
- `GetAssetPreviewQuery`
- `AssetPreviewDto`

### 10.2 方法

建议规则：

- 异步方法统一以 `Async` 结尾
- 需要可取消的长操作必须接收 `CancellationToken`
- 不要在方法中同时承担查询、修改、UI 通知三类职责

### 10.3 类型职责

建议：

- 一个类只处理一个核心职责
- 一个文件尽量只放一个公开类型
- 避免巨型 Service 或巨型 ViewModel

## 11. 状态管理建议

由于项目支持主窗口、多工作区和标签页浏览，必须尽早统一状态模型。

建议拆分为：

- 工作区状态 `WorkspaceState`
- 工作区目录状态 `WorkspaceCatalogState`
- 标签页状态 `DocumentTabState`
- 窗格状态 `PaneState`
- 详情区状态 `InspectorState`

原则：

- 工作区状态是单一事实来源
- 工作区目录状态负责“有哪些工作区、当前活动工作区是谁”
- 标签页与窗格状态只保存视图上下文
- 所有变更先写入工作区状态，再反映到 UI

建议进一步约束：

- `WorkspaceSessionRegistry` 不承担最近工作区持久化
- `WorkspaceCatalogStore` 不直接持有大体量运行时资产数据
- `WorkspaceContextService` 只协调当前 UI 会话中的活动工作区和摘要列表
- 页面切换工作区时，先切活动工作区，再重新查询页面数据

## 12. 测试建议

至少保证以下测试边界：

- `Domain` 层单元测试
- `Application` 层 Use Case 单元测试
- 替换兼容性测试
- 保存流程测试
- 本地化资源完整性测试

本地化资源完整性测试建议校验：

- 关键资源键是否缺失
- `zh-CN` 和 `en-US` 键集合是否一致

## 13. 实现前建议的落地顺序

建议按下面顺序推进：

1. 先调整解决方案结构或至少完成命名空间分层
2. 建立 `Domain / Application / Infrastructure / Presentation` 边界
3. 建立工作区状态模型
4. 建立本地化资源机制和语言服务
5. 再开始落 WinUI 页面骨架
6. 最后逐步接入 AB 读取、提取、替换、保存等 Use Case

## 14. 本文档的执行结论

后续代码实现默认遵循以下约束：

- 采用 Clean Architecture
- 业务逻辑不写进 UI
- 用户可见文本不允许硬编码
- 多语言切换从架构层面预留
- 结果模型与本地化文本分离
- 代码按模块职责清晰拆分

## 15. 当前实现偏差待收口

以下问题当前先记录为架构代办，不阻塞正在推进的 M9 功能链路：

1. `Application` 层部分 Handler 仍保留默认回退到 `InMemoryWorkspaceSessionRegistry.Shared` 的构造方式，后续应统一改为显式构造注入，避免应用层偷偷依赖具体实现。
2. `Presentation` 层的 `WorkspaceContextService` 当前仍直接调用 `IWorkspaceCatalogStore / IWorkspaceMetadataStoreFactory` 读写目录摘要与浏览状态，后续应把这部分持久化收敛为明确的 Application Use Case。
3. `Presentation` 层的 `WorkspaceBrowserService / WorkspaceCatalogService` 当前仍直接持有具体 Handler 类型，后续可继续评估是否收口为更稳定的应用层网关接口，减少 UI 对用例实现细节的耦合。

后续处理原则：

- 优先保证依赖方向正确，再逐步降低组合根之外的实现细节暴露
- 架构收口尽量与后续功能开发同向进行，避免为单次“纯重构”打断主链路
- 收口过程中应同步清理默认单例回退和已失效的临时兼容代码
