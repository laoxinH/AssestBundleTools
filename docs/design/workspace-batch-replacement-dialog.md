# 工作区级批量替换窗口设计

## 1. 文档目的

本文档定义工作区级 `批量替换` 窗口的目标体验、manifest 回读模型、Application 契约、后台执行和落地阶段。

这里的 `批量替换` 指：用户选择一个外部导出目录，把该目录中已修改的文件批量匹配回当前工作区内的 AB 资源，并追加可撤销的替换操作记录。它不是当前批处理页中“把已经进入替换队列的项聚合成摘要”的旧骨架。

后续实现新窗口时，旧的批量替换骨架可以直接移除，不再保留兼容入口：

- `GetWorkspaceBatchReplacementPreviewQuery / Handler`
- `ExecuteWorkspaceBatchReplacementCommand / Handler`
- 仅面向旧批处理页摘要的 `WorkspaceBatchReplacementPlanner`
- 与旧骨架强绑定的批处理页入口和 DTO

如果仍需要批处理页展示“最近批量替换结果”，应改为读取新批量替换操作记录或后台任务结果摘要，而不是继续维护旧骨架计划模型。

## 2. 设计目标

批量替换窗口要解决四个核心问题：

1. 易用性
   用户从一个导出目录开始，窗口自动识别 manifest、列出可替换项、未变化项和问题项；用户只需要确认范围和排除不想应用的项。
2. 精确性
   优先使用 `_abt-export-manifest.json` 中的稳定身份匹配，不依赖文件名、显示名或 ` [n]` 后缀。
3. 可恢复性
   执行结果必须落入现有 `ReplaceFromFile` 操作事实流，保持撤销、重做、关联资源刷新、保存 / 重建读取模型一致。
4. 性能与反馈
   目录扫描、hash 比对和计划生成都在后台执行；窗口首帧先出现，扫描期间仅禁用确认替换，不阻断筛选和选择操作。

## 3. 推荐用户流程

1. 用户在工作台顶部或批处理页入口点击 `批量替换`。
2. 应用打开轻量独立窗口，不切换主导航，不改变当前活动工作区。
3. 用户选择外部目录：
   - 如果目录根部存在 `_abt-export-manifest.json`，进入 `Manifest 精确匹配` 模式。
   - 如果没有 manifest，进入 `启发式匹配` 模式，并在窗口中明确标记可靠性较低。
4. 窗口后台生成替换计划：
   - manifest 模式按稳定身份回读目标资源。
   - 先比较当前文件大小；大小不同直接列为已修改。
   - 大小相同时再计算 MD5；MD5 不同列为已修改。
   - 大小和 MD5 都相同的项进入未变化摘要，不默认进入替换候选。
5. 用户查看计划，按资源包、类型、匹配状态筛选，并可逐项排除。
6. 用户点击 `开始替换`，窗口提交一个可见 `BatchReplacement` 后台任务。
7. 执行完成后，窗口展示摘要；资源详情、Spine 组、关联资源和变更页通过现有工作区操作通知刷新。

默认策略：

- manifest 模式下默认勾选 `Changed` 项，不勾选 `Unchanged / Missing / TypeMismatch / Ambiguous / Error` 项。
- 启发式模式下默认不全选高风险项；用户需要显式确认可替换项。
- 如果外部目录 manifest 的 `workspaceKey` 与当前工作区不一致，默认阻止执行，并提供只读诊断；后续如要支持跨工作区迁移，必须另行设计显式确认。

## 4. UI 布局

批量替换窗口沿用批量提取窗口的清爽工作流风格，但内容更偏“扫描结果确认”：

