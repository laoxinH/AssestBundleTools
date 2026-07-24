# 工作区级批量替换窗口设计

## 1. 文档目的

本文档定义工作区级 `批量替换` 窗口的目标体验、manifest 回读模型、Application 契约、后台执行和落地阶段。

这里的 `批量替换` 指：用户选择一个外部导出目录，把该目录中已修改的文件批量匹配回当前工作区内的 AB 资源，并追加可撤销的替换操作记录。它不是当前批处理页中“把已经进入替换队列的项聚合成摘要”的旧骨架。

当前实现已经移除旧批量替换预览骨架，不再保留兼容入口：

- `GetWorkspaceBatchReplacementPreviewQuery / Handler`
- 旧语义的 `ExecuteWorkspaceBatchReplacementCommand / Handler`
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
  manifest 摘要、启发式风险摘要或诊断

主体区
  左侧：资源包范围
    资源包搜索框
    全选 / 清空 / 仅有变更
    资源包列表 / 数量

  右侧：替换候选列表
    顶部候选工具条：搜索、匹配状态、资源类型、只看已选、排序
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
- 左侧不得同时承载资源包、资源类型和匹配状态三组长列表。资源包数量可能非常大，左侧只保留资源包范围选择；资源类型、匹配状态和候选搜索放到右侧候选列表顶部工具条。
- 启发式模式下，来源区必须显示清晰但不夸张的风险提示：没有 manifest 基线，匹配基于目录、类型和名称推断，默认只选择高置信唯一匹配项；多候选、低置信和类型不一致项必须默认排除。

交互要求：

- 扫描 / 计划刷新期间，目录选择以外的筛选和列表滚动可用，`开始替换` 禁用。
- 快速切换筛选不应重新扫描磁盘；筛选只作用于已生成计划。
- 切换目录、重新读取 manifest 或点击刷新时，取消旧扫描请求，并用请求代次丢弃过期结果。
- 用户勾选 / 取消候选只改变本地选择状态，不触发重新 hash。
- manifest 精确匹配计划生成后，候选区默认展示 `已修改`；如果没有已修改候选，则回退展示全部状态。
- 资源包范围中的 `仅已变更` 只过滤左侧资源包列表显示，不隐式取消隐藏资源包的选择，避免破坏连续选择状态。
- 资源类型筛选支持多选；未勾选任何类型时等价于 `全部类型`。

### 4.1 设计草图

默认窗口尺寸作为批量替换、批量提取、工作区导出三类独立批量窗口的统一基准，宽屏下建议约 `1520 x 920`，最小尺寸不低于 `1180 x 760`。草图中的卡片只表示视觉分组，不表示可以继续嵌套额外卡片。

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│  [folder] 批量替换                                                       [_] [□] [×]        │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                              │
│  批量替换                                                                                    │
│  从导出目录回读已修改文件，并应用到当前工作区。                                                │
│                                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ 来源目录                                                                               │  │
│  │ C:\Users\...\Downloads\提取测试                                    [选择目录] [刷新]     │  │
│  │                                                                                        │  │
│  │ [Manifest 精确匹配]  _abt-export-manifest.json · 11 个资源包 · 960 项 · 33 项已修改       │  │
│  └────────────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                              │
│  ┌───────────────────────────────┐  ┌─────────────────────────────────────────────────────┐  │
│  │ 资源包范围                    │  │ 替换候选                                            │  │
│  │ 选择参与批量替换的资源包。      │  │ 只默认勾选 manifest 中确认已修改的文件。              │  │
│  │                               │  │                                                     │  │
│  │ ┌───────────────────────────┐ │  │ ┌─────────────────────────────────────────────────┐ │  │
│  │ │ 🔍 搜索资源包名称或路径     │ │  │ │ 🔍 搜索候选资源    [已修改 ▾] [类型 ▾] [只看已选] │ │  │
│  │ └───────────────────────────┘ │  │ └─────────────────────────────────────────────────┘ │  │
│  │ [全选] [清空] [仅有变更]       │  │ ┌─────────────────────────────────────────────────┐ │  │
│  │                               │  │ │ ☑ char_452_bstalk_snow#4 [1]        已修改      │ │  │
│  │                               │  │ │   char_452_bstalk.ab · Texture2D                 │ │  │
│  │                               │  │ │   Texture2D\char_452_bstalk_snow#4 [1].png       │ │  │
│  │                               │  │ ├─────────────────────────────────────────────────┤ │  │
│  │                               │  │ │ ☐ char_452_bstalk_snow#4 [2]        未变化      │ │  │
│  │                               │  │ │   char_452_bstalk.ab · Texture2D                 │ │  │
│  │                               │  │ │   Texture2D\char_452_bstalk_snow#4 [2].png       │ │  │
│  │                               │  │ ├─────────────────────────────────────────────────┤ │  │
│  │                               │  │ │ ☑ char_452_bstalk_unveiling#4 [1]   已修改      │ │  │
│  │                               │  │ │   char_452_bstalk.ab · SpineTexturePage          │ │  │
│  │                               │  │ │   Spine\char_452...\char_452_bstalk....png       │ │  │
│  │                               │  │ └─────────────────────────────────────────────────┘ │  │
│  │ 资源包                        │  │                                                     │  │
│  │ ☑ char_452_bstalk.ab   33     │  │                                                     │  │
│  │ ☐ char_1001_amiya2.ab  0      │  │                                                     │  │
│  └───────────────────────────────┘  └─────────────────────────────────────────────────────┘  │
│                                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ [document] 计划摘要                                                                     │  │
│  │ 已选 33 项可替换资源 · 2 个资源包 · 0 个问题项                                           │  │
│  │                                                        [取消] [开始替换]                 │  │
│  └────────────────────────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

