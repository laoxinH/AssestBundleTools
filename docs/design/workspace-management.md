# AssestBundleTools 工作区管理设计

## 1. 文档目的

本文档用于约束“多工作区切换”能力的设计边界，目标是让工作区机制逐步演进到类似 VS Code、JetBrains IDE 这类工具的模型：

- 应用可以登记多个工作区
- 任意时刻只有一个活动工作区参与主任务
- 工作区运行时状态和持久化元数据分离
- 工作区内的资源包浏览通过标签页承载，而不是子窗口

当前文档与以下规范配套使用：

- [code-architecture-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/code-architecture-guidelines.md)
- [ui-final-spec.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/ui-final-spec.md)
- [project-build-roadmap.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/project-build-roadmap.md)

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
    bundle-index/
      <workspace-key>/
        <bundle-cache-key>.json
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
- `cache/bundle-index/...`
  负责保存每个 AB 文件的解析索引缓存，用于加速资源包列表、资产列表和基础恢复。
- `cache/previews/...`
  负责保存缩略图、文本片段等预览派生缓存。
- `staging/decrypt/...`
  负责保存解密阶段派生文件、外部进程结果文件与短期工作目录；
  路径必须带上 `workspaceKey + decryptorId + bundleCacheKey` 或等价唯一段，避免后续并行导入时仅凭同名源文件发生覆写冲突。
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
- Mesh 预览的轻量几何 sidecar 当前包含顶点、边、submesh 摘要、UV0、通道摘要以及用于贴图化预览的三角面索引；Presentation 只能消费该 DTO 与关联贴图缓存载荷，不得为了补贴图效果直接解析 AB。
- 当预览能力从 `Image / Text / Summary` 扩展到 `Audio / Model / Atlas` 时，应把这类结构化数据写入 `relativeMetadataPath` 指向的 sidecar metadata 文件。
- `previewKind` 必须保持通用枚举语义，避免为某一个 Unity 资源类型直接设计专用缓存目录结构。
- Presentation 层只消费 `AssetPreviewDto` 与缓存产物，不直接感知资源包或解密细节。

### 5.5 变更暂存

建议字段：

- `workspaceKey`
- `updatedAtUtc`
- `changes[]`

每个 `changes[]` 条目当前可先保持通用结构：

- `changeKey`
- `changeKind`
- `bundleId`
- `assetId`
- `summary`
- `recordedAtUtc`
- `sourceDescriptor`
- `outputPath`

后续在真实保存链路稳定后，再按提取、工作区替换、文件替换拆成更细的专用模型。

### 5.6 缓存失效规则

解析缓存和预览缓存必须是“可重建的”。

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
- 对失效或已移除的资源包，只清理对应的资源包索引缓存与关联预览缓存

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
- 资产详情预览加载采用当前活动标签优先策略：切换到其他资产详情标签时，旧标签的未完成预览请求会被标记为失效并清空旧 loading 状态；尚未开始的旧请求会跳过，已经开始但不再相关的旧请求不会回写 UI，用户切回旧标签时再按需懒加载预览
- 当前预览类型覆盖如下：
  - 图像预览：`Texture2D`、`Sprite`
  - 文本预览：`TextAsset`、`Shader`
  - 音频预览：`AudioClip`（当前已支持标准音频容器直通播放，并对 `FSB5` 补上独立外部转码能力）
  - 结构化摘要预览：`AudioClip`、`Mesh`、`Material`、`GameObject`、`MonoBehaviour`、`AnimationClip`、`AnimatorController`、`Font`、`SpriteAtlas`
  - 通用摘要回退：其余可读取资源类型
