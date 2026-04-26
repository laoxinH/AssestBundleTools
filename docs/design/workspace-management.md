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

Application 层拆成三部分：

1. `WorkspaceSessionRegistry`
   负责运行时工作区会话，承载当前已加载的数据和变更队列。
2. `WorkspaceCatalog` / `WorkspaceMetadataStore`
   负责工作区目录元数据读写与恢复。
3. `BundleIndexCacheStore` / `PreviewCacheStore` / `WorkspaceChangeStore`
   负责工作区数据目录中的可失效缓存与变更暂存。

当前仓库里已经存在的 `IWorkspaceSessionRegistry` 继续承担第一部分职责。
后续新增持久化能力时，应该补充独立接口，而不是继续把“最近工作区 / 目录索引 / 解析缓存”塞进 Session Registry。

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
   负责当前活动工作区和已登记工作区摘要。
2. `BundleExplorer` 页面级标签页协调逻辑
   负责当前工作区内的资源包标签页状态。
3. `WorkspaceImportCoordinatorService`
   负责 `文件导入 / 目录导入 / 后续拖拽导入` 的入口协调、文件选择与轻量反馈，
   并把收集结果提交给 Application 层的工作区导入登记命令。

补充约束：

- 工作台中的拖拽导入桥接当前只挂在左侧资源包列表区域，不挂整页
- 这样可以为后续资产列表、检查器等区域保留独立拖拽语义，而不让多个交互在页面根级别互相争抢

这两层都不是持久化仓储。

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
- `staging/changes/...`
  负责保存结构化变更暂存，不与轻量元数据混写。

补充约束：

- 目录索引继续保持全局统一，避免最近工作区入口分散到多个目录
- 单工作区元数据目录由用户显式选择，并记录到目录索引中
- 删除工作区记录时只删除工具生成的元数据文件，不删除用户选择的数据根目录本身
- 删除工作区记录时，工具生成的解析缓存和变更暂存也应一并清理

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
- 标签页切换时必须先捕获当前标签页的列表状态，再应用目标标签页状态，避免程序化筛选变更把旧标签页的选中资源误写回新标签页
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
- `metadataSchemaVersion`（可选）
- `previewInfoText`（可选）
  用于承载图片类预览的轻量附加信息，例如尺寸、裁切矩形、源格式标识等，避免把这类专属信息混回通用详情元数据。

补充约束：

- `previewInfoText` 只承载人类可读的轻量补充信息，不承载音频波形点、atlas 矩形集合、模型默认相机等结构化富预览数据。
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
5. 打开已有工作区时，工作区名称字段只读，仅用于展示目录中识别出的现有名称，不承担重命名职责。
6. 打开已有工作区时，要求所选目录中存在且只能明确识别出一个工作区；若目录里没有工作区或存在多个候选，则保持停留在当前面板中提示用户重新选择。
7. 控制台页和工作台顶部命令区复用同一套新建 / 打开工作区流程，避免入口行为不一致。
8. 如果用户通过目录直接打开了一个当前尚未进入最近工作区索引的工作区，则在工作区真正绑定成功后自动补登记到目录索引。

最近工作区列表中的再次打开也遵循相同的最后一步：

- 直接复用已登记的来源路径和数据目录
- 打开策略由“设置与诊断”中的偏好设置控制
- 当前支持 `默认当前窗口 / 默认新建窗口 / 每次询问` 三种模式
- 当策略为 `默认新建窗口` 时，如果目标工作区已被某个主窗口承载，则直接激活该窗口而不是重复派生新窗口

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
- 新建工作区时，如果目录中已存在工作区元数据，当前实现已支持“直接打开 / 重置后重建”的显式决策
- 工作区名称已从“来源目录名推导”演进为“用户可配置并持久化”的元数据字段
- 新建工作区已支持“空工作区”模式，不再强制要求先指定 AB 来源目录
- 新建工作区时已支持选择已有解密器，打开已有工作区时会只读展示并沿用元数据中的解密器配置
- 再次打开已登记工作区时已改为由设置项统一控制打开策略，支持默认当前、默认新建和每次询问
- 当再次打开策略命中新建窗口且目标工作区已在其他主窗口中打开时，当前实现会直接置顶已有窗口，避免同一工作区重复派生多个窗口
- Shell 已收敛为“控制台态 / 工作台态”两态模型：可根据当前状态动态承载控制台页或正式工作台，并支持从工作台返回控制台
- 一级导航第一入口位会在控制台态显示“控制台”，进入工作台态后显示“工作台”
- 统一导入协调服务当前已覆盖 `文件 / 目录 / 拖拽` 三类入口，工作台中的拖拽导入只作用于左侧资源包列表区域
- 导入入口当前已经从“只记录 UI 选择”推进到“登记工作区导入来源 + 执行解密器 + 生成资源包索引缓存”
- 导入处理当前已补上“导入源识别”层：目录、文件和拖拽来源展开后，会先执行轻量直检，尽早命中普通 AB 并筛除空文件等明显无效输入
- 导入处理当前会把来源展开为真实文件列表，经工作区默认解密器和 `IAssetBundleReader` 处理后写入 `BundleIndexCacheStore`
- 工作区默认解密器当前已支持“自动识别”模式：导入时会先按工作区级探测非回退解密器，再回退到直通解密器，并在成功建立或复用索引后把真实解密器固化回工作区元数据
- 工作区导入结果当前已补充第一轮结构化失败诊断：会回传自动识别探测摘要，以及“导入源筛除 / 解密失败 / 读取器拒绝 / 读取失败”四类问题明细，供工作台日志与后续诊断页复用
- 工作区导入结果当前已补上结构化缓存诊断：会回传 `缓存未命中 / 来源指纹变化 / 解密器变化 / 解密器版本变化 / 工具版本变化 / 解析器版本变化 / 来源移除` 等标准原因，而不是只保留数量统计
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
- 当前图片预览缓存已按资源独立落盘，不再共用单一 `payload.txt / payload.png` 覆写文件
- 当前预览类型覆盖如下：
  - 图像预览：`Texture2D`、`Sprite`
  - 文本预览：`TextAsset`、`Shader`
  - 结构化摘要预览：`AudioClip`、`Mesh`、`Material`、`GameObject`、`MonoBehaviour`、`AnimationClip`、`AnimatorController`、`Font`、`SpriteAtlas`
  - 通用摘要回退：其余可读取资源类型
