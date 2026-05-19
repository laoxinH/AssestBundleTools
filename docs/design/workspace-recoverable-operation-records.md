# 工作区可恢复操作记录设计

## 1. 文档目的

本文档定义“可恢复操作记录”模型，用于把当前工作区中的提取、替换、编辑草稿应用、保存输出等用户操作记录为可持久化、可校验、可撤销和可恢复的事实流。

它补齐现有 `staging/changes/<workspaceKey>.json` 的下一阶段设计。当前代码已经具备：

- `WorkspaceChangeSetDto`
  保存 `changes[]`、`editDrafts[]`、`operations[]`、`schemaVersion` 和 `operationSequence`。
- `WorkspaceChangeRecordDto`
  当前是提取、替换和保存前暂存的通用摘要记录。
- `WorkspaceAssetEditDraftDescriptorDto`
  当前只保存编辑草稿描述元数据，不保存草稿内容。
- `WorkspaceChangeSetAssetEditDraftStore`
  当前从工作区变更暂存层读取 `editDrafts[]`，用于窗口恢复提示前的草稿候选预检。
- `CachedWorkspaceSessionRegistry`
  当前在运行时保存 `LastExtractedAtUtc / AppliedWorkspaceReplacement / AppliedFileReplacement`，并由这些状态生成 `WorkspaceChangeSummaryDto`。

这些能力已经能展示变更摘要，但还不能完整回答：

- 应用重启后如何恢复未保存的提取 / 替换 / 编辑操作？
- 如何在多个替换或编辑动作之间按顺序撤销？
- 崩溃发生在长操作中间时，如何区分“已完成 / 可继续 / 需要用户确认 / 已失效”？
- 保存输出后，哪些操作已经成为检查点，哪些仍可撤销？

## 2. 设计结论

新增“操作记录”层，作为工作区变更暂存中的主事实来源：

```text
staging/
  changes/
    <workspaceKey>.json       # 操作记录、当前物化变更摘要、草稿描述
  drafts/
    <workspaceKey>/
      <draftId>/...           # 后续真实草稿内容
  operation-payloads/
    <workspaceKey>/
      <operationId>/...       # 后续大载荷或长操作中间产物
```

当前阶段先扩展 `staging/changes/<workspaceKey>.json`，不急于创建新存储文件。后续真实草稿内容或大操作载荷再落到 `drafts/` 与 `operation-payloads/`。

推荐演进后的 `WorkspaceChangeSetDto` 逻辑结构：

```text
workspaceKey
updatedAtUtc
schemaVersion
operationSequence
operations[]       # 新增：可恢复操作记录，事实流
changes[]          # 保留：当前物化变更摘要，供旧 UI / 保存预览兼容
editDrafts[]       # 保留：草稿描述元数据
checkpoints[]      # 后续：保存 / 导出检查点
```

其中：

- `operations[]` 是可恢复事实来源。
- `changes[]` 是由操作记录物化出的当前待处理摘要，不再承担完整历史职责。
- `editDrafts[]` 继续只保存草稿描述，不保存草稿内容。
- 保存输出稳定后，再用 `checkpoints[]` 标记哪些操作已经被某次保存 / 导出消费。

## 3. 目标

1. 支持工作区重新打开后恢复未保存的提取、替换和编辑状态。
2. 支持当前会话和重启后的撤销。
3. 支持崩溃恢复：长操作可识别为 `Pending / Interrupted / Applied / Failed`。
4. 支持批处理页展示更可信的操作历史、结果明细和保存前影响范围。
5. 为真实写包前的输出计划、覆盖保护、失败分类和回滚边界打基础。
6. 保持工作区元数据、运行时会话、预览缓存和变更暂存职责分离。

## 4. 非目标

当前设计不立即实现：

- 完整 redo 栈。
- 保存后跨输出包自动反向写回。
- 自动删除用户已经导出的外部文件。
- 真实编辑草稿内容格式。
- 多人协作或跨进程同时编辑同一工作区。
- 直接把操作记录当作用户可见文案来源。