- 工作台检查器当前会直接展示资源 `PathId`，并在图片预览成功后展示尺寸、裁切矩形、像素编码与源格式标识等专属信息；Sprite 预览会额外展示源贴图上的裁切框、pivot 点、9-slice border 辅助线，以及独立裁切结果卡片
- 预览缓存当前按 `source fingerprint + generatorVersion + audioTranscoderId + audioTranscoderVersion` 校验命中；对于曾因“无转码器”而回退成摘要的音频预览，在后续安装转码器后也会自动触发重建
- 预览查询当前已补上结构化预览诊断：会把预览缓存命中 / 失效原因，以及 `定位信息缺失 / 解密器不可用 / 解密失败 / 预览生成失败` 等摘要回退原因统一沉到应用层 DTO
- 当前真实 Reader 已补上第一轮依赖深读：会从序列化文件 `externals` 中提取资源包级外部依赖，并递归解析资源字段树中的 `PPtr` 引用，写入资产级依赖缓存
- 工作台检查器当前已收敛为 `详情 / 依赖 / 替换 / 变更` 四页签；其中“详情”页统一承载预览画面、基础信息、预览补充信息与预览诊断，“依赖”页签已扩展为 `资产依赖 / 反向引用 / 依赖闭包 / Bundle 外部依赖` 四块
- 工作台右侧当前已从“单实例检查器”扩展为“资源包标签页内的资产文档标签”结构；不同资产会独立打开为详情标签，并保留各自的检查器页签位置、预览状态与替换候选状态
- 工作区元数据当前会跟随每个资源包标签页一起保存其已打开资产文档标签与活动资产标签，切换资源包标签和重启应用后会优先恢复对应的资产详情上下文
- 当前资源包标签与资产详情标签都已支持拖拽重排，且重排后的集合顺序会随工作区浏览状态一起持久化和恢复
- 资产依赖当前会补充展示依赖目标的 `PathId`、Unity 类型名与闭包层级，便于后续把“查看依赖”与“按对象替换”稳定关联到真实序列化对象
- 依赖解析当前已兼容 Unity 有符号 `PathId` 语义，不再把负数 `PathId` 误判为无效引用；这类修正会通过 Reader 解析版本提升自动触发旧缓存重建
- 运行时工作区会话当前已在基础设施缓存恢复后构建资产依赖图，并在运行时派生 `反向引用` 与 `依赖闭包`，避免把图遍历逻辑下沉到 Presentation
- Bundle 外部依赖当前会对 `archive:/CAB-xxx/CAB-xxx` 这类 Unity 外部序列化文件引用做第一轮友好解析，优先显示 `CAB-*` 标识与依赖类型；这里的 `CAB-*` 表示 Unity 内部 archive / serialized file 标识，不等于用户直接导入的 `char_xxx.ab` 这类资源包文件名
- 当目标 serialized file 所属资源包也已经导入到当前工作区时，Bundle 外部依赖当前会进一步把标题优先还原为具体 `xxx.ab` 文件名，并在副标题继续保留 `CAB-*` 标识，兼顾可读性与底层定位稳定性
- 资产友好路径当前优先采用 `m_Container` 路径；当资源缺少容器路径或名称冲突时，再回退到 `SerializedFileName / 资产类型 / PathId` 组合摘要，确保依赖与预览入口都能稳定定位同一个对象
- 批处理页与“设置与诊断”页已统一响应活动工作区切换
- “设置与诊断”页已接入活动工作区摘要和工作区目录摘要承载
- “设置与诊断”页已接入解密器管理区，支持新增、编辑、删除、刷新用户管理定义
- 工作台顶部状态标签已把“当前选中”收敛为“当前解密器”
- 工作区目录重载时已保留仍然存在的会话摘要，避免刷新最近工作区目录时把活动会话退化成纯元数据项
- 最近工作区目录已支持固定 / 取消固定，并统一按 `Pinned > LastActivatedAtUtc` 排序
- 最近工作区目录已支持不可用状态标识，不可用项保留但禁用打开
- 应用启动时已支持通过设置项控制是否自动恢复上次活动且仍可用的工作区
- 删除工作区记录时，已同步清理该工作区对应的元数据、解析缓存、预览缓存与结构化变更暂存
- 删除当前活动工作区记录时，当前进程内的活动上下文、窗口注册和工作区占用也会同步失效，避免同目录再次新建时复用删除前的旧页面状态