```text
标题区
  批量替换
  从导出目录回读已修改文件，并应用到当前工作区。

来源区
  外部目录路径
  选择目录
  匹配模式：Manifest 精确匹配 / 启发式匹配
  manifest 摘要或诊断

主体区
  左侧：筛选与资源包范围
    搜索框
    匹配状态筛选
    资源类型筛选
    资源包列表 / 数量

  右侧：替换候选列表
    勾选框
    目标资源显示名
    Bundle / 类型 / role
    来源相对路径
    状态：已修改 / 未变化 / 缺失 / 类型不匹配 / 多候选 / 错误

底部固定动作条
  计划摘要
  仅应用已修改项
  取消
  开始替换
```

视觉要求：

- 不做营销式大卡片或解释性长文；第一屏就是可操作窗口。
- 标题字号、卡片阴影、按钮尺寸与批量提取窗口保持同一密度。
- 主体使用两列工作区布局，避免把大量候选塞进窄 `ContentDialog`。
- 搜索框、筛选控件和底部按钮必须使用本地化文本。
- 列表项保持紧凑，优先服务扫描、比较和批量确认。
- 问题项用状态徽标或轻量文本标记，不用大面积警告卡片挤占列表。

交互要求：

- 扫描 / 计划刷新期间，目录选择以外的筛选和列表滚动可用，`开始替换` 禁用。
- 快速切换筛选不应重新扫描磁盘；筛选只作用于已生成计划。
- 切换目录、重新读取 manifest 或点击刷新时，取消旧扫描请求，并用请求代次丢弃过期结果。
- 用户勾选 / 取消候选只改变本地选择状态，不触发重新 hash。

## 5. 匹配模型

### 5.1 Manifest 精确匹配

manifest 模式是默认推荐路径。输入为导出目录根部 `_abt-export-manifest.json`。

匹配键：

- 普通资源：`workspaceKey + bundleId + assetId`
- Spine 组成资源：`workspaceKey + bundleId + spineGroupId + outputRole + assetId`
- 诊断辅助字段：`pathId / assetType / originalName / disambiguatedName / containerPath / relativePath`

变更判定：

1. 文件不存在：`Missing`
2. 文件大小读取失败：`SizeUnavailable`
3. 文件大小不同：`Changed`
4. 文件大小相同，manifest hash 算法不支持：`HashUnsupported`
5. 文件大小相同，当前 MD5 计算失败：`HashUnavailable`
6. 文件大小相同且 MD5 不同：`Changed`
7. 文件大小相同且 MD5 相同：`Unchanged`

manifest 模式下只有 `Changed` 默认进入可执行候选。`Unchanged` 只展示在摘要或可选筛选中，不追加替换操作记录。

### 5.2 启发式匹配

manifest 缺失、损坏或用户显式选择时，才允许启发式匹配。

启发式依据：

- 目录层级：`<AB>/<类型>/<文件>`、`<AB>/Spine/<组名>/<文件>`
- 类型兼容：扩展名和目标资源类型必须通过 Application 兼容规则。
- 名称投影：可参考 `disambiguatedName` 风格，但不能把 ` [n]` 当作事实身份。

启发式诊断：

- `NameMatched`
- `Ambiguous`
- `MissingTarget`
- `TypeMismatch`
- `UnsupportedType`
- `Untrusted`

启发式模式没有导出基线，不做 size / MD5 未变化过滤；它只发现可能匹配项，并要求用户确认。

## 6. Application 契约

新批量替换应使用独立契约，不复用旧骨架 query / command。

推荐查询：

```text
GetWorkspaceBatchReplacementPlanQuery
  workspaceId
  workspaceKey
  sourceRootDirectory
  matchingMode              // Auto / Manifest / Heuristic
  includeUnchanged
  cancellationToken
```

推荐执行：

```text
ExecuteWorkspaceBatchReplacementCommand
  workspaceId
  workspaceKey
  sourceRootDirectory
  matchingMode
  selectedCandidateIds[]
  batchId?
```

计划 DTO 至少包含：