## 5. 术语

- **操作记录**：用户或系统确认执行的一次可恢复动作，例如提取、替换、应用草稿、撤销替换。
- **操作事实流**：按 `sequence` 递增保存的 `operations[]`。
- **物化变更摘要**：由操作事实流计算出的当前工作区待保存 / 待展示状态，对应现有 `WorkspaceChangeSummaryDto` 和 `changes[]`。
- **编辑草稿**：尚未应用到工作区变更队列的临时编辑内容。当前只保存描述元数据。
- **检查点**：保存 / 导出动作成功后产生的边界，用来标记哪些操作已经被外部输出消费。

## 6. 操作记录模型

建议新增 `WorkspaceOperationRecordDto`，字段保持稳定、可 JSON 序列化、可向后兼容：

```text
operationId
sequence
operationKind
status
workspaceKey
target
source
previousState
result
createdAtUtc
appliedAtUtc
undoneAtUtc
completedAtUtc
checkpointId
batchId
supersedesOperationId
diagnostics[]
```

字段说明：

- `operationId`
  稳定唯一 ID。建议使用 `op_<utcTicks>_<shortRandom>` 或 GUID 字符串。
- `sequence`
  工作区内递增序号，用于恢复顺序和撤销栈排序。
- `operationKind`
  稳定枚举字符串，不直接本地化。
- `status`
  表示当前恢复语义。
- `target`
  目标资源身份，例如 `bundleId / assetId / serializedFileName / pathId / assetType / sourceFingerprint`。
- `source`
  操作来源，例如工作区内源资源、外部文件、草稿引用、输出目录。
- `previousState`
  支持撤销所需的前一状态快照。
- `result`
  操作结果，例如输出路径、替换来源、生成的草稿引用、保存输出文件。
- `checkpointId`
  标记该操作是否已被某次保存或导出消费。
- `diagnostics[]`
  稳定错误码或结构化消息令牌，不保存原始异常文案作为主提示。

### 6.1 `operationKind`

第一阶段建议支持：

- `ExtractAsset`
- `ExtractBatch`
- `ReplaceFromWorkspace`
- `ReplaceFromFile`
- `UndoReplacement`
- `EditDraftCreated`
- `EditDraftUpdated`
- `EditDraftApplied`
- `EditDraftDiscarded`
- `SavePreviewAccepted`
- `SaveExecutionStarted`
- `SaveExecutionCompleted`
- `SaveExecutionFailed`

后续可扩展：

- `RedoOperation`
- `OutputCleanup`
- `ImportSourceRegistered`
- `BundleRemoved`

导入和资源包移除当前仍优先属于工作区目录 / 导入链路，不急于纳入同一个撤销模型。

### 6.2 `status`

建议稳定状态：

- `Planned`
  已生成计划，尚未执行副作用。
- `Pending`
  长操作已开始，可能在崩溃后需要恢复判断。
- `Applied`
  操作已成功应用到工作区状态。
- `Undone`
  操作已被撤销，不参与当前物化变更。
- `Failed`
  操作失败，保留诊断和可见结果。
- `Interrupted`
  应用启动时发现操作停在 `Pending`，需要用户确认处理。
- `Committed`
  操作已被保存 / 导出检查点消费。
- `Invalid`
  操作记录本身无法解析或目标已不可定位。

### 6.3 目标与来源描述

建议拆出轻量结构，而不是继续把所有内容塞进 `summary / sourceDescriptor`：

```text
target:
  bundleId
  assetId
  assetName
  assetType
  assetTypeDisplayName
  serializedFileName
  pathId
  sourceFingerprint

source:
  sourceKind
  sourceBundleId
  sourceAssetId
  sourceFilePath
  sourceFileFingerprint
  draftId
  draftMode
  outputDirectory
```

Presentation 负责把这些结构化字段格式化为用户可见文本；Application 和 Infrastructure 不保存面向用户的整句文案。