扫描态草图：

```text
来源目录卡片
  C:\...\提取测试                                      [选择目录] [刷新]
  [Manifest 精确匹配] 正在生成替换计划...

主体区
  左侧资源包范围保持可操作，但每包候选计数显示为上一次结果或 loading 占位。
  右侧候选工具条可操作，候选列表显示轻量 loading 行，不清空用户已经看到的旧结果，直到新计划完成。

底部动作条
  计划摘要  正在比较文件大小与 MD5...
  [取消] [开始替换 disabled]
```

manifest 异常态草图：

```text
来源目录
  C:\...\SomeExport                                  [选择目录] [刷新]
  [Manifest 异常] workspaceKey 与当前工作区不一致

主体区
  右侧显示只读诊断：
    manifest 工作区：abc...
    当前工作区：def...
    该目录不能直接用于当前工作区的精确替换。

底部动作条
  计划摘要  无可执行替换项
  [取消] [开始替换 disabled]
```

执行完成态草图：

```text
底部动作条
  计划摘要  已应用 31 项 · 跳过 2 项 · 失败 0 项
  [关闭]

候选列表
  已执行项的状态从“已修改”切为“已应用”，失败项保留错误诊断和来源路径。
```

启发式模式草图：

```text
来源目录
  C:\...\ExternalFiles                                [选择目录] [刷新]
  [启发式匹配] 未找到 _abt-export-manifest.json · 基于目录、类型和名称推断 · 需要人工确认

主体区
  左侧资源包范围
    [全选] [清空] [仅有匹配]
    ☑ char_452_bstalk.ab       高置信 12 · 需确认 3
    ☐ char_1001_amiya2.ab      无匹配

  右侧候选工具条
    🔍 搜索候选资源    [高置信 ▾] [类型 ▾] [只看已选] [置信度排序]

  候选列表
    ☑ char_452_bstalk_snow#4 [1]       高置信
      Texture2D · 文件名和类型唯一匹配
      Texture2D\char_452_bstalk_snow#4 [1].png

    ☐ char_452_bstalk_snow#4 [2]       多候选
      Texture2D · 找到 2 个同名目标，需要手动选择目标
      Texture2D\char_452_bstalk_snow#4.png

    ☐ char_452_bstalk.atlas            类型不一致
      TextAsset · 目标类型或扩展名不符合替换规则

底部动作条
  计划摘要  高置信 12 项 · 需确认 3 项 · 已选 12 项
  [取消] [开始替换]
```

布局比例建议：

