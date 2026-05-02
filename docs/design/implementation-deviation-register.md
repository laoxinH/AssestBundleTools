# AssestBundleTools 当前实现偏差登记

## 1. 文档目的

本文档用于集中记录“当前代码实现与仓库既有规范/设计文档尚未完全对齐”的确认项，
作为后续逐步收口时的统一基线。

它不替代以下主文档：

- [code-architecture-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/code-architecture-guidelines.md)
- [workspace-management.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/workspace-management.md)
- [project-build-roadmap.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/project-build-roadmap.md)

主文档负责定义“应当如何设计”，
本文档负责记录“当前哪些地方仍处于过渡态、为什么先记账、建议如何收口”。

## 2. 使用约定

### 2.1 登记范围

只记录已经确认存在、且对后续扩展或维护有持续影响的偏差。

不记录：

- 纯讨论中的想法
- 尚未验证的怀疑项
- 与当前规范并不冲突、只是个人偏好的重构建议

### 2.2 每条记录至少回答四件事

1. 当前状态是什么
2. 它与哪条规范或设计边界不一致
3. 它会给后续扩展带来什么成本或风险
4. 建议优先按什么方向收口

### 2.3 文档维护规则

- 当偏差被修复后，应同步更新本文档和对应主文档，而不是只改代码。
- 如果后续决定“保留当前实现并上升为正式规则”，则应把规则写回主文档，并从本文档移除该项。
- 若新的实现继续引入过渡结构，必须先补登记，再继续扩散。

## 3. 当前偏差总览（2026-05-01）

| ID | 优先级 | 主题 | 当前状态 |
|---|---|---|---|
| D-01 | 高 | `WorkspaceContextService` 工作区目录编排过渡态 | 第三轮已收口（目录编排语义已下沉到 Application） |
| D-02 | 高 | 工作区浏览能力注入面仍需继续压窄 | 第三轮已收口（聚合门面已退场） |
| D-03 | 高 | 资产工作流 `ViewModel` 与 sibling 状态宿主边界仍待继续收口 | 第三轮已收口 |
| D-04 | 高 | `BundleExplorer` 相关 `ViewModel` 明显超出文件规模阈值 | 第十一轮已收口（资产命令编排已拆出） |
| D-05 | 中 | `Presentation -> Infrastructure` 边界仍偏宽，组合根例外尚未完全收束 | 第一轮已收口（边界已显式化） |
| D-06 | 高 | 自动化测试覆盖深度仍待继续补强 | 第三轮已收口（当前剩余为深层回归补齐） |
| D-07 | 中 | 用户文本资源化与结构化错误仍有残余热点 | 第二轮已收口（仅保留外部工具诊断回退） |
| D-08 | 中 | 工作区进程占用注册时机仍偏后 | 第二轮已收口（预登记 + 晋升/回滚） |
| D-09 | 中 | 工作区导入多线程扩展规范落地收口 | 第二轮已收口（主链路已并行化） |

## 4. 详细登记

### D-01 `WorkspaceContextService` 工作区目录编排过渡态

当前状态：

- `src/AssestBundleTools.Presentation.WinUI/Services/Workspace/WorkspaceContextService.cs`
  当前约 `261` 行。
- `src/AssestBundleTools.Application/Workspace/Services/WorkspaceContextCatalogService.cs`
  当前约 `329` 行。
- 当前已完成第三轮边界收口：
  - `WorkspaceContextCatalogCoordinator` 已从 Presentation 删除
  - 最近工作区目录恢复、可用性状态映射、“上次活动工作区”恢复请求推导、激活元数据补齐与浏览状态回写编排
    已统一下沉到 Application 层的 `IWorkspaceContextCatalogService`
  - 目录快照、工作区元数据、浏览状态回写仍统一经 `IWorkspaceContextPersistenceService` 进入 Application
- `WorkspaceContextService` 当前主要承担：
  - 当前 UI 会话中的活动工作区协调
  - 最近工作区目录项的登记、激活与排序委托
  - Application 恢复请求到 Presentation 导航请求的窄映射
  - 当前活动工作区的事件通知与页面共享入口

与规范冲突点：

- `code-architecture-guidelines.md` 已明确：
  - `WorkspaceContextService` 只协调当前 UI 会话中的活动工作区和摘要列表
  - 持久化细节应停留在 Application 抽象与 Infrastructure 实现之后