## 7. 当前操作语义

### 7.1 提取

提取属于“外部输出操作”，不修改资源包内容。

提取的 payload 获取可以复用预览缓存、标准化资源包物理缓存和进程内 loaded-session 缓存，但这些缓存只负责性能和资源复用，不参与提取操作事实语义。

记录规则：

1. 用户确认提取计划后，先写入 `ExtractAsset / ExtractBatch` 的 `Planned` 或 `Pending` 记录。
2. 真实导出成功后更新为 `Applied`，并保存输出路径、输出文件存在性、命名冲突策略和完成时间。
3. 撤销提取默认只撤销“工作区提取记录”和批处理结果，不自动删除用户目录中的外部文件。
4. 如果未来提供删除输出文件，应作为单独 `OutputCleanup` 操作，并有明确确认。

恢复规则：

- `Applied`：恢复为“已提取”记录，结果明细可继续展示。
- `Pending`：启动后标记为 `Interrupted`，批处理页提示用户重新执行或标记失败。
- 输出文件缺失：操作仍可恢复，但结果标记为“输出文件不可用”，不自动删除记录。

缓存边界：

- 预览缓存命中、标准化资源包 artifact 命中或 loaded-session 命中都不能单独生成操作记录；只有用户确认的提取 / 替换 / 保存等动作才进入 `operations[]`。
- 批量提取和 Spine 整组提取可以按 AB 复用同一个标准化 artifact 和 loaded session，但仍必须为每个成功导出的组成资源记录独立 `ExtractAsset`，并按批量 / Spine 入口追加汇总记录。
- 提取撤销只撤销工作区操作记录和物化提取状态，不删除 `cache/bundles`、`cache/previews` 或用户输出目录中的文件；缓存清理属于独立缓存维护动作。

### 7.2 工作区内替换

工作区内替换属于“待保存内容修改”。

记录规则：

1. 应用替换前记录目标资源的 `previousState`。
2. `source` 记录源资源身份和源资源指纹。
3. 同一目标资源的多次替换形成顺序栈，新的替换可以 `supersedesOperationId` 指向上一次仍生效的替换。
4. 物化状态只采用该目标资源最新的未撤销替换。

撤销规则：

- 撤销最新替换时，追加 `UndoReplacement` 操作，并把目标恢复到 `previousState`。
- 如果 `previousState` 为空，目标恢复为原始未修改状态。
- 如果 `previousState` 指向更早的替换，目标恢复为该替换的来源。

恢复规则：

- 目标资源不存在：操作变为 `Invalid`，不参与物化。
- 源资源不存在或指纹不匹配：操作保持记录，但恢复计划标为 `SourceMissing / SourceMismatch`，需要用户重新选择来源或撤销。

### 7.3 外部文件替换

外部文件替换和工作区内替换共享目标物化模型，但来源来自文件系统。

记录规则：

1. `source` 必须保存规范化 `sourceFilePath`、文件名、文件大小、最后修改时间和可选内容哈希。
2. 只保存来源文件引用，不把整个外部文件内容塞进工作区元数据。
3. 如果用户选择“复制到工作区暂存”，后续大载荷应进入 `operation-payloads/<workspaceKey>/<operationId>/`。

恢复规则：

- 来源文件仍存在且指纹一致：自动恢复为可保存替换。
- 来源文件缺失：保持操作记录但标为 `SourceMissing`，UI 提供重新选择文件或撤销。
- 来源文件指纹变化：标为 `SourceMismatch`，默认不静默恢复为可保存状态。

### 7.4 编辑草稿

编辑草稿分为“草稿描述”和“草稿内容”两层。

当前已落地的是描述层：

- `editDrafts[]`
- `WorkspaceAssetEditDraftDescriptorDto`
- `AssetEditDraftRestorePlan`

后续操作记录只引用草稿，不直接保存草稿内容：

