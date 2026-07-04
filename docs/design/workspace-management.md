# AssestBundleTools 工作区管理设计

## 1. 文档目的

本文档用于约束“多工作区切换”能力的设计边界，目标是让工作区机制逐步演进到类似 VS Code、JetBrains IDE 这类工具的模型：

- 应用可以登记多个工作区
- 任意时刻只有一个活动工作区参与主任务
- 工作区运行时状态和持久化元数据分离
- 工作区内的资源包浏览通过标签页承载，而不是资源包浏览子窗口
- 资源详情后续可独立为同一工作区进程内的资源编辑窗口，但该窗口只迁移资产文档宿主和页面级状态，不改变工作区事实来源

当前文档与以下规范配套使用：

- [code-architecture-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/code-architecture-guidelines.md)
- [ui-final-spec.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/ui-final-spec.md)
- [project-build-roadmap.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/project-build-roadmap.md)
- [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md)
- [asset-editor-window-mode.md](asset-editor-window-mode.md)

## 2. 设计目标

### 2.1 工作区像 IDE 的“项目上下文”

工作区不是单纯的一次“打开文件”结果，而是一个可恢复、可切换、可记录最近状态的任务上下文。

一个工作区至少应包含：

- 工作区标识 `WorkspaceId`
- 展示名称
  该名称由用户显式配置并保存到工作区元数据中，不强制等于来源目录名
- 来源路径
  对于“先创建空工作区、后续再导入资源”的模式，允许暂时为空
- 工作区默认解密器
  用于约束当前工作区后续真实 AB 导入时默认采用的解密策略
- 打开时间 / 最近激活时间
- 当前模式
- 最近浏览的资源包
- 最近输出目录、筛选条件等 UI 偏好

### 2.2 不把重解析结果塞进元数据文件

后续真实接入 AB 读取后，不应把“每个资源包的完整读取结果”直接写入 `WorkspaceMetadataDto`。

推荐采用“三层持久化”：

1. 工作区元数据
   只保存轻量恢复信息，例如工作区名称、打开标签、筛选条件和最近输出目录。
2. 解析缓存
   单独保存每个 AB 的索引结果、预览描述和可失效缓存文件。
3. 变更暂存
   单独保存提取、替换、保存前暂存等结构化修改记录。

这样做的原因是：

- 工作区切换和恢复会更快
- 预览可直接命中派生缓存
- 解析器升级时可以只失效缓存，不破坏工作区元数据
- 元数据文件不会随着真实资源规模膨胀

补充约束：

- 工作区重新打开时，应对 `cache/bundle-index` 执行与正式导入相同的复用判定
- 当 Reader 的 `ParserVersion`、工具缓存 schema 版本、解密器版本或源文件指纹发生变化时，应自动重建受影响的资源包索引
- 不能要求用户必须重新手动导入一次，才能让资产列表和资源详情拿到新的缓存字段

### 2.3 区分三类状态

后续实现必须把工作区相关数据拆成三层：

1. 运行时会话状态
   例如已加载的 Bundle、资产、替换队列、批处理摘要。
2. 应用级工作区目录元数据
   例如最近工作区、固定工作区、最近激活时间、最近输出目录。
3. 页面级瞬时状态
   例如当前滚动位置、搜索框展开状态、资源包标签页、临时多选结果。

补充约束：

- “控制台页是否还能返回”属于窗口级 Presentation 状态，不属于工作区业务状态
- 当前窗口进入工作区后默认切到正式工作台，但仍允许通过显式操作返回控制台页

### 2.4 不污染原始资源目录，且数据目录由用户选择

工作区元数据不主动写回 AB 原始目录。

当前实现采用“双路径”策略：

- 全局目录索引默认写入应用数据目录
- 单工作区元数据文件写入用户在创建或打开工作区时显式选择的数据目录

这样既能避免污染原始资源目录，也能让用户把工作区数据放到更合适的位置，例如独立工具目录、缓存盘或项目辅助目录。

主要避免的问题包括：

- 污染游戏资源目录
- 引入权限问题
- 让用户误以为工具会直接修改源文件

如果未来需要“可分享工作区文件”，再单独引入显式导出机制。

### 2.5 自动识别的职责边界

当前项目里的“自动识别”不是单一判断点，而是一条分层链路。

后续实现默认按下面四层理解和收口：

1. 导入源识别
   把 `文件 / 目录 / 拖拽` 统一展开成真实文件，并在解密前尽早过滤明显无效输入。
2. 解密器识别
   这是当前最重要的自动识别主体。
   对无法通过普通头部或扩展名直接确认的来源，继续调用解密器 `ProbeAsync` 判断“该不该用这个解密器”。
3. Reader 路径识别
   解密后再进入 `IAssetBundleReader.CanRead(...)`，决定走真实 Reader 还是在读取阶段被拒绝。
   Reader 不承担加密格式探测职责。
4. 缓存命中识别
   最后根据 `source fingerprint + decryptorId + decryptorVersion + parserVersion + toolVersion`
   等维度自动判断索引和预览缓存能否复用，还是必须失效重建。

补充约束：

- 导入源识别和解密器识别属于“解密前自动识别”
- Reader 路径识别属于“解密后自动识别”
- 预览如何展示属于后续预览策略，不属于当前自动识别主轴

## 3. 推荐模型分层

### 3.1 Domain

Domain 继续只保留稳定概念：

- `WorkspaceId`
- `WorkspaceMode`
- 与工作区本身强相关的值对象

Domain 不负责：

- 最近工作区列表
- JSON 文件格式
- UI 记忆项
- 标签页实现细节

### 3.2 Application

Application 层拆成五部分：

1. `WorkspaceSessionRegistry`
   负责运行时工作区会话，承载当前已加载的数据和变更队列。
2. `WorkspaceContextPersistenceService`
   负责目录快照、工作区元数据与浏览状态的持久化抽象。
3. `WorkspaceContextCatalogService`
   负责最近工作区目录恢复、可用性语义、“上次活动工作区”恢复请求推导，以及激活/浏览状态元数据回写编排。
4. `BundleIndexCacheStore` / `PreviewCacheStore` / `WorkspaceChangeStore`
   负责工作区数据目录中的可失效缓存与变更暂存。
5. `WorkspaceImportExecutionGate` / `WorkspaceImportProcessingCoordinator`
   负责同一工作区导入任务的互斥、导入计划构建、受控并行 worker 调度，以及结果聚合与提交编排。

当前仓库里已经存在的 `IWorkspaceSessionRegistry` 继续承担第一部分职责。
当前目录恢复与元数据编排也已经通过 `IWorkspaceContextPersistenceService / IWorkspaceContextCatalogService`
从 Presentation 收口回 Application。
后续新增持久化能力时，应该继续补充独立接口，而不是把“最近工作区 / 目录索引 / 解析缓存”塞进 Session Registry。

### 3.3 Infrastructure

Infrastructure 负责工作区元数据落盘。

推荐默认实现：

- `JsonWorkspaceCatalogStore`
- `JsonWorkspaceMetadataStore`
- `JsonBundleIndexCacheStore`
- `JsonPreviewCacheStore`
- `JsonWorkspaceChangeStore`

它们只负责序列化、反序列化和文件系统访问，不负责业务决策。

### 3.4 Presentation

Presentation 层保留两个协同层次：

1. `WorkspaceContextService`
   负责当前活动工作区、已登记工作区摘要，以及 Application 恢复请求到 Presentation 导航请求的窄映射。
2. `BundleExplorer` 页面级标签页协调逻辑
   负责当前工作区内的资源包标签页状态。
3. `WorkspaceImportCoordinatorService`
   负责 `文件导入 / 目录导入 / 后续拖拽导入` 的入口协调、文件选择与轻量反馈，
   并把收集结果提交给 Application 层的工作区导入登记命令。

补充约束：

- 最近工作区目录恢复、可用性判断、“上次活动工作区”恢复请求推导，以及浏览状态/激活元数据回写编排
  当前都停留在 Application 的 `IWorkspaceContextCatalogService` 后面，而不是继续堆在 WinUI 服务里
- `WorkspaceContextService` 继续保持窄协调定位，不直接持有目录仓储、文件系统判断或工作区元数据落盘细节
- 工作台中的拖拽导入桥接当前只挂在左侧资源包列表区域，不挂整页
- 这样可以为后续资产列表、检查器等区域保留独立拖拽语义，而不让多个交互在页面根级别互相争抢

以上几个 Presentation 协同对象都不是持久化仓储。

## 4. 推荐存储结构

采用“全局索引 + 元数据 + 缓存层 + 暂存层”的结构。

推荐位置：

```text
%LocalAppData%/AssestBundleTools/
  workspace-catalog.json

<user-selected-workspace-data-root>/
  workspaces/
    <workspace-key>.json
  cache/
    bundles/
      <workspace-key>/
        <bundle-cache-key>/
          manifest.json
          normalized.bundle
    bundle-index/
      <workspace-key>/
        <bundle-cache-key>.json
    spine-groups/
      <workspace-key>/
        catalog.json
    previews/
      <workspace-key>/
        <preview-cache-key>.json
        <preview-payload-file>
  staging/
    decrypt/
      <workspace-key>/
        <decryptor-id>/
          <bundle-cache-key>/
            <derived-output-file>
            <result-file>
    changes/
      <workspace-key>.json
```

其中：

- `workspace-catalog.json`
  负责保存最近工作区列表、固定项、排序、最后激活工作区等轻量索引。
- `<user-selected-workspace-data-root>/workspaces/<workspace-key>.json`
  负责保存某个工作区自己的用户偏好和恢复信息。
- `cache/bundles/...`
  负责保存已解密、已标准化、可被 Reader / Preview / Extraction 直接读取的资源包物理缓存。
  该缓存是可重建派生产物，不是工作区元数据，也不是用户源文件的替代来源。
- `cache/bundle-index/...`
  负责保存每个 AB 文件的解析索引缓存，用于加速资源包列表、资产列表和基础恢复。
- `cache/spine-groups/...`
  负责保存当前工作区内 Spine 资源组目录的派生读模型，用于避免在 `资源 / Spine 组` 视图切换、资源包标签切换和窗口宿主迁移时重复遍历全工作区资产索引。
  该目录是可删除、可重建缓存，不是工作区元数据，也不是 Spine 详情、atlas 解析结果或预览载荷的事实来源。
- `cache/previews/...`
  负责保存缩略图、文本片段等预览派生缓存。
- `staging/decrypt/...`
  负责保存解密阶段派生文件、外部进程结果文件与短期工作目录；
  路径必须带上 `workspaceKey + decryptorId + bundleCacheKey` 或等价唯一段，避免后续并行导入时仅凭同名源文件发生覆写冲突。
  后续稳定实现中，`staging/decrypt` 只承载单次解密执行的临时文件和结果文件；可复用的标准化资源包应晋升到 `cache/bundles`。
- `staging/changes/...`
  负责保存结构化变更暂存，不与轻量元数据混写。

补充约束：

- 目录索引继续保持全局统一，避免最近工作区入口分散到多个目录
- 单工作区元数据目录由用户显式选择，并记录到目录索引中
- 删除工作区记录时只删除工具生成的元数据文件，不删除用户选择的数据根目录本身
- 删除工作区记录时，工具生成的解析缓存和变更暂存也应一并清理
- 新建工作区导航必须视为一次性新运行时会话，即使数据目录与此前删除或打开过的工作区相同，也不得激活或恢复旧页面状态。
- 删除当前活动工作区记录时，Presentation 必须同步清理当前活动上下文、窗口注册和进程级占用；
  后续在相同数据目录中新建工作区时不得激活或恢复删除前的资源包树、资产列表或浏览状态