- 来源目录卡片高度约 `112-136px`，只放路径、选择按钮、匹配模式和 manifest 摘要。
- 主体区占窗口主要高度，左侧资源包范围宽度约 `320-380px`，右侧候选列表占剩余空间。左侧只允许一个长列表滚动；资源类型和匹配状态不能放在左侧长列表里。
- 右侧候选工具条高度约 `48-56px`，承载候选搜索、匹配状态下拉、类型下拉、只看已选和排序。资源类型超过 `6` 个时使用多选下拉；少量常用状态可以用 segmented control 或 chip。工具条允许横向自适应换行到两行，但不得压缩候选列表到不可用高度。
- 底部动作条高度约 `96-116px`，与批量提取计划摘要保持同一质感和按钮尺寸。
- 候选列表每行约 `72-88px`，问题项可以增高到 `96px` 展示一行诊断，不使用展开大卡片作为默认形态。

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

启发式匹配的目标不是“自动猜中所有资源”，而是在缺少稳定导出基线时尽量找出可人工确认的候选。实现必须分阶段生成候选，并保留每一步的证据和风险等级。

输入目录形态：

- 批量提取标准目录：
  - 普通资源：`<AB>/<类型>/<文件>`
  - Spine 组：`<AB>/Spine/<组名>/<文件>`
- 单个 AB 导出目录：
  - 当用户直接选择 `<AB>` 目录时，普通资源允许使用 `<类型>/<文件>`，Spine 组允许使用 `Spine/<组名>/<文件>`；此时来源目录名作为隐式 AB 上下文参与匹配。
- 单个资源类型目录：
  - 当用户直接选择 `<AB>/<类型>` 目录时，普通资源允许使用 `<文件>`；此时来源目录名作为隐式资源类型上下文，父目录名作为隐式 AB 上下文参与匹配。
- 单个 Spine 分类目录：
  - 当用户直接选择 `<AB>/Spine` 目录时，Spine 组允许使用 `<组名>/<文件>`；此时父目录名作为隐式 AB 上下文，第一段相对路径作为 Spine 组名参与匹配。
- 单个 Spine 组目录：
  - 当用户直接选择 `<AB>/Spine/<组名>` 目录时，Spine 组允许使用 `<文件>`；此时当前目录名作为隐式 Spine 组名，祖父目录名作为隐式 AB 上下文参与匹配。
- 用户手工整理目录：
  - 允许缺少 `<AB>` 层或 `<类型>` 层，但置信度必须降低。
  - 不允许递归扫描无限深目录；默认只扫描到 `4` 层，超过深度的文件归为 `IgnoredTooDeep`。

候选生成阶段：

1. 文件枚举
   - 排除 `_abt-export-manifest.json`、隐藏文件、临时文件和不支持扩展名。
   - 对每个文件记录 `relativePath / fileName / extension / parentSegments / size`。
2. 类型推断
   - 根据目录类型段、扩展名和现有 `ReplacementFileTypeCatalog` 推断可替换目标类型。
   - 目录类型和扩展名冲突时，标记 `TypeConflict`，默认不选中。
3. Bundle 范围推断
   - 如果路径第一段能唯一匹配当前工作区 bundle 显示名或 bundle 路径，限定在该 bundle 内查找。
   - 如果不能唯一匹配，允许跨工作区查找，但置信度降低，并在 UI 中显示 `跨资源包推断`。
4. 名称归一化
   - 对文件名和目标资源显示名做同一套归一化：去扩展名、去非法路径转义、忽略大小写、规范空白。
   - ` [n]` 后缀只作为弱证据：如果同名目标存在多个，` [n]` 可用于排序候选，但不能单独把候选升级为确定匹配。
   - `#`、空格、下划线等原始名称字符保留参与匹配，不做激进模糊化，避免把相近角色名误并到一起。
5. 目标候选排序
   - 优先同 bundle、同类型、归一化名称完全相同。
   - 其次同 bundle、同类型、去 ` [n]` 后名称相同。
   - 再次跨 bundle、同类型、名称完全相同。
   - 不做编辑距离自动匹配；相似名称只可作为后续手动搜索辅助，不生成默认候选。
6. 置信度判定
   - `HighConfidence`：同 bundle、同类型、名称唯一完全匹配。
   - `MediumConfidence`：同 bundle、同类型、去 ` [n]` 后唯一匹配，或标准目录缺失类型但扩展名唯一兼容。
   - `LowConfidence`：跨 bundle 唯一匹配、目录层级不标准、或只靠弱名称证据。
   - `Ambiguous`：存在多个同分候选。
   - `Rejected`：类型不兼容、目标不存在、扩展名不支持、路径过深或来源文件不可读。