- `EditDraftCreated`
- `EditDraftUpdated`
- `EditDraftApplied`
- `EditDraftDiscarded`

恢复规则：

- 窗口 / 标签恢复只携带轻量 `EditDraftReference`。
- 真正恢复草稿前必须先走 `GetAssetEditDraftRestorePlanHandler`。
- 只有 `Restorable` 草稿才能重新挂回标签或进入编辑器。
- 草稿内容后续应放在 `staging/drafts/<workspaceKey>/<draftId>/`，不得写入 `openAssetTabs[]` 或 `operations[]`。

### 7.5 保存与重建

保存 / 重建是检查点，不是普通撤销动作。

记录规则：

1. 用户确认保存输出计划后，追加 `SavePreviewAccepted`。
2. 真实写包开始前追加 `SaveExecutionStarted`，状态为 `Pending`。
3. 写包成功后追加 `SaveExecutionCompleted`，并生成 `checkpointId`。
4. 被该检查点消费的操作标记为 `Committed` 或挂接 `checkpointId`。
5. 写包失败后追加 `SaveExecutionFailed`，保留稳定错误码、受影响资源包和输出路径。

撤销规则：

- `Committed` 操作不再通过普通撤销直接回到保存前状态。
- 如果未来需要“生成反向补丁”，应作为新的操作设计，而不是删除历史记录。

## 8. 物化规则

工作区打开后按以下顺序物化：

1. 读取 `WorkspaceChangeSetDto`。
2. 校验 `workspaceKey / schemaVersion / operationSequence`。
3. 按 `sequence` 排序 `operations[]`。
4. 对每条操作执行恢复校验：
   - 目标资源是否存在；
   - 源资源或文件是否仍可定位；
   - 草稿描述是否可恢复；
   - 检查点状态是否一致。
5. 只把 `Applied` 且未被 `Undone / Invalid` 的操作投影到运行时工作区状态。
6. 由物化结果生成 `WorkspaceChangeSummaryDto`、保存预览和批处理页结果。

物化结果不得依赖 WinUI 控件实例。

## 9. 恢复提示策略

启动或打开工作区时：

- 正常 `Applied` 操作可以静默恢复，并在变更页显示。
- `Interrupted / SourceMissing / SourceMismatch / DraftMissing / Invalid` 操作应进入恢复计划。
- 恢复计划由 Application 返回结构化 DTO，Presentation 只负责展示和接收用户选择。

推荐恢复入口：

1. 工作台顶部 toast：提示存在需要处理的恢复项。
2. 批处理页：展示可继续、可重试、需重新选择来源、可忽略的操作列表。
3. 资源详情“变更”页：展示当前资源相关的恢复诊断。

恢复动作：

- `Retry`
- `RelinkSource`
- `MarkFailed`
- `Undo`
- `IgnoreForNow`
- `DiscardRecord`

`DiscardRecord` 必须有确认，不应作为默认恢复动作。

## 10. 撤销策略

第一阶段只支持“按目标资源撤销最新可撤销操作”：

- 替换：可撤销。
- 编辑草稿应用：可撤销到应用前状态，前提是 `previousState` 可用。
- 提取：只撤销工作区记录，不删除外部文件。
- 保存 / 重建：不可普通撤销。

撤销本身也必须追加操作记录，而不是直接删除原记录：

```text
ReplaceFromFile(op-10) Applied
UndoReplacement(op-11) Applied, targetOperationId = op-10
```

这样做可以保证：

- 崩溃后仍能解释为什么某个替换不再生效；
- 批处理结果和日志可追溯；
- 后续 redo 或历史查看有基础。

## 11. 与现有代码的演进关系

建议分阶段落地。

### 第一阶段：DTO 与持久化 schema