### 4.1 Spine 组目录缓存

Spine 组列表是跨资产、跨资源包的派生读模型，不应在每次从普通资产列表切换到 `Spine 组` 时重新遍历所有 bundle 资产索引。
当前第一版已经把它收口为“运行时目录缓存 + 工作区派生目录缓存”的两层模型：

1. 运行时目录缓存
   `LoadSpineAssetGroupsHandler` 通过进程内 `SpineGroupCatalogCacheCoordinator` 优先复用已构建的 `SpineGroupCatalog`，供资源包标签切换、列表搜索、窗口模式放回 / 窗口化和同一工作区内多宿主共享。
2. 工作区派生目录缓存
   当查询携带工作区数据根时，`JsonSpineGroupCatalogCacheStore` 会将可重建目录写入 `cache/spine-groups/<workspace-key>/catalog.json`，供工作区重开后复用。
   持久化文件携带 `schemaVersion`、`workspaceKey`、`catalogFingerprint`、参与目录构建的 bundle index fingerprint、`detectorVersion`、创建时间和最近访问时间。

目录缓存只保存列表和搜索所需的轻量信息：

- 稳定 `GroupId`、组名和归一化搜索 token。
- skeleton / atlas / texture page 的资产身份、显示名、所在 bundle 和基础类型。
- texture page 数、组成资产数量和轻量诊断摘要。
- 能够从目录快速投影到当前资源包的锚点 bundle / group 关系。

目录缓存不得保存：

- atlas 完整文本、region overlay、Texture2D 图片 payload、runtime animation DTO 或完整详情诊断。
- 已解析的第三方 runtime 对象、WinUI 控件状态或 D3D / Win2D 资源。
- 替换后的有效载荷本体或撤销 / 重做事实。

失效规则：

- 任一参与匹配的 bundle index 指纹变化、资源包被导入 / 移除、Reader parser 版本变化、Spine 组检测版本变化、依赖图或名称归一化规则变化时，目录缓存必须失效或换 key 重建。
- 搜索关键字、当前资源包标签、列表本地筛选和窗口宿主变化只做内存投影，不进入昂贵目录构建 key。
- 普通替换、Spine 组成文件编辑保存、撤销 / 重做和删除操作记录通常不改变 Unity 资产身份与组成员关系，不应强制重跑目录检测；它们通过工作区操作记录读模型在查询返回时叠加“已修改 / 当前有效状态 / 关联影响”投影。
- 只有未来出现会改变资产身份、名称、路径、类型或组匹配关系的操作时，才把该操作声明为结构性失效源。

并发与一致性：

- 冷缓存构建当前按 `workspaceKey + catalogFingerprint` 使用进程内 single-flight，同一工作区内多个标签或窗口同时请求 Spine 组列表时只允许一个目录构建任务执行，其他请求等待或复用最近有效快照。
- 如果存在旧目录且新目录正在后台重建，Presentation 可继续展示旧目录的投影并标记轻量加载状态；没有任何可用目录时才显示空载入状态。
- 目录缓存保存前会剥离组成资产的当前替换来源和变更记录；查询返回前再通过当前工作区 session 重新 hydrate 操作记录投影，避免把变更记录状态固化进结构性目录缓存，导致撤销 / 重做频繁写缓存。
- 显式清理工作区缓存、移除资源包和移除工作区记录会删除该目录；删除后下一次进入 `Spine 组` 视图重新构建，不写操作记录。
- 当前第一版 `WorkspaceCacheUsageDto` 仍只统计 `cache/bundles` 和 `cache/previews`，`cache/spine-groups` 已参与清理和结构失效，但尚未纳入工作台顶部缓存总量展示。

## 5. 建议保存内容

### 5.1 全局目录索引

建议字段：

- `version`
- `lastActiveWorkspaceKey`
- `recentWorkspaces[]`

每个 `recentWorkspaces` 条目建议包含：

- `workspaceKey`
- `displayName`
- `sourcePath`
- `workspaceKind`
- `lastActivatedAtUtc`
- `isPinned`
- `lastKnownState`

### 5.2 单工作区元数据

建议字段：

- `workspaceKey`
- `displayName`
- `sourcePath`
- `createdAtUtc`
- `lastOpenedAtUtc`
- `lastBundleId`
- `lastAssetId`
- `lastOutputDirectory`
- `bundleSearchKeyword`
- `assetSearchKeyword`
- `assetTypeFilter`
- `showModifiedOnly`
- `showPreviewableOnly`
- `assetBundleDecryptorId`
- `excludedBundleSourcePaths[]`
- `openBundleTabs[]`
- `activeBundleTabId`
- `openAssetEditorWindows[]`

其中 `excludedBundleSourcePaths[]` 用于承载“从工作区移除单个资源包”的结果：

- 如果资源包来自显式文件导入且不再被其他导入目录覆盖，可直接从 `importedSources[]` 中移除
- 如果资源包来自目录导入，或在移除显式文件导入后仍被某个目录来源覆盖，则应把该资源包的源路径记录到 `excludedBundleSourcePaths[]`
- 工作区重新打开或重新处理导入来源时，资源发现阶段必须跳过这些被排除的资源包路径

其中 `openBundleTabs[]` 只记录页面级恢复所需的轻量状态，例如：

- `bundleId`
- `logicalPath`
- `isPreview`
- `selectedAssetId`
- `searchKeyword`
- `assetTypeFilter`
- `showModifiedOnly`
- `showPreviewableOnly`

其中 `openAssetTabs[]` 只记录资产详情标签恢复所需的轻量浏览状态，例如资产标识、显示名、选中的检查器页签，以及图像背景、Sprite 裁切显示模式、图片缩放/平移、Mesh 线框 / UV 显示模式、贴图叠加开关、网格/结构层显隐、Mesh 缩放值和线框旋转角度等预览观察选项；不保存真实详情内容或预览载荷。后续编辑器存在未应用草稿时，该结构只允许携带轻量 `EditDraftReference`，用于确认 / 恢复提示定位，不保存草稿内容。

其中 `openSpineGroupTabs[]` 只记录主窗口 Spine 组文档标签恢复所需的轻量浏览状态，例如：

- `groupId`
- `displayName`
- `groupKey`
- `previewState`

该结构只用于在资源包标签恢复后重新建立 `BundleId + GroupId` 的页面级组文档身份，并允许携带动画名、皮肤名、播放速度、播放时间、播放/暂停、setup pose、预览背景、视口缩放/平移和 atlas page 选择这类轻量观察状态；不保存 atlas 解析结果、Texture2D 预览载荷、region overlay 数据、诊断明细或组成资产快照。真实组详情仍在激活后通过 `LoadSpineAssetGroupDetailsQuery` 和现有预览管线重新读取。Spine 组文档当前已可在会话内与普通资产文档共用主窗口文档标签组和独立资源编辑窗口宿主；独立窗口快照使用 `openDocumentTabs[]` 保存混合标签顺序，Spine 组标签只写入 `BundleId / GroupId / DisplayName / GroupKey / PreviewState` 等轻量身份与观察信息。

其中 `openAssetEditorWindows[]` 只记录独立资源编辑窗口的轻量页面级状态，例如窗口位置 / 大小、窗口内统一文档标签顺序、活动文档、普通资产检查器分段，以及图片 / Mesh / Spine 预览浏览选项；不保存真实资源详情、预览缓存载荷、替换结果或编辑草稿内容。普通资产文档标签可以携带与主窗口同形状的轻量草稿引用；Spine 组标签只携带轻量组身份和预览观察状态。应用重新打开工作区时当前会读取该字段并弹出一次可控恢复提示；如果存在轻量草稿引用，提示会显示带草稿标记的标签数量，并允许用户决定本次成功打开的标签是否带回该标记，同时在弹窗中显示草稿恢复候选预检摘要；如果待恢复标签已在主窗口或现有独立窗口中打开，提示和工具栏入口会显示冲突标签数量，并引导用户关闭对应标签后再恢复。用户选择稍后时仍可通过工具栏入口恢复，但系统仍不自动创建独立资源编辑窗口。恢复执行会按当前资源包可用性和已打开文档身份拆分快照：可恢复项打开，不可恢复或冲突项继续保留为待恢复状态，用户可在提示中显式忽略；窗口 bounds 应在应用前做最小尺寸和当前显示器工作区安全归位。

打开资源详情、依赖跳转或请求新建独立资源编辑窗口时，Presentation 需要先检查主窗口和所有独立资源编辑窗口中是否已有同一 `BundleId + AssetId` 的资产文档标签。Spine 组文档同样按 `BundleId + GroupId` 做会话内冲突检测。命中时应激活并切换到已有标签，再把所在窗口置前，不再创建重复标签或重复独立窗口；显式移动标签 / 移动整组仍保持“移动现有标签实例”的语义。

跨宿主拖入检测范围应与可接收宿主的视觉范围一致：单个资产文档标签拖入主窗口时，目标是整个右侧资源详情宿主区域；拖入独立资源编辑窗口时，目标是整个窗口可接收区域，而不是仅限标签栏。整组资源详情拖入继续复用相同的全尺寸宿主检测。

独立资源编辑窗口只有一个资产文档标签时，单标签拖出窗口外不应创建新的等价独立窗口并关闭原窗口；该手势应按取消拖出处理，保留原窗口与原标签上下文。

编辑草稿的真实状态不写入 `openAssetTabs[]` 或 `openAssetEditorWindows[]`。当前 Application 层只提供最小草稿恢复候选读模型：`IAssetEditDraftStore` 按 `workspaceKey + draftId + draftMode` 读取草稿描述元数据，`GetAssetEditDraftRestorePlanHandler` 再用轻量引用里的 `SourceFingerprint` 判断该标记是可恢复、缺失、来源不匹配、已过期还是引用无效。Presentation 弹窗当前会在显示前通过 `AssetEditDraftRestorePlanGateway` 读取该计划，并把数量摘要用于恢复提示和重复弹窗判定；当预检计划可用时，执行恢复只把预检为可恢复的轻量标记带回新标签，缺失、来源不匹配、已过期和引用无效的草稿标记不会继续保留。组合根已接入 `WorkspaceChangeSetAssetEditDraftStore`，从 `staging/changes/<workspaceKey>.json` 的 `editDrafts[]` 描述列表读取草稿元数据。该流程不读取真实草稿内容，也不提交、丢弃或合并草稿；后续真实草稿队列和持久化实现应继续落在 `staging/changes` 或等价变更暂存层，并与工作区轻量元数据分离。

补充约束：

- `selectedAssetId` 不是装饰字段，而是资源列表焦点恢复的稳定键
- `openBundleTabs[]` 与其中的 `openAssetTabs[]` 顺序都应保留用户最后一次拖拽重排结果，不能在恢复时退化成默认打开顺序
- 标签页切换时必须先捕获当前标签页的列表状态，再应用目标标签页状态，避免程序化筛选变更把旧标签页的选中资源误写回新标签页
- 资源列表筛选仅影响列表展示与焦点恢复，不应直接关闭该资源包下已经打开的资产详情标签；当当前筛选结果为空时，资产详情标签仍应保持独立可见，且列表焦点不应自动跳到筛选结果第一项
- 如果筛选变化后当前激活的资产详情标签并未改变，则应直接复用该标签既有的详情与预览状态，不重复触发图片预览加载，也不应刷新详情页内部滚动位置
- 资源包标签与资产详情标签都应支持统一的右键关闭菜单，至少包含“关闭其他 / 关闭全部”；若用户在非活动标签上触发该菜单，关闭完成后焦点应稳定落到保留目标
- 标签拖拽重排后的最终顺序必须回写到底层页面级集合，再进入浏览状态落盘；不能只停留在控件临时视觉顺序
- 页面卸载或窗口关闭前应主动刷新一次当前工作区浏览状态，避免只依赖节流写入导致最后一次选中资源没有落盘