启发式诊断：

- `NameMatched`
- `NameMatchedAfterSuffixNormalization`
- `BundleMatched`
- `BundleInferred`
- `CrossBundleInferred`
- `Ambiguous`
- `MissingTarget`
- `TypeMismatch`
- `TypeConflict`
- `UnsupportedType`
- `IgnoredTooDeep`
- `SourceUnreadable`
- `Untrusted`

启发式默认选择规则：

- `HighConfidence` 默认选中。
- `MediumConfidence` 默认不选中，除非用户打开“允许中等置信匹配默认选中”的高级选项；第一版不建议提供该高级选项。
- `LowConfidence / Ambiguous / Rejected` 默认不选中。
- 执行前如果仍存在已选中的 `MediumConfidence / LowConfidence` 项，确认按钮应触发二次确认摘要，而不是直接执行。

启发式模式没有导出基线，不做 size / MD5 未变化过滤；它只发现可能匹配项，并要求用户确认。即使用户选择了启发式候选，执行阶段仍必须重新做目标存在、来源可读和类型兼容校验。

### 5.3 启发式 UI 规则

启发式模式 UI 必须让风险始终可见，但不把窗口变成警告墙。

来源区：

- 显示 `启发式匹配` 标签。
- 显示一句短风险说明：`未找到 manifest，匹配基于目录、类型和名称推断。`
- 显示扫描摘要：`高置信 / 需确认 / 无法匹配 / 已排除`。

左侧资源包范围：

- 每个资源包显示该包内 `高置信 / 需确认 / 问题项` 数量。
- 提供 `仅有匹配` 快捷按钮，隐藏完全无候选资源包。
- 资源包选择只决定候选显示和执行范围，不重新扫描磁盘。

右侧候选工具条：

- 匹配状态筛选至少包含：`高置信`、`需确认`、`多候选`、`已排除`、`全部`。
- 类型筛选继续使用多选下拉。
- 排序默认按 `风险优先`：已选高置信、需确认、多候选、错误、未选无匹配。

候选列表：

- 高置信项可用普通行样式。
- 需确认项在状态列显示原因，例如 `中等置信`、`跨资源包推断`。
- 多候选项必须提供目标选择入口；用户未选择唯一目标前不能勾选执行。
- Rejected 项只读展示，不能勾选。

底部动作条：

- 摘要必须区分 `已选可执行` 与 `需确认 / 无法执行`。
- 如果用户选中了非高置信项，开始替换按钮旁显示轻量提示，并在执行前弹出二次确认。

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
  workspaceDataRootPath
  sourceRootDirectory
  matchingMode
  candidates[]                 // UI 已确认的候选快照
  progress?
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
  sourceRootDirectory
  matchingMode
  requestedCandidateCount
  replacedAssetCount
  skippedAssetCount
  failedAssetCount
  processedBundleCount
  processedSpineGroupCount
  completedAtUtc
  itemResults[]
```

设计要求：

- Application 负责读取 manifest DTO、匹配工作区目标、做 size-first / MD5 判定、执行类型兼容校验。
- Infrastructure 负责 manifest 文件读取、外部文件大小 / hash 读取和文件内容载荷读取。
- Presentation 不直接解析 manifest、不直接计算 MD5、不直接扫描 AB。
- 执行时仍逐项复用单资源 `ReplaceAssetFromFileHandler` 并追加真实 `ReplaceFromFile` 操作记录；Spine 组替换要拆成真实组成资产的替换记录，并共享同一 `batchId / relationId`。
- 当前真实执行范围为 `.atlas`、`.skel`、`.json` 文本组成文件、`.png` 图片资源、`.wav/.mp3/.ogg` 音频资源，以及由这些组成文件构成的 Spine 组候选；其他候选只进入跳过结果，不写替换操作记录。
- 候选区对同一 `SpineGroupId`、同一状态的组成文件采用组级展示和 `Spine` 类型筛选；用户勾选该组级候选时，提交阶段展开为组内真实组成资产候选。
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
  SemanticKind = WorkspaceMutation
  Priority = Visible
  WorkspaceId = current workspace
  WorkspaceKey = current workspace key
  CanCancel = true
```

执行策略：