- 已新增 `WorkspaceOperationRecordDto`、目标 / 来源 / 前态 / 结果 / 诊断 / 恢复计划等 DTO。
- 已新增 `WorkspaceOperationLogService`，负责追加操作、分配工作区内递增 `sequence`、更新状态、筛选当前生效操作和生成基础恢复计划。
- `WorkspaceChangeSetDto` 已增加 `Operations`、`SchemaVersion` 和 `OperationSequence`。
- `JsonWorkspaceChangeStore` 继续读写同一个 JSON 文件，`operationKind / status` 以稳定字符串枚举落盘。
- `changes[] / editDrafts[]` 已保持向后兼容。
- 已补 Application 操作日志服务测试和 Infrastructure JSON round-trip 测试。

### 第二阶段：替换操作先接入

- 已让 `ReplaceAssetFromWorkspaceHandler / ReplaceAssetFromFileHandler / UndoAssetReplacementHandler`
  在成功应用后追加操作记录。
- `WorkspaceSummaryDto` 已携带轻量 `WorkspaceKey / WorkspaceDataRootPath`，Presentation 通过命令传给 Application；Presentation 不直接读写 `staging/changes`。
- 已新增替换操作 recorder，由 Application 通过 `IWorkspaceDataStoreFactory` 读取并保存 `WorkspaceChangeSetDto`。
- `CachedWorkspaceSessionRegistry.OpenWorkspace` 已在打开工作区时读取 `operations[]`，并把当前生效的替换操作投影到运行时状态。
- `WorkspaceChangeSummaryDto` 继续保持现有形状，替换状态可由重启恢复后的运行时状态生成。
- 当前仍未完成来源文件指纹校验、来源缺失恢复计划、当前会话内多层撤销的精确回退 UI 提示；这些进入后续恢复计划 / 撤销栈细化。

### 第三阶段：提取操作接入

- 已让单资源提取通过现有 `GetAssetPreviewQuery` 管线取得真实 payload，并由 Presentation 先选择保存文件、Application 保留用户选择的文件名、Infrastructure 文件写出端口落到导出文件。
- 已明确后续提取性能优化不改变操作记录事实模型：预览、提取和 Spine 解析应共享 `cache/bundles` 标准化资源包物理缓存与进程内 loaded-session 缓存；缓存命中只减少解密、解包和 `LoadBundleFile` 开销，不替代 `ExtractAsset / ExtractBatch` 操作记录。
- 已覆盖 `Image / Audio / Binary` 二进制 payload、`Text / Summary` 完整文本 payload，`Mesh` 可导入专业建模软件的 OBJ 预览几何导出，以及 `Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController` 结构化 JSON payload 的统一写出策略。
- 当前导出格式按“能直接进入专业软件优先，其次保留完整结构化摘要”的推荐表落地：
  - `Texture2D / Sprite`：PNG。来源为预览管线生成的无损图片 payload，不做有损压缩。
  - `TextAsset / Shader / MonoScript`：保留原始可读文本扩展名并写完整文本载荷；`.skel / .skel.bytes` 若为二进制预览则保持二进制写出。
  - `AudioClip`：优先写预览 / 转码后的标准音频 payload，扩展名随内容类型落到 `wav / ogg / mp3 / flac / m4a / aac`，无法识别时保留二进制。
  - `Mesh`：默认写 OBJ，保存文件对话框同时提供实验性 FBX；两者都只包含当前 Mesh 预览 DTO 中可读取的静态顶点、UV0 和三角面，不包含材质、骨骼、蒙皮、法线/切线或 AnimationClip。若预览几何被截断，导出文件会保留截断注记，仍不伪装成完整源模型。
  - `Material / AnimationClip / Font / GameObject / AnimatorController`：写可读 JSON 摘要，作为后续编辑 / 替换确认的结构化数据，不声明可直接导入 Unity 或 DCC 工具。
  - `MonoBehaviour`：默认写 TypeTree JSON 字段树，保存文件对话框额外提供摘要 JSON、YAML 投影和原始字段清单文本；TypeTree JSON 来自 Infrastructure 读取到的 Unity 字段树，摘要 / YAML / 字段文本来自当前 Application 预览 DTO，均不等同于 Unity 原始二进制对象，字段清单在预览字段被截断时会写入警告。