- 当前这条偏差的主体已收口：
  工作区目录恢复、可用性语义与“上次活动工作区”恢复请求不再停留在 Presentation 内部协作对象，
  而是进入了稳定的 Application 服务边界

影响：

- `WorkspaceContextService` 本身的变化原因已进一步下降，更接近“当前活动工作区 + 已登记摘要 + 事件通知”的窄协调层；
- 工作区目录恢复和元数据编排语义现在可以在 Application 层稳定演进，而不必继续挤在 WinUI 服务里；
- 后续如果再扩展更细粒度的目录诊断、刷新策略或恢复规则，已有明确落点，不需要再回长一个 Presentation 协调器。

建议收口方向：

- 保留 `IWorkspaceContextCatalogService + IWorkspaceContextPersistenceService` 作为当前第三轮边界，不回退到 Presentation 直连仓储；
- 后续若新增目录诊断或恢复规则，优先继续扩展 Application DTO / 服务，而不是在 WinUI 再长出新的目录协调层；
- 持续维持 `WorkspaceContextService` 的窄协调定位，防止目录编排语义重新回灌。

### D-02 工作区浏览能力注入面仍需继续压窄

当前状态：

- `src/AssestBundleTools.Application/Workspace/Services/`
  当前已把原先的单一 `WorkspaceBrowserGateway` 拆为四个能力簇网关：
  - `WorkspaceExplorerGateway`（约 `68` 行）
  - `WorkspaceAssetInspectionGateway`（约 `42` 行）
  - `WorkspaceAssetOperationGateway`（约 `67` 行）
  - `WorkspaceBatchOperationsGateway`（约 `81` 行）
- 当前已完成第三轮收口：
  - `WorkspaceBrowserService / WorkspaceCatalogService` 及其 Presentation 接口已移除
  - `App.xaml.cs` 当前直接暴露：
    - `IWorkspaceExplorerGateway`
    - `IWorkspaceAssetInspectionGateway`
    - `IWorkspaceAssetOperationGateway`
    - `IWorkspaceBatchOperationsGateway`
    - `IWorkspaceCatalogGateway`
  - `BundleExplorerPage`、`BatchTasksPage`、`HomePage` 当前已改为直接把对应窄网关注入到页面根对象或 code-behind
- `WorkspaceImportCoordinatorService`、`BatchTasksViewModel`、`BundleExplorerAssetDocumentTabCoordinator`、
  `BundleExplorerAssetPreviewCoordinator`、`BundleExplorerAssetPaneCoordinator`、
  `BundleExplorerWorkspaceBindingCoordinator`、`BundleExplorerAssetModificationActionHandler`、
  `BundleExplorerViewModel`、`BundleExplorerAssetWorkflowViewModel`
  当前都已直接依赖对应的窄网关接口，而不是继续拿整份聚合门面

与规范冲突点：

- 该项的主要偏差已经收口：
  当前页面与页面级协调器已直接面向 Application 能力簇网关，
  不再通过一层空转发的 Presentation 聚合门面间接取用。

影响：

- Presentation 对具体 Handler 的直接耦合已经明显下降，组合根与页面调用面也更稳定；
- 单一超宽 `WorkspaceBrowserGateway` 已被拆除，页面根对象也不再保留额外的聚合门面依赖；
- 后续新增用例时，只需要继续选择对应能力簇网关，而不必再决定“是否先包一层薄 UI 门面”。

建议收口方向：

- 保持当前“组合根直接提供能力簇网关、页面根对象按需依赖窄接口”的第三轮边界；
- 后续新增用例时继续优先落到对应能力簇网关，避免重新引入新的聚合宽接口或空转发门面；
- 若未来确实出现页面级复合语义，应优先新增明确语义的协调器/Presenter，而不是恢复 `WorkspaceBrowserService` 一类总入口门面。

### D-03 资产工作流 `ViewModel` 与 sibling 状态宿主边界仍待继续收口

当前状态：

- `BundleExplorerViewModel` 当前直接创建：
  - `BundleExplorerWorkspaceShellViewModel`
  - `BundleExplorerBundlePaneViewModel`
  - `BundleExplorerDocumentWorkspaceViewModel`
  - `BundleExplorerAssetWorkflowViewModel`