- 单个后台任务承载整批替换。
- 同一 AB 内顺序执行，避免第三方 loaded-session 对象并发访问风险。
- 不同 AB 可以受控并行，但第一版可先顺序执行，等替换写入、通知聚合和失败恢复稳定后再扩展并发。
- 每个候选执行前重新校验目标仍存在、目标仍允许文件替换、类型仍兼容；未实现的目标类型或来源扩展名进入 `Skipped` 结果。
- 用户取消后保留已成功追加的操作事实，并在结果中标记 `Cancelled`；不回滚已经完成的替换操作记录。

## 8. 操作记录、通知和预览刷新

操作记录：

- 每个成功替换的真实目标资产追加 `ReplaceFromFile`。
- 同一次批量替换使用统一 `batchId`。
- Spine 组候选拆成组成资产记录，不追加“Spine 组本身”的虚假替换记录。
- 批量替换结果摘要可以作为批处理历史展示，但不替代真实资源级操作事实。

通知：

- 成功执行后发布带 `Reason = BatchReplacementApplied` 的工作区操作通知。
- 通知必须携带已替换资源的 affected targets，并通过统一关联资源影响注册表展开隐式受影响目标；跳过项、未变化项和失败项不进入 affected targets。
- 对 Spine 组成文件同批替换，通知层按组聚合，Presentation 先刷新轻量操作投影，再合并富预览刷新。
- `Unchanged`、跳过项和失败项不应触发目标预览刷新。

预览刷新：

- 批量替换窗口本身不直接重载资源预览。
- 资源详情、独立窗口和 Spine 组预览通过现有 `WorkspaceOperationChangeRefreshCoordinator` 处理。
- 不允许为了批量替换在 ViewModel 中手动读写 `staging/changes` 或直接刷新关联资源。

### 8.1 资产列表拖入替换快捷入口

资产列表允许把外部文件直接拖到普通资产行，或把单个 Spine 组导出目录拖到 Spine 组行。该入口是现有单资产 / Spine 编辑替换链路的 Presentation 快捷方式，不引入新的操作类型、匹配事实或持久化状态。

交互与路由约束：

- 只有指针实际落在已实现的资产行或 Spine 组行内才进入替换；列表空白、表头和其他窗格继续沿用原有行为。
- 普通模式使用 WinUI `StorageItems`，管理员兼容模式使用统一 `WM_DROPFILES` 精确目标路由；两种入口必须生成相同的“目标行 + 来源路径”请求。
- 管理员模式下，资产行目标使用 `Item` 优先级，高于资源包窗格的工作区导入 `Surface` 目标，避免替换来源被误导入为新 AB。
- 普通资产行必须按原始拖入形态分流：直接拖入唯一普通文件时，命中的行就是确定目标，不要求来源文件名与资源名完全一致，只校验目标资源类型、文件基础格式和必要的语义角色；拖入目录、多个文件或文件与目录混合时，命中的行只作为入口落点，Application 层应展开来源，并按文件名把来源匹配到当前资产列表中每一个可见普通资产。单个目录即使最终只展开出一个文件，也不得降级为精确目标替换。目录枚举、来源形态判定、名称键、资源类型兼容性和冲突判断不得复制到 Presentation。Spine 组继续使用现有目录 / 多文件混合匹配查询和组内组成角色规则，不因普通资产入口扩展而改变。
- 普通资产精确目标替换的基础格式规则沿用单资产文件替换能力：Texture2D / Sprite 接受受支持的图片格式，AudioClip 接受受支持的音频格式，Mesh、Material 等按既有类型目录校验；Spine skeleton 组成资产允许 `.skel / .bytes / .json / .txt` 进入真实 payload 格式校验，atlas 组成资产只接受 `.atlas / .txt` 文本来源，其他 TextAsset 只接受既有文本类来源。精确目标模式不得仅因文件名不同而拒绝，也不得因扩展名可接受而绕过 skeleton JSON / binary 一致性检查。
- “全部类型”列表中的普通资产必须同时使用原始名称、列表显示名和导出文件名进行匹配，并继续服从资源类型兼容表。同类型重名资产沿用列表和批量提取生成的稳定 `[1]`、`[2]` 后缀，带编号来源只能命中相同编号目标；未带编号的同名来源若可命中多个目标，应进入结构化冲突。不同类型同名且扩展名不同时（例如 PNG / WAV）可分别确定匹配；Texture2D / Sprite 等共享同一来源扩展名时，不得仅凭类型猜测目标。
- 普通资产多来源匹配中，同一目标资产命中多个兼容来源，或同一来源文件命中多个重名目标时，整次匹配必须返回包含目标名称和冲突来源路径的结构化冲突计划；Presentation 应展示哪些文件发生冲突并立即结束本次操作，不显示替换确认，也不得按拖放顺序、目录顺序或扩展名优先级静默选取来源。
- Spine skeleton 的兼容性以真实 payload 为准：目标必须通过既有预览管线的 `Original` 内容范围读取原始 AB 载荷，来源则通过外部文件预览端口识别为 JSON 或 binary；只有来源格式与原始目标格式一致时才能形成候选并最终写入。已应用文件替换、工作区替换或文本编辑不得改变后续替换的目标格式基线；`.skel / .json / .bytes / .txt` 只参与文件选择和名称匹配，不能作为格式判据。当前官方 runtime 适配器只承担读取与播放，不提供可证明无损的跨版本 serializer，因此不得隐式执行 JSON 与 binary 互转。
- 普通资产列表中已被 Spine 组识别为 skeleton 组成资产的 `TextAsset` 同样必须复用上述真实 payload 格式兼容规则；匹配查询应按目标 Bundle 主动取得 Spine 组目录，不能依赖用户是否已切换过 Spine 组视图。来源后缀不能绕过 binary / JSON 一致性检查。
- 对名称本身带语义后缀的普通 `TextAsset`，名称匹配必须保留该后缀边界，例如 `foo.atlas` 不得因去后缀后与 `foo.skel` 同名而形成候选；只有已经确认是 skeleton 组成资产时，才允许 `.skel / .json / .bytes / .txt` 之间跨后缀进入真实 payload 格式校验。
- 路径形态不符合目标要求、普通资产没有名称与类型均兼容的来源、skeleton 内容格式不一致，或 Spine 来源没有名称与内容格式均兼容的组成资源时，不显示替换确认，也不写操作记录。

