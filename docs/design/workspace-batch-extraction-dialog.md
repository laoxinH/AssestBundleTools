# 工作区级批量提取对话框设计

## 1. 文档目的

本文档定义工作台顶部 `批量提取` 入口的目标交互与实现边界。

工作台顶部 `批量提取` 入口当前已经调整为：用户点击后在当前工作区内打开轻量独立批量提取窗口，先选择资源包范围和资源类型；点击开始后再选择输出目录，或勾选“导出到工作区提取目录”直接使用工作区数据目录下的提取目录，然后把提取任务提交到现有统一后台任务调度。

本文记录该入口的目标交互、Application 契约和当前落地范围。第一版 `ContentDialog` 承载空间不足，已按本文约束演进为同工作区进程内的轻量独立窗口。

## 2. 落地前问题

落地前已经具备几条可复用能力：

- 资产列表多选 `提取选中` 已能复用单资源提取链路。
- 普通资源批量提取会追加 `ExtractAsset / ExtractBatch` 操作记录。
- Spine 资源组整组 / 多选提取已能复用现有 Spine 组解析与提取链路。
- 提取、Spine 详情和预览已经共享标准化 bundle artifact 与 loaded-session 缓存。
- `IBackgroundWorkScheduler` 已有 `BatchExtraction` profile，并能把可见长任务投影到 Shell 状态条。

顶部工作区级 `批量提取` 入口曾存在三个缺口，本轮已覆盖前两项并开始收口第三项：

1. 入口语义不对：点击后跳到批处理页，用户无法在原工作台上下文内完成“选择资源包 -> 选择资源类型 -> 提取”的连贯流程。当前已改为打开工作区内轻量独立窗口。
2. 批处理页的批量提取预览只支持单个 `AssetType?` 筛选，不支持多资源包范围、多类型同时选择和 Spine 这种聚合类型。当前 Application 契约已支持 `selectedBundleIds[]` 和 `selectedCategories[]`，并保留旧 `AssetType?` 兼容入口。
3. 顶部入口如果继续依赖 `BatchTasksViewModel`，会把工作台快速操作、批处理页摘要和提取执行编排混在一起。当前顶部入口已通过独立对话框 ViewModel / Coordinator 承载；批处理页批量提取区域后续仍需要继续降级为历史摘要或复用同一契约。

## 3. 目标体验

用户流程如下：

1. 用户在工作台顶部点击 `批量提取`。
2. 工作台打开一个同进程轻量独立窗口。该窗口不切换主导航，不改变活动工作区，只为资源包列表、类型选择、输出方式和计划摘要提供足够空间。
3. 对话框展示当前活动工作区的资源包列表，支持搜索、全选、清空和逐包勾选。
4. 对话框展示当前勾选资源包中实际存在的可提取资源类型选择：
   - `全部`
   - `Texture2D`
   - `Sprite`
   - `TextAsset`
   - `AudioClip`
   - `Mesh`
   - `Material`
   - `AnimationClip`
   - `Font`
   - `GameObject`
   - `MonoBehaviour`
   - `AnimatorController`
   - `Spine`
   - 后续由 Application 读模型补充的其他稳定可导出类型。
   类型列表由 Application 预览计划返回，不由 Presentation 静态枚举；列表采用单列布局，并显示当前勾选资源包内每类候选数量。
5. 用户可勾选 `导出到工作区提取目录`。未勾选时，点击 `开始提取` 后再弹出目录选择器；勾选时直接使用 `<workspaceDataRootPath>/extractions`。
6. 对话框展示当前选择的计划摘要，例如已选资源包数、候选资源数、Spine 组数、预计输出大小和无候选诊断。
7. 用户确认后，对话框提交一个 `BatchExtraction` 后台任务。任务运行期间 Shell 状态条展示当前资源包 / 当前资源或 Spine 组、成功 / 失败计数、确定性进度和取消入口；任务完成后通过现有 toast / 日志 / 操作记录通知刷新变更页和批处理页摘要。
8. 提交后不导航到批处理页。批处理页仍可作为历史、变更摘要、保存计划和任务结果查看入口。

默认选择策略：