当前暂不实现：

- 顶部全局工作区切换器

## 9. 后续实施建议

建议按下面顺序继续推进：

1. 继续补活动工作区在更多依赖页面中的摘要与入口承载，并在后续需要时补跨 Bundle 级依赖跳转
2. 再评估是否需要顶层工作区切换器
3. M9 当前已完成真实资产级读取、索引缓存复用、工作区级解密器自动识别、失败诊断、依赖深读与缓存诊断主链路收口
4. 富预览扩展当前已完成 `AudioClip` 与 `Mesh` 第一版，下一步按样本频率继续评估 `SpriteAtlas -> Material / Font / AnimationClip`
5. 最后再评估是否需要引入可分享工作区文件
6. 工作区窗口模型当前已按“跨进程隔离优先 + formal/pending 双阶段注册”落地，并已补上 pending 注册 / 页面级失败回滚的首轮 `Presentation` 回归；后续若继续加强稳定性，更适合继续补跨进程窗口激活与重定向链路，而不是重新回退注册时机

### 9.1 预览扩展建议

真实内容预览的后续扩展建议单独按下面顺序推进，避免和当前已完成范围混淆：

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
3. `SpriteAtlas`
4. `Material / Font / AnimationClip` 的半可视化预览

其中 `GameObject / MonoBehaviour / AnimatorController` 当前继续保留结构化摘要，不进入近期重型预览主线。

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
- 资产预览查询通过 Presentation 预览执行队列后台运行，默认并发度为 `clamp(Environment.ProcessorCount / 2, 1, 4)`；执行队列保留 1 个前台槽优先服务当前活动资产详情标签，其余槽用于已打开资产详情标签的后台预热。若并发度为 1，则后台预热不参与竞争；切换到尚未完成的标签时，应取消该标签旧的后台请求并以前台优先级重开，已完成的预览则直接复用。关闭资产标签、关闭资源包标签、重建标签集合或工作区失效时，必须取消对应标签范围内的预览请求。
- 工作区默认解密器属于工作区元数据，由工作区向导设置，并在工作台状态标签中呈现
- 导入入口先收口到 Presentation 共享协调服务，再通过 Application 层统一登记工作区导入来源，并进一步接到解密、读取和缓存写入链路
- 真实导入前若需要解密，应先通过独立解密器扩展点把来源转换为可读取文件，再进入 `IAssetBundleReader`
- `AudioClip` 等富预览若需要额外转码，应先通过独立音频转码层把载荷转换为标准可播放格式，再交给 Presentation 渲染
- `Mesh` 等模型富预览的几何、三角面索引、UV 与通道读取必须停留在 Infrastructure 预览生成器中；Mesh 贴图关联由运行时工作区依赖图派生为 `TextureAssociations`，Presentation 只能消费结构化 DTO、派生关联关系与缓存载荷路径，不得直接解析资源包
- 自动识别默认按“导入源识别 -> 解密器识别 -> Reader 路径识别 -> 缓存命中识别”四层推进，而不是把所有判断都塞进 Reader 或 UI
- 拖拽导入的页面桥接应优先挂在具体工作区区域，而不是整页根节点
- AB 真实解析结果应优先写入独立缓存层，而不是写进工作区元数据文件
- 解密后的临时文件默认属于 `staging` 层，不应混入工作区元数据
- 同一工作区的导入刷新默认只允许一个运行中作业；当前多线程导入也只允许在 `Application` 导入链路内部按 bundle 粒度做受控并行，并维持“串行计划 -> 有界并行 worker -> 串行提交”的三段模型
- 通过工作区向导新建或打开工作区时，仍可显式选择当前窗口或新窗口
- 工作区向导应优先用“工作区数据目录”识别已有工作区，并在新建时显式处理目录冲突
- 从控制台再次打开最近工作区时，打开策略由设置项统一控制，可选默认当前、默认新建或每次询问
- 启动自动恢复只在设置开启时，根据最后活动且可用的工作区条目生成导航请求，不直接破坏运行时会话边界