确认与执行约束：

- 无冲突候选校验或 Spine 目录匹配完成后，必须显示本地化确认对话框；普通资产单项显示目标名和来源文件名，多项显示命中资产数量，Spine 组同时显示命中的组成资源数量。
- 用户取消时不得应用替换、不得追加 `ReplaceFromFile`，也不得遗留 Spine 编辑草稿。
- 用户确认后，普通资产逐项复用 `GetFileReplacementCandidate / ReplaceAssetFromFile`；Spine 组逐组成资源复用相同链路并共享现有 `batchId / relationId`。
- 成功后的列表状态、详情、关联资源和 Spine 预览只通过现有 workspace operation notification 与读模型重读刷新，拖放处理器不得手动读写 `staging/changes`。
- 从资产列表拖出后再把生成文件或 Spine 组目录拖回同一目标，应形成完整的提取 -> 用户确认 -> 替换闭环；拖出临时目录的保留期规则保持不变。

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

第一阶段：窗口与 manifest 计划（已完成）

- 新增批量替换窗口和 ViewModel。
- 新增 manifest 计划查询，支持目录选择、manifest 回读、size-first / MD5 判定、默认选择状态和诊断。
- 当前进度：已完成主工作台 `批量替换` 入口、独立窗口、目录选择、后台 `TransientRead` 计划扫描、manifest 工作区 key 校验、size-first / MD5 变更判定、默认选择状态、中英本地化，以及候选区搜索 / 默认已修改状态 / 多选资源类型 / 资源包范围仅已变更的本地过滤；manifest 缺失时已接入第一版启发式计划，按文件枚举、类型推断、资源包推断、名称归一化和置信度生成 `HighConfidence / MediumConfidence / LowConfidence / Ambiguous / Rejected` 候选，并在 UI 显示启发式风险提示、状态筛选和高置信默认选择。
- 旧批处理页的批量替换预览 query / handler / planner 已从 Application 网关移除，旧页不再承担批量替换语义；真正入口为工作台级批量替换窗口。

第二阶段：执行与操作记录（已接入第一版）

