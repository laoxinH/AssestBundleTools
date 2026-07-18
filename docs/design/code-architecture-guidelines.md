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
- 独立批量操作窗口与后台任务
- 导入解密 / 导出资源处理 CLI 插件
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
- 优先让页面或页面级协调器直接依赖 `Application` 的窄网关接口；若确实保留 Presentation 门面，它也只能作为语义适配层，不能在内部继续 `new Handler`

#### 4.4.1 组合根例外边界

当前允许保留一个**受控例外**：

- `src/AssestBundleTools.Presentation.WinUI/Composition/` 可以直接引用 `AssestBundleTools.Infrastructure`
- 该目录只负责：
  - 实例化具体 `Infrastructure` 实现
  - 组装 `Application` Handler / Gateway
  - 把基础设施细节适配回 `Presentation` 自己的窄抽象

明确禁止：

- `Composition/` 之外的 `Presentation` 文件直接 `using AssestBundleTools.Infrastructure.*`
- `App.xaml.cs`、普通 `Service`、`View`、`ViewModel` 直接依赖 `Infrastructure` 具体类型
- 为了图省事，把新的持久化、文件系统、Reader、Preview 具体实现继续散落到普通 UI 类中

如果某个 Presentation 自有服务需要底层技术能力，但又不适合上升为 `Application` 端口，应：

1. 先在 `Presentation` 内定义一个窄抽象  
2. 再在 `Composition/` 中用基础设施具体实现做适配  
3. 普通 UI 代码继续只依赖该窄抽象

## 5. 推荐的目录组织方式与命名规则

目录规范不仅约束 `BundleExplorer`，而且适用于整个解决方案。目录名和类型名应共同表达“这一组代码归谁拥有、承担什么职责”，避免目录语义和文件职责长期脱节。

### 5.1 总体规则

- 源码目录优先按“层 -> 功能 -> 角色”组织，而不是先按纯技术角色把全项目横向切碎。
- 目录名统一使用 `PascalCase` 的稳定名词或名词短语，优先表达业务功能、外部集成边界或明确职责。
- `bin/`、`obj/`、`.artifacts/`、`.build-check/` 都属于生成或检查产物目录，不计入项目逻辑结构。
- 避免新建 `Helpers`、`Utils`、`Misc`、`Temp`、`Manager` 这类低语义目录；`Common` 只用于确实被多个功能共享、且暂时不适合归属到单一功能根的稳定内容。
- 不要为了机械统一单复数而大规模重命名现有稳定目录；是否调整命名，应以语义清晰度、迁移成本和当前文档约束为准。

### 5.2 解决方案顶层

- `src/` 下每个项目目录对应一个稳定层或可独立构建的模块。
- `tests/` 下测试项目按生产层所有权镜像命名，避免把所有测试揉进单一项目。
- 新项目目录继续采用 `AssestBundleTools.<Layer>[.<Flavor>]` 这一命名模式。

### 5.3 Application 层

`Application` 内部继续采用“按功能分目录”的主结构。

推荐形态：

```text
AssestBundleTools.Application/
  Assets/
    Commands/
    Queries/
    Dtos/
  Bundles/
    Commands/
    Queries/
    Dtos/
    Services/
  Editing/
    Queries/
    Dtos/
    Services/
  Extraction/
    Commands/
    Dtos/
  Replacement/
    Commands/
    Queries/
    Dtos/
    Services/
  Saving/
    Commands/
    Dtos/
  Extensions/
    Dtos/
    Services/
  Workspace/
    Commands/
    Queries/
    Dtos/
    Services/
    Internal/
  Common/
```

补充规则：

- 顶层优先按功能根组织，如 `Assets`、`Bundles`、`Editing`、`Extraction`、`Replacement`、`Saving`、`Extensions`、`Workspace`、`Common`。`Extensions` 只承载在线组件、插件目录和外部扩展生命周期等稳定契约，不作为通用杂项目录。
- 功能根下允许的常见子目录为 `Commands`、`Queries`、`Dtos`、`Services`、`Internal`。
- 不要在 `Application` 根下再建横切型总目录，如全局 `Handlers/`、`Models/`、`Helpers/`。
- `Common/` 仅放跨多个功能根复用、且不属于单一业务场景的应用层契约、结果模型或共享支持类型。

### 5.4 Domain 层

`Domain` 目录应按稳定业务概念组织，而不是先按纯技术角色总分堆。

推荐形态：

```text
AssestBundleTools.Domain/
  Assets/
  Bundles/
  Workspace/
  Common/
```

补充规则：

- `Domain` 顶层目录优先表达业务概念，如 `Assets`、`Bundles`、`Workspace`、`Common`。
- 不要在整个 `Domain` 根下统一建 `Entities/`、`ValueObjects/`、`Enums/` 这类纯技术分类总目录；若单一功能根内部确有必要，可在该功能根内部局部细分。
- `Common/` 只承载稳定的跨功能领域概念，不得混入基础设施、UI 或序列化实现暗示。

### 5.5 Infrastructure 层

`Infrastructure` 目录按外部集成边界和技术能力组织。

推荐形态：

```text
AssestBundleTools.Infrastructure/
  Bundles/
    Decryption/
    Previewing/
    Reading/
    Internal/
  Settings/
    Persistence/
  Extensions/
    Installation/
  Workspace/
    Importing/
    Persistence/
    Runtime/
```

补充规则：