### 5.3 资源包索引缓存

建议字段：

- `workspaceKey`
- `bundleCacheKey`
- `bundleId`
- `displayName`
- `logicalPath`
- `sourceFingerprint`
- `cachedAtUtc`
- `parserVersion`
- `toolVersion`
- `decryptorId`
- `decryptorVersion`
- `dependencies[]`
- `assets[]`

其中 `assets[]` 当前建议只保存轻量浏览字段，例如：

- `assetId`
- `assetName`
- `assetType`
- `assetPath`
- `containerPath`
- `sizeInBytes`
- `hasPreview`
- `canExtract`
- `canReplace`
- `serializedFileName`
- `pathId`

### 5.3.1 标准化资源包物理缓存

标准化资源包物理缓存用于把原始 AB 经解密、必要解包或格式标准化后的结果保存到工作区数据目录，供后续 Reader、预览生成、提取导出和 Spine 资源组解析共同复用。

推荐目录：

```text
cache/
  bundles/
    <workspace-key>/
      <bundle-cache-key>/
        manifest.json
        normalized.bundle
```

`manifest.json` 建议字段：

- `workspaceKey`
- `bundleCacheKey`
- `sourceFingerprint`
- `sourcePath`
- `decryptorId`
- `decryptorVersion`
- `normalizerVersion`
- `artifactFileName`
- `artifactFingerprint`
- `artifactSizeInBytes`
- `createdAtUtc`
- `lastAccessedAtUtc`
- `diagnostics[]`

设计规则：

- 该缓存是派生 artifact，可删除、可重建，不作为用户源文件或操作记录事实来源。
- 导入刷新、首次预览、首次提取都可以触发缓存生成，但生成入口必须收敛在 Application / Infrastructure 边界内，Presentation 不直接创建或读取该文件。
- `IAssetBundleReader`、`IAssetPreviewGenerator`、提取导出器和 Spine 详情加载都应优先使用同一份标准化资源包路径，避免各链路分别对原始 AB 重复解密或解包。
- `PassThrough` 与外部解密器场景都应落为可复用物理 artifact：`manifest.json` 只描述 artifact，实际可读包统一放在同目录 `normalized.bundle`，避免工作区继续依赖用户源文件路径。
- 缓存身份不能只使用文件路径；至少要包含 `workspaceKey + bundleCacheKey + sourceFingerprint + decryptorId + decryptorVersion + normalizerVersion`。
- 源文件指纹、解密器身份、解密器版本或标准化版本变化时，该 artifact 必须失效，并级联失效依赖它的资源包索引缓存和预览缓存。
- 工作区关闭不要求删除该缓存；移除工作区记录、移除资源包来源或显式清理缓存时才删除对应 artifact。

当前实现状态：

- Application 已新增 `IBundleArtifactStore`、`BundleArtifactManifestDto`、`BundleArtifactSaveRequestDto` 和 `WorkspaceBundleArtifactResolver`，导入 worker 与资源详情预览查询都会先通过同一条 artifact resolver 取得可读取 bundle 路径。
- Infrastructure 已新增 `JsonBundleArtifactStore`，目录落在 `cache/bundles/<workspaceKey>/<bundleCacheKey>/`；外部解密器与 `PassThrough` 场景都会复制为 `normalized.bundle` 并写入 `manifest.json`。旧版仅 manifest、`isMaterialized=false` 的 artifact 会在解析时被视为不可复用，并在下次导入 / 预览 / 提取触发时重建为标准化物理包。
- `manifest.json` 当前已实际记录 `workspaceKey / bundleCacheKey / resolvedBundlePath / isMaterialized / sourceFingerprint / bundleFingerprint / decryptorId / decryptorVersion / normalizerVersion / createdAtUtc / lastAccessedAtUtc`。`IBundleArtifactStore.GetUsage(...)` 已可统计 artifact 数量、占用字节和每项访问时间；诊断数组仍属于后续增强字段。
- 标准化 artifact 的复用判定当前基于 `workspaceKey + bundleCacheKey + sourceFingerprint + decryptorId + decryptorVersion + normalizerVersion`；不匹配或物理文件缺失时会删除旧 artifact 并重新解密 / 保存。
- `WorkspaceBundleArtifactResolver` 已对同一 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 的 artifact 生成阶段加 single-flight 闸门；并发预览、提取或导入命中同一资源包时，后续请求会等待首个生成请求完成，再通过 manifest 复用同一 artifact。
- 资源包移除、工作区记录移除和工作区数据重置都会同步清理 `cache/bundles`。导入刷新中若只是 parser / tool 版本变化，会保留仍有效的标准化 artifact，只清理旧索引与预览缓存，避免刚生成的新 `normalized.bundle` 被提交阶段误删。
- 当前已提供基于 `MaximumSizeInBytes / MaximumArtifactCount` 的 `IBundleArtifactStore.Prune(...)` LRU 清理能力，并按 `lastAccessedAtUtc`、`createdAtUtc` 和稳定键淘汰最旧 artifact；标准化 artifact 在保存和成功复用时会回写 `lastAccessedAtUtc`，loaded-session 已基于标准化 artifact identity 复用。`WorkspaceCacheMaintenanceGateway` 当前提供两层入口：历史 bundle artifact usage / prune 入口保留给兼容调用；面向 UI 的当前工作区缓存 usage / clear 入口会聚合 `cache/bundles` 标准化资源包和 `cache/previews` 预览缓存，设置与诊断页的显式清理会同时清理两类可重建派生产物，并通过事件通知工作台顶部缓存统计刷新。导入、预览、提取首次生成 artifact 后仍按当前工作区容量上限自动 prune 标准化 artifact，并通过 `ProtectedBundleCacheKeys` 保护刚生成的 artifact；预览缓存保存后也会按同一偏好触发 `IPreviewCacheStore.Prune(...)`，通过 `ProtectedPreviewCacheKeys` 保护刚生成的预览项。进程内 loaded-session 已补默认容量上限和内存压力退避；SaveRebuild 保存预览 / 真实执行当前已读取已有标准化 artifact 和 loaded-session identity，并已预检目标输出路径 blockers；缺失或陈旧 artifact 只标记诊断，不提前 materialize。
- 后续 Export / SaveRebuild 不得直接打开用户源 AB、解密 staging 或自行复制可读包。Application 应继续用 `WorkspaceBundleArtifactResolver` 为每个目标 bundle 解析标准化 artifact 和 loaded-session identity，并通过 `IWorkspaceSaveOutputProbe` 把目标输出路径 blockers 作为无副作用 preflight 纳入计划，再把标准化只读输入、操作变更集和输出计划交给 Infrastructure 导出 / 写包适配器。若写包需要可变上下文，应从标准化 artifact 派生 staging 副本；标准化 artifact 与 loaded-session 仍只作为可重建的只读基线。真实工作区导出 / 保存重建的资源处理器并发、AB 写包并发、检查点和 `_abt-bundle-export-manifest.json` 契约见 [workspace-export-save-pipeline.md](workspace-export-save-pipeline.md)。

### 5.3.2 活跃资源包加载会话缓存

活跃资源包加载会话缓存是进程内短生命周期缓存，用于复用 `AssetsManager.LoadBundleFile(...)` 和 `LoadAssetsFileFromBundle(...)` 之后的加载上下文，解决多预览标签、批量提取、Spine 整组提取在短时间内对同一 AB 重复物理打开和重复解析的问题。

它不写入工作区目录，也不参与重启恢复。

设计规则：

- 缓存 key 应基于标准化资源包 artifact 身份，而不是单纯文件路径；当前 Application 会从 `cache/bundles` manifest 派生包含 `workspaceKey + bundleCacheKey + normalizerVersion + bundleFingerprint` 的 loaded-session identity，并传给 Infrastructure。没有 artifact identity 的直接 Reader / 测试调用才回退到物理路径 key。
- 必须支持 single-flight：同一 key 的首次加载尚未完成时，后续请求应等待同一个加载任务，而不是并发启动多个 `LoadBundleFile`。
- 缓存对象通过 lease 暴露给 Infrastructure 内部消费者；有活动 lease 时不得卸载，最后一个 lease 释放后再进入滑动 TTL。
- TTL 应为滑动过期，每次命中刷新过期时间；首版建议取 `30-120` 秒，并支持后续通过设置或诊断调整。
- 应设置最大保留数量或内存预算，并按 LRU / 最近访问时间淘汰；大 bundle、解包失败或内存不足诊断命中的 bundle 可以使用更短 TTL 或直接禁用后台预热复用。
- 当前应默认把同一 loaded session 的 AssetsTools 访问串行化或使用 bundle 级 `SemaphoreSlim` 保护，不假设 `AssetsManager / BundleFileInstance / AssetsFileInstance` 可安全多线程无锁并发读取。
- 工作区关闭、资源包移除、导入刷新失效、标准化 artifact 失效、解密器变更或内存压力触发时，必须主动驱逐对应 loaded session。
- 该缓存属于 Infrastructure 实现细节，不得把 AssetsTools.NET 类型暴露到 Application DTO、Presentation ViewModel 或工作区持久化状态。

### 5.4 预览缓存

建议拆成：

- 描述文件
- 内容文件

描述文件建议字段：

- `workspaceKey`
- `previewCacheKey`
- `bundleCacheKey`
- `assetId`
- `previewKind`
- `contentType`
- `relativePayloadPath`
- `relativeMetadataPath`（可选）
- `sourceFingerprint`
- `generatedAtUtc`
- `generatorVersion`
- `audioTranscoderId`（可选）
- `audioTranscoderVersion`（可选）
- `metadataSchemaVersion`（可选）
- `previewInfoItems` / `previewMessage`（可选）
  用于承载尺寸、源格式标识、摘要回退原因等结构化预览信息，避免把这类专属信息混回通用详情元数据。
- `imageOverlay` / `imageFocus`（可选）
  用于图片类富预览的结构化附加数据，例如 Sprite 裁切框、pivot、9-slice border 与裁切结果载荷。

补充约束：