- 如果当前有活动资源包标签，默认选中活动资源包，降低误触后全工作区大规模导出的风险。
- 如果没有活动资源包但工作区已有资源包，默认不自动执行，允许用户手动全选；确认按钮在至少选中一个资源包、至少选中一个资源类型后才可用，输出目录在确认后解析。
- `全部` 是快捷语义。选择 `全部` 时表示“所有当前支持的普通资源类别 + Spine 组类别”，执行命令中应传递稳定的 `All` 语义，而不是 Presentation 硬编码完整类型列表。

## 4. UI 结构

批量提取窗口采用宽屏工作流布局，不做新的大页面，也不复用批处理页：

```text
标题区
  批量提取
  操作说明

主体区
  左侧：资源包列表
    搜索框
    全选 / 清空
    资源包项：勾选框 + 显示名 + 资源数 / 可提取摘要

  右侧：资源类型
    资源类型多选

底部固定动作条
  计划摘要 / 诊断
  导出到工作区提取目录
  取消
  开始提取
```

当前实现要求：

- 使用 `WorkspaceBatchExtractionWindow` 承载标题、底部命令和计划摘要，默认尺寸约为 `1536 x 1024`，最小尺寸约为 `1180 x 780`，避免初始打开时内容不完整。
- 标题区只保留强标题和短说明；当前选择与候选数量统一在底部计划摘要展示，避免顶部摘要与底部计划摘要重复。
- `WorkspaceBatchExtractionDialogContent` 只承载主体选择内容；资源包列表独占左侧滚动，右侧显示单列资源类别及候选数量。
- 主体左右两个面板保持同一视觉层级；面板内部列表项可以使用行分隔线，但不得再嵌套一层卡片。
- 搜索框需要使用原生搜索输入反馈，图标、占位文本、输入文本和清除按钮必须垂直居中，并保留鼠标聚焦时的底部边框强调；主体面板和底部计划摘要使用同一套主题化卡片层级与浅阴影，避免厚重浮层质感。
- 顶部入口再次触发时优先激活既有批量提取窗口，不重复打开多个同类窗口。
- 窗口关闭或取消时不提交任务；点击开始后才把提交对象交回 coordinator，并继续复用 `BatchExtraction` 后台任务。
- 打开窗口时必须先完成独立窗口创建与首帧显示，再启动计划预览刷新。资源包资产列表聚合、类别统计、Spine 组目录读取和计划大小计算都不得在 `ShowForSubmissionAsync()` 之前运行，也不得在 WinUI 事件调用栈内同步阻塞主窗口。
- 计划预览刷新属于可丢弃的 `TransientRead`，应通过统一后台调度的 `TransientRead` profile 执行，默认静默不进入 Shell 底部状态条；窗口内部使用加载态表达当前计划正在生成。用户改变资源包或类别选择、关闭窗口或提交任务时，旧预览请求必须取消，并用请求代次丢弃过期结果。
- 计划预览刷新期间必须保持资源包勾选、类别勾选、全选、清空和输出目录选项可用，避免打断用户连续选择；`开始提取` 必须禁用，直到最新一轮计划预览完成并且当前选择有效。
- 连续选择变化必须通过短去抖、取消旧请求和请求代次丢弃过期结果来收敛，不允许为每一次勾选堆积一串仍会更新 UI 的过期计划任务。

WinUI 控件选择建议：

- 资源包列表使用 `ListView` 或 `ItemsRepeater + CheckBox`，并明确由该列表自己持有滚动。
- 资源类型使用单列 `CheckBox` 列表；`全部` 作为快捷项，不和普通类型混成难以解释的第三态，每个类别显示当前勾选资源包内的候选数量。
- 输出目录不在窗口主体中提前选择；底部提供“导出到工作区提取目录”勾选项，未勾选时由开始按钮流程触发文件夹选择。
- 计划诊断使用 `InfoBar` 或紧凑状态文本，不把大段日志塞进窗口。

本地化要求：

- 对话框标题、按钮、空状态、类型显示名、错误提示和计划摘要都必须走 `ResourceKeys` / `.resw`。
- Application 只返回稳定枚举、类型名、计数和诊断码；Presentation 负责格式化用户可见文本。

## 5. 选择模型

资源包范围应使用稳定身份，不使用显示名：

```text
WorkspaceBatchExtractionBundleSelection
  bundleId
  bundleCacheKey
  displayName
  isSelected
  assetCount
  extractableCount
```

资源类型选择应使用“导出类别”，而不是只使用 Unity `AssetType`：