- 顶层优先按功能或外部集成边界组织，如 `Bundles`、`Extensions`、`Settings`、`Workspace`。`Extensions/Installation` 用于可信在线组件来源、下载校验与安装实现。
- 第二层目录按能力或实现职责命名，如 `Reading`、`Previewing`、`Decryption`、`Importing`、`Persistence`、`Runtime`。
- 适配器、持久化实现、Reader / Writer、缓存实现应放在其所属功能根下，不要集中堆到全局 `Adapters/`、`Repositories/`、`Utilities/`。
- `Internal/` 只用于某个功能根内部不希望继续扩散的实现细节，不应用来收纳跨功能共享逻辑。

### 5.6 Presentation.WinUI 层

`Presentation.WinUI` 允许保留少量 WinUI 语义明确的顶层技术壳目录，但壳内仍要优先按功能组织。

当前允许存在的顶层技术壳包括：

- `Views`
- `ViewModels`
- `Services`
- `Composition`
- `Localization`
- `Strings`
- `Diagnostics`
- `Assets`
- `Properties`

补充规则：

- 这些顶层技术壳之下，优先按功能目录继续组织，如 `ViewModels/BundleExplorer`、`ViewModels/Home`、`ViewModels/Shell`。
- `ViewModels/<Feature>/` 应理解为“该功能的 Presentation 代码包”，而不是“这里的每个文件都必须是 ViewModel”。
- 只有直接参与绑定、通知或命令暴露的类型才应命名为 `*ViewModel`。
- 不直接参与绑定的协作对象，应使用 `Coordinator`、`Presenter`、`Formatter`、`Resolver`、`Loader`、`ActionHandler`、`Contracts` 等更明确的职责命名；不要为了迎合目录名而伪装成 `ViewModel`。
- 对于很小的功能范围，功能目录下可以临时并列放置少量协作对象；但当某个功能目录开始长期承载多类职责时，应继续在该功能目录内拆出明确子目录，如 `Bindings`、`Coordinators`、`Presenters`、`Formatting`、`Contracts`、`ActionHandlers`、`State`。
- `Services/` 仅承载应用级或多功能共享的 Presentation 服务；功能局部工作流协作对象不应因为“也是服务”就提升到全局 `Services/`。
- 当 `Services/` 已长期承载多个稳定主题时，也应继续在壳内按稳定职责拆分，如 `Services/Feedback`、`Services/Preferences`、`Services/Platform`、`Services/Workspace`、`Services/AssetBundles`；不要把通知、偏好、工作区编排、平台适配和资源包展示辅助长期平铺在同一层。
- `Views/` 可以保留少量应用级页面文件；可复用控件放在 `Views/Controls/`，应用级共享视图表面放在 `Views/Shared/`，持续增长的功能视图族应优先落到 `Views/<Feature>/`。
- 当某个功能已经同时拥有页面和局部控件时，页面本身也应优先进入 `Views/<Feature>/`，不要继续把 `*Page` 平铺在 `Views/` 根目录；`ShellPage` 这类应用级壳页可保留在根目录。
- 当某个控件明显只服务单一页面域时，应优先放到 `Views/<Feature>/Controls/`；`Views/Controls/` 更适合跨页面共享对话内容、通用控件或暂时无法归属到单一功能的视图片段。
- 复杂 XAML 不能因为“只是标记语言”而无限扩充。若页面或控件中的某一块已经形成稳定独立表面，如预览区、音频传输控件、裁切覆盖层、加载/空态组合、重复卡片族，应优先拆成同功能目录下的独立 `UserControl` 或资源字典，而不是继续塞回父级 XAML。
- 当页面 code-behind 开始长期承载明确的页面视图行为协作，如窗口注册、标签同步、局部动画或页面静态 chrome 同步时，应优先在 `Views/<Feature>/Coordinators/`、`Views/<Feature>/Presenters/` 等明确子目录中拆出页面协作对象，而不是继续把这些职责堆回单一 `*Page.xaml.cs`。
- 仍处于 `ViewModels/<Feature>/` 根目录平铺多角色文件的区域，应视为可继续渐进收口的功能包过渡态；后续新增类型时，优先在该功能包内补齐明确子目录，而不是继续把不同角色平铺在同一层。
- `ViewModels/Common/Support/` 这类共享 MVVM 基础支撑目录可以保留，但只应用于跨功能复用的稳定基类、命令封装和轻量支撑类型。

示例：

```text
AssestBundleTools.Presentation.WinUI/
  ViewModels/
    BundleExplorer/
      Bindings/
      ActionHandlers/
      Coordinators/
      Presenters/
      Formatting/
      Contracts/
    Shell/
      Bindings/
      Contracts/
    Common/
      Support/
    Home/
  Services/
    Feedback/
    Preferences/
    Platform/
    Workspace/
    AssetBundles/
  Views/
    BundleExplorer/
      BundleExplorerPage.xaml
      Coordinators/
      Controls/
    Home/
      HomePage.xaml
    SettingsDiagnostics/
      SettingsDiagnosticsPage.xaml
      Controls/
    Controls/
    Shared/
    ShellPage.xaml
```

### 5.7 Tests 层

测试目录同样遵循“按生产所有权镜像，再按功能组织”的原则。

推荐规则：

- 测试项目名称继续镜像生产层，如 `AssestBundleTools.Application.Tests`、`AssestBundleTools.Presentation.Tests`。
- 测试项目内部优先按生产功能根组织，如 `Assets`、`Editing`、`Workspace`、`Replacement`、`Saving`。
- `Testing`、`TestDoubles`、`Architecture` 这类支持目录可以保留，但必须保持语义单一，不能逐渐演变成新的兜底堆放区。

这样做的好处是：

- 同一业务功能相关代码聚在一起
- 后续新增资源类型或用例时不容易散乱
- 目录语义、类型命名和架构边界可以相互印证
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

### Asset Editing 模块

负责：