- `previewInfoItems` 只承载轻量补充信息，不承载音频波形点、atlas 矩形集合、模型默认相机等结构化富预览数据。
- `imageOverlay / imageFocus` 仅用于图片类预览中与主 PNG 同源的轻量结构化数据；更复杂的富预览元数据仍应进入 sidecar metadata。
- `Text` 预览缓存当前过渡实现会保存完整规范化文本载荷；`AssetPreviewDto.TextContent` 只作为 Presentation 有界展示文本，展示样本由统一 `AssetPreviewTextDisplayPolicy` 生成，并通过 `TextDisplay` 暴露截断和行数字段。preview-cache-backed 文本预览会额外暴露 `FullContentReference`，完整查看只能把该 reference 传回 Application 的 `LoadPreviewTextChunkQuery` 读取有限片段，Presentation 不得自行解析缓存路径。`JsonPreviewCacheStore.LoadTextChunk` 已用文件流按规范化字符 offset 顺序读取，常规分块查询不再把完整 payload 拷贝进内存；旧缓存缺失完整字符数元数据时仍会扫描剩余文本计算 `TotalLength`。结构化 Application 消费者当前仍可读取 `FullTextContent ?? TextContent`，避免 Spine atlas / JSON skeleton 等长文本被 UI 截断策略破坏；这是过渡形态，后续应迁移到内部 payload reader 或 reference query。影响文本载荷语义或展示预算的变更需要递增 `generatorVersion` 或预算策略版本，让旧截断缓存自动失效。后续完整文本查看 / 编辑应按 [asset-preview-payload-contract.md](asset-preview-payload-contract.md) 继续补文件替换源 reference、工作区 staging reference、编辑草稿 reference、行索引和搜索 query。
- Mesh 预览的轻量几何 sidecar 当前包含顶点、边、submesh 摘要、UV0、通道摘要以及用于贴图化预览的三角面索引；Presentation 只能消费该 DTO 与关联贴图缓存载荷，不得为了补贴图效果直接解析 AB。
- 当预览能力从 `Image / Text / Summary` 扩展到 `Audio / Model / Atlas` 时，应把这类结构化数据写入 `relativeMetadataPath` 指向的 sidecar metadata 文件。
- `previewKind` 必须保持通用枚举语义，避免为某一个 Unity 资源类型直接设计专用缓存目录结构。
- Presentation 层只消费 `AssetPreviewDto` 与缓存产物，不直接感知资源包或解密细节。

当前实现状态：

- `IPreviewCacheStore` 已提供 usage / prune 契约，`JsonPreviewCacheStore` 会统计描述文件、主 payload 和 `imageFocus` 附加载荷占用，并可按生成时间清理最旧预览缓存项。
- 当前工作区缓存统计不再只看 `cache/bundles`：Application 的 `GetWorkspaceCacheUsageHandler` 会汇总标准化资源包 artifact 与预览缓存，设置页摘要和工作台顶部缓存标签均使用该聚合读模型。
- 设置页的“清理当前工作区”通过 `ClearWorkspaceCacheHandler` 同时清理标准化资源包和预览缓存；其中标准化资源包清理仍会主动失效对应 loaded-session，预览缓存清理不写操作记录，也不刷新资产列表。
- `GetAssetPreviewHandler` 会在预览 payload / sidecar 落盘后调用 `IPreviewCachePrunePolicy`，按设置页同一容量偏好清理最旧预览项，并保护刚生成的 `previewCacheKey`，避免新预览被自身容量治理立刻删除。

### 5.5 变更暂存

建议字段：

- `workspaceKey`
- `updatedAtUtc`
- `schemaVersion`
- `operationSequence`
- `operations[]`
- `changes[]`
- `editDrafts[]`

当前设计已进入“操作记录 + 物化摘要”两层模型，详细见 [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md)。

`operations[]` 是后续提取、替换、编辑草稿应用和保存输出的可恢复事实来源，负责记录操作顺序、状态、目标、来源、前一状态、结果和恢复诊断。它用于支持重启恢复、撤销、长操作中断识别和保存检查点。

`changes[]` 保留为当前物化变更摘要和旧 UI / 保存预览兼容结构，不再继续扩展为完整历史记录。每个 `changes[]` 条目当前可先保持通用结构：

- `changeKey`
- `changeKind`
- `bundleId`
- `assetId`
- `summary`
- `recordedAtUtc`
- `sourceDescriptor`
- `outputPath`

后续不再优先通过扩展 `summary / sourceDescriptor / outputPath` 来表达新语义；需要恢复、撤销或检查点参与的字段应进入 `operations[]` 的结构化目标、来源、前一状态和结果模型。

`editDrafts[]` 当前只保存草稿描述元数据，不保存草稿内容。字段包括：

- `draftId`
- `draftMode`
- `sourceFingerprint`
- `updatedAtUtc`
- `expiresAtUtc`
- `sourceDescription`
- `summary`

该列表服务于恢复提示里的候选预检：恢复提示只需要知道草稿标记是否仍可定位、是否与来源指纹匹配、是否过期，以及可展示的轻量说明。预检计划可用时，执行窗口恢复只允许保留预检为可恢复的轻量标记。真实草稿内容、合并策略、确认 / 丢弃队列和保存应用语义仍由后续编辑器模型定义。

真实草稿内容后续应进入 `staging/drafts/<workspaceKey>/<draftId>/`，真实长操作或大载荷中间产物后续应进入 `staging/operation-payloads/<workspaceKey>/<operationId>/`，不得塞入工作区元数据或资源包浏览状态。

### 5.6 缓存失效规则

标准化资源包物理缓存、解析缓存和预览缓存必须是“可重建的”。

建议至少基于以下字段判断失效：

- `sourcePath`
- `fileSize`
- `lastWriteTimeUtc`
- `contentHash`（可选）
- `decryptorId`
- `decryptorVersion`
- `parserVersion`
- `toolVersion`

当前阶段实现补充约束：

- 导入刷新不再先整工作区清空 `BundleIndexCacheStore`
- 对同一 `bundleCacheKey` 会优先基于上述字段执行命中复用
- 对失效或已移除的资源包，只清理对应的标准化资源包物理缓存、资源包索引缓存与关联预览缓存
- 进程内活跃资源包加载会话缓存不落盘；上述持久缓存失效时，必须同步驱逐对应 loaded session，避免继续读取旧 artifact

### 5.7 多线程导入与并行度规则

工作区导入后续允许扩展为受控并行，
但并发边界必须停留在 `Application` 的导入链路内部，
不能把解析、缓存写入或共享状态竞争回推到 `Presentation`、`WorkspaceSessionRegistry`
或任意页面级协调对象。

当前实现进度补充：

- 第一阶段前置约束已落地：`WorkspaceImportExecutionGate` 已统一保护手动导入与工作区重新打开时的自动导入刷新
- 外部解密 staging 路径当前也已升级为 `workspaceKey + decryptorId + bundleCacheKey` 级别的稳定隔离目录
- 第二阶段已落地：导入协调器当前已正式拆为 `PlanBuilder -> Worker Pool -> Committer`，并在 `Application` 导入链路内部打开 bundle 粒度有界并行
- worker 池必须把每个 worker loop 作为独立后台任务调度，不能只依赖 `async/await` 自然让出；当前 AssetsTools Reader 和直通解密器都可能同步完成并返回已完成 `Task`，如果不显式调度，会退化为第一个 worker 顺序吃完整个队列
- 默认并发度当前已通过 `WorkspaceImportExecutionOptionsResolver` 固化为 `1 / 2 / 3 / 4` 四档；用户可在“设置与诊断”中手动指定 `1-4` 个导入 worker 或保持自动模式；提交阶段与工作区级 Probe 仍保持串行
- 手动导入和工作区打开/恢复触发的自动导入刷新当前都由 Presentation 后台任务调度器承载，并在工作台左侧资源包窗格内显示正式导入进度覆盖层；导入、解析、缓存写入的职责仍停留在 Application 导入链路内
- 导入进度由 Application 层通过 `WorkspaceImportProgressDto` 暴露阶段、已完成资源包数、资源包总数与当前进度项；Presentation 只负责把该进度格式化为本地化文本和进度条，不自行推断导入总量。
- 导入处理结果当前已补上 `Planning / Screening / Worker / Commit / Total` 五段耗时诊断，并额外记录 `ProbeDuration / ProbeDecryptorCount / ProbeEvaluationCount`，供后续评估 `Probe` 是否值得单独并行化

推荐规则如下：

1. 工作区级互斥
   同一 `workspaceKey` 在任意时刻只允许存在一个进行中的导入/刷新作业。
   手动导入、工作区重新打开时的自动导入刷新，以及后续任何“重建导入缓存”入口都应复用同一执行闸门。
2. 并行粒度
   第一版只允许在“已发现的 bundle 来源文件”粒度做并行，
   不把单个 bundle 内部的深读、依赖遍历或预览派生拆成多线程主路径。
3. 分阶段模型
   导入处理默认按三段组织：
   - 计划阶段串行执行：加载工作区元数据、展开导入源、做来源筛选、解析工作区级解密器、快照现有索引缓存和预览描述。
   - worker 阶段有界并行执行：按单 bundle 处理 `fingerprint -> 缓存复用判断 -> decrypt -> Reader.CanRead -> Reader.Read -> 构建索引结果/诊断`。每个 worker loop 需要显式调度到后台任务，确保同步型 Reader / Decryptor 也能按 worker 数重叠执行。
   - 提交阶段串行执行：统一保存/删除缓存、清理关联预览、汇总统计与诊断、清理陈旧来源，并按条件回写真实解密器。
4. 共享状态规则
   并行 worker 不直接修改 `WorkspaceMetadataStore`、`WorkspaceSessionRegistry`、
   共享 `Dictionary/List` 计数器或全局缓存集合。
   worker 只返回不可变结果对象、诊断或写入意图；
   真正的 `BundleIndexCacheStore / PreviewCacheStore` 写入与删除由提交阶段统一完成。
5. staging 路径规则
   解密阶段的工作目录和输出文件路径必须包含 `workspaceKey + decryptorId + bundleCacheKey`
   或等价唯一标识，不能只基于源文件名推导。
   这样即便两个不同目录里存在同名 `hero.ab`，后续并行导入时也不会覆写彼此的派生文件。
6. 默认并发度
   导入 worker 池默认并发度按下面规则确定：
   - 逻辑核心数 `<= 1` 时，使用 `1`
   - 逻辑核心数 `2-4` 时，使用 `2`
   - 逻辑核心数 `5-6` 时，使用 `3`
   - 逻辑核心数 `>= 7` 时，使用 `4`

   等价公式可写为：
   `processorCount <= 1 ? 1 : Min(4, Max(2, Ceiling(processorCount / 2)))`

   第一版每个工作区的导入并发度上限不应超过 `4`，
   避免 `AssetsTools.NET` 深读、外部解密进程和磁盘 IO 同时放大时把桌面交互压垮。
   用户设置允许把导入 worker 固定为 `1-4`；
   固定值只覆盖自动推导，不改变同一工作区上限和三段模型边界。
7. Probe 并发规则
   工作区级解密器自动识别在第一版仍保持串行 Probe。
   若后续确认 Probe 成为明显瓶颈，再单独引入低并发 Probe 池，
   其默认并发度上限为 `2`，且不得高于正式导入 worker 池并发度。

## 6. 切换流程建议

工作区切换建议遵循下面的顺序：

1. 当前页面把需要保留的标签页状态和筛选偏好写回工作区元数据。
2. `WorkspaceContextService` 切换活动工作区。
3. 页面根据新工作区摘要重新拉取资源包列表、资产列表和变更摘要。
4. 批处理页、设置页等依赖活动工作区的页面收到通知后刷新。

切换时不应直接复用上一工作区的页面缓存，否则容易出现：

- 资源包列表残留
- 检查器显示旧数据
- 标签页恢复到错误工作区
- 批处理页绑定到错误工作区

### 6.1 打开方式提示

当前实现规定：