- 已移除旧批量替换预览 query / handler / planner 及其批处理页执行入口，避免两套不同含义的“批量替换”并存。
- 已新增批量替换执行命令，命令携带 UI 已确认候选快照。
- 已逐项复用单资源文件替换能力，并通过现有记录器写入 `ReplaceFromFile` 操作记录。
- 已按“普通候选先执行、Spine 组候选后执行”的顺序处理；Spine 组候选拆成组成资产替换，并共享批次关系和 Spine 组关联关系。
- 已让批量替换候选区将 manifest / 启发式计划中的 Spine 组成文件按组展示，类型筛选显示独立 `Spine` 选项，提交时再展开为真实组成资产执行。
- 已让主工作台资产列表支持行级拖入替换：普通资产通过 WinUI `StorageItems` 或管理员 `WM_DROPFILES` 精确行路由接收文件、目录及两者混合来源；直接拖入唯一文件时按命中行做类型 / 语义兼容校验后替换，目录、多文件和混合来源则由 Application 展开后按当前资产列表逐项匹配、检测双向重名冲突并校验 skeleton 真实载荷格式。Spine 组通过同一路由继续复用现有目录 / 多文件角色匹配。两者都在用户确认后才调用现有 `ReplaceFromFile` 链路。
- 已接入 `BatchReplacement` 可见后台任务、进度、完成摘要和操作通知聚合。
- 当前仍未开放低置信/多候选的目标手动确认执行页；这些候选若被用户勾选，会按支持范围执行或进入跳过结果。
- 批处理任务页中的旧批量替换卡片、旧 source kind 和旧摘要 ViewModel 已移除；工作台级批量替换窗口是唯一入口。

第三阶段：启发式回退增强

- 当前已在 manifest 缺失时提供保守的名称 / 目录 / 类型兼容匹配。
- 当前已支持用户选择批量导出根目录、单个 AB 导出目录、AB 下的单个资源类型目录、单个 Spine 分类目录或单个 Spine 组目录；单个 AB 目录会把目录名作为隐式 AB 上下文，单个资源类型目录会把当前目录名作为隐式类型、父目录名作为隐式 AB 上下文，Spine 下级目录会保留隐式 AB、组名和 `Atlas / Skeleton / TexturePage` role 推断。
- 当前已明确 `Ambiguous / TypeConflict / UnsupportedType / IgnoredTooDeep / MissingTarget` 等诊断，高风险项默认不选中。
- 后续增强多候选的目标手动选择、启发式候选执行前二次确认、来源文件不可读细分诊断和更完整的低置信搜索辅助。

第四阶段：结果与历史

- 批处理页改为展示最近批量替换结果、失败明细和可恢复操作摘要。
- 补充来源缺失、来源变化、取消后恢复等诊断。

## 11. 测试建议

Application 测试：

- manifest 工作区 key 不匹配时阻止执行。已覆盖。
- manifest 精确匹配按 `bundleId + assetId` 找到普通资源。已覆盖基础目标存在校验。
- Spine manifest 项按 `bundleId + groupId + role + assetId` 生成组成资产候选。
- 文件大小不同直接进入 `Changed`，不计算 MD5。已覆盖。
- 文件大小相同且 MD5 相同进入 `Unchanged`，不默认选中。已覆盖。
- 文件大小相同且 MD5 不同进入 `Changed`。已覆盖。
- 缺失文件、hash 失败、类型不兼容返回结构化诊断。
- manifest 缺失时，标准导出目录中的同资源包 / 同类型 / 同名唯一项进入 `HighConfidence` 并默认选中。已覆盖。
- manifest 缺失时，同分同名目标进入 `Ambiguous` 且默认不选中。已覆盖。
- 执行只对选中且支持的 `Changed / HighConfidence / MediumConfidence / LowConfidence` 候选追加替换操作记录。已覆盖支持与跳过混合结果。
- 同一批次的替换记录共享 `batchId`；Spine 组成资产共享 Spine 组 `relationId`。

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

截至当前实现，主工作台新窗口已经接入 manifest 计划读取、第一版启发式回退计划和真实替换执行。真实执行会复用单资源文件替换能力写入 `ReplaceFromFile` 操作记录，并通过聚合通知触发工作区投影和预览刷新；旧批处理页骨架已经移除，后续批量替换开发必须继续基于本文定义的 manifest / heuristic 计划模型和真实资源级操作记录链路推进。