```text
WorkspaceBatchReplacementPlanDto
  workspaceId
  sourceRootDirectory
  matchingMode
  manifestPath?
  manifestWorkspaceKey?
  totalManifestItems
  changedCount
  unchangedCount
  missingCount
  ambiguousCount
  errorCount
  candidates[]

WorkspaceBatchReplacementCandidateDto
  candidateId
  target
    bundleId
    bundleDisplayName
    assetId
    pathId
    assetType
    assetTypeDisplayName
    assetName
    containerPath
  source
    relativePath
    fullPath
    fileSizeInBytes
    hashAlgorithm?
    hash?
  spine
    groupId?
    groupDisplayName?
    outputRole?
  matchStatus
  matchMode
  isDefaultSelected
  diagnostics[]
```

执行结果 DTO 至少包含：

```text
WorkspaceBatchReplacementResultDto
  workspaceId
  batchId
  processedCount
  appliedCount
  skippedCount
  failedCount
  completedAtUtc
  operationIds[]
  itemResults[]
```

设计要求：

- Application 负责读取 manifest DTO、匹配工作区目标、做 size-first / MD5 判定、执行类型兼容校验。
- Infrastructure 负责 manifest 文件读取、外部文件大小 / hash 读取和文件内容载荷读取。
- Presentation 不直接解析 manifest、不直接计算 MD5、不直接扫描 AB。
- 执行时仍逐项追加真实 `ReplaceFromFile` 操作记录；Spine 组替换要拆成真实组成资产的替换记录，并共享同一 `batchId / relationId`。
- 如果一批 Spine 组包含 skeleton、atlas、texture page 多个组成文件，执行完成后只发布一次聚合操作通知，避免预览多次闪烁。

## 7. 后台任务与性能

计划扫描：

- 使用统一后台调度的 `TransientRead` profile。
- 默认不进入 Shell 底部任务列表，只在窗口内显示扫描状态。
- 读取 manifest、枚举文件、比较大小和计算 MD5 都不能阻塞 UI 线程。
- MD5 计算应只发生在大小相同且 manifest hash 可用的项上。

执行替换：

```text
BackgroundWorkRequest<Result<WorkspaceBatchReplacementResultDto>>
  Profile = BatchReplacement
  SemanticKind = VisibleMutation
  Priority = Visible
  WorkspaceId = current workspace
  WorkspaceKey = current workspace key
  CanCancel = true
```

执行策略：

- 单个后台任务承载整批替换。
- 同一 AB 内顺序执行，避免第三方 loaded-session 对象并发访问风险。
- 不同 AB 可以受控并行，但第一版可先顺序执行，等替换写入、通知聚合和失败恢复稳定后再扩展并发。
- 每个候选执行前重新校验目标仍存在、来源文件仍可读、类型仍兼容。
- 用户取消后保留已成功追加的操作事实，并在结果中标记 `Cancelled`；不回滚已经完成的替换操作记录。

## 8. 操作记录、通知和预览刷新

操作记录：

- 每个成功替换的真实目标资产追加 `ReplaceFromFile`。
- 同一次批量替换使用统一 `batchId`。
- Spine 组候选拆成组成资产记录，不追加“Spine 组本身”的虚假替换记录。
- 批量替换结果摘要可以作为批处理历史展示，但不替代真实资源级操作事实。

通知：

- 成功执行后发布带 `Reason = Replacement` 的工作区操作通知。
- 通知必须携带 `operationIds / primaryTargets / relatedTargets / implicitAffectedTargets`。
- 对 Spine 组成文件同批替换，通知层按组聚合，Presentation 先刷新轻量操作投影，再合并富预览刷新。
- `Unchanged`、跳过项和失败项不应触发目标预览刷新。

预览刷新：

- 批量替换窗口本身不直接重载资源预览。
- 资源详情、独立窗口和 Spine 组预览通过现有 `WorkspaceOperationChangeRefreshCoordinator` 处理。
- 不允许为了批量替换在 ViewModel 中手动读写 `staging/changes` 或直接刷新关联资源。