- 编辑草稿引用解析
- 编辑草稿恢复候选诊断
- 后续草稿确认 / 丢弃 / 恢复队列契约
- 与真实保存链路解耦的草稿元数据访问端口

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

### 8.1 ViewModel 之间的依赖边界

`ViewModel` 不要求彼此完全隔离，但依赖关系必须单向、清晰、可维护。

建议规则：

- 允许页面根 `ViewModel` 组合多个子 `ViewModel`。
- 允许子 `ViewModel` 依赖共享服务、共享状态对象或窄接口。
- 子 `ViewModel` 不应反向依赖父 `ViewModel`。
- 同级 `ViewModel` 默认不应直接依赖彼此的具体类型。
- 若多个 `ViewModel` 需要协作，优先通过父级编排、共享状态对象、事件回调或窄接口完成，而不是直接互相调用大量方法。
- `ViewModel` 依赖图必须保持无环，不允许双向依赖或循环依赖。

可接受的结构：

- 页面 `ViewModel` -> 子 `ViewModel`
- 子 `ViewModel` -> `Service / Formatter / Coordinator / Host Interface`
- 子 `ViewModel` -> 只读状态或明确归属的状态宿主

应避免的结构：

- A `ViewModel` <-> B `ViewModel`
- 子 `ViewModel` -> 父 `ViewModel`
- 同级 `ViewModel` 直接依赖多个 sibling `ViewModel` 的完整实现

触发重构的信号：

- 一个 `ViewModel` 需要频繁调用另一个 `ViewModel` 的很多公开方法
- 一个 `ViewModel` 需要了解另一个 `ViewModel` 的大量内部状态细节
- 为了协作而出现多个 `PropertyChanged` / 事件互相转发链

这通常意味着职责边界尚未稳定，应进一步抽出共享状态模型、`Coordinator`、`Presenter` 或更窄的宿主接口。

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

对于跨层传递的“可展示信息”，还应额外遵守：

- `Presentation` 负责把资源键、原因码、结构化字段格式化为最终文案
- `Application` 负责返回稳定语义，不直接拼界面句子
- `Infrastructure` 负责返回原始数据、结构化消息或技术细节，不直接承载本地化 UI 文本

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

### 10.4 单文件规模与拆分阈值

为避免“单文件持续堆功能，最终退化为难以维护的巨型类”，本项目进一步约束手写业务代码文件的规模与拆分时机。

建议阈值：

- 常规手写业务类、Presenter、Coordinator、Handler、ViewModel 目标体量控制在 `200-500` 行。
- 页面级 facade / orchestrator 型 `ViewModel` 允许略大，但软上限为 `600` 行。
- 手写非生成代码文件达到 `800` 行时，视为必须拆分，不应继续直接堆加新功能。
- 手写非生成代码文件超过 `1000` 行时，只能视为阶段性过渡状态，必须在架构文档或路线图中登记后续收口计划。
- XAML 文件同样适用体量和职责审查。页面级 XAML 可以略大于普通控件，但当新增功能会让某个 XAML 继续向 `800+` 行增长，且其中存在独立视觉表面或重复模板族时，应优先拆为局部 `UserControl`、资源字典或明确的页面域控件。

执行语义：

- 若当前变更触达的手写文件已经超过上述阈值，或本次修改导致其越过阈值，而文档已明确了推荐拆分方向，则该次变更应继续完成必要的拆分/收口，不应只在结项说明中报告后延。
- “先报告、后续再说”只适用于拆分路径存在真实歧义，或不同拆分方案会带来明显产品/架构取舍，且现有文档尚未给出答案的情况；遇到这种情况应先和用户确认，而不是自行发明新边界。
- 不允许仅通过 `partial`、区域折叠或机械搬运代码块来规避行数约束；若职责边界、依赖关系和变化原因没有变清晰，视为未完成拆分。
- 对已拆出子状态宿主、`Coordinator`、`Presenter`、`ActionHandler` 的页面域，后续修改不得再把同类职责静默回灌到父级 `ViewModel`，也不得重新长出宽兼容透传层来掩盖耦合。

除行数外，还应同时检查职责数量：

- 一个类如果同时回答超过 `3` 个独立问题，应优先拆分，即使尚未超过行数阈值。
- 一个类如果会因超过 `3` 类不同原因而频繁修改，也应拆分，即使当前实现仍可编译运行。
- 构造函数注入依赖超过 `6` 个时，应触发职责审查，确认该类是否已经混入过多协作对象。

对 `ViewModel` 的额外约束：

- `ViewModel` 只保留可绑定状态、命令入口和少量页面级编排逻辑。
- 只有直接参与 XAML 绑定、需要属性变更通知的对象才命名为 `ViewModel`。
- 不直接参与绑定的逻辑，应优先拆为 `Coordinator`、`Presenter`、`Loader`、`ActionHandler`、`Formatter` 等更明确的协作对象，而不是继续堆进 `ViewModel`。

推荐的拆分维度：

- 列表/树/标签选择与状态同步
- DTO 到检查器或卡片的展示投影
- 预览查询、媒体对象构造与预览诊断
- 提取、替换、保存、撤销等修改动作
- 错误格式化、日志输出与通知反馈
- 浏览状态持久化、恢复与节流保存

超限后的处理原则：

- 优先把“变化节奏不同”的逻辑拆开，而不是只按代码块长度机械切文件。
- 优先抽离不需要直接属性通知的纯协作逻辑，避免生成更多“名为 ViewModel、实为工具箱”的类型。
- 拆分后主类应退回为外观/协调器，而不是继续保留全部细节实现。
- XAML 拆分后，父级 XAML 应只保留布局槽位、局部控件组合和必要事件桥接；具体视觉表面、复杂状态层、媒体控制、透明棋盘格底图、覆盖层绘制等细节应留在被拆出的控件内。
- 若当前阶段因交付节奏暂时无法完全拆分，应在文档中明确登记为过渡状态，而不是默许其持续膨胀。