```text
WorkspaceBatchExtractionCategory
  All
  UnityAssetType(Texture2D)
  UnityAssetType(Sprite)
  UnityAssetType(TextAsset)
  ...
  SpineGroup
```

原因：

- `Texture2D / Sprite / Mesh` 等是普通 Unity 资产类型。
- `Spine` 是由 `.atlas + .skel/.json + Texture2D` 组成的聚合资源组，不是普通 Unity `AssetType`。
- 后续如果加入 `SpriteAtlas`、序列帧或其他派生导出类别，也不应强行塞进 `AssetType?`。

`全部` 语义：

- `All` 表示不按导出类别限制，包含当前实现支持的普通资源导出和 Spine 组导出。
- UI 上选择 `All` 时可以清空普通类型勾选，或把普通类型视觉上显示为被包含；命令侧必须保持一个明确的 `All` 语义。
- 如果用户取消 `All` 并手动选择多个类型，则只导出所选类别。

## 6. Application 契约

现有 `GetWorkspaceBatchExtractionPreviewQuery` 和 `ExtractWorkspaceAssetsCommand` 只支持：

```text
WorkspaceId
AssetType? AssetTypeFilter
OutputDirectory?
```

这不足以承载新对话框。实现阶段应优先扩展或新增工作区级批量提取契约，目标形态如下：

```text
GetWorkspaceBatchExtractionPlanQuery
  workspaceId
  selectedBundleIds[]
  selectedCategories[]
  outputDirectory?

ExecuteWorkspaceBatchExtractionCommand
  workspaceId
  workspaceKey
  workspaceDataRootPath
  selectedBundleIds[]
  selectedCategories[]
  outputDirectory
  conflictPolicy
```

设计要求：

- Application 负责根据资源包范围和类型类别生成计划，不让 Presentation 遍历资产列表并拼业务规则。
- 普通资源计划继续复用当前单资源 / 批量提取能力。
- Spine 类别由 Application 解析为资源组计划，再复用现有 `ExtractSpineAssetGroupHandler` 或其下沉后的共享服务。
- 如果继续保留旧的 `GetWorkspaceBatchExtractionPreviewQuery / ExtractWorkspaceAssetsCommand`，它们应作为兼容入口委托到新计划模型，而不是并行维护两套规划规则。
- 对话框 ViewModel 不应直接依赖 `BatchTasksViewModel`；可以复用 Application DTO、Presenter / Formatter 和后台调度服务。

## 7. 执行与后台任务

确认执行后，Presentation 只提交一个可见后台任务：

```text
BackgroundWorkRequest<Result<WorkspaceBatchExtractionResultDto>>
  Profile = BatchExtraction
  SemanticKind = VisibleOutput
  Priority = Visible
  WorkspaceId = current workspace
  WorkspaceKey = current workspace key
  CanCancel = true
```

### 7.1 并发策略

工作区级批量提取采用“单个后台任务 + 跨资源包受控并行 + 同资源包内顺序提取”的策略：

```text
一个 BatchExtraction 后台任务
  -> 按 AB 资源包分组
    -> 不同 AB 资源包可并行
    -> 同一 AB 资源包内按计划顺序处理普通资源 / Spine 组
```

这里的“并行”只发生在 Application 用例内部。Presentation 仍然只提交一个 `BatchExtraction` 后台任务，Shell 状态条也只展示一个可见任务。

跨资源包并发度规则：

- 默认 worker 数按 `Environment.ProcessorCount / 2` 推导。
- worker 数最小为 `1`，最大为 `4`。
- 实际 worker 数不得超过本次计划中的 AB 资源包数量。
- 同一 `workspaceKey + bundleCacheKey` 仍必须 single-flight，不允许同一 AB 被并行重复加载。
- 同一 AB 内的资产按计划顺序提取，避免同一个 loaded-session 上的第三方对象被无锁并发访问。

GC 压力退避：

- 每个 worker 开始处理下一个 AB 分组前重新检查 `GC.GetGCMemoryInfo()`。
- 当 `MemoryLoadBytes >= HighMemoryLoadThresholdBytes * 0.85` 时，新的 AB 分组启动数退避到 `1`。
- 已经开始处理的 AB 分组不被硬中断；退避只阻止更多 AB 分组同时启动。
- 压力解除后，后续 AB 分组可以恢复到 CPU 推导的并发度。