1. 新建工作区时，先打开一个集中式可视化配置面板。
2. 新建和打开已有工作区时，面板统一优先展示工作区数据目录，工作区名称字段紧跟在目录输入区下方。
3. 新建工作区时，工作区名称可由用户编辑，默认优先读取目录中已有工作区元数据的名称；如果目录还没有工作区，则退回到目录名生成建议值。
4. 新建工作区时，如果所选目录里已经存在可识别的工作区，则继续提示“直接打开”或“重置后重新创建”。
5. 选择“新建工作区”后，本次导航必须视为一次性强制新运行时会话；选择“重置后重新创建”时还应强制回到当前窗口加载，避免跨进程占用与重置清理互相争抢。上述两类新建流程都不得激活同 `workspaceKey` 的旧页面状态，不得在加载失败时恢复重建前的资源包树、资产列表或浏览恢复点；新会话绑定成功后应清除该一次性标记，后续刷新和再次打开恢复普通去重语义。
6. 打开已有工作区时，工作区名称字段只读，仅用于展示目录中识别出的现有名称，不承担重命名职责。
7. 打开已有工作区时，要求所选目录中存在且只能明确识别出一个工作区；若目录里没有工作区或存在多个候选，则保持停留在当前面板中提示用户重新选择。
8. 控制台页和工作台顶部命令区复用同一套新建 / 打开工作区流程，避免入口行为不一致。
9. 如果用户通过目录直接打开了一个当前尚未进入最近工作区索引的工作区，则在工作区真正绑定成功后自动补登记到目录索引。

最近工作区列表中的再次打开也遵循相同的最后一步：

- 直接复用已登记的来源路径和数据目录
- 打开策略由“设置与诊断”中的偏好设置控制
- 当前支持 `默认当前窗口 / 默认新建窗口 / 每次询问` 三种模式
- 当策略为 `默认新建窗口` 时，如果目标工作区已被某个主窗口承载，则直接激活该窗口而不是重复派生新窗口
- 当当前窗口尚未承载任何工作区时，首次打开工作区会优先复用当前窗口进入工作台；只有当前窗口已经存在工作区上下文时，“新建窗口”策略才会真正派生新进程
- 当策略为 `每次询问` 时，如果结果实际上已可确定为“当前窗口打开”（例如冷启动后的首次打开，或控制台再次打开当前窗口已加载的工作区），则应直接进入工作台，不再弹出额外询问框

该偏好属于 Presentation 层窗口行为，不下沉到 Domain。

### 6.2 启动自动恢复规则

当前阶段新增以下启动恢复规则：

1. 应用启动且当前窗口没有显式启动参数时，先读取“启动时恢复上次工作区”偏好设置。
2. 只有当该设置开启时，才继续读取工作区目录索引中的 `lastActiveWorkspaceKey`。
3. 如果对应工作区条目仍存在且处于可用状态，则当前窗口直接进入正式工作台并打开该工作区。
4. 如果设置关闭、对应条目不存在或已不可用，则保持停留在控制台页，由用户手动选择处理。

补充约束：

- 自动恢复只生成 Presentation 层导航请求，不会在启动阶段提前创建运行时工作区会话
- “不可用工作区不自动恢复”的优先级高于“最近活动工作区优先恢复”

## 7. 去重与恢复规则

建议采用以下规则：

1. 相同规范化来源路径视为同一个工作区目录项。
2. 刷新工作区时更新已有目录项，不重复增加最近列表。
3. 如果来源路径暂时不可访问，目录项保留，但标记为“不可用”。
4. 最近工作区列表按 `Pinned > LastActivatedAtUtc` 排序。
5. 同一工作区内同一资源包默认只恢复一个标签页实例。

## 8. 当前阶段落地范围

当前实现已经收敛并完成以下稳定子集：

- `WorkspaceContextService` 从“仅当前工作区”演进为“活动工作区 + 已登记工作区列表”
- 最近工作区目录恢复、可用性语义、“上次活动工作区”恢复请求推导与浏览状态/激活元数据编排
  当前已下沉到 Application 层的 `WorkspaceContextCatalogService`
- `BundleExplorer` 在打开或刷新工作区时，把摘要登记到应用级工作区上下文
- 主工作区内的资源包浏览扩展能力统一改为标签页
- `Infrastructure` 已提供 `JsonWorkspaceCatalogStore / JsonWorkspaceMetadataStore`
- `Infrastructure` 已补充 `JsonBundleIndexCacheStore / JsonPreviewCacheStore / JsonWorkspaceChangeStore`
- 工作区目录索引已落盘到应用数据目录
- 单工作区元数据已支持落到用户显式选择的数据目录
- 工作区数据目录已按“元数据 / 解析缓存 / 变更暂存”分层，避免把重解析结果塞进元数据文件
- 单工作区元数据已支持保存工作区级 `AssetBundleDecryptorId`
- 当前活动工作区的资源包标签页、活动标签页、资源包搜索和活动筛选状态已可回写并恢复
- 控制台页已接入最近工作区真实列表、删除记录和再次打开入口
- 新建 / 打开工作区时已接入类似 IDE 项目向导的可视化配置面板
- 工作区向导已进一步收敛为“目录优先”的单表单流程，名称字段位于目录输入区下方并按目录自动建议
- 新建工作区时，如果目录中已存在工作区元数据，当前实现已支持“直接打开 / 重置后重建”的显式决策；其中重置后重建会强制创建新的页面/运行时会话，避免复用重置前的资源包树、资产列表或浏览恢复点
- 工作区名称已从“来源目录名推导”演进为“用户可配置并持久化”的元数据字段
- 新建工作区已支持“空工作区”模式，不再强制要求先指定 AB 来源目录
- 新建工作区时已支持选择已有解密器，打开已有工作区时会只读展示并沿用元数据中的解密器配置
- 再次打开已登记工作区时已改为由设置项统一控制打开策略，支持默认当前、默认新建和每次询问
- 当前长期窗口策略已收敛为“一个工作区由一个应用进程承载”：再次打开工作区时，不论偏好是当前窗口还是新建窗口，只要目标工作区已在任一已运行窗口中承载，就会优先激活既有实例；若命中的是当前进程已加载的工作区，则直接切回对应工作台，不再停留在控制台页
- “新建窗口打开工作区”当前已改为启动新的主进程，而不再在同一 `App` 进程内继续 `CreateMainWindow(...)`
- 当前已补上显式启动参数解析：新进程会通过命令行负载接收 `Shell` 首屏导航请求，并在启动早期完成跨进程重定向判断
- 当前已补上工作区级跨进程激活：运行中的工作区进程会按稳定 `workspaceKey` 注册实例占用，并接收后续“激活已有工作区窗口”的请求
- 当前工作区窗口注册已完成第二轮稳定化：窗口会同时区分已正式绑定成功的 formal 工作区和正在加载中的 pending 工作区；目标工作区在真正加载前先做 pending 预登记，加载成功后晋升为 formal，加载失败时则清理 pending 并优先恢复此前已稳定存在的工作区上下文
- Shell 已收敛为“控制台态 / 工作台态”两态模型：可根据当前状态动态承载控制台页或正式工作台，并支持从工作台返回控制台
- 一级导航第一入口位会在控制台态显示“控制台”，进入工作台态后显示“工作台”
- 统一导入协调服务当前已覆盖 `文件 / 目录 / 拖拽` 三类入口，工作台中的拖拽导入只作用于左侧资源包列表区域
- 导入入口当前已经从“只记录 UI 选择”推进到“登记工作区导入来源 + 执行解密器 + 生成资源包索引缓存”
- 导入处理当前已补上“导入源识别”层：目录、文件和拖拽来源展开后，会先执行轻量直检，尽早命中普通 AB 并筛除空文件等明显无效输入
- 导入处理当前会把来源展开为真实文件列表，经工作区默认解密器和 `IAssetBundleReader` 处理后写入 `BundleIndexCacheStore`
- 工作区默认解密器当前已支持“自动识别”模式：导入时会先按工作区级探测非回退解密器，再回退到直通解密器，并在成功建立或复用索引后把真实解密器固化回工作区元数据
- 工作区导入结果当前已补充第一轮结构化失败诊断：会回传自动识别探测摘要，以及“导入源筛除 / 解密失败 / 读取器拒绝 / 读取失败”四类问题明细，供工作台日志与后续诊断页复用
- 工作区导入结果当前已补上结构化缓存诊断：会回传 `缓存未命中 / 来源指纹变化 / 解密器变化 / 解密器版本变化 / 工具版本变化 / 解析器版本变化 / 来源移除` 等标准原因，而不是只保留数量统计
- 当前导入入口已经补上 `workspaceKey` 级执行闸门，工作区重新打开时的自动刷新也会复用同一互斥链路
- 当前工作台会把手动导入与工作区打开/恢复触发的自动导入刷新放到后台执行，并在左侧资源包窗格内显示正式进度覆盖层，避免长时间解析时阻塞窗口响应或遮住整页工作区
- 外部解密 staging 目录当前已补上 `workspaceKey + decryptorId + bundleCacheKey` 唯一路径，避免不同来源下的同名 bundle 派生文件互相覆写
- 当前导入处理协调器已正式拆为 `WorkspaceImportProcessingPlanBuilder -> WorkspaceImportWorkerPool -> WorkspaceImportCommitter`，bundle 粒度有界并行也已在 `Application` 导入链路内部落地
- 当前导入结果与工作台日志已补上阶段耗时诊断，能够直接观察 `Planning / Screening / Worker / Commit` 的相对开销，并额外看到 `Probe` 的独立耗时与评估规模
- 工作区运行时会话当前已改为优先从 `BundleIndexCacheStore` 恢复资源包列表，不再依赖假数据资源包树
- Infrastructure 当前已接入 `AssetsTools.NET` 作为真实解析库，并保留启发式 Reader 作为失败回退
- 当前阶段的 Reader 已可解析资源包内序列化文件、真实资产名与 `m_Container` 路径，并把资产列表 / 基础详情摘要写入 `BundleIndexCacheStore`
- 解密后当前仍统一经 `IAssetBundleReader.CanRead(...)` 决定进入真实 Reader 还是在读取阶段拒绝，保持“Reader 不负责探测加密格式”的边界
- 资产缓存当前已额外保存 `AssetTypeDisplayName`，便于工作台在 `AssetType.Unknown` 场景下仍展示真实 Unity 类型名
- 资产缓存当前已额外保存 `SerializedFileName / PathId`，用于把预览请求稳定定位到真实序列化对象
- 资源包索引缓存当前已额外保存 `SerializedFileNames` 集合，用于在工作区运行时把 `archive:/CAB-*` 这类外部依赖尽量反查回具体资源包文件名
- 资源包索引缓存当前已纳入 `source fingerprint + decryptorId + decryptorVersion + parserVersion + toolVersion` 的命中复用判断
- 导入处理当前已从“整工作区清空后重建”收敛为“逐资源包复用、逐资源包失效重建、移除缺失缓存”
- 当资源包索引失效或导入来源移除时，当前实现会同步清理对应 `bundleCacheKey` 下的预览缓存描述与内容文件
- 工作台资源详情当前已正式接入 `PreviewCacheStore` 的按需生成、命中与失效清理链路
- 当前图片预览缓存已按资源独立落盘，不再共用单一 `payload.txt / payload.png` 覆写文件；Sprite 裁切结果会作为同一预览缓存项下的 `focus.png` 附加载荷保存
- 资产详情预览加载采用当前活动标签优先策略：切换到其他资产详情标签时，旧标签的未完成前台预览请求会被标记为失效并降级为后台预热；新活动标签按 `PreviewForeground` 重新调度，前台调用方取消可释放前台 profile 槽。已打开标签的后台预热按最近激活、最近打开优先，而不是单纯集合顺序。
- 当前 Infrastructure 已接入 `AssetsToolsBundleLoadedSessionCache`：同一标准化 artifact identity 的 `LoadBundleFile(..., unpackIfPacked: true)` 和 `LoadAssetsFileFromBundle(...)` 通过 single-flight 进入一次加载任务，成功后用 lease 方式短时复用，并以 30 秒滑动 TTL 驱逐；默认最多保留 6 个空闲 loaded session，检测到 GC 内存压力时会退避到 1 个空闲 session，活动 lease 和仍在底层同步加载的任务不会被硬中断；调用方取消等待 in-flight 加载时，只释放自身 lease 计数，不会把仍保留在缓存中的加载结果安排成完成后释放，避免后续快速切换预览命中已释放 session；同一 loaded session 的 AssetsTools 访问默认串行化，避免假设第三方对象可无锁并发。DEBUG 包会输出 `[AssetsToolsBundleLoadedSessionCache] Acquire miss / in-flight / hit` 与 `Load start / completed / failed`，用于验证快速打开多预览、批量提取和 Spine 整组提取是否共享同一 AB 加载会话；该 cache 当前由 `AssetsToolsAssetBundleReader`、`AssetsToolsAssetPreviewGenerator` 和经预览管线导出的提取共享；没有 artifact identity 的直接调用回退到物理路径 key。
- 当前已接入首版 `cache/bundles` 标准化资源包物理缓存：导入、预览、提取和 Spine 详情链路会通过 Application artifact resolver 复用同一 manifest；外部解密派生结果与 PassThrough 原始可读包都会晋升为 `normalized.bundle`，后续读取不再继续指向用户源文件。当前 `cache/previews` 预览缓存也已纳入当前工作区聚合 usage / clear。
- 仍需继续治理的预览内存风险：loaded-session 和标准化 artifact 已覆盖 Reader / Preview / Extraction 的路径复用，artifact 生成阶段也已按资源包键串行化，artifact store 已提供 usage / LRU prune、偏好驱动自动容量治理和 loaded-session 主动失效；预览缓存 store 已提供 usage / prune，并接入设置页显式清理、自动容量治理和工作台顶部缓存统计。预览 `OutOfMemoryException` 当前会进入专用诊断并触发同 bundle 后台预热短期退避。SaveRebuild 保存计划已开始共享 artifact resolver 只读输入图，并已接入目标输出路径 preflight；后续 Export / SaveRebuild 真实写包还必须继续共享 artifact resolver 和 loaded-session identity，不能新增平行读取路径；连续打开多个不同大 bundle 的缓存未命中预览时仍应继续关注整体内存峰值。
- 当前预览类型覆盖如下：
  - 图像预览：`Texture2D`、`Sprite`
  - 文本预览：`TextAsset`、`Shader`
  - 音频预览：`AudioClip`（当前已支持标准音频容器直通播放，并对 `FSB5` 补上独立外部转码能力）
  - 模型预览：`Mesh`
  - 半可视化摘要预览：`Material`、`AnimationClip`、`Font`、`GameObject`、`MonoBehaviour`、`AnimatorController`
    `Material` 当前会把 Shader PPtr 和 TexEnv 贴图行按资产详情依赖图解析到 `Shader / Texture2D / Sprite`，并通过现有预览查询管线加载前若干贴图缩略图和主纹理平面预览；主纹理卡只叠加 `_Color` 基础 tint 并显示“非 Shader 仿真”的限制说明。Presentation 还会基于常见 Unity 材质属性名展示主纹理、法线、自发光、Alpha 裁剪、金属 / 光滑度、溶解 / 扰动、透明 / 混合等语义候选提示；这些提示只用于阅读辅助，不代表真实 Shader 渲染。解析不到具体资产时保留内部 / 外部引用未匹配状态，不在 Presentation 侧读取 AB 或打开额外资产。`Mesh` 贴图叠加与 `Material` 贴图展示共享同一贴图预览 payload loader，因此继续复用现有预览缓存、命名和失效链路。`AnimationClip` 当前输出 schema v3 binding / 曲线摘要，并把自适应摘要预览区控制在主要信息范围内；完整时间、循环、binding 与事件明细进入通用信息卡。若 `m_AnimationClipSettings` 缺失，起止时间和循环字段回退读取 `m_MuscleClip`；packed generic binding 的 keyframe 数在未解码前显示为未知，不显示为 0。
    `GameObject` 当前输出 `payload.game-object.json`，自适应摘要预览区只展示对象名、激活 / Layer / Tag、组件摘要、Transform 摘要和层级摘要；对象基础字段、Transform 坐标 / 父子关系、组件 PPtr / 类型 / 脚本 / 启用状态以及常见组件的 `Mesh / Material / Sprite / Controller / Avatar / AudioClip / Script` 引用进入通用信息卡。组件和引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态。该预览只消费现有预览管线和 sidecar，不在 WinUI 侧读取 AB，也不做场景渲染。
    `MonoBehaviour` 当前输出 `payload.mono-behaviour.json`，自适应摘要预览区只展示对象名、脚本摘要、启用状态、字段数、字段分组数和引用数；对象基础字段、MonoScript 信息、字段分组、常见 Unity 序列化容器、前若干标量 / 数组字段和 PPtr 引用进入通用信息卡。若 MonoScript 位于未导入的外部 CAB 且类名不可读，Presentation 只回退显示“未解析脚本 + PathId”，不猜测业务脚本名。脚本和引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态。该预览只消费现有预览管线和 sidecar，不在 WinUI 侧读取 AB，也不做脚本业务语义推断、编辑或运行时模拟。
    `AnimatorController` 当前输出 `payload.animator-controller.json`，自适应摘要预览区只展示控制器名、Layer / Parameter、状态 / 转场和引用状态；Layer、Parameter、`m_Controller` 常量区容器摘要与 `m_AnimationClips` 等 PPtr 引用进入通用信息卡。真实样本中 `m_AnimatorLayers / m_AnimatorParameters` 可为空，预览以 `m_Controller` 常量区摘要为主。引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态。该预览只消费现有预览管线和 sidecar，不在 WinUI 侧读取 AB，也不做状态机画布或播放模拟。
  - 结构化摘要预览：`SpriteAtlas`
  - 聚合资源组预览：`Spine`
    Spine 面向 2D 游戏中 `.atlas + .skel/.skel.bytes/.json + Texture2D` 的跨资产组合，不作为 `AssetType` 下拉里的 Unity 类型，也不作为 `仅已修改 / 仅可预览` 同级布尔筛选。检测到 Spine 组合时，应在资产列表提供 `资源 / Spine 组` 视图切换；普通资源视图继续展示单资产，Spine 组视图展示聚合条目。首版预览覆盖资源组识别、skeleton / atlas / texture page 匹配状态、JSON skeleton 轻量结构摘要、atlas region overlay、缺失 page / 外部引用未解析诊断、相关资产跳转、实验性动画预览、真实 skin 切换、V38 / V42 官方 runtime adapter，以及 runtime playback session 实时交互主线；后续范围收敛为基础播放稳定性、动画 / skin 切换、进度 / 速度控制和固定 fps 导出，不主动追完整查看器、调试渲染或复杂 runtime 行为。
  - 通用摘要回退：其余可读取资源类型