### 10.4.1 修改后合规检查

每次非琐碎修改结束后，应对本次触达范围执行一次最小合规检查，而不是仅以“代码能编译”作为完成标准。

至少检查以下事项：

- 触达的手写业务文件是否越过 `200-500 / 600 / 800 / 1000+` 约束，以及是否已经出现职责数量失控。
- `ViewModel` 之间是否重新长出 sibling 具体类型依赖、子依赖父、循环依赖或宽兼容透传层。
- 依赖方向是否仍满足 `Presentation -> Application -> Domain` 与 `Infrastructure -> Application + Domain`。
- `Presentation.WinUI/Composition/` 之外是否重新出现 `Presentation -> Infrastructure` 直接引用。
- 目录放置、命名和协作对象命名是否仍符合本文档约束，而不是把新类型塞进泛化目录或伪装成 `ViewModel`。
- 用户可见文本、预览读取链路、工作区持久化/运行时边界是否重新违背既有规则。

处理要求：

- 若检查发现的是“文档已明确定义如何修正”的违规项，应在同一变更中继续修正，不应只登记已知问题后结束。
- 若检查发现的问题涉及未定义边界、存在多个非显然拆分方向，或修正会改变产品行为/长期架构承诺，应先与用户确认。
- 只有在文档未定义、且用户明确接受延后处理时，才允许把该问题保留为后续代办。

### 10.5 通用逻辑抽取与 Helper 使用约束

项目中的通用逻辑应主动收敛，避免“相似逻辑散落在多个页面/服务中各自演化”。

建议规则：

- 同一语义、同一变化原因的逻辑如果在两个及以上位置出现，应优先考虑抽取。
- 抽取后的名称应体现业务职责，如 `Formatter`、`Mapper`、`Presenter`、`Coordinator`、`Resolver`，而不是笼统使用 `Helper`、`Utils`、`Common`。
- 只有当逻辑确实是稳定、无状态、低语义负担的微型辅助时，才允许使用 `Helper` 命名。
- 不应为了“看起来复用”而过早抽象明显仍在变化的流程；流程尚未稳定时，可先局部保留，但一旦出现第二处同构实现，应尽快收口。

应避免：

- 把无关逻辑持续堆进同一个 `Helper` / `Utils` 文件
- 不同页面各自复制一份“几乎一样”的格式化、错误转换、状态判定或映射逻辑
- 在多个 `ViewModel` 中各自手写同一套 `Result` 错误转日志 / 提示 / 本地化处理

推荐做法：

- 展示文本格式化统一抽到共享 `Formatter`
- DTO 到展示模型的映射统一抽到 `Mapper / Presenter`
- 多步骤页面流程统一抽到 `Coordinator / ActionHandler`
- 错误到日志/通知的转换统一抽到共享反馈组件

抽取原则：

- 优先按“变化原因一致”抽取，而不是按“代码长得像”抽取
- 优先抽离纯函数和无 UI 状态逻辑
- 优先复用已有抽象，避免在不同目录再次定义相同职责的新类型

### 10.6 UI 组件复用与定制边界

UI 组件设计必须优先考虑项目级复用性，避免每个页面单独定义一套近似控件、样式或交互。

建议规则：

- 当一个 UI 元素具备相同语义、相同交互模式、相近视觉风格，并且预计会在两个及以上页面出现时，应优先抽为公共组件或公共样式。
- 公共组件应通过参数、模板槽、样式资源或轻量状态配置支持变体，而不是复制一份新组件。
- 只有在交互模型、信息密度、布局结构或视觉语义明显不同的情况下，才应单独定制新组件。

项目级应优先统一的内容包括：

- 标签按钮 / 标签组
- 工具栏按钮与图标按钮
- 空状态卡片
- 信息卡片 / 信息行
- 搜索框展开交互
- 列表项选中态、悬停态、右键菜单基础结构

应避免：

- 每个页面单独写一套“看起来相似”的标签按钮 XAML
- 同一类按钮在不同页面使用不同的圆角、间距、悬停态和激活态实现
- 页面为了临时需求直接复制控件模板后局部修改，最终产生多个难以维护的变种

推荐做法：

- 先寻找现有公共样式、公共控件或共享资源字典
- 若现有组件只差少量配置，应扩展参数，而不是新建一套
- 页面级定制优先通过包装公共组件完成，而不是重新定义核心交互
- 对需要长期复用的视觉模式，应沉淀为共享控件或共享样式资源

判定是否应抽成公共组件的实用标准：

- 已出现第 2 处复用需求
- 后续路线图明确还会继续出现
- 视觉和交互必须在多个页面保持一致
- 修改一处后希望全局同步生效

一句话原则：

- 默认优先复用项目已有组件与样式；只有在明确需要独立语义或独立交互模型时，才允许重新定义。

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
- 同一工作区的多窗口承载默认通过“多进程 + 跨进程激活”实现，而不是继续在同一进程内叠加多个共享 `CurrentWorkspace` 的主窗口
- 页面切换工作区时，先切活动工作区，再重新查询页面数据

## 12. 测试建议

测试本身也属于架构的一部分，不应以“只是测试”为理由绕过分层和依赖边界。

至少保证以下测试边界：

- `Domain` 层单元测试
- `Application` 层 Use Case 单元测试
- 替换兼容性测试
- 保存流程测试
- 本地化资源完整性测试