结果汇总仍按原计划顺序投影，避免并行完成顺序影响操作记录摘要和批处理结果展示。

执行规则：

- 批量提取窗口只负责收集用户输入、展示计划和提交任务。
- 批量提取窗口的“计划预览”不是正式提取任务。它只用于窗口内摘要和类别候选数量，应按静默 `TransientRead` profile 调度，允许取消、丢弃和重新计算；只有用户点击 `开始提取` 后，才提交可见 `BatchExtraction` 后台任务并进入 Shell 状态条。
- 真实提取循环按 AB 分组并行执行，同一 AB 内按计划项顺序执行；可成功项继续处理、失败项汇总、操作记录追加由 Application 用例负责。
- `ExtractWorkspaceAssetsCommand.Progress` 上报结构化 `WorkspaceBatchExtractionProgressDto`；Application 只提供阶段、当前资源包 / 当前项、总数、成功 / 失败计数和 GC 退避标记，Presentation 负责本地化为 Shell 状态条文案。
- Shell 状态条显示当前任务标题、当前资源包 / 当前资源或 Spine 组、已完成数和总数；准备阶段显示本次候选项数与候选资源包数。
- 高频进度必须合并投影，不逐资源向 UI 线程刷大量更新。
- 用户主动取消不视为失败；Presentation 只结束当前流程并依赖 Shell 状态条收敛任务状态，不额外弹出成功 / 失败 / 信息 toast，也不显示“未预期错误”。
- 任务完成后通过现有 `IWorkspaceOperationChangeNotifier` 刷新资源详情变更页、批处理页摘要和独立资源编辑窗口，不手动刷新资产列表。

## 8. 输出布局与命名身份

普通资源输出沿用已记录的多选批量提取布局：

```text
<输出根目录>/<AB 资源包名称>/<资源类型>/<文件名>
```

示例：

```text
D:\Exports\char_1001_amiya2.ab\Texture2D\avatar.png
D:\Exports\char_1001_amiya2.ab\Sprite\portrait.png
```

Spine 类别使用独立的聚合类型目录：

```text
<输出根目录>/<AB 资源包名称>/Spine/<Spine 组名>/...
```

Spine 不是普通 Unity `AssetType`，而是“整组导出”，包含 skeleton、atlas、主贴图和可匹配的 alpha 贴图。`Spine` 目录与 `Texture2D / Sprite / TextAsset` 等普通类型目录处于同一层级，组目录再由单组提取逻辑创建。

### 8.1 稳定身份优先

所有导出、显示去重和后续批量匹配替换都必须以稳定身份为准，而不是以用户可见名称为准：

- 普通 Unity 资产身份：`workspaceId + bundleId + assetId` 为主键；读模型和 manifest 继续携带 `pathId / assetType / assetTypeDisplayName / originalName / containerPath` 作为诊断、排序和人工核对信息。
- Spine 资源组身份：`workspaceId + bundleId + groupId` 为主键；组内组成文件继续保存真实组成资产的 `assetId / role / originalName / containerPath`。
- 显示名、导出文件名和目录名只是身份的投影，允许为了可读性追加后缀，但不得成为替换或恢复的唯一依据。

### 8.2 显示名与文件名去重

普通资源和 Spine 资源组必须使用同一套确定性去重策略：

1. 在同一个碰撞作用域内，先按“基础显示名 + 输出扩展名”分组。
   - 普通资源的作用域是 `<AB>/<资源类型>/`。
   - Spine 组的作用域是 `<AB>/Spine/`。
   - Spine 组内部组成文件的作用域是 `<AB>/Spine/<组名>/`。
2. 分组内只有一个候选时，保留原始基础名。
3. 分组内存在多个候选时，对所有候选都追加 ` [n]`，从 `1` 开始，而不是只给第二个之后追加后缀。这样列表展示、导出目录和用户截图中的语义保持一致，例如 `char_452_bstalk_snow#4 [1]`、`char_452_bstalk_snow#4 [2]`。
4. `n` 的排序必须稳定：优先按 `pathId` 或资源导入顺序，其次按 `assetId`，最后按 `containerPath / originalName` 做兜底；Spine 组按 `groupId` 和组成资产稳定排序。排序不得依赖当前 UI 列表筛选、当前滚动位置或后台任务完成顺序。
5. 清理非法路径字符后如果再次发生碰撞，仍按清理后的基础名重新分组去重。
6. 空名称或无法安全落盘的名称使用稳定兜底，例如 `<资源类型> #<pathId>`；仍参与同一去重流程。