- 工作台检查器当前会直接展示资源 `PathId`，并在图片预览成功后展示尺寸、裁切矩形、像素编码与源格式标识等专属信息；Sprite 预览会额外展示源贴图上的裁切框、pivot 点、9-slice border 辅助线，以及独立裁切结果卡片
- 预览缓存当前按 `source fingerprint + generatorVersion + audioTranscoderId + audioTranscoderVersion` 校验命中；对于曾因“无转码器”而回退成摘要的音频预览，在后续安装转码器后也会自动触发重建
- 预览查询当前已补上结构化预览诊断：会把预览缓存命中 / 失效原因，以及 `定位信息缺失 / 解密器不可用 / 解密失败 / 预览生成失败` 等摘要回退原因统一沉到应用层 DTO
- 当前真实 Reader 已补上第一轮依赖深读：会从序列化文件 `externals` 中提取资源包级外部依赖，并递归解析资源字段树中的 `PPtr` 引用，写入资产级依赖缓存
- 工作台检查器当前已收敛为 `详情 / 依赖 / 替换 / 变更` 四页签；其中“详情”页统一承载预览画面、基础信息、预览补充信息与预览诊断，“依赖”页签已扩展为 `资产依赖 / 反向引用 / 依赖闭包 / Bundle 外部依赖` 四块
- 工作台右侧当前已从“单实例检查器”扩展为“资源包标签页内的资产文档标签”结构；不同资产会独立打开为详情标签，并保留各自的检查器页签位置、预览状态与替换候选状态
- 工作区元数据当前会跟随每个资源包标签页一起保存其已打开资产文档标签与活动资产标签，切换资源包标签和重启应用后会优先恢复对应的资产详情上下文
- 当前资源包标签与资产详情标签都已支持拖拽重排，且重排后的集合顺序会随工作区浏览状态一起持久化和恢复
- 后续资源详情会演进为 [资源编辑窗口模式](asset-editor-window-mode.md)：资产文档标签或整个资产文档组可以从主窗口独立为资源编辑窗口，也可以在窗口之间组合或放回主窗口。该模式只迁移页面级文档宿主、预览浏览状态、内层分段位置、替换候选选择和后续编辑草稿标识；已导入资源包、资产索引、依赖图、预览缓存、替换队列、变更队列和保存计划仍归同一个活动工作区运行时会话所有，不随窗口复制。
- 独立资源编辑窗口的恢复优先级低于主窗口浏览状态恢复。当前阶段先保证主窗口浏览状态恢复后可继续保留独立窗口快照，并通过弹窗确认或工具栏手动入口触发窗口重建；最大化 / 最小化、显示器、独立标签组更细粒度冲突处理和未应用编辑草稿恢复仍在窗口模型稳定后再逐步落地。
- 资产依赖当前会补充展示依赖目标的 `PathId`、Unity 类型名与闭包层级，便于后续把“查看依赖”与“按对象替换”稳定关联到真实序列化对象
- 依赖解析当前已兼容 Unity 有符号 `PathId` 语义，不再把负数 `PathId` 误判为无效引用；这类修正会通过 Reader 解析版本提升自动触发旧缓存重建
- 运行时工作区会话当前已在基础设施缓存恢复后构建资产依赖图，并在运行时派生 `反向引用` 与 `依赖闭包`，避免把图遍历逻辑下沉到 Presentation
- Bundle 外部依赖当前会对 `archive:/CAB-xxx/CAB-xxx` 这类 Unity 外部序列化文件引用做第一轮友好解析，优先显示 `CAB-*` 标识与依赖类型；这里的 `CAB-*` 表示 Unity 内部 archive / serialized file 标识，不等于用户直接导入的 `char_xxx.ab` 这类资源包文件名
- 当目标 serialized file 所属资源包也已经导入到当前工作区时，Bundle 外部依赖当前会进一步把标题优先还原为具体 `xxx.ab` 文件名，并在副标题继续保留 `CAB-*` 标识，兼顾可读性与底层定位稳定性
- 资产友好路径当前优先采用 `m_Container` 路径；当资源缺少容器路径或名称冲突时，再回退到 `SerializedFileName / 资产类型 / PathId` 组合摘要，确保依赖与预览入口都能稳定定位同一个对象
- 资产级依赖解析当前会保留外部 `PPtr` 的 serialized file 名称、外部引用路径与 `PathId` 占位；当对应外部 CAB 也已导入同一工作区并能通过稳定资产 ID 或 `SerializedFileName + PathId` 匹配时，运行时依赖图会把占位解析回具体资产，用于 Material 依赖页、反向引用、依赖闭包与 Mesh 贴图关联等跨 Bundle 场景
- 独立批量操作窗口与“设置与诊断”页已统一响应活动工作区切换
- “设置与诊断”页已接入活动工作区摘要和工作区目录摘要承载
- 解密器管理区将迁移到侧边栏一级“插件管理”页，后续该页统一承载导入解密 CLI 插件和导出资源处理 CLI 插件
- 工作台顶部状态标签已把“当前选中”收敛为“当前解密器”
- 工作区目录重载时已保留仍然存在的会话摘要，避免刷新最近工作区目录时把活动会话退化成纯元数据项
- 最近工作区目录已支持固定 / 取消固定，并统一按 `Pinned > LastActivatedAtUtc` 排序
- 最近工作区目录已支持不可用状态标识，不可用项保留但禁用打开
- 应用启动时已支持通过设置项控制是否自动恢复上次活动且仍可用的工作区
- 删除工作区记录时，已同步清理该工作区对应的元数据、解析缓存、预览缓存与结构化变更暂存
- 删除当前活动工作区记录时，当前进程内的活动上下文、窗口注册和工作区占用也会同步失效，避免同目录再次新建时复用删除前的旧页面状态