### 12.1 测试项目分层边界

默认按测试项目承担如下职责：

- `AssestBundleTools.Domain.Tests`
  - 只覆盖领域规则、值对象、实体、领域服务和稳定领域错误/契约
  - 不直接引用 WinUI、基础设施持久化实现或页面级协调逻辑
- `AssestBundleTools.Application.Tests`
  - 覆盖 Handler、Use Case、`Result` 语义、DTO 契约、工作流分支和应用层错误返回
  - 通过 fake / stub / in-memory double 驱动应用层端口
  - 不直接依赖 WinUI 页面对象，也不把基础设施实现细节写成应用层测试前提
- `AssestBundleTools.Infrastructure.Tests`
  - 覆盖序列化、持久化、目录布局、适配器行为和受控临时目录 IO
  - 可以使用受控的临时文件系统，但不承担 WinUI 交互或页面状态语义验证
- `AssestBundleTools.Localization.Tests`
  - 覆盖资源键完整性、`ResourceKeys` 常量约束和仓库级架构 guardrail
  - 适合承载“某些代码位置禁止出现某类字符串 / 命名空间引用”的静态检查
- `AssestBundleTools.Presentation.Tests`
  - 覆盖页面级协调器、ViewModel、窗口/导航/回滚语义，以及受控的展示层边界检查
  - 不应在这里偷偷变成“真实 Reader + 真实缓存 + 真实页面”的大杂烩联动测试，除非该集成范围被明确声明

总原则：

- 测试不应成为穿透架构边界的后门
- 低层测试不得向上攀附 WinUI
- 展示层测试也不应无约束地把业务、基础设施、外部工具一次性全拉进来

### 12.2 测试编写时机与契约冻结边界

测试编写时机需要与实现成熟度一致：

- 已稳定、已文档化的行为契约，以及高风险回归修复，应在同一变更中补齐或更新自动化测试。
- 仍处于骨架、占位或过渡态的流程，只测试“当前已经实现且文档已承认”的行为，不要在测试里提前冻结未来写链路、冲突处理、取消态或更细粒度失败语义。
- 当前替换与保存链路仍有一部分属于过渡实现，因此现阶段的最小要求是覆盖候选解析、兼容性、摘要、保存前预览、资源包聚合、执行骨架结果以及已定义失败；真实写包、最终持久化失败分类、取消/回滚等更深测试，应在对应实现和契约真正落地时随同补齐。

### 12.3 测试依赖与副作用限制

默认自动化测试应满足以下约束：

- 不依赖网络访问
- 不依赖机器上是否额外安装某个外部工具、命令行程序或 SDK 才能通过
- 不读写真实用户目录、真实工作区目录或其他机器全局状态
- 不依赖脆弱的 `Thread.Sleep(...)`、不可控时钟漂移或竞态时序
- 若必须访问文件系统，应使用受控临时目录，并在测试内自行创建和清理

如果未来确实需要“真实外部工具 / 真实跨进程 / 真实集成环境”级别的验证，应把它明确标成更高层级的集成测试，而不是默认塞进日常最小回归集合。

### 12.4 断言风格限制

测试应优先断言稳定契约和可观察结果，而不是偶发实现细节。

建议优先断言：

- `Result` 成败、错误码、消息键、结构化参数
- DTO 字段、聚合摘要、状态变化、缓存是否命中/失效
- 已文档化的页面级回滚、恢复、导航和 guardrail 行为

建议避免：

- 私有字段或临时内部结构
- 纯属实现细节的调用顺序和调用次数
- 尚未文档化的日志句子
- 完整本地化整句文本，除非“这句文本本身”就是稳定契约

### 12.5 本地化与架构 Guardrail 测试

本地化资源完整性测试建议校验：

- 关键资源键是否缺失
- `zh-CN` 和 `en-US` 键集合是否一致
- `Presentation` 是否绕过 `ResourceKeys` 直接写资源键字面量
- `Presentation -> Infrastructure` 受控例外边界是否被新代码破坏

### 12.6 修改后代码审查与 Guardrail 执行

除自动化测试外，每次完成非琐碎修改后还应执行一轮面向文档约束的代码审查。

建议把这轮审查视为交付关闭前的必做步骤，至少覆盖：

- 本次触达文件是否命中 `10.4` 的体量阈值或职责失衡信号。
- 本次改动是否破坏 `8.1` 的 `ViewModel` 依赖边界，或重新引入 sibling 具体耦合、子到父耦合、循环依赖。
- 本次改动是否破坏 `4.x / 7` 的分层依赖方向、`Composition/` 例外边界和用户文本资源化要求。
- 本次改动是否绕开了工作区、导入、预览、缓存等既有窄网关与受控流程。

执行原则：

- 文档已经明确定义的违规项，不应以“后续再整理”为常态处理方式；发现后应在同一变更继续修正。
- 若文档尚未定义修正策略，或存在多个代价不同的修复路径，应停止猜测并先和用户确认。
- 路线图中的功能完成，不应以牺牲上述 guardrail 为代价；结构收口本身属于完成定义的一部分。

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

更细的偏差登记、受影响文件与建议收口方向，统一记录于
[`implementation-deviation-register.md`](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/implementation-deviation-register.md)。
本节继续只保留与当前架构规范直接相关的高优先级摘要。

以下问题当前先记录为架构代办，不阻塞正在推进的 M9 功能链路：