普通资源当前不能继续把同名 `Texture2D / TextAsset / MonoBehaviour` 直接写到同一文件路径并覆盖。正式实现时，计划预览阶段就应计算唯一 `SuggestedOutputPath`，执行阶段只消费计划结果，不在写文件端临时猜测新文件名。

### 8.3 导出 Manifest

为支撑后续工作区级批量匹配替换，批量提取必须生成机器可读 manifest，而不是让替换入口反向解析文件名。推荐在本次批量输出根目录下写入 `_abt-export-manifest.json`，并在 Spine 组目录中可选写入轻量 `_abt-spine-group.json` 便于人工迁移。manifest 属于外部导出元数据，不进入工作区操作事实流；操作记录只保存 manifest 路径或摘要。

Manifest 至少包含：

```text
schemaVersion
workspaceKey
exportedAtUtc
outputRoot
items[]
  relativePath
  bundleId
  bundleDisplayName
  assetId
  pathId
  assetType
  assetTypeDisplayName
  originalName
  disambiguatedName
  containerPath
  outputRole        // Asset / SpineSkeleton / SpineAtlas / SpineTexturePage / SpineAlphaTexturePage
  spineGroupId?
  spineGroupDisplayName?
  exportedFileSizeInBytes
  exportedFileHashAlgorithm  // first version: MD5
  exportedFileHash
  exportedFileLastWriteTimeUtc
```

后续批量替换的推荐匹配优先级：

1. 用户选择了带 `_abt-export-manifest.json` 的目录时，优先按 manifest 中的 `bundleId + assetId` 或 `bundleId + groupId + role` 精确匹配。
2. manifest 精确匹配模式下，批量替换预览必须先比较当前外部文件大小与 `exportedFileSizeInBytes`。大小不同可直接判定为 `ManifestChanged`；大小相同才重新计算当前外部文件 MD5，并与 manifest 中的 `exportedFileHash` 比较。只有文件存在且大小或内容哈希发生变化的项才进入可替换候选。大小与哈希都未变化的项应计入 `Unchanged` 摘要，不创建替换候选、不追加操作记录，也不打扰用户确认列表。
3. 文件缺失、大小无法读取、哈希无法计算、manifest 哈希算法不支持或 manifest 项损坏时，不应把该项当作“未变化”；应返回结构化诊断，例如 `Missing / SizeUnavailable / HashUnavailable / ManifestUnsupported / ManifestInvalid`，由用户决定是否重新导出或改用启发式导入。
4. manifest 缺失时，才退回“目录层级 + 去重后文件名 + 类型兼容”的启发式匹配。启发式模式没有可靠的导出基线，不按 MD5 过滤未变化文件；它只负责发现可能匹配项，并把重名、类型不一致、多候选和缺失项作为需要用户确认的诊断，不自动静默替换。
5. 批量替换预览必须展示匹配来源：`ManifestChanged / ManifestUnchanged / NameHeuristic / Ambiguous / Missing / TypeMismatch / HashUnavailable`，并允许用户排除不确定项。
6. 即使命中 manifest 且 MD5 发生变化，替换执行仍要校验目标资产存在、类型兼容和来源文件可读；manifest 不替代 Application 的替换校验。

去重规则：

- 普通资源类别之间按目标资产身份和目标输出路径去重。
- Spine 组内部由现有 Spine 提取逻辑处理 skeleton / atlas / texture page 的组成资源。
- 如果用户同时选择 `Texture2D` 和 `Spine`，同一 Texture2D 可能分别作为普通贴图输出和 Spine 组依赖输出，因为两者输出目录和用户语义不同；第一版不跨类别静默丢弃其中一个输出。

## 9. 操作记录与缓存

操作记录规则：

- 每个成功导出的普通资源继续追加 `ExtractAsset`。
- 每次对话框批量任务追加一条 `ExtractBatch` 汇总记录，记录资源包范围、类别范围、输出根目录、成功 / 失败 / 取消数量和完成时间。
- Spine 组仍按整组提取规则记录组成资源和组级汇总。
- 输出文件缺失、中断恢复、失败分类和后续撤销语义继续以 [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md) 为准。

缓存规则：