- 工作台检查器当前会直接展示资源 `PathId`，并在图片预览成功后展示尺寸、裁切矩形、像素编码与源格式标识等专属信息
- 预览缓存当前按 `source fingerprint + generatorVersion` 校验命中，避免旧预览在资源包更新后继续误用
- 预览查询当前已补上结构化预览诊断：会把预览缓存命中 / 失效原因，以及 `定位信息缺失 / 解密器不可用 / 解密失败 / 预览生成失败` 等摘要回退原因统一沉到应用层 DTO
- 当前真实 Reader 已补上第一轮依赖深读：会从序列化文件 `externals` 中提取资源包级外部依赖，并递归解析资源字段树中的 `PPtr` 引用，写入资产级依赖缓存
- 工作台检查器当前已重构为 `概览 / 预览 / 依赖 / 替换 / 变更` 五页签；其中“依赖”页签已扩展为 `资产依赖 / 反向引用 / 依赖闭包 / Bundle 外部依赖` 四块，“预览”页签会展示缓存状态、摘要回退原因与诊断 detail
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

当前暂不实现：

- 顶部全局工作区切换器

## 9. 后续实施建议

建议按下面顺序继续推进：

1. 继续补活动工作区在更多依赖页面中的摘要与入口承载，并在后续需要时补跨 Bundle 级依赖跳转
2. 再评估是否需要顶层工作区切换器
3. M9 当前已完成真实资产级读取、索引缓存复用、工作区级解密器自动识别、失败诊断、依赖深读与缓存诊断主链路收口
4. 下一步进入富预览扩展：`AudioClip -> SpriteAtlas -> Mesh -> Material / Font / AnimationClip`
5. 最后再评估是否需要引入可分享工作区文件

### 9.1 预览扩展建议

真实内容预览的后续扩展建议单独按下面顺序推进，避免和当前已完成范围混淆：

1. `AudioClip`
2. `SpriteAtlas`
3. `Mesh`
4. `Material / Font / AnimationClip` 的半可视化预览

其中 `GameObject / MonoBehaviour / AnimatorController` 当前继续保留结构化摘要，不进入近期重型预览主线。

## 10. 执行结论

后续所有“多工作区”能力默认按以下原则实现：

- 运行时会话和持久化元数据必须分离
- 工作区目录索引默认存放在应用数据目录
- 单工作区元数据目录由用户显式选择
- 相同来源路径默认合并为同一工作区条目
- 页面只依赖活动工作区，不直接处理元数据文件
- 资源包标签页属于页面级状态，不直接侵入 Application 层
- 当前已通过工作区元数据文件恢复资源包标签页与活动筛选状态
- 工作区默认解密器属于工作区元数据，由工作区向导设置，并在工作台状态标签中呈现
- 导入入口先收口到 Presentation 共享协调服务，再通过 Application 层统一登记工作区导入来源，并进一步接到解密、读取和缓存写入链路
- 真实导入前若需要解密，应先通过独立解密器扩展点把来源转换为可读取文件，再进入 `IAssetBundleReader`
- 自动识别默认按“导入源识别 -> 解密器识别 -> Reader 路径识别 -> 缓存命中识别”四层推进，而不是把所有判断都塞进 Reader 或 UI
- 拖拽导入的页面桥接应优先挂在具体工作区区域，而不是整页根节点
- AB 真实解析结果应优先写入独立缓存层，而不是写进工作区元数据文件
- 解密后的临时文件默认属于 `staging` 层，不应混入工作区元数据
- 通过工作区向导新建或打开工作区时，仍可显式选择当前窗口或新窗口
- 工作区向导应优先用“工作区数据目录”识别已有工作区，并在新建时显式处理目录冲突
- 从控制台再次打开最近工作区时，打开策略由设置项统一控制，可选默认当前、默认新建或每次询问
- 启动自动恢复只在设置开启时，根据最后活动且可用的工作区条目生成导航请求，不直接破坏运行时会话边界