1. `WorkspaceContextService` 的目录快照、工作区元数据与浏览状态持久化已完成第一轮收口；其最近目录恢复、可用性映射、“上次活动工作区”恢复请求推导与激活元数据补齐也已在第三轮继续下沉到 `Application.Workspace.Services.IWorkspaceContextCatalogService`。当前重点已转为维持 `WorkspaceContextService` 的窄协调定位，防止目录编排语义重新回灌到 Presentation。
2. 工作区浏览能力注入面已完成第三轮收口：原先的 `WorkspaceBrowserService / WorkspaceCatalogService` 薄门面已从主注入链退场，`App.xaml.cs` 当前直接暴露 `IWorkspaceExplorerGateway / IWorkspaceAssetInspectionGateway / IWorkspaceAssetOperationGateway / IWorkspaceBatchOperationsGateway / IWorkspaceCatalogGateway`，页面根对象与协调器按需依赖对应窄接口；后续重点转为防止新的聚合总入口重新长回 Presentation。
3. 工作区窗口模型已完成 D-08 第二轮收口：当前采用“formal 正式注册 + pending 预登记 + 成功晋升 + 失败回滚”的进程占用语义；工作区加载前会先建立目标工作区的 pending 占用，加载成功后晋升为正式注册，加载失败时则优先恢复此前已稳定存在的工作区上下文。对应的 pending 占用与页面级恢复主路径，当前也已补上 `Presentation.Tests` 回归保护。
4. 当前默认不走“窗口级 `WorkspaceContextService` 重构”路线；若未来要支持“一进程多工作区并行”，应单独重构窗口作用域上下文，而不是继续扩散当前全局工作区模型。
5. 用户文本资源化当前已完成第二轮宿主侧收口：检查器 / 预览主链路、工作区导入探测、Reader 直检、预览诊断 detail、解密器设置页失败提示、启动兜底页以及页面代码中残留的资源键字面量都已统一收口；当前有意保留的例外只剩外部工具原始执行输出或结果文件 `message` 作为诊断回退文本。
6. `Infrastructure` 层的预览构建组件、音频转码主链路以及 Reader / Preview 失败原因当前都已优先返回结构化字段、原因码和参数；外部工具 `stderr / stdout` 当前只允许通过 `LocalizedMessageDto.FallbackText` 停留在诊断附属信息位置，不应再扩散为宿主程序直接面向用户的句子拼装。
7. 测试体系已完成第三轮最小骨架：`Domain / Application / Infrastructure / Localization / Presentation` 五层当前都已有可执行回归入口，并已补上 `Presentation -> Infrastructure` 边界、`ResourceKeys` 集中管理、工作区窗口回滚主路径、预览缓存的音频摘要升级重建 / 脏 descriptor 容错回归、替换链路中的候选解析 / 来源路径校验 / 缺失资源失败 / 类型不匹配 / 单资源替换 / 撤销替换 / 批量替换摘要回归，以及保存链路中的保存前预览空态 / 资源包分组聚合 / 保存执行骨架主路径回归；当前剩余重点转为真实写包前置校验、跨进程激活与更细粒度结构化错误覆盖，而不再是“测试体系为空骨架”。
8. `Presentation.WinUI` 项目当前仍直接引用 `Infrastructure`，但该例外现已显式收束到 `src/AssestBundleTools.Presentation.WinUI/Composition/` 目录：`App.xaml.cs` 与普通 `Service` 已不再直接依赖 `Infrastructure` 具体类型，并已有仓库级检查防止新的越界引用；后续应继续保持这一边界稳定，而不是重新扩散。
9. `Presentation` 层的 `BundleExplorerViewModel` 已在子 ViewModel 拆分基础上继续完成多轮页面级收口：工作区壳层、左侧资源包窗格、资源包/资产标签宿主、资产窗格与检查器工作流已分别下沉到 `BundleExplorerWorkspaceShellViewModel`、`BundleExplorerBundlePaneViewModel`、`BundleExplorerDocumentWorkspaceViewModel`、`BundleExplorerAssetWorkflowViewModel`，工作区浏览状态恢复/持久化节流与页面级结果反馈已下沉到 `BundleExplorerBrowserStateCoordinator`、`BundleExplorerOperationFeedbackBridge`，工作区加载/重绑定与首屏恢复编排继续下沉到 `BundleExplorerWorkspaceBindingCoordinator`，资源包标签打开/激活/关闭/重排编排也已收口到 `BundleExplorerBundleTabCoordinator`，导入动作与资源包节点动作已分别下沉到 `BundleExplorerWorkspaceImportActionHandler / BundleExplorerBundleActionHandler`；在此基础上，`BundleExplorerPage` 已直接绑定 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 子状态宿主，旧页面兼容透传属性面与 `BundleExplorerPageStateBridge` 已移除，当前主类已进一步退回为页面级外观与少量入口转发层。
10. 按本文档新增的“单文件规模与拆分阈值”约束，超过 `800` 行的手写 `ViewModel` 应视为必须继续拆分的过渡状态。当前 `BundleExplorerAssetWorkflowViewModel` 已完成 D-04 第六轮体量收缩：`Preview` 加载/缓存呈现逻辑已下沉到 `BundleExplorerAssetPreviewCoordinator`，提取/替换/撤销等修改动作已下沉到 `BundleExplorerAssetModificationActionHandler`，检查器投影、替换状态格式化与依赖展示映射已下沉到 `BundleExplorerAssetInspectorPresenter`，资产文档标签同步、激活与活动资源装载编排已下沉到 `BundleExplorerAssetDocumentTabCoordinator`，资产文档宿主的构造、空态检查器初始化与宿主清理已下沉到 `BundleExplorerAssetDocumentHostCoordinator`，主资产窗格的加载/筛选刷新/空态回退编排已下沉到 `BundleExplorerAssetPaneCoordinator`，资产级命令入口与页面动作编排也已下沉到 `BundleExplorerAssetCommandCoordinator`，共享结果失败反馈统一复用 `BundleExplorerOperationFeedbackBridge`；当前主类体量约 `273` 行。`BundleExplorerViewModel` 也已继续由 `1258` 行降到页面级 facade 约 `511` 行，并把浏览状态持久化、结果反馈、工作区绑定/恢复编排、资源包标签导航、导入动作、资源包节点动作以及旧页面兼容面压缩职责下沉/收缩到专用协作对象或子状态宿主；后续重点转为防止职责重新回灌或兼容面回弹。
11. `BundleExplorerAssetWorkflowViewModel` 当前已完成第三轮 sibling / 页面状态边界收口：除改依赖 `IBundleExplorerWorkspaceShellHost / IBundleExplorerBundlePaneHost / IBundleExplorerDocumentWorkspaceHost` 等窄接口宿主之外，`BundleExplorerPage.xaml / BundleExplorerPage.xaml.cs` 也已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 的语义化状态宿主；`BundleExplorerPageStateBridge` 已移除，不再保留页面兼容属性外观层；当前剩余重点已转为持续防止新流程回灌回父级主类，或重新长出新的页面兼容透传层。