- 普通资源提取、Spine 组提取、预览和详情读取继续通过标准化 bundle artifact 与 loaded-session 复用链路。
- 对话框不新增直接读取 AB 的路径。
- 对话框首屏只应使用已经在工作区读模型中的资源包摘要和初始选择状态；动态类别、候选数量、Spine 组数量和总大小由后台计划预览补齐。批量提取预览查询与正式执行命令都必须把当前 `workspaceDataRootPath` 传给 Spine 计划器，使 `LoadSpineAssetGroupsQuery` 可以命中运行时目录缓存或 `cache/spine-groups/<workspace-key>/catalog.json` 派生缓存。后续优化应优先复用资产类型 facets、SpineGroupCatalog 运行时 / 派生缓存，并减少 `BuildPlan` 与 `BuildCategoryPlans` 对同一资源包资产列表的重复遍历。
- Spine 组正式提取只需要组成资产清单，不需要重建交互预览诊断或 runtime 动画预览；提取入口应优先从 `SpineGroupCatalog` 的 skeleton / atlas / texture part 清单生成计划，并让每个组成资源继续走单资源提取和预览 payload 缓存。只有 catalog 缺失、目标组未命中或组成清单不完整时，才回退 `SpineAssetGroupDetailsLoadMode.ExtractionPlan` 详情模式，避免在“检查资源 / 开始提取”阶段重复执行完整预览读取流程。
- DEBUG 包中已有的 loaded-session `Acquire miss / in-flight / hit` 和 `Load start / completed / failed` 日志可以用于验证该批量入口是否复用同一 AB 加载会话。

## 10. 与批处理页关系

批处理页继续保留以下职责：

- 当前工作区变更摘要。
- 最近任务结果。
- 批量替换骨架。
- 保存前摘要和后续保存 / 重建输出计划。
- 需要长期查看或恢复的操作记录入口。

顶部 `批量提取` 不再通过导航进入批处理页。它是工作台里的直接操作入口。

批处理页中已有的批量提取区域可以在实现阶段逐步调整为：

- 复用同一个 Application 计划 / 执行契约；
- 或降级为“最近批量提取结果 / 历史摘要”入口；
- 但不应继续成为工作台顶部批量提取的唯一交互承载面。

## 11. 当前实施状态

第一版已完成：

1. Application 计划模型
   `GetWorkspaceBatchExtractionPreviewQuery` 和 `ExtractWorkspaceAssetsCommand` 已扩展资源包范围、多导出类别、`All` 与 `SpineGroup` 类别；普通资源和 Spine 组共享计划入口；预览 DTO 同时返回当前勾选资源包内的动态类别统计。
2. 窗口 Presentation
   已新增工作台批量提取 ViewModel / Coordinator / 独立 Window，顶部按钮改为打开工作区批量提取窗口，不再 `Navigate(typeof(BatchTasksPage))`，也不再使用 `ContentDialog` 挤压主体布局。窗口打开流程已调整为先显示窗口，再通过统一后台调度的 `TransientRead` profile 静默刷新计划预览，避免资源聚合阻塞主窗口首帧。
3. 执行链路
   对话框确认后提交 `BatchExtraction` 后台任务，执行命令复用普通资源提取和 Spine 组提取能力；普通资源、单资源、Spine 组和批量入口均通过后台任务壳执行，不在 UI 动作调用栈中直接等待重型提取；完成后通过既有操作变更通知刷新相关读模型。
4. 跨 AB 并发
   执行用例已按 AB 分组，在单个后台任务内部执行跨 AB 受控并行、同 AB 内顺序提取；默认并发度为 `CPU/2`、最大 `4`，GC 压力下退避到 `1`。
5. Spine 缓存复用
   批量提取预览、正式执行和单 Spine 组提取都会把 `workspaceDataRootPath` 传入 Spine 组计划器，避免工作区派生目录缓存存在时仍每次冷构建 Spine 组列表；单 Spine 组提取优先使用 `SpineGroupCatalog` 清单，目录不可用时才使用 `ExtractionPlan` 详情模式，避免提取前重复构建 skeleton 摘要、Texture2D 诊断和 runtime 动画预览。
6. Spine 输出分层
   工作区级和资产窗格多选 Spine 组批量提取已统一输出到 `<输出根>/<AB>/Spine/<组名>/...`，避免 Spine 组目录散落在 AB 根目录。