- `BundleExplorerAssetWorkflowViewModel` 当前已完成第一轮收口：
  - 不再直接依赖前三者的具体类型；
  - 已改为依赖 `IBundleExplorerWorkspaceShellHost / IBundleExplorerBundlePaneHost / IBundleExplorerDocumentWorkspaceHost`；
  - 资产文档宿主状态变化也已收口为专用事件，而不是继续读取 sibling `ViewModel` 的具体 `PropertyName`。
- 当前已完成第三轮收口：
  - `BundleExplorerPage.xaml / BundleExplorerPage.xaml.cs` 已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 子状态宿主；
  - `BundleExplorerViewModel` 中大部分旧页面兼容透传属性已删除，仅保留页面级外观、入口命令与编排职责；
  - `BundleExplorerPageStateBridge` 已移除，旧页面兼容桥接层不再作为稳定结构继续存在；
  - `BundleExplorerPage.xaml.cs` 已继续按页面协作职责拆为 `BundleExplorerPageNavigationCoordinator / BundleExplorerPagePaneLayoutCoordinator / BundleExplorerPageTabCoordinator / BundleExplorerPageChromeCoordinator`，页面主 code-behind 当前约 `362` 行，重新回到常规手写 UI 类目标区间；
  - 浏览状态持久化请求仍通过 `BundleExplorerBundlePaneViewModel.BrowserStateChanged` 这一条窄事件入口汇总。
- 当前剩余过渡点：
  - 页面级资源包窗格动画、工作区窗口注册与资产文档选中同步当前已停留在显式 page coordinator 边界内；后续仍要防止这些页面视图行为重新回灌到父级 `ViewModel`，或重新堆回单一巨型 code-behind；
  - 浏览状态持久化仍由父级汇总入口接收；若后续继续扩展页面共享状态，应继续优先落到明确状态宿主或语义事件。

与规范冲突点：

- `code-architecture-guidelines.md` 已明确：
  - 同级 `ViewModel` 默认不应直接依赖彼此的具体实现
  - 子 `ViewModel` 协作优先通过父级编排、共享状态对象、事件回调或窄接口完成
- 第一轮接口收口已经对齐了“不要直接吃 sibling 具体实现”这一点；
- 第二轮收口已经把页面兼容属性刷新从多条通用 `PropertyChanged` 字符串转发链，收窄为更明确的页面状态语义通知；
- 第三轮收口又进一步把旧页面绑定迁回子状态宿主边界，并移除了父级兼容桥接层；
- 当前主要仍处于过渡态的，已经不再是父级兼容属性外观本身，而是页面级视图同步/视觉行为协作对象与浏览状态持久化入口仍需持续保持在明确边界内。

影响：

- sibling `ViewModel` 之间的具体类型耦合、通用属性名转发耦合以及父级兼容透传面都已明显下降；
- 页面内共享状态的宿主边界更清晰，页面 XAML / code-behind 已能直接围绕子状态宿主协作；
- 当前剩余风险已更多转向“未来新流程是否会重新长出父级兼容属性或新的桥接层”，而不是旧桥接结构本身。

建议收口方向：

- 保留当前“窄接口宿主 + 直接子状态绑定 + 语义化 `PageStateChanged`”作为第三轮过渡边界；
- 后续若新增页面绑定或页面共享状态，优先直接落到对应子状态宿主，避免重新把兼容透传属性堆回 `BundleExplorerViewModel`；
- 若再出现新的持久化触发点或页面级同步需求，优先扩展明确宿主/语义事件，而不是恢复 `BundleExplorerPageStateBridge` 一类桥接层。

### D-04 `BundleExplorer` 相关 `ViewModel` 仍存在文件规模偏大风险

当前状态（2026-04-29 抽查）：

- `BundleExplorerAssetWorkflowViewModel.cs`：`273` 行（已由 `1906` 行降至 `273` 行）
- `BundleExplorerViewModel.cs`：`426` 行（已由 `1258` 行降至 `426` 行）
- `WorkspaceContextService.cs`：`261` 行
- `App.xaml.cs`：`375` 行
- `BundleExplorerAssetWorkflowViewModel` 已完成第一轮 `Preview / Modification Action` 协作对象下沉：
  - `BundleExplorerAssetPreviewCoordinator`
  - `BundleExplorerAssetModificationActionHandler`