后续处理原则：

- 优先保证依赖方向正确，再逐步降低组合根之外的实现细节暴露
- 架构收口尽量与后续功能开发同向进行，避免为单次“纯重构”打断主链路
- 收口过程中应同步清理默认单例回退和已失效的临时兼容代码

## 16. 当前符合度审查结论（2026-04-28）

基于 2026-04-28 的代码抽查，当前项目**已经建立了正确的大方向分层骨架，但尚未完全满足本规范的全部约束**。

当前已经基本满足的部分：

- 四层项目结构已经建立，`Domain / Application / Infrastructure / Presentation` 的主边界总体清晰。
- `Application -> Domain`、`Infrastructure -> Application + Domain` 的主依赖方向已经落地，`Domain` 仍保持最内层。
- WinUI 侧大部分页面行为已经通过 ViewModel、应用层 Handler 和展示服务进行组织，没有明显退回到“单文件堆叠式 UI 业务代码”。
- 工作区、多标签页、导入、缓存、预览等核心链路已经开始围绕统一状态模型和用例编排收敛。

当前仍需持续守住的高优先级边界：

- 用户可见文本资源化主链路与 `ResourceKeys` 集中管理已完成第二轮收口；当前主要剩余的是少量内部哨兵值、外部工具诊断回退以及调试日志格式的一致性优化，而不再是工作区导入、预览、启动兜底或 WinUI 壳层对用户可见文本的直接硬编码。
- `WorkspaceContextService` 已完成第三轮收口，主类本身已进一步收窄；最近目录恢复、可用性语义与“上次活动工作区”恢复请求推导当前已进入 `Application.Workspace.Services.IWorkspaceContextCatalogService`，当前重点转为防止新的目录编排语义重新回流到 Presentation，以及防止工作区浏览聚合宽接口重新长回 WinUI。
- `Presentation -> Infrastructure` 的组合根例外边界已完成第一轮显式化：当前仅 `src/AssestBundleTools.Presentation.WinUI/Composition/` 允许直接装配基础设施实现，`App.xaml.cs` 与普通壳层服务已改回依赖组合结果或窄抽象；后续重点转为防止新的越界引用回流到常规 UI 代码。
- `BundleExplorer` 相关子 `ViewModel` 之间已完成第三轮窄接口 / 页面状态收口；`BundleExplorerPage` 已直接绑定 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 子状态宿主，旧的 `BundleExplorerPageStateBridge` 与大部分父级兼容透传属性已移除；当前主要剩余的是保持页面级视图同步逻辑与浏览状态持久化入口继续停留在明确宿主边界内，避免重新把兼容面堆回父级主类。
- 测试体系已完成第三轮最小骨架：`Domain` 的 `AppError`、`Application` 目录 Handler / `Result`、`Infrastructure` 的工作区目录与预览缓存持久化回归、`Localization` 中的资源完整性与 Presentation 架构边界检查，以及 `Presentation` 中的工作区窗口回滚主路径当前均已落地；其中预览链路已补上音频摘要在新转码器可用时的升级重建，以及缺失 payload / 非法 payload path 下的缓存容错回归；替换链路已补上文件候选类型解析、来源路径校验、缺失资源失败、类型不匹配、工作区候选空集失败、单资源替换、撤销替换与批量替换摘要回归；保存链路也已补上保存前预览空态 / 资源包分组聚合与保存执行骨架主路径回归；当前剩余重点转为扩展更完整的 `Application / Infrastructure / Presentation` 关键路径回归，而不再是完全空白状态。

本日已完成的高优先级收口：