当前暂不实现：

- 顶部全局工作区切换器
- 资源编辑窗口的完整布局恢复、跨窗口编辑冲突解决和真实写包回滚语义；这些能力等待独立窗口、编辑草稿和保存链路契约稳定后再落地

## 9. 后续实施建议

当前工作区主链路已经完成阶段性收口。后续工作区相关开发默认服务于提取、替换和保存输出主线，不再优先扩展顶层工作区切换器或新的大布局。

建议按下面顺序继续推进：

1. 保持现有活动工作区、导入缓存、预览缓存和浏览状态恢复边界，不把新的提取 / 替换 / 保存语义写回 Presentation。
2. 真实提取输出已接入工作区数据根下的默认导出路径、单资源保存文件选择、输出结果记录和重启恢复；Spine 资源组已接入整组提取入口，按 atlas page 匹配 skeleton / atlas / 主贴图 / 独立 alpha 贴图，并继续复用单资源真实写出与提取操作记录；后续补批量用户可选最近输出目录、命名冲突策略 UI、失败明细面板和撤销提取标记。
3. 为替换和保存继续复用现有 `staging/changes` 变更暂存；替换 / 编辑阶段只追加可恢复操作记录并更新待保存状态，不直接改写 AB，真实 AB 替换只在统一导出 / 保存重建阶段消费操作记录后执行，也不把真实写包结果直接塞进工作区元数据。当前 `Texture2D` 已先落地“替换记录 -> 有效预览 -> 撤销 / 重做”：文件来源和工作区来源都会投影为目标资产的有效图片预览，撤销后回到上一层有效内容或原始预览，重做后重新应用最近一次撤销的替换；后续统一导出 / 保存阶段应通过替换读模型读取当前有效替换列表。修改阶段的资源详情和独立资源编辑窗口应按当前有效内容刷新富预览，例如图片 / Sprite、Mesh 贴图叠加、Material 引用缩略图和 Spine 组播放都显示替换后的结果；目标资产的原始身份、Bundle、PathId、Unity 类型、导入索引和原始依赖仍作为原始信息保留，不被替换来源覆盖。
4. 在保存输出计划稳定后，再评估是否需要可分享工作区文件。
5. 工作区窗口模型继续保持“跨进程隔离优先 + formal/pending 双阶段注册”；后续若加强稳定性，优先补跨进程窗口激活与重定向回归，不重新回退注册时机。

已完成的预览与窗口支线状态继续保留在 [project-build-roadmap.md](project-build-roadmap.md)、[ui-final-spec.md](ui-final-spec.md)、[asset-editor-window-mode.md](asset-editor-window-mode.md) 和 Spine 专题文档中。本文不再重复维护完整预览阶段日志。

### 9.1 预览扩展记录与样本门控建议

真实内容预览已经完成当前高价值主线。下面内容保留为已落地能力和样本门控修补方向，避免和下一轮提取 / 替换 / 保存主线混淆：

1. `AudioClip`
   - 第一阶段已落地“容器识别 + 可播放载荷透传 + 摘要回退”主链路。
   - 当前可直接播放 `WAV / OGG / MP3 / FLAC / M4A(MP4) / AAC` 等标准音频容器。
   - 第二阶段已落地独立音频转码层：当前会优先通过外部 `vgmstream-cli` 把 `FSB5` 转为标准 `WAV` 后播放。
   - 工作台默认展示已调整为“紧凑音频工作卡”：首屏只展示资源名、原始大小 / 预览大小摘要与 `容器 / 时长 / 声道 / 采样率` 四个关键标签，中部承载自定义紧凑试听控制条；其余载荷与转码细节继续落在基础信息卡与预览信息卡中，避免重复堆叠。
   - 预览面板顶部统一提供“打开预览文件所在位置”动作，该动作面向整个预览容器，而不是只绑定在音频类型上；后续图片、文本和更复杂富预览都复用同一入口。
   - 若 `vgmstream-cli` 不可用或转码失败，仍会稳定回退为摘要信息，并展示容器类型、载荷来源、载荷大小、采样率、声道、时长和压缩格式等信息。
2. `Mesh`
   - 当前已落地线框视口与 UV0 视图：预览生成器输出轻量几何、UV0、顶点通道摘要 JSON，Application 缓存并反序列化为 DTO，Presentation 通过 Win2D 渲染旋转 / 缩放 / 重置交互和 UV 视图切换。
   - 贴图关联主路径由运行时工作区缓存图派生：优先从 `Mesh` 的反向引用进入 `MeshFilter / Renderer / ParticleSystemRenderer / GameObject` 等宿主，再沿宿主到 `Material`、`Texture2D / Sprite` 的依赖链查找可关联贴图；只有主路径没有结果时，才把 `Mesh -> Texture2D / Sprite` 直接依赖作为兜底。Presentation 只消费 `AssetDetailsDto.TextureAssociations` 与现有预览查询管线，不在 WinUI 侧追加资源包解析。
   - 当前 Mesh sidecar 已升级为 schema v3，并额外保存预览范围内的三角面索引；线框视图在贴图按钮开启、三角 / UV / 可预览关联贴图都可用时，会通过 Win2D 逐三角裁剪并按 UV 仿射映射贴图，再叠加线框结构。贴图化线框预览提供独立 Mesh/UV 网格显隐开关，可在线框视图隐藏边线、顶点与轴线，也可在 UV 视图隐藏 UV 单位格、UV 边线与顶点，以观察最终贴图映射外观；贴图缺失、贴图关闭或贴图位图尚未加载完成时仍保留结构层，避免空白预览。UV 视图仍通过现有 `GetAssetPreviewQuery` 管线加载首个可预览的 `Texture2D / Sprite` 关联图片并叠加到 `[0,1]` UV 空间，用于检查 UV 展开。
   - 当前贴图化 Mesh 预览只表达几何和 UV 对贴图的基础对应关系，不模拟 Shader、Material tiling/offset、多材质 submesh 分配、骨骼蒙皮或光照效果；缺少三角 / UV / 可预览贴图时继续稳定退回纯线框。
   - 当前只把可读取的非压缩 Mesh 几何提升为富预览；无法读取顶点 / 索引 / 布局时继续使用结构化摘要。
3. `SpriteAtlas` 样本驱动回补
4. `Spine` 资源组预览
   - Spine 是跨资产聚合，不直接扩展 `AssetType` 下拉项。首版先在资产列表增加 `资源 / Spine 组` 视图切换，并由 Application / Infrastructure 输出资源组 DTO。
   - 当前 Application 已落地 `LoadSpineAssetGroupsQuery` 与 `LoadSpineAssetGroupDetailsQuery` 第一版读模型：基于现有资产索引元数据识别当前 Bundle 视图下的 `.atlas / .skel / .skel.bytes / .json` TextAsset 锚点，并在当前工作区内按归一化组名匹配 `Texture2D` page 候选；详情查询继续通过 `GetAssetPreviewQuery` 取得 atlas TextAsset 预览文本，并使用完整文本载荷在 Application 层解析 atlas page / region 统计，避免大 atlas 被显示层截断影响。`Full / InteractiveDiagnostics` 模式继续通过预览管线校验匹配 Texture2D 的图片预览可用性与尺寸，并读取 `.json` skeleton 的 bone / slot / skin / animation / event / constraint 轻量计数；`InteractivePreview` 模式用于主窗口与独立编辑窗口初始播放，只加载 atlas / skeleton / runtime 播放所需内容，跳过 skeleton 摘要、Texture2D 尺寸诊断和静态 atlas overlay 贴图预览。`.json` skeleton 在默认 skin region attachment、基础 timeline、draw order timeline 与 slot color/tint 可解析时输出实验性 `SpineAnimationPreviewDto`；`.skel / .skel.bytes` 二进制 skeleton 当前会作为 `AssetPreviewKind.Binary` 载荷优先交给已注册 runtime adapter，未命中时再保留结构化不支持诊断；结果包含稳定 `GroupId`、skeleton / atlas / texture page 组成资产、skeleton 摘要、atlas page / region 摘要、可选动画预览读模型、runtime-neutral sampled frames / draw commands，以及缺失 atlas / texture page、atlas 预览不可用、atlas page 未匹配 Texture2D、Texture2D 预览不可用、page 尺寸与图片尺寸不一致等诊断。
   - 当前 Presentation 已接入中间资产窗格的 `资源 / Spine 组` 视图切换。普通资源视图继续使用真实 Unity 资产列表、类型筛选和本地显示筛选；Spine 组视图通过 `LoadSpineAssetGroupsQuery` 加载组摘要，只显示组名、组成数量、Texture page 数和诊断状态，并把视图模式保存到资源包标签页浏览状态。
   - Spine 组列表选择当前会在主窗口右侧复用现有资源详情窗格打开组文档，文档身份使用 `BundleId + GroupId`，不会借用某个 TextAsset 的 `AssetId`。当前范围覆盖组摘要、skeleton / atlas / texture page 组成资产跳转、JSON skeleton 结构摘要、诊断摘要、atlas page / region 数量回填、atlas page 选择器和当前 page 的 region overlay；主窗口组文档会通过资源包标签页下的 `openSpineGroupTabs[]` 做轻量恢复。
   - Spine 组当前作为统一资源文档标签参与普通独立资源编辑窗口；它与普通资产文档共享混合标签、拖拽合并、放回主窗口、冲突检测和 `openAssetEditorWindows[]` 重启恢复链路，恢复时只重建 `BundleId + GroupId` 轻量组身份。
   - 识别主线优先覆盖同 Bundle 或当前工作区内可匹配的 `.atlas`、`.skel/.skel.bytes/.json` 和 `Texture2D` page；搜索和跳转基于组名、skeleton、atlas、texture page 与已解析资产目标。
   - 预览主线当前已经把 JSON skeleton 的多 skin / region attachment / rotate / translate / scale / slot attachment / draw order / slot color timeline 提升为实验播放，并接入 `.skel / .skel.bytes` 二进制载荷到 V38 / V42 官方 runtime adapter；V38 / V42 runtime adapter 已支持 mesh draw command、mesh deform timeline 与 IK constraint timeline 采样，V38 runtime adapter 已支持 transform constraint timeline 采样，runtime draw order 与 slot tint 会合成到中立 DTO。Presentation 渲染层保留已完成的三角网格提交与 D3D 批量渲染能力，确保基础播放不块状近似；但后续不主动追 SpineViewer 的完整查看器能力，更多官方 runtime 版本、linked mesh / 更复杂 FFD / path / physics constraint 和更完整 mesh 行为只在真实样本阻塞基础播放、动画切换或导出时做最小兼容，否则保留结构化诊断。
   - 大量三角 Spine 的长期渲染方向仍固定为 `D3D11 + SwapChainPanel + vertex batch renderer`：Application 继续只输出 runtime-neutral draw command DTO，Presentation 内部把 DTO 投影成按 texture / blend / tint 分组的 GPU 顶点批次，`SwapChainPanel` 只作为 WinUI 承载面。当前 Presentation 已完成渲染后端抽象、Win2D 回退后端适配、DTO 到顶点批次投影、默认 D3D11 顶点批次提交、Texture2D shader resource 上传、动态 vertex / index buffer、device lost 一次重建、暂停 / 未变帧减少刷新、world-space vertex batch 缓存、池化 clip-space 投影和 D3D 分段诊断；非 recoverable D3D render / resize / present 失败时仍自动切回 Win2D 兼容路径。D3D 类型、WinUI 控件和 GPU resource 不得进入 Application 契约或工作区持久化状态。
   - Spine 实时播放已改为 runtime playback session 主线，而不是继续提高整段 sampled frame 上限或全量 D3D batch prewarm。Application 定义窄播放端口，Infrastructure 在 V38 / V42 等 adapter 内持有官方 Spine runtime state，按 animation / skin / setup pose / 绝对 seek 时间生成当前帧 DTO；Presentation 由显示刷新驱动播放，并只渲染当前帧。固定 fps 采样保留给导出、序列帧和 runtime session 不可用时的兼容回退。详细边界见 [`spine-runtime-playback-session.md`](spine-runtime-playback-session.md)。