- 已让单资源提取追加 `ExtractAsset` 操作记录，保存实际输出路径、输出目录、输出文件存在性和完成时间。
- 已让普通资源批量提取复用单资源提取链路，并追加 `ExtractBatch` 汇总记录；资产窗格多选批量提取会在用户选择的输出根目录下按 `<输出根目录>/<AB 资源包名称>/<资源类型>/` 组织文件，当前批处理仍以“继续处理可成功项，汇总失败数”为第一版策略。
- 工作台顶部工作区级批量提取入口已改为对话框：用户选择当前工作区资源包范围，窗口按当前勾选资源包动态生成 `全部 / Texture2D / Sprite / Spine` 等导出类别及候选数量；点击开始后再选择输出根目录，或勾选工作区提取目录后直接使用 `<workspaceDataRootPath>/extractions`，随后提交 `BatchExtraction` 后台任务。工作区级批量提取在单个后台任务内按 AB 资源包受控并行，默认并发度为 `CPU/2`、最大 `4`，GC 压力下退避到 `1`，同一 AB 内仍按计划顺序提取，以兼顾吞吐、内存峰值和 loaded-session 访问安全；它仍必须复用 `ExtractAsset / ExtractBatch` 操作记录语义，不把对话框状态或 Shell 进度当作事实来源。详细见 [workspace-batch-extraction-dialog.md](workspace-batch-extraction-dialog.md)。
- 已让 Spine 资源组整组提取复用单资源提取链路：组级入口负责解析 skeleton / atlas / atlas page 主贴图和独立 alpha 贴图匹配；每个组成资源仍追加独立 `ExtractAsset` 操作记录并更新资源级 `LastExtractedAtUtc`，组级入口同时追加一条 `ExtractBatch` 汇总记录，记录 Spine 组目标、输出目录、成功 / 失败数量和已关联资源 ID。工作区变更摘要会保留已提取的隐藏依赖资产，避免 Spine 组关联资源已经导出但“最近变更记录”被资产列表可见性过滤掉；窗口化 Spine 详情在提取成功后也会即时重投影当前项变更摘要。
- 已让资产窗格的 Spine 资源组多选模式接入批量提取入口：用户选择一次输出根目录后，Presentation 为每个组把应用层输出目录设置为 `<输出根目录>/<AB 资源包名称>`，应用层单组提取再在该目录下创建组名子目录并写出组成资源。
- 已把资产窗格多选批量提取和 Spine 资源组多选批量提取的逐项写出循环移动到 Presentation 后台任务 runner；Shell 级后台任务状态服务负责显示任务标题、当前对象和进度。该状态条只表达当前会话中的运行中反馈，不进入 `operations[]`，也不改变 `ExtractAsset / ExtractBatch` 的事实记录、撤销和恢复语义。后台批量提取的状态上报必须做 UI 合并刷新：后台线程只更新最新任务状态，Shell 状态条在 UI 线程按最新状态投影，避免大量小文件逐项写出时向 DispatcherQueue 注入成百上千个进度刷新。批量提取成功日志默认写摘要，不逐资源插入日志；失败明细也应有限展示，避免操作完成阶段因为日志集合膨胀造成二次卡顿。
- 已让工作区打开时把 `Applied ExtractAsset` 操作物化回资源 `LastExtractedAtUtc`，用于重启后恢复已提取状态。
- 已在 Presentation 层增加工作区操作变更通知管线：组合根用 notifying gateway 包装 `IWorkspaceAssetOperationGateway / IWorkspaceBatchOperationsGateway`，单资源提取、Spine 组提取、普通批量提取、替换、撤销替换和保存骨架成功后会发布当前 `WorkspaceId` 的读模型失效事件。订阅者包括：
  - 批处理页：活动工作区匹配时自动重新读取 `WorkspaceChangeSummaryDto / SavePreview / BatchPreview`。
  - 主工作台资源详情区：通过 `WorkspaceOperationChangeRefreshCoordinator` 合并通知，重读当前工作区已打开的全部资源 / Spine 详情标签，使批量操作后未激活标签的“变更”页签也能自动更新；操作记录变化不重载资产 / Spine 列表，避免提取完成后打断列表滚动、筛选和选择状态。
  - 独立资源编辑窗口：订阅同一通知，重读窗口内已打开的全部资源或 Spine 资源组详情，窗口化场景不再依赖发起操作的一侧手动重投影。
  操作入口只负责执行命令、清理本地候选状态和展示结果反馈，不再在提取 / 替换 / 撤销成功后手动调用页面刷新回调；非持久化的候选来源选择仍允许即时重投影当前详情。