- `Application` Handler 中对 `InMemoryWorkspaceSessionRegistry.Shared` 的默认回退已移除，统一改为显式构造注入。
- 工作区导入登记与导入处理链路中的 `File.Exists / Directory.Exists / FileInfo` 已抽象为应用层端口，并由 `Infrastructure` 提供文件系统实现。
- 预览查询链路已从 `PreviewInfoText` 文本拼装收敛为 `PreviewInfoItems + PreviewMessage + Diagnostic` 结构化 DTO，`Presentation` 统一负责本地化格式化。
- 工作区导入自动识别、解密器 `probe`、缓存诊断和 Reader 拒绝原因当前已统一改为 `LocalizedMessageDto` 结构化消息，由展示层统一本地化输出。
- 预览诊断 `Detail` 已进一步从裸字符串收敛为结构化消息；Reader 直检与预览生成阶段的稳定失败原因也已抬升为标准错误令牌，不再由 `Infrastructure` 直接拼英文说明。
- 解密器设置页中的“保存失败 / 删除失败 / 保存后校验失败”已改为标准错误令牌；工作台预览显示层的兜底日志也不再直接输出 `ex.Message`。
- `Presentation` 已新增共享值格式化器，`BundleExplorer / Inspector` 以及独立批量操作窗口的字节大小展示不再各自硬编码 `MB / KB / B`。
- 音频转码器与内置直通解密器已去除直接面向用户的内建说明文本，内置解密器显示语义统一回到稳定标识并由展示层本地化。
- 外部音频转码与外部解密进程当前已进一步区分“外部工具原始输出”和“宿主程序自身失败原因”：前者只作为回退文本保留，后者统一收敛为稳定消息令牌或标准错误令牌。
- WinUI 壳层中的文件选择器、跨进程启动协议、工作区新进程启动失败与“缺少回退解密器”等异常当前已统一资源化；启动兜底页也不再直接显示 `exception.Message` 原文。
- `Presentation` 中残留的 `UiText.Get("...")` 资源键字面量当前已统一回收到 `ResourceKeys`，并新增仓库级测试阻止新的界面代码绕过资源键常量。
- `WorkspaceContextService` 已完成 D-01 第三轮收口：最近目录恢复、可用性映射、“上次活动工作区”恢复请求推导、激活元数据补齐与浏览状态回写编排已下沉到 `Application.Workspace.Services.WorkspaceContextCatalogService`，主类体量已由约 `545` 行降至约 `261` 行。
- 已完成 D-02 第三轮收口：`WorkspaceBrowserService / WorkspaceCatalogService` 已从主注入链移除，`BundleExplorerPage`、`HomePage` 与独立批量操作窗口当前直接消费 `WorkspaceExplorer / AssetInspection / AssetOperation / BatchOperations / Catalog` 等窄网关；组合根不再额外制造一层空转发门面。
- `BundleExplorerViewModel` 已继续完成一轮页面级收口：资源包浏览左栏、标签工作区状态、资产工作流、浏览状态协调、结果反馈桥接、工作区绑定编排、资源包标签导航/关闭/激活、导入动作以及资源包节点动作已分别拆入独立子 ViewModel / 协作对象，`BundleExplorerPage` 也已直接迁回子状态宿主绑定，主类进一步退回为页面级 facade 与少量入口转发层，当前约 `511` 行。
- `BundleExplorer` 页面级共享状态已完成 D-03 第三轮收口：`BundleExplorerPage.xaml / BundleExplorerPage.xaml.cs` 已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 的语义化状态宿主，`BundleExplorerPageStateBridge` 已移除；浏览状态持久化仍通过窄事件入口汇总，后续应继续防止新的页面兼容透传属性回灌。
- `BundleExplorerAssetWorkflowViewModel` 已完成 D-04 第六轮体量收缩：在 `Preview`、`Modification Action`、`Inspector Projection`、`Asset Document Coordination` 与 `Asset Pane Coordination` 之外，资产级命令入口与页面动作编排也已下沉到 `BundleExplorerAssetCommandCoordinator`，共享结果失败反馈统一复用 `BundleExplorerOperationFeedbackBridge`；主类当前约 `273` 行，`BundleExplorerViewModel` 当前约 `511` 行且低于页面级 facade `600` 行软上限。当前若继续收口，重点应转向防止职责回灌和稳定页面内共享状态，而不是继续把页面命令入口碎片化。
- 已完成 D-06 第三轮最小测试骨架：在 `Domain / Application / Infrastructure / Localization` 四层最小骨架之外，`tests/AssestBundleTools.Presentation.Tests` 也已补上 `WorkspaceProcessCoordinatorService` 与 `BundleExplorerWorkspaceBindingCoordinator` 的回归覆盖；同时 `Application.Tests / Infrastructure.Tests` 也已继续扩展到 `RegisterWorkspaceImportHandler` 的导入登记链路、`ProcessWorkspaceImportsHandler` 的缓存复用命中 / 解密失败 / Reader 拒绝 / 读取失败 / 陈旧缓存清理链路、`GetAssetPreviewHandler` 的预览缓存命中 / `SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建 / 音频摘要升级重建 / 预览解密失败 / 生成失败摘要回退链路、`GetFileReplacementCandidateHandler / GetWorkspaceReplacementCandidatesHandler / ReplaceAssetFromFileHandler / ReplaceAssetFromWorkspaceHandler / UndoAssetReplacementHandler / GetWorkspaceBatchReplacementPreviewHandler / ExecuteWorkspaceBatchReplacementHandler` 的替换主链路回归、`RemoveWorkspaceBundleHandler` 的缓存失效链路，以及 `JsonPreviewCacheStore` 在缺失 payload / 非法 payload path 下的预览缓存持久化容错回归；当前测试收口重点已转向扩展保存链路、跨进程激活关键路径与更细粒度结构化错误校验，而不是继续维持空白。

因此，当前阶段更准确的判断应为：

- **架构方向正确，主体分层基本成立。**
- **截至 2026-04-29，D-01 ~ D-08 中除 D-06 的后续深层测试补齐外，其余非测试主体偏差已基本收口。**
- **后续重点已转为守边界、防回弹，以及在功能扩展时继续沿现有协作对象演进。**
- 后续演进优先级建议保持为：
  1. 外部工具诊断回退与少量内部哨兵值的进一步清理
  2. `BundleExplorer` 页面级共享状态宿主与页面命令协作对象继续防回灌，避免旧兼容透传层或大而全命令入口重新长回父级主类
  3. 在现有最小测试骨架上继续扩展 `Application / Infrastructure` 回归