5. `Font` 样本驱动回补，以及 `AnimationClip / AnimatorController` 更细的 binding / 状态机分类摘要增强

其中 `MonoBehaviour` 已进入轻量信息浏览预览但不做脚本语义推断、编辑或运行时模拟；`AnimatorController` 已进入轻量信息浏览预览，当前从 `m_Controller` 常量区和 `m_AnimationClips` 提取状态机 / 状态 / 转场 / 引用摘要，不进入近期重型状态机画布或播放主线；`GameObject` 已进入轻量信息浏览预览，但仍不进入重型场景渲染主线；Spine 当前按资源组阅读、atlas 定位、runtime-neutral DTO、V38/V42 runtime adapter 和实验播放层推进，不让 Presentation 直接持有 runtime 或读取 AB。

### 9.2 预览解包内存风险后续治理

已确认问题：

- 2026-05-03 在连续打开多个资源详情预览时，预览诊断捕获到 `System.OutOfMemoryException`。
- 堆栈落在 `AssetsToolsAssetPreviewGenerator.GenerateAsync` 调用 `AssetsManager.LoadBundleFile(request.ResolvedBundlePath, unpackIfPacked: true)` 的阶段，具体表现为 `SevenZip.Compression.LZMA` 解包过程中 `MemoryStream` 扩容失败。
- 该异常发生在 bundle 解包阶段，通常早于具体 `Texture2D / Sprite / AudioClip / Mesh / TextAsset` 内容解析，因此不能简单归因于单个资源类型的预览渲染错误。

当前风险判断：

- 当前预览执行队列限制的是查询任务数量，不等于限制底层同步解包任务的真实内存峰值。
- 预览请求被取消或超时后，如果底层 `LoadBundleFile(..., unpackIfPacked: true)` 已经进入同步 LZMA 解包，取消令牌无法立刻中断该解包；若执行槽提前释放，新请求可能继续进入解包，造成实际并发超过预期。
- 后台预热会对已打开资产详情标签发起预览请求；Mesh 贴图叠加预览还会通过同一预览管线请求关联 `Texture2D / Sprite` 载荷。这些路径在缓存未命中且源 bundle 较大时会放大解包内存压力。
- `x64` 运行能提高可用地址空间，但只能缓解症状，不能替代预览解包生命周期与缓存策略治理。

当前收口状态：

1. 后台预热策略已完成一轮收紧：对已发生内存不足的 bundle，后台预热会短期跳过，前台活动资产预览仍优先执行；缓存未命中 / 大 bundle 的更细粒度预测跳过仍按真实样本再扩展。
2. 补齐工作区派生缓存治理：当前 `cache/bundles` 已落地并被 Reader / Preview / Extraction / Spine 详情复用，artifact 生成阶段已按资源包键 single-flight，manifest 已回写 `lastAccessedAtUtc`，Infrastructure store 已支持 usage 统计、按大小 / 数量上限的 LRU prune 和偏好驱动自动清理；`cache/previews` 已支持 usage / prune，并与标准化 artifact 一起进入设置页显式清理、自动容量治理和工作台顶部缓存统计。后续应增加更完整清理诊断，并评估是否需要把两类缓存从“各自上限”收敛成严格的工作区总容量预算。
3. 当前已覆盖 Reader / Preview / Extraction、并基于标准化 artifact identity 复用的 AssetsTools loaded-session 缓存已补齐默认容量上限和内存压力退避；SaveRebuild 保存计划已通过 Application artifact resolver 建立只读输入图，并已把输出路径 preflight 纳入计划，后续真实导出 / 写包不允许绕开标准化 artifact 或跳过最终冲突确认。
4. `OutOfMemoryException` 已有专用诊断分类和短期退避：Infrastructure 保留异常 detail，Application 暴露 `Bundle.Preview.OutOfMemory / AssetPreviewFallbackReasonKind.OutOfMemory`，Presentation 暂停同 bundle 后台预热并在 DEBUG 输出退避诊断，避免连续失败。

## 10. 执行结论

后续所有“多工作区”能力默认按以下原则实现：

- 运行时会话和持久化元数据必须分离
- 工作区目录索引默认存放在应用数据目录
- 同一工作区的多窗口承载默认通过“多进程 + 跨进程激活”实现，而不是在同一进程内共享一个全局 `CurrentWorkspace`
- 工作区实例占用默认采用“formal 正式注册 + pending 预登记 + 成功晋升 + 失败回滚”语义，避免加载中的目标工作区与已稳定存在的当前工作区互相覆盖
- 单工作区元数据目录由用户显式选择
- 相同来源路径默认合并为同一工作区条目
- 页面只依赖活动工作区，不直接处理元数据文件
- 资源包标签页属于页面级状态，不直接侵入 Application 层
- 当前已通过工作区元数据文件恢复资源包标签页与活动筛选状态
- 当前已通过工作区元数据文件恢复资源包标签页下的资产详情标签、活动资产标签与检查器页签位置
- 资产预览查询通过 Presentation 预览执行队列后台运行，预览 worker 总数由应用偏好控制：自动模式为 `clamp(Environment.ProcessorCount / 2, 1, 4)`，也可在“设置与诊断”页固定为 `1-4`。执行队列保留 1 个前台槽优先服务当前活动资产详情标签，其余槽用于已打开资产详情标签的后台预热。若并发度为 1，则后台预热不参与竞争；快速切换到新标签时，新活动标签按前台优先级重开，旧活动标签取消前台请求并在仍打开时降级为后台预热，后台预热按最近激活 / 最近打开优先，已完成的预览则直接复用。若某个 bundle 的预览生成返回 `OutOfMemory`，同 bundle 后台预热会短期退避，前台用户请求仍可执行且成功后清除退避。Spine runtime 当前帧使用独立 `SpineRuntimeFrame` profile，不与普通详情预览争用槽位。关闭资产标签、关闭资源包标签、重建标签集合或工作区失效时，必须取消对应标签范围内的预览请求。导入、预览、提取、导出和后续保存 / 重建的 Presentation 层后台任务调度统一边界见 [background-task-scheduling.md](background-task-scheduling.md)。
- 工作区默认解密器属于工作区元数据，由工作区向导设置，并在工作台状态标签中呈现
- 导入入口先收口到 Presentation 共享协调服务，再通过 Application 层统一登记工作区导入来源，并进一步接到解密、读取和缓存写入链路
- 真实导入前若需要解密，应先通过独立解密器扩展点把来源转换为可读取文件，再进入 `IAssetBundleReader`
- `AudioClip` 等富预览若需要额外转码，应先通过独立音频转码层把载荷转换为标准可播放格式，再交给 Presentation 渲染
- `Mesh` 等模型富预览的几何、三角面索引、UV 与通道读取必须停留在 Infrastructure 预览生成器中；Mesh 贴图关联由运行时工作区依赖图派生为 `TextureAssociations`，Presentation 只能消费结构化 DTO、派生关联关系与缓存载荷路径，不得直接解析资源包
- `Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController` 这类半可视化摘要或轻量信息浏览也必须通过 Infrastructure 生成 JSON sidecar、Application 水化 DTO、Presentation 渲染 DTO 的同一管线完成，不得在 WinUI 中直接读取字段树或资源包；Material 的属性语义提示只能从已水化 DTO 派生，并应保持“候选 / 检测到”措辞，避免暗示已经完成 Shader 仿真；GameObject / MonoBehaviour / AnimatorController 的引用跳转只能消费详情依赖图和预览 DTO，不得在 Presentation 侧重读资源包
- 自动识别默认按“导入源识别 -> 解密器识别 -> Reader 路径识别 -> 缓存命中识别”四层推进，而不是把所有判断都塞进 Reader 或 UI
- 拖拽导入的页面桥接应优先挂在具体工作区区域，而不是整页根节点
- AB 真实解析结果应优先写入独立缓存层，而不是写进工作区元数据文件
- 解密后的临时文件默认属于 `staging` 层，不应混入工作区元数据
- 可复用的标准化资源包当前已属于 `cache/bundles` 层，并作为 Reader、预览、提取和 Spine 解析的共同物理输入；它可以被清理和重建，不能成为操作记录或撤销语义的事实来源
- 进程内 loaded-session 缓存只负责短时间复用已加载 AB 上下文；当前 Infrastructure 已落地 `AssetsToolsBundleLoadedSessionCache`，Reader、预览生成器和经预览管线导出的提取会按标准化 artifact identity 共享 single-flight、lease、串行访问保护、30 秒滑动 TTL、默认 6 个空闲会话容量上限和内存压力下 1 个空闲会话退避，没有 artifact identity 的直接调用回退到物理路径 key；DEBUG 包通过 `Acquire miss / in-flight / hit` 和 `Load start / completed / failed` 标记命中、等待同一加载任务和真实物理加载次数；artifact 删除、prune、资源包移除和工作区记录删除会通过 Application 的 `IBundleArtifactSessionInvalidator` 主动驱逐对应 loaded session，新请求不会继续命中旧会话，已有 lease 会自然释放后卸载；SaveRebuild 骨架当前只读取已有 artifact identity 并预检输出路径 blockers，不提前 materialize artifact；该缓存不得暴露 AssetsTools.NET 对象到 Application 或 Presentation
- 同一工作区的导入刷新默认只允许一个运行中作业；当前多线程导入也只允许在 `Application` 导入链路内部按 bundle 粒度做受控并行，并维持“串行计划 -> 有界并行 worker -> 串行提交”的三段模型
- 通过工作区向导新建或打开工作区时，仍可显式选择当前窗口或新窗口
- 工作区向导应优先用“工作区数据目录”识别已有工作区，并在新建时显式处理目录冲突
- 从控制台再次打开最近工作区时，打开策略由设置项统一控制，可选默认当前、默认新建或每次询问
- 启动自动恢复只在设置开启时，根据最后活动且可用的工作区条目生成导航请求，不直接破坏运行时会话边界