7. 普通资源重名去重
   普通资源列表展示和普通资源批量提取路径已接入同一套稳定命名投影：同一 `<AB>/<资源类型>` 下重名资源会按 `pathId / assetId / path` 稳定排序并显示、导出为 `名称 [1]`、`名称 [2]`，避免同名 `Texture2D / TextAsset / MonoBehaviour` 在批量提取时互相覆盖。

仍待收口：

- 批量提取 manifest 和后续批量替换 manifest 精确匹配仍待实现；实现前不得继续扩大依赖文件名启发式匹配的范围。
- 批处理页批量提取区域改为复用新契约或明确降级为摘要入口。
- 窗口预览刷新已具备 in-flight 取消和过期结果丢弃；后续可继续增加 debounce，减少快速勾选时的无效计划查询。
- 计划预览内部仍可继续优化为单次扫描和缓存优先，避免普通资源计划、类别统计和 Spine 组目录分别重复读取相同工作区资产列表。
- 当前动态类别只会出现 Domain `AssetType` 已支持且当前勾选资源包中实际存在的类型；`AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController` 等类型需要等领域枚举与提取支持扩展后再自然进入预览计划。

## 12. 测试建议

Application 已覆盖并需持续保持：

- 多资源包范围只规划被选中的资源包。
- 多导出类别可以同时规划 `Texture2D / Sprite / Spine`。
- `All` 覆盖当前支持的普通资源和 Spine 组。
- 批量提取预览和正式执行会把 `workspaceDataRootPath` 传给 Spine 组计划查询，保证可以复用工作区 SpineGroupCatalog 派生缓存。
- Spine 组正式提取优先使用 `SpineGroupCatalog` 清单；catalog 命中时不得加载组详情，catalog 不可用时使用 `ExtractionPlan` 详情模式，不回退到完整交互预览详情构建。
- 普通资源输出路径为 `<输出根>/<AB>/<资源类型>/<文件名>`。
- Spine 组输出为 `<输出根>/<AB>/Spine/<组名>/...`。
- 普通资源与 Spine 组的重名输出必须使用稳定去重后缀，不能覆盖同目录同名文件。
- 批量提取 manifest 必须记录导出路径到稳定资产身份的映射，供后续批量替换精确匹配。
- 无候选、部分失败、取消和成功摘要返回稳定 DTO。
- `ExtractAsset / ExtractBatch` 操作记录追加语义保持不变。

Presentation 已覆盖并需持续保持：

- 工作台顶部 `批量提取` 打开独立窗口协调器，不再导航到 `BatchTasksPage`。
- 窗口在未选资源包或未选类别时禁用确认；输出目录在确认后通过目录选择器或工作区提取目录选项解析。
- `All` 和显式多选类别的状态转换稳定。
- 确认后提交 `BackgroundWorkProfile.BatchExtraction`。
- 单资源和单 Spine 组提取也必须通过可见后台任务提交，不能阻塞主窗口。
- 打开窗口不会在首帧前同步生成计划；计划预览通过静默 `TransientRead` profile 后台任务刷新，关闭窗口或选择变化会取消旧请求。
- 计划预览 loading 期间保持范围、类别和输出方式可编辑，只禁用确认提取；预览完成或失败收敛后，如果当前选择有效则恢复确认。
- 快速连续变更选择时，旧预览请求会被取消并由请求代次丢弃过期结果，最终只允许最新选择对应的摘要更新 UI。
- 任务完成后依赖操作变更通知刷新，不直接刷新资产列表。

Localization / guardrail 测试继续覆盖：

- 新增用户可见文本存在 `zh-CN / en-US` 资源键。
- Presentation 不直接写资源键字面量。
- 常规 `Presentation` 文件不直接引用 `Infrastructure` 具体类型。

## 13. 当前结论

顶部工作区级批量提取当前已经按“工作台内轻量独立窗口”落地，不再是“跳转到批处理页的快捷入口”，也不再受 `ContentDialog` 尺寸约束影响。

后续继续沿用的方向是：

1. 用轻量独立窗口收集资源包范围、资源类别和输出方式。
2. 用 Application 计划模型解析普通资源和 Spine 组。
3. 用 `BatchExtraction` 后台任务执行。
4. 用现有操作记录、通知、缓存和 loaded-session 链路承载结果与性能复用。