- 已继续完成检查器投影与替换/依赖展示下沉：
  - `BundleExplorerAssetInspectorPresenter`
- 已继续完成资产文档标签同步、激活与活动资源装载编排下沉：
  - `BundleExplorerAssetDocumentTabCoordinator`
- 已继续完成资产文档宿主构造、空态检查器初始化与宿主清理下沉：
  - `BundleExplorerAssetDocumentHostCoordinator`
- 已继续完成主资产窗格加载、筛选刷新与空态回退编排下沉：
  - `BundleExplorerAssetPaneCoordinator`
- 已继续完成共享结果失败反馈桥接复用与资产级命令共同前置条件收口：
  - 统一复用 `BundleExplorerOperationFeedbackBridge`
  - 提取/替换/撤销入口统一改走共享 guard
- 已继续完成资产级命令入口与页面动作编排下沉：
  - `BundleExplorerAssetCommandCoordinator`
- `BundleExplorerViewModel` 已继续完成一轮页面级协作对象下沉：
  - `BundleExplorerBrowserStateCoordinator`
  - `BundleExplorerOperationFeedbackBridge`
- `BundleExplorerViewModel` 已继续完成一轮页面级兼容面收缩：
  - `BundleExplorerPage` 已直接绑定 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow`
  - 旧页面兼容透传属性与 `BundleExplorerPageStateBridge` 已移除
- `BundleExplorerViewModel` 已继续完成一轮工作区编排下沉：
  - `BundleExplorerWorkspaceBindingCoordinator`
- `BundleExplorerViewModel` 已继续完成资源包标签导航、激活、关闭与重排编排下沉：
  - `BundleExplorerBundleTabCoordinator`
- 当前 `BundleExplorerAssetWorkflowViewModel / BundleExplorerViewModel`
  都已脱离 `800+ / 1000+` 强制拆分区，也都已重新回到常规手写业务类 `200-500` 行目标内；
- 当前 D-04 的主体偏差已收口，剩余关注点转为防止页面主类、页面代码和资产级命令逻辑再次回灌。

与规范冲突点：

- 常规手写业务类/`ViewModel` 目标体量建议控制在 `200-500` 行；
- 页面级 facade / orchestrator 型 `ViewModel` 软上限为 `600` 行；
- `800+` 视为必须继续拆分的过渡态；
- `1000+` 不能作为稳定状态长期存在。

影响：

- 单文件同时承载太多变化原因；
- 后续功能扩展会自然倾向“继续往现有大类里塞”；
- 评审、定位回归、拆分测试边界都会越来越吃力。

建议收口方向：

- `BundleExplorerAssetWorkflowViewModel` 已完成五轮协作对象下沉：
  - `Preview`
  - `Modification Action`
  - `Inspector Projection`
  - `Asset Document Tab Coordination`
  - `Asset Command Coordination`
- `BundleExplorerAssetPaneCoordinator` 已把主资产窗格的加载/刷新职责从资产工作流主类中收走；
- 当前资产工作流主类也已继续把共享失败反馈统一回收到 `BundleExplorerOperationFeedbackBridge`，并把提取/替换/撤销入口的共同前置条件与页面动作编排收口到共享命令协调器；
- `BundleExplorerViewModel` 已完成“工作区恢复/浏览状态持久化节流”“页面级结果反馈与通知桥接”“工作区绑定/恢复编排”“资源包标签导航/关闭/激活”以及“旧页面兼容面压缩”五类职责下沉/收缩；
- `BundleExplorerAssetWorkflowViewModel` 当前约 `273` 行，`BundleExplorerViewModel` 当前约 `426` 行；两者都已回到常规手写业务类体量区间。后续继续推进 D-04，更适合把焦点转到
  - 防止新的页面级反馈/浏览逻辑回灌回 `BundleExplorerAssetWorkflowViewModel / BundleExplorerViewModel`
  - 新增资产级命令时继续优先落到 `BundleExplorerAssetCommandCoordinator` 或新的窄协作对象
  - 维持页面内共享状态宿主稳定，不重新长出父级兼容透传层
  而不是重新把页面级浏览编排塞回主类。
- `WorkspaceContextService` 与 `App.xaml.cs` 虽处于基础设施/组合根交界，但也应控制职责继续扩散。

### D-05 `Presentation -> Infrastructure` 边界仍偏宽

当前状态：

- `src/AssestBundleTools.Presentation.WinUI/AssestBundleTools.Presentation.WinUI.csproj`
  仍直接引用 `AssestBundleTools.Infrastructure.csproj`
- 当前已完成第一轮显式收口：
  - `Infrastructure` 直接引用已收束到 `src/AssestBundleTools.Presentation.WinUI/Composition/`
  - `App.xaml.cs` 已退回为消费组合结果的应用入口，不再直接 `using AssestBundleTools.Infrastructure.*`
  - `AppPreferencesService` 已改为依赖 Presentation 内部的偏好持久化抽象，不再直接吃 `JsonAppPreferencesStore`
  - 仓库级检查当前已覆盖“`Composition/` 之外的 Presentation 文件不得直接引用 Infrastructure 命名空间”
- 当前仍保留的受控例外是：
  - `Composition/AppCompositionRoot.cs`
  - `Composition/InfrastructureAppPreferencesPersistenceStore.cs`
  这两处负责装配和适配，不承载页面级业务逻辑

与规范冲突点：

- 组合根可以作为技术装配例外；
- 但若例外边界没有被明确收住，就会逐渐演变为 Presentation 普遍感知 Infrastructure 细节；
- 当前第一轮收口已经把“例外只限 Composition 目录”固化为显式规则和代码结构，但长期仍要防止例外范围再次扩张。

影响：

- 当前“允许例外”的真实边界已经从口头约定改成了明确目录边界；
- 普通 `Service / View / ViewModel / App.xaml.cs` 再继续直接引用 Infrastructure 的风险已明显下降；
- 但只要 `Presentation.WinUI.csproj` 仍直接引用 `Infrastructure`，后续仍需靠结构和校验持续防止新的越界引用。

建议收口方向：

- 保留当前“`Composition/` 目录是唯一受控例外边界”的规则，不要再把装配细节回流到 `App.xaml.cs` 或普通服务；
- 若 Presentation 自有服务还需要底层能力，优先新增 Presentation 窄抽象，再由 `Composition/` 适配具体 Infrastructure 实现；
- 后续若要继续缩窄边界，可再评估是否把组合根装配进一步外提为独立启动/installer 工程；在此之前，当前规则已可视为明确有效的受控边界。

### D-06 自动化测试覆盖深度仍待继续补强

当前状态：

- 当前已完成第三轮最小回归骨架：
  - `tests/AssestBundleTools.Domain.Tests`
    - 覆盖 `AppError` 基础行为
  - `tests/AssestBundleTools.Application.Tests`
    - 当前采用零依赖控制台 runner
    - 覆盖 `Result` 基础行为、`RegisterWorkspaceCatalogEntryHandler`、`SetWorkspaceCatalogEntryPinnedStateHandler`
    - 覆盖 `RegisterWorkspaceImportHandler` 的路径去重、更新计数、路径类型统计与排除列表修正
    - 覆盖 `ProcessWorkspaceImportsHandler` 的缓存复用命中、解密失败、Reader 拒绝、读取失败，以及陈旧 bundle / preview 缓存清理与 `SourceRemoved` 诊断
    - 覆盖 `GetAssetPreviewHandler` 的预览缓存命中、`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建、音频摘要在新转码器可用时的升级重建，以及预览解密失败 / 预览生成失败摘要回退链路
    - 覆盖 `GetFileReplacementCandidateHandler / GetWorkspaceReplacementCandidatesHandler / ReplaceAssetFromFileHandler / ReplaceAssetFromWorkspaceHandler / UndoAssetReplacementHandler / GetWorkspaceBatchReplacementPreviewHandler / ExecuteWorkspaceBatchReplacementHandler` 的候选解析、来源路径校验、缺失资源失败、类型不匹配、空集失败、单资源替换、撤销替换与批量替换摘要链路
    - 覆盖 `GetWorkspaceSavePreviewHandler / SaveWorkspaceAsNewBundleHandler` 的保存预览空态、资源包分组聚合、缺失工作区、无候选失败与执行摘要链路
    - 覆盖 `RemoveWorkspaceBundleHandler` 的元数据修正、排除列表补齐与缓存清理链路
  - `tests/AssestBundleTools.Infrastructure.Tests`
    - 当前也采用零依赖控制台 runner
    - 覆盖 `JsonWorkspaceCatalogStore` 的缺失文件空载入与保存回读
    - 覆盖 `JsonWorkspaceMetadataStore` 的保存回读与删除清理
    - 覆盖 `JsonPreviewCacheStore` 的描述文件 / 载荷文件保存回读、删除清理，以及缺失 payload / 非法 payload path 下的容错加载、路径解析与描述文件清理
  - `tests/AssestBundleTools.Localization.Tests`
    - 覆盖 `zh-CN / en-US` 资源键一致性
    - 覆盖 `ResourceKeys.cs` 到 `.resw` 的资源键存在性校验
    - 覆盖 `Presentation -> Infrastructure` 组合根边界检查
    - 覆盖 `Presentation` 本地化调用必须通过 `ResourceKeys` 常量，而不是直接写资源键字面量
  - `tests/AssestBundleTools.Presentation.Tests`
    - 覆盖 `WorkspaceProcessCoordinatorService` 的 `pending` 预登记、正式晋升与清理回归
    - 覆盖 `BundleExplorerWorkspaceBindingCoordinator` 在工作区打开/恢复失败时的页面级回滚语义