## 9. 与批量提取 Manifest 的关系

批量替换是批量提取 manifest 的主要消费方。

实现必须遵守：

- `_abt-export-manifest.json` 是精确匹配入口。
- manifest 中的 `relativePath` 只定位外部文件，稳定身份来自 `bundleId / assetId / pathId / spineGroupId / outputRole`。
- 文件名和 ` [n]` 后缀只作为人工识别，不参与事实匹配。
- manifest 模式必须先比较大小，再在大小相同时比较 MD5。
- 大小和 MD5 都未变化时，不创建替换候选、不追加替换操作记录。
- manifest 缺失时才使用启发式匹配，并明确显示风险。

## 10. 分阶段落地

第一阶段：窗口与 manifest 计划

- 新增批量替换窗口和 ViewModel。
- 移除旧批量替换骨架 query / command / planner 及其批处理页入口。
- 新增 manifest 计划查询，支持目录选择、manifest 回读、size-first / MD5 判定、默认选择状态和诊断。
- 只生成计划，不执行替换。

第二阶段：执行与操作记录

- 新增批量替换执行命令。
- 逐项复用单资源文件替换能力。
- Spine 组候选拆成组成资产替换，并共享批次关系。
- 接入 `BatchReplacement` 可见后台任务和操作通知聚合。

第三阶段：启发式回退

- 在 manifest 缺失时提供名称 / 目录 / 类型兼容匹配。
- 明确 `Ambiguous / TypeMismatch / UnsupportedType` 诊断。
- 高风险项默认不选中。

第四阶段：结果与历史

- 批处理页改为展示最近批量替换结果、失败明细和可恢复操作摘要。
- 补充来源缺失、来源变化、取消后恢复等诊断。

## 11. 测试建议

Application 测试：

- manifest 工作区 key 不匹配时阻止执行。
- manifest 精确匹配按 `bundleId + assetId` 找到普通资源。
- Spine manifest 项按 `bundleId + groupId + role + assetId` 生成组成资产候选。
- 文件大小不同直接进入 `Changed`，不计算 MD5。
- 文件大小相同且 MD5 相同进入 `Unchanged`，不默认选中。
- 文件大小相同且 MD5 不同进入 `Changed`。
- 缺失文件、hash 失败、类型不兼容返回结构化诊断。
- 执行只对选中 `Changed` 候选追加替换操作记录。
- 同一批次的替换记录共享 `batchId`。

Infrastructure 测试：

- manifest 读取失败返回结构化错误。
- 外部文件大小和 MD5 读取稳定。
- 相对路径必须限制在用户选择的源目录下，避免 manifest 路径穿越。

Presentation 测试：

- 打开窗口不会同步扫描目录阻塞首帧。
- 扫描期间禁用 `开始替换`，筛选和滚动可用。
- 切换目录会取消旧扫描，并且旧结果不能覆盖新结果。
- manifest 模式默认只勾选 `Changed`。
- 启发式模式显示风险提示且不静默全选高风险项。
- 确认后提交 `BatchReplacement` 后台任务。

本地化 / 架构守卫：

- 新窗口所有用户可见文本进入 `zh-CN / en-US` 资源。
- Presentation 不直接引用 Infrastructure manifest store。
- Presentation 不直接解析 JSON、不计算 MD5、不读取 AB。

## 12. 当前结论

批量替换应作为“从导出目录回读并批量应用修改”的独立工作流落地。

新设计的主线是：

1. 轻量独立窗口收集来源目录并展示计划。
2. manifest 优先，稳定身份匹配。
3. size-first / MD5 判断真实变化。
4. 执行阶段复用真实资源级替换操作记录。
5. 通过现有通知和预览失效模型刷新 UI。

旧批量替换骨架没有继续兼容价值，实现新窗口时应直接移除，避免形成两套含义不同的“批量替换”。