- 提取撤销仍未实现；当前原则保持为“只撤销工作区提取标记，不自动删除用户目录中的外部文件”。
- 批处理页的中断、失败和输出文件不可用状态展示仍待细化。

### 第四阶段：编辑草稿与操作记录合流

- `editDrafts[]` 继续保存草稿描述。
- 操作记录引用 `draftId / draftMode`。
- 草稿内容落到 `staging/drafts/` 后，再补真实恢复 / 丢弃 / 应用流程。

### 第五阶段：保存检查点

- 保存输出计划生成检查点。
- 成功写包后把被消费操作标记为 `Committed`。
- 失败时保留 `SaveExecutionFailed` 和可恢复诊断。

## 12. 架构边界

- Application 定义操作记录 DTO、恢复计划、撤销语义和物化规则。
- Infrastructure 只负责 JSON 持久化、外部文件指纹、真实导出 / 写包实现。
- Presentation 只负责触发命令、展示恢复计划、接收用户选择、本地化文本，以及页面级读模型失效协调；操作变更通知不得反向进入 Application，也不得让页面直接读取 `staging/changes`。
- Presentation 可以维护 Shell 级后台任务状态条来展示运行中任务进度；该状态条属于瞬时 UI 状态，不得作为恢复、撤销、批处理历史或变更摘要的数据来源。
- 页面或窗口订阅操作通知时必须按 `WorkspaceId` 过滤，并在自身忙碌时合并为一次待刷新请求；统一使用 `WorkspaceOperationChangeRefreshCoordinator`，不要在新的操作 handler 中新增“成功后刷新某个页面”的回调。
- 发起操作的 handler 可以更新仅存在于当前 UI 的瞬时状态，例如清理待替换候选；已持久化的提取 / 替换 / 撤销 / 保存状态必须通过通知后的读模型重读来反映到详情页和批处理页。
- 工作区元数据只保存轻量浏览状态；操作记录属于 `staging/changes`。
- 预览缓存仍是可重建缓存，不得成为撤销事实来源。
- 外部工具 stdout / stderr 只能作为诊断 detail，不进入操作主提示。

## 13. 测试建议

第一批测试优先覆盖：

- `JsonWorkspaceChangeStore` 可 round-trip `operations[]`、旧 `changes[]` 和 `editDrafts[]`。
- 替换操作记录可在重启后恢复为当前替换状态。
- 多次替换同一目标后，撤销只回退最新替换。
- 文件替换来源缺失或指纹变化时，恢复计划给出结构化诊断。
- 提取中断后重启显示 `Interrupted`，不会误报为成功。
- `Committed` 操作不参与普通撤销。
- Presentation 本地化 guardrail 继续保证恢复提示不直接展示稳定错误令牌或原始异常文本。

## 14. 当前执行结论

下一步实现时不应继续扩大 `WorkspaceChangeRecordDto` 的通用字符串字段。正确方向是：

1. 保留它作为兼容摘要。
2. 新增 `operations[]` 作为事实来源。
3. 让提取、替换、编辑草稿和保存逐步通过操作记录驱动物化状态。
4. 最后再把保存 / 重建接到真实写包。