- `Presentation.WinUI` 已通过 `InternalsVisibleTo("AssestBundleTools.Presentation.Tests")` 暴露必要的内部测试入口；
- `tests/README.md` 已同步改为当前骨架说明；
- 五层关键入口当前已经具备最小回归保护，但更细粒度业务链路回归仍未完全建立。
- 当前仓库也已明确一条补测规则：
  - 稳定契约与确认回归，应在同一变更中补测试；
  - 骨架/过渡态流程只覆盖当前已实现、已文档化行为；
  - 替换与保存链路在真实写包和最终持久化语义落地前，不提前用测试冻结未来行为。

与规范冲突点：

- “完全空白或只有单层样例”的问题已经解除，但当前覆盖深度仍未完全满足主架构文档和错误处理文档中的测试建议。
- 当前真正的偏差已从“没有测试”转为“部分关键链路还只有骨架级回归，尚未进入真实写链路深测”。

影响：

- 当前已经为结果模型、工作区目录关键 Handler、工作区目录持久化、资源键一致性、关键展示层边界以及工作区窗口回滚主链路补上最小回归保护；
- 工作区导入登记、导入处理中的缓存复用/解密失败/Reader 拒绝/读取失败分支、预览查询中的缓存命中/`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效/音频摘要升级重建/解密失败/生成失败摘要回退、替换链路中的候选解析/来源路径校验/缺失资源失败/类型不匹配/空集失败/单资源替换/撤销替换/批量替换摘要、资源包移除时的缓存失效，以及预览缓存存储在缺失 payload / 非法 payload path 下的容错主链路，当前也已经补上自动化回归；
- 保存链路中的保存前预览空态/资源包分组聚合和保存执行骨架主路径，当前也已经补上自动化回归；
- 当前替换与保存测试的停留点是有意为之：现阶段先保护已定义骨架契约，不提前为尚未实现的真实写回、冲突处理、取消/回滚结果写死断言；
- 但后续做缓存失效、预览扩展、替换流程与更完整窗口激活时，仍缺少更深入的 `Application / Infrastructure / Presentation` 自动化回归。

建议收口方向：

- 保留当前五层最小骨架，后续不要回退到“无测试 / 无 Presentation 关键回归”状态；
- 下一轮优先补：
1. 更多 `Application / Infrastructure` 关键用例测试（当前保存预览 / 保存执行骨架主路径与替换链路常见校验失败已覆盖；但替换链路取消态 / “不可替换”语义、真实写包前置校验与最终持久化失败细分，必须等对应实现与契约明确后再补，不提前臆造语义）
2. `Presentation` 层更完整的窗口激活与跨进程重定向回归
3. 资源键、错误码与结构化消息参数的更细粒度自动校验

### D-07 用户文本资源化与结构化错误仍有残余热点

当前状态：

- 宿主程序自身的用户可见文本、启动期/壳层异常提示以及主要 WinUI 服务失败分支当前已经完成第二轮收口；
- `BundleExplorerPage / ShellPage / LogPanel / AssetPaneView / InspectorPanelView` 中残留的资源键字面量已统一回收到 `ResourceKeys`；
- `tests/AssestBundleTools.Localization.Tests` 已补上边界检查，阻止新的 `UiText.Get("...")` 式资源键直写重新回流到 Presentation；
- 当前仅保留的受控例外是：
  - 外部工具原始 `stdout / stderr`
  - 外部工具结果文件中的原始 `message`
  这两类内容只作为诊断回退文本保留，不作为宿主程序主提示文案。

与规范冲突点：

- 当前主要不再是“宿主程序直接对用户拼句子”的问题；
- 真正需要持续守住的是“外部工具原始输出只能停留在诊断附属信息位置”，以及“新 UI 代码不得绕过 `ResourceKeys` 常量直接写资源键字符串”。

影响：

- 当前宿主侧的资源化与结构化错误主链路已经稳定，后续新增语言与文案调整不再需要回头清理大量散落的资源键字符串；
- 若后续富预览扩展重新把外部工具原始输出或新的资源键字面量带回 UI，才会重新破坏这一轮收口成果。

建议收口方向：

- 继续保持“宿主程序自身失败原因 -> 错误码 / 消息键 / 结构化参数”的边界；
- 外部工具原始输出只作为诊断附属信息保留，不作为主提示文本；
- 后续若识别出稳定可归类的外部工具失败模式，再把它们逐步提升为标准消息令牌或 `AppError`。

### D-08 工作区进程占用注册时机仍偏后

当前状态：

- 当前已完成第二轮收口：
  - 主窗口同时区分“已正式绑定成功的工作区 A”和“正在加载中的目标工作区 B”
  - 页面开始加载目标工作区前，会先对 B 做一次 pending 预登记
  - 若 B 最终加载成功，则 pending 注册会晋升为正式注册
  - 若 B 加载失败，则会清理 pending 注册，并优先回退到此前已稳定存在的 A；只有原本就没有工作区时才退回空态
  - 跨进程激活、启动期重定向与同进程窗口查找，当前都会同时识别 formal / pending 两类占用态
- 当前已补首轮自动化回归：
  - `WorkspaceProcessCoordinatorServiceTests` 已覆盖 pending 预登记、正式晋升与清理后的占用行为
  - `BundleExplorerWorkspaceBindingCoordinatorTests` 已覆盖目标工作区加载失败时恢复既有工作区，以及“原本无工作区时退回空态”的页面级回滚语义

与设计边界不一致点：

- 该偏差的核心问题当前已经收口；
- 当前实现已与“加载前预占用 + 成功晋升 + 失败回滚”的窗口稳定性目标对齐。

影响：

- 极端并发启动或跨进程重定向竞争下的重复打开窗口风险已明显下降；
- 工作台从 A 切到 B 时，如果 B 打开失败，也不再把既有 A 一并清空成空白页。
- 当前这条语义主线也已经有了最小自动化保护，后续重构时更容易及时发现回弹。

建议收口方向：

- 保持当前“formal 注册 + pending 预登记 + 成功晋升 + 失败回滚”模型，不要再回退为“只有加载成功后才注册”；
- 后续若继续加强这一块，更适合继续补跨进程激活重定向、事件派发与窗口定位链路的自动化回归，而不是重新调整语义边界。

### D-09 工作区导入多线程扩展规范落地收口

当前状态：

- `src/AssestBundleTools.Application/Workspace/Internal/WorkspaceImportProcessingCoordinator.cs`
  当前已完成第二阶段收口：导入主链路已正式拆为
  `WorkspaceImportProcessingPlanBuilder -> WorkspaceImportWorkerPool -> WorkspaceImportCommitter`，
  并在 `Application` 导入链路内部打开 bundle 粒度有界并行。
- `src/AssestBundleTools.Application/Workspace/Services/WorkspaceImportExecutionOptionsResolver.cs`
  当前已把默认 worker 并发度规则固化为 `1 / 2 / 3 / 4` 四档，
  并允许 Presentation 偏好设置提供 `1-4` 的手动覆盖值，
  供导入链路统一解析运行时并发选项。
- `src/AssestBundleTools.Presentation.WinUI/Services/Workspace/IBackgroundTaskRunner.cs`
  与 `BundleExplorerWorkspaceBindingCoordinator` 当前已把手动导入和
  工作区打开/恢复时的自动导入刷新放到后台任务调度器执行，
  工作台通过左侧资源包窗格内的正式导入进度覆盖层反馈长耗时处理状态。
- `src/AssestBundleTools.Infrastructure/Bundles/Decryption/ExternalProcessAssetBundleDecryptor.cs`
  当前已升级为 `workspaceKey + decryptorId + bundleCacheKey`
  级别的稳定隔离路径，不再仅围绕源文件名推导工作目录。
- `docs/design/workspace-management.md`
  当前已经把多线程导入正式收敛为：
  - 同一 `workspaceKey` 只允许一个进行中的导入作业
  - `串行计划 -> bundle 粒度有界并行 worker -> 串行提交`
  - worker loop 必须独立调度到后台任务，避免同步型 Reader / Decryptor 让并行 worker 退化为单 worker 顺序消费
  - 默认 worker 并发度为 `1 / 2 / 3 / 4`，允许用户手动固定 `1-4`，且每个工作区上限不超过 `4`
- `tests/AssestBundleTools.Application.Tests` / `tests/AssestBundleTools.Infrastructure.Tests`
  当前已补上首轮并发回归：
  - 同一工作区执行闸门串行化
  - bundle 粒度并行处理
  - staging 输出路径隔离
- `WorkspaceImportProcessingResultDto`
  当前也已补上 `Planning / Screening / Worker / Commit / Total`
  五段耗时诊断，并额外记录 `ProbeDuration / ProbeDecryptorCount / ProbeEvaluationCount`，
  便于后续判断 `Probe` 是否成为新的吞吐瓶颈

也就是说，这条偏差的主体实现缺口已经关闭；
当前剩余的只是未来是否继续扩展 `Probe` 并发、是否调整并发度上限等增强议题。

与规范冲突点：

- `workspace-management.md` 已明确：
  - 多线程导入的并发边界必须停留在 `Application` 导入链路内部
  - 同一工作区需要先有执行闸门，再允许 bundle 粒度有界并行
  - 缓存写入、预览清理和元数据回写必须保持串行提交
- 当前实现已与上述边界对齐；
  当前不再存在“规范已定义但主链路仍串行”的核心偏差。

影响：

- 导入吞吐增强当前已经落在明确的三段模型内，
  后续不需要再通过“原地并行化旧串行循环”的高风险方式推进；
- 执行闸门、提交串行化与 staging 唯一路径已经把共享统计/诊断集合竞争、
  缓存删除与缓存保存交错，以及同工作区双重导入作业这类高风险点压回到受控边界内；
- 这条偏差当前已从“实现缺口”转为“持续守住并发边界”的维护项。

建议收口方向：

- 保持当前 `Application` 内部的 `PlanBuilder -> Worker Pool -> Committer`
  三段边界，不回退到单体串行协调器，也不把并发扩散到 `Presentation`；
- 继续把缓存写入、预览清理、元数据回写维持在提交阶段串行完成；
- 后续若要进一步提高吞吐，优先补 profiling 与回归，
  再评估是否需要单独引入低并发 `Probe` 池或调整每工作区 `4` 个 worker 的上限。

## 5. 建议的收口顺序

本轮 D-01 ~ D-09 已按原推荐顺序完成阶段性梳理。
当前除 D-06 后续深层测试补齐外，
其余非测试收口项已进入“维持边界、防止回弹”的阶段。

后续继续推进时，仍建议沿同一原则演进，而不是回头做一次性大重构：

1. 先保证边界不回弹
   - 持续防止 Presentation 对持久化细节、聚合宽接口、sibling `ViewModel` 具体实现重新长回直接耦合
2. 再围绕真实高风险路径补回归
   - 优先补缓存失效、预览失败、跨进程窗口激活与工作区回滚等关键链路
3. 最后在功能扩展时顺手收紧主类体量
   - 把新增流程优先落到明确协作对象，避免把职责重新灌回页面主类

执行原则：

- 先保证边界正确，再追求实现优雅；
- 优先抽离“变化原因不同”的逻辑，而不是只按行数切文件；
- 每完成一轮收口，都要同步更新主规范文档和路线图，避免文档落后于代码。
