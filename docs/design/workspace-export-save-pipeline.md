# AssestBundleTools 工作区真实导出与保存重建设计

## 1. 文档目的

本文档定义工作区导出窗口进入真实 AB 输出后的应用层流程、后台任务边界、多线程策略、导出资源处理器执行协议、AB 写包并发策略，以及输出完成后写出的 AB 导出修改清单 JSON。

这里的“工作区导出”指用户在当前工作区中选择已修改资源包，应用工作区替换 / 编辑 / 资源处理器结果后，保存为新的 AssetBundle 文件。它与普通资源批量提取不同：批量提取输出 PNG / 文本 / OBJ / JSON 等外部资源文件，当前真实导出输出的是修改后的 AB 文件。

本文补齐 M10 真实保存与重建前置契约。实现阶段应优先对照本文档，避免把真实写包、CLI 资源处理、多线程 worker 和导出后 manifest 分散到 Presentation 或临时服务中。

## 2. 当前状态

已具备：

- 工作区导出窗口已经能展示保存预览中的资源包计划。
- 工作区导出窗口已经能按 `Texture2D / Audio / Atlas / Skel` 读取真实资源处理器 catalog，并按资源类型槽选择处理器。
- 保存预览和保存执行骨架已经读取现有 `cache/bundles` 标准化 artifact 输入图，并通过 `IWorkspaceSaveOutputProbe` 做输出路径 preflight。
- `SaveRebuild` 后台 profile 已存在，工作区导出窗口可以复用统一后台任务调度。
- 普通批量提取已经具备“单个后台任务 + 跨 AB 受控并行 + 同 AB 内顺序 + 输出 manifest”的成熟范式。
- 工作区导出窗口已接到真实 `SaveRebuild` 后台任务：默认输出到 `<workspaceDataRootPath>/exports`，取消“导出到工作区输出目录”后点击开始导出会打开输出目录选择器。
- `SaveWorkspaceAsNewBundleCommand` 已携带输出目录、选中资源包、资源处理器选择、工作区 artifact 上下文和执行进度回调。
- Application 已通过 `IWorkspaceBundleExporter` / `IWorkspaceBundleExportManifestStore` 编排第一阶段真实导出：按选中 bundle 有界并发写出，汇总 `Succeeded / PartiallySucceeded / Failed` 结果，并在有成功输出时写出 `_abt-bundle-export-manifest.json`。
- Application 已定义 `IWorkspaceResourceProcessorExecutor` 和处理器执行 DTO；保存执行会校验资源处理器选择、构建文件替换载荷的资源处理 work item、按有界并发执行、稳定排序结果，并在处理器失败时阻断所属 bundle 写包。
- Infrastructure 已接入外部 CLI 资源处理器执行：按 catalog 中的 `executablePath / processArguments / timeoutMs` 启动进程，展开占位符，解析 result JSON，校验输出路径范围，捕获诊断并支持取消时终止进程。
- Infrastructure 已把单 bundle writer 从 artifact 文件级复制推进到首版对象级 AssetsTools 写包：从标准化 artifact 独立加载 AB，按 `serializedFileName + pathId` 定位对象，消费文件替换、同 bundle 工作区内替换、Sprite 几何编辑和资源处理器输出载荷，先写入带 replacer 的未压缩 bundle，再按原压缩方式重新打包、提升输出文件并记录 MD5 / SHA-256。
- AB 导出 manifest 已记录每个 bundle 的资源处理器结果摘要，包括资源类型、处理器 id、状态、输入 / 成功 / 失败计数、result JSON 路径和诊断。

尚未实现：

- 资源处理器输入目前只覆盖文件替换载荷；工作区内替换、编辑草稿和后续有效载荷端口仍需接入同一资源处理计划。
- 对象级 writer 首版只覆盖 `TextAsset` 文本载荷、`Texture2D` 图片载荷、同 bundle 工作区内对象复制和 Sprite `m_Rect / m_Pivot` 几何写入；跨 bundle 工作区内替换、AudioClip / Mesh / Material / MonoBehaviour 等更多对象类型和更完整的格式转换仍需后续扩展。
- 保存检查点、部分成功和取消 / 回滚语义。

## 3. 设计结论

真实导出采用一个可见 `SaveRebuild` 后台任务，Application 在该任务内部继续拆分受控 worker：

```text
WorkspaceExportWindow 收集选择
  -> ExecuteWorkspaceExport / SaveWorkspaceAsNewBundle command
  -> Application BuildPlan
  -> SaveExecutionStarted 操作记录
  -> 资源处理器阶段，跨 bundle / resource item 受控并行
  -> AB 写包阶段，跨 bundle 受控并行，同 bundle 单 writer
  -> 输出 AB 导出修改清单 JSON
  -> SaveExecutionCompleted / Failed 操作记录与检查点
  -> Presentation 消费任务结果；检查点落地后再广播已提交操作通知
```

核心约束：

1. Presentation 只收集资源包范围、输出目录和资源处理器选择，不运行 CLI、不读写 AB、不生成 manifest。
2. Application 负责编排计划、操作记录、进度、取消、失败分类、检查点和稳定 DTO。
3. Infrastructure 负责外部进程执行、result JSON 解析、AB Reader / Writer、临时文件、原子写出和 manifest 文件 IO。
4. 同一资源包内写包必须串行；不同资源包可以并行。
5. 资源处理器可以跨资源包和资源项并行，但必须使用有界并发、独立 staging 路径和结构化结果。
6. 批量导出以“单 bundle 原子、整批可部分成功”为第一版策略；失败 bundle 不标记为已提交检查点。
7. 新增 AB 导出 manifest 与普通资源提取的 `_abt-export-manifest.json` 分开，避免两个场景互相误读。

## 4. 分层职责

### 4.1 Presentation

Presentation 负责：

- 在工作区导出窗口展示保存预览、资源包范围和资源处理器选择。
- 生成命令输入 DTO：选中的 `bundleId[]`、输出目录、每个资源类型槽的 `processorId?`。
- 提交一个 `BackgroundWorkRequest<Result<WorkspaceExportExecutionResultDto>>`。
- 使用 `BackgroundWorkProfile.SaveRebuild`、`WorkspaceMutation` 语义和可取消任务。
- 将 Application 返回的结构化结果格式化为本地化摘要、toast、日志和窗口状态。

Presentation 不负责：

- 解析 `ResourceProcessorManifest` 或执行外部进程。
- 检查输出文件大小 / hash。
- 生成 AB 导出 manifest。
- 直接修改工作区操作记录或缓存文件。

### 4.2 Application

Application 负责：

- 扩展保存 / 导出命令契约，携带资源包范围、资源处理器选择和输出策略。
- 构建真实导出计划，包括 artifact 输入、变更操作、有效替换载荷、资源处理项、写包项和输出 manifest 项。
- 校验资源处理器选择是否仍可用、是否支持目标资源类型。
- 编排资源处理阶段和写包阶段的有界并发。
- 统一进度 DTO、取消语义、失败分类和 `Result / AppError`。
- 追加 `SaveExecutionStarted / SaveExecutionCompleted / SaveExecutionFailed` 操作记录。
- 成功写包后按 bundle 建立保存检查点，只把已成功输出 bundle 中被消费的操作标记为 `Committed`。

Application 不负责：

- 直接访问文件系统。
- 直接 `Process.Start`。
- 直接调用 WinUI 调度器、窗口或本地化服务。

### 4.3 Infrastructure

Infrastructure 负责：

- 基于 catalog manifest 执行外部 CLI 资源处理器。
- 为每次处理器调用创建唯一 `inputPath / outputPath / resultPath` staging 文件。
- 解析处理器 result JSON，捕获 stdout / stderr 作为诊断明细。
- 通过真实 AB Reader / Writer 消费标准化 artifact、有效替换载荷和处理器输出，生成新的 AB 文件。
- 写临时输出、验证大小 / hash、按策略原子提升到最终路径。
- 原子写出 `_abt-bundle-export-manifest.json`。

Infrastructure 不负责：

- 决定哪些工作区操作被提交为检查点。
- 决定用户是否覆盖已有输出文件。
- 拼接用户可见主提示文案。

## 5. 应用层命令与 DTO

当前先复用 `SaveWorkspaceAsNewBundleCommand` 作为工作区导出执行命令，后续如果引入独立 `ExecuteWorkspaceExportCommand`，也应委托到同一执行服务。当前 / 目标字段如下：

```text
SaveWorkspaceAsNewBundleCommand / ExecuteWorkspaceExportCommand
  workspaceId
  workspaceKey
  workspaceDataRootPath
  outputDirectory
  selectedBundleIds[]
  processorSelections[]
  progress
  bundleWriteConcurrency?
  outputConflictPolicy

WorkspaceExportProcessorSelectionDto
  resourceKind
  processorId?          # null 或空表示无处理器

WorkspaceSaveExecutionResultDto / WorkspaceExportExecutionResultDto
  workspaceId
  outputDirectory
  status                # Succeeded / Partial / Failed / Cancelled
  plannedBundleCount
  succeededBundleCount
  failedBundleCount
  skippedBundleCount
  processedResourceCount
  writtenBundleCount
  exportManifestPath?
  bundleResults[]
  diagnostics[]
```

`selectedBundleIds[]` 必须来自保存预览中的稳定 `BundleId`，不得用显示名或输出路径作为事实身份。`processorSelections[]` 必须按资源类型槽表达；导出窗口里的“无处理器”在命令中应投影为 `processorId = null`，不要把 UI 内部的 `texture2d:none` 伪 id 持久化到 Application。

输出冲突策略第一版建议：

```text
OutputConflictPolicy
  FailIfExists        # 默认，保留当前 preflight blocker 语义
  ReplaceConfirmed    # 用户已确认覆盖，写临时文件成功后再替换
```

真实写包入口不得在未确认覆盖时覆盖已有 AB 文件。即使用户确认覆盖，也必须先写临时文件并验证成功，再替换最终路径。

## 6. 导出计划

真实导出计划构建分为五步：

```text
ValidateWorkspace
  -> ResolveSelectedBundles
  -> ResolveArtifactInputs
  -> ResolveEffectiveChanges
  -> ResolveProcessorAndWriterWorkItems
```

### 6.1 资源包范围

- 默认只导出用户勾选的资源包。
- 如果勾选资源包中没有待保存操作，应保留在计划摘要中并标记为 `SkippedNoChanges`，不进入写包 worker。
- 如果某个资源包缺少标准化 artifact，计划阶段标记为阻断诊断；第一版真实执行不在写包阶段隐式触发解密 / materialize。

### 6.2 有效变更输入

导出计划必须从 Application 的工作区操作事实流和物化读模型读取当前有效变更：

- 文件替换。
- 工作区内替换。
- Sprite 编辑几何。
- 后续可落盘的编辑草稿。
- 资源处理器输出。

预览缓存、loaded-session 命中和 Shell 任务状态不能作为保存事实来源。预览 payload 可以作为某些资源的导出 / 替换中间格式来源，但必须通过 Application 的有效载荷端口显式进入计划。

### 6.3 Staging 目录

建议为每次真实导出创建稳定 `exportOperationId`，并使用隔离 staging：

```text
<workspaceDataRootPath>/staging/exports/<exportOperationId>/
  resource-processing/
    <bundleId>/<resourceKind>/<assetId>/
      input
      output
      result.json
  bundle-write/
    <bundleId>/
      output.tmp
  diagnostics/
```

最终 AB 临时文件可以先写在 staging，再移动到输出根目录；如果目标文件系统不支持跨卷原子移动，Infrastructure 应优先在最终输出目录旁创建同卷临时文件：

```text
<outputDirectory>/.abt-tmp/<exportOperationId>/<bundleFileName>.tmp
```

成功提升后的 staging 可清理；失败和取消时可以保留带 operation id 的诊断 staging，后续再由缓存 / 维护任务清理。

## 7. 资源处理器阶段

资源处理器阶段发生在“应用工作区有效变更后、AB 写包前”。它只处理用户在导出窗口中为某个资源类型槽选择了处理器的资源项。

```text
BuildResourceProcessingItems
  -> BoundedResourceProcessorWorkerPool
  -> ParseResultJson
  -> CollectEffectivePayloads
```

当前已落地的第一版处理器阶段先消费 `WorkspaceChangeItemDto.FileReplacementSourcePath` 代表的文件替换载荷，并将处理器输出作为 `WorkspaceResourceProcessorExecutionResultDto` 传给后续 writer 端口和 manifest。对象级 writer 已消费成功处理项的 `outputPath` 作为优先载荷：同一资源存在处理器输出时，写包阶段使用处理器输出替换原始文件替换路径；没有处理器输出时，回退到工作区有效替换源。

### 7.1 处理项粒度

第一版处理项粒度为单个资源项：

```text
WorkspaceResourceProcessingWorkItem
  exportOperationId
  bundleId
  bundleCacheKey
  assetId
  pathId
  assetName
  resourceKind
  processorId
  inputPayloadPath
  outputPayloadPath
  resultPath
```

后续如果某些资源处理器天然需要整组输入，例如 Spine atlas + skel + texture page，可以在 Application 中把多个资源项聚合为一个 work item，但仍必须返回每个受影响资源的稳定身份和结果映射。

### 7.2 CLI 协议

资源处理器继续使用插件管理页定义的 `processArguments` 数组和占位符。每次调用必须提供唯一的：

- `{inputPath}`
- `{outputPath}`
- `{resultPath}`
- `{resourceKind}`
- `{assetName}`
- `{bundleName}`
- `{workspaceKey}`
- `{workspaceDataRootPath}`
- `{manifestDirectory}`

result JSON 第一版建议：

```json
{
  "schemaVersion": 1,
  "status": "succeeded",
  "message": "optional diagnostic fallback",
  "outputPath": "D:/.../output",
  "contentType": "image/png",
  "diagnostics": [
    {
      "code": "Processor.Warning.Example",
      "severity": "warning",
      "message": "optional diagnostic fallback"
    }
  ]
}
```

规则：

- `status` 只允许 `succeeded / skipped / failed`。
- `outputPath` 必须位于本次 work item 允许的 staging 输出范围内，或等于调用方传入的 `{outputPath}`。
- stdout / stderr 只能作为诊断 detail；主失败语义以退出码、result JSON 和结构化诊断为准。
- result JSON 缺失、无法解析、越权输出路径或输出文件缺失都应归类为处理器失败，而不是未预期异常。
- 处理器进程退出码非零、超时、可执行文件不可用或输入文件缺失会返回 `Bundle.Save.ResourceProcessor.ExecutionFailed`，并生成 failed 处理器结果用于 bundle 结果摘要。

### 7.3 并发策略

资源处理器阶段使用 Application 内部有界 worker 池：

```text
ResourceProcessorWorkerCount
  默认 = min(4, max(1, Environment.ProcessorCount / 2))
  GC / 内存压力或外部进程失败风暴时可退避到 1
```

并发规则：

- 不同 bundle、不同资源项可以并行。
- 同一个 `bundleId + assetId` 不允许并发处理。
- 每个 work item 使用独立 staging 输入 / 输出 / result 路径。
- 处理器 manifest 后续可增加可选 `maxParallelism`；未声明时使用全局 worker 池，声明为 `1` 时同一 processor id 串行。
- 取消时停止派发新 work item，并尽力终止当前外部进程。
- 处理器失败只阻断其所属 bundle 的后续写包；其他独立 bundle 默认继续执行。

资源处理阶段的结果必须稳定排序后交给写包计划，避免多线程完成顺序影响 manifest 或测试断言。

## 8. AB 写包阶段

AB 写包阶段按资源包分组。每个资源包最多一个 writer，多个资源包可以并行：

```text
BuildBundleWriteItems
  -> BoundedBundleWriterPool
  -> WriteTempBundle
  -> ValidateOutput
  -> PromoteOutput
```

### 8.1 写包项

```text
WorkspaceBundleWriteWorkItem
  exportOperationId
  bundleId
  bundleCacheKey
  sourceArtifactPath
  bundleSessionCacheKey?
  outputFilePath
  tempOutputFilePath
  effectiveAssetChanges[]
  processorOutputs[]
```

写包端口建议在 Application 中定义：

```text
IWorkspaceBundleExporter
  ExportAsync(WorkspaceBundleExportRequest request, CancellationToken cancellationToken)
```

Infrastructure 的当前实现已经在同一端口内消费标准化 artifact、有效变更载荷和处理器输出。写包器按 `WorkspaceBundleAssetWriteItemDto` 中的对象身份查找 serialized file 与 path id，调用 AssetsTools replacer 修改对象，再按官方写包流程先输出带 replacer 的未压缩 bundle，随后在需要时按原压缩格式重新 `Pack`。Application 只消费结构化结果。

### 8.2 写包并发

```text
BundleWriterWorkerCount
  默认 = min(4, max(1, Environment.ProcessorCount / 2))
  受 SaveRebuild profile、IO 闸门、memory-heavy bundle load 和 GC 压力约束
  内存压力或连续 OOM 时退避到 1
```

并发规则：

- 同一 bundle 的读取、变更应用和写包串行。
- 不同 bundle 可以并行写包。
- 写包阶段可以复用标准化 artifact identity 和 loaded-session single-flight，但 writer 必须持有独占写入上下文，不得共享可变对象。
- 输出提升以单 bundle 为原子边界：临时文件完整写出并验证后，才移动或替换最终路径。
- 如果某个 bundle 失败，不影响已经成功输出的其他 bundle；最终结果为 `Partial`。

### 8.3 输出校验

每个成功输出的 AB 至少记录：

- 最终路径。
- 相对输出根路径。
- 文件大小。
- 最后写入时间。
- MD5。
- SHA-256。

MD5 用于与已有批量替换 manifest 的轻量变更判定习惯保持一致；SHA-256 用于后续传给外部应用 / 插件时提供更稳的内容身份。

## 9. 进度、取消和失败分类

### 9.1 进度模型

Application 向 Presentation 报告结构化进度：

```text
WorkspaceExportProgressDto
  phase                 # Planning / ProcessingResources / WritingBundles / WritingManifest / Committing / Completed
  currentBundleDisplayName?
  currentResourceDisplayName?
  completedUnits
  totalUnits
  succeededBundleCount
  failedBundleCount
  processedResourceCount
  messageToken?
  messageParameters[]
```

Presentation 只做节流投影，不逐资源刷 UI。Shell 状态条显示当前阶段、当前 bundle / resource、总体进度和取消入口。

### 9.2 取消语义

取消分阶段处理：

- `Planning`：直接取消，不写输出文件。
- `ProcessingResources`：停止派发新处理项，尽力终止当前 CLI 进程，保留已完成 staging 诊断。
- `WritingBundles`：不再启动新 bundle writer，正在写的 bundle 由 Infrastructure 尽力取消；已成功提升的 bundle 保留，并进入 manifest。
- `WritingManifest / Committing`：尽量完成 manifest 和操作记录收尾；如果失败，返回结构化诊断，已输出文件不自动删除。

第一版不承诺跨多个 AB 文件的全局事务。语义是“单 bundle 原子，整批可部分成功，可通过 manifest 解释结果”。

### 9.3 失败分类

建议稳定诊断码方向：

```text
Export.Plan.WorkspaceMissing
Export.Plan.NoSelectedBundles
Export.Plan.ArtifactMissing
Export.Plan.OutputBlocked
Bundle.Save.ResourceProcessor.NotFound
Bundle.Save.ResourceProcessor.Unavailable
Bundle.Save.ResourceProcessor.UnsupportedResourceKind
Bundle.Save.ResourceProcessor.ExecutionFailed
Bundle.Save.ResourceProcessor.ResultInvalid
Bundle.Save.WriteFailed
Export.Bundle.OutputValidationFailed
Export.Manifest.WriteFailed
Export.Operation.CommitFailed
```

外部 CLI 原始输出、Writer 原始异常和文件系统异常只作为 diagnostic detail，不作为主用户提示。

## 10. AB 导出修改清单 JSON

真实导出完成后，在输出根目录写入：

```text
<outputDirectory>/_abt-bundle-export-manifest.json
```

它与普通批量提取使用的 `<outputRoot>/_abt-export-manifest.json` 不同。前者描述“输出了哪些修改后的 AB 文件”，后者描述“提取了哪些资源文件”。后续传递给其他应用或导出后处理插件时，应优先消费 `_abt-bundle-export-manifest.json`。

### 10.1 写出规则

- 只要有任意 bundle 已经成功提升到最终输出路径，就应尽量写 manifest。
- 全部失败且没有输出文件时，可以不写 manifest，但结果 DTO 必须包含失败诊断。
- manifest 写出使用临时文件 + 原子替换。
- manifest 路径写入 `WorkspaceExportExecutionResultDto.ExportManifestPath`。
- `SaveExecutionCompleted` 的 result 中记录 manifest 路径、输出根目录、成功 / 失败 bundle 数和检查点 id。

### 10.2 Schema

第一版 schema 建议：

```json
{
  "schemaVersion": 1,
  "manifestKind": "workspaceBundleExport",
  "tool": "AssestBundleTools",
  "exportId": "20260703T120000Z-...",
  "workspaceId": "workspace-id",
  "workspaceKey": "workspace-key",
  "exportedAtUtc": "2026-07-03T12:00:00Z",
  "status": "partial",
  "outputRoot": "D:/Exports/Game",
  "processorSelections": [
    {
      "resourceKind": "Texture2D",
      "processorId": "resource.processor.texture2d.png",
      "displayName": "Texture PNG Processor"
    }
  ],
  "bundles": [
    {
      "bundleId": "bundle-id",
      "bundleCacheKey": "bundle-cache-key",
      "displayName": "characters.ab",
      "sourceArtifactPath": "D:/Workspace/cache/bundles/.../bundle",
      "outputRelativePath": "characters.ab",
      "outputPath": "D:/Exports/Game/characters.ab",
      "status": "succeeded",
      "checkpointId": "checkpoint-id",
      "changeCounts": {
        "total": 3,
        "modifiedAssets": 2,
        "fileReplacements": 1,
        "workspaceReplacements": 1,
        "editDrafts": 1
      },
      "file": {
        "sizeBytes": 123456,
        "md5": "md5-hex",
        "sha256": "sha256-hex",
        "lastWriteTimeUtc": "2026-07-03T12:00:05Z"
      },
      "processorResults": [
        {
          "resourceKind": "Texture2D",
          "processorId": "resource.processor.texture2d.png",
          "status": "succeeded",
          "inputCount": 2,
          "processedCount": 2,
          "failedCount": 0,
          "resultPaths": [
            "D:/Workspace/staging/exports/.../result.json"
          ],
          "diagnostics": []
        }
      ],
      "diagnostics": []
    }
  ],
  "diagnostics": []
}
```

约束：

- `outputRelativePath` 是相对 `outputRoot` 的稳定路径，供外部工具迁移目录时使用。
- `outputPath` 是本机绝对路径，方便同机后处理插件直接执行。
- `sourceArtifactPath` 可以帮助诊断，但外部工具不得把它当作可修改源。
- `checkpointId` 只对成功 bundle 存在；失败 bundle 不应伪造检查点。
- `diagnostics[]` 使用稳定 `code / severity / parameters`，可选保留外部工具 fallback message。

## 11. 操作记录与检查点

真实导出必须接入工作区可恢复操作记录：

1. 用户确认执行后，先追加 `SaveExecutionStarted`，状态为 `Pending`，记录 `exportOperationId`、资源包范围、输出根目录和处理器选择摘要。
2. 每个 bundle 写包成功后，Application 在内存结果中记录被消费的 operation ids，但不立即把失败 bundle 的操作标记为已提交。
3. manifest 成功写出后，追加或更新 `SaveExecutionCompleted`，记录 manifest 路径、成功 / 失败 / 跳过 bundle 数和输出摘要。
4. 只对成功 bundle 创建检查点，并把这些 bundle 中被消费的替换 / 编辑操作标记为 `Committed`。
5. 如果全部失败，更新为 `SaveExecutionFailed`，保留结构化诊断，不创建检查点。
6. 如果部分成功，整体结果为 `Partial`；成功 bundle 的操作可标记 `Committed`，失败 bundle 的操作仍保持待保存状态。

提取历史操作 `ExtractAsset / ExtractBatch` 仍然表示外部资源提取事实，不因为 AB 导出成功而变成可撤销修改。保存检查点只消费会影响 AB 输出内容的替换、编辑和后续可写草稿操作。

## 12. 与后台任务设计的关系

工作区真实导出只向 Presentation 调度器提交一个 `SaveRebuild` profile 任务：

```text
BackgroundWorkRequest<Result<WorkspaceSaveExecutionResultDto>>
  Profile = SaveRebuild
  SemanticKind = WorkspaceMutation
  Priority = Visible
  WorkspaceId = current workspace
  WorkspaceKey = current workspace key
  CanCancel = true
```

资源处理器 worker 池和 bundle writer worker 池属于 Application 用例内部并行，不应提升为多个 Presentation 后台任务。Presentation 的任务列表只显示一个“工作区导出 / 保存重建”任务，任务内部进度由 Application 结构化上报。

`SaveRebuild` 与同一工作区的导入提交、替换提交、撤销 / 重做提交互斥。预览和只读提取可以继续并行，但必须共享 artifact / loaded-session / IO / memory-heavy 闸门，避免真实写包期间把同一 bundle 重复加载到不可控内存峰值。

## 13. 测试计划

Application 回归优先覆盖：

- 导出命令会拒绝缺失工作区、空资源包选择、不可用处理器和不支持资源类型。
- 导出计划只包含用户选择的资源包，并保留 `bundleId / bundleCacheKey / outputPath`。
- 资源处理器选择中的“无处理器”不会进入执行队列。
- 当前第一阶段覆盖选中 bundle 写出、processor selection 透传、文件替换载荷的资源处理 work item 构建、处理器结果透传、失败回填、失败 bundle 跳过写包、bundle writer 跨 bundle 有界并发、对象级写包项透传和输出摘要。
- 后续补资源处理器取消、工作区内替换 / 编辑草稿有效载荷的更多资源类型和跨 bundle 写包回归。
- 部分成功时只提交成功 bundle 的检查点。

Infrastructure 回归优先覆盖：

- 资源处理器 result JSON 解析、非法路径拒绝、stdout / stderr 诊断捕获和超时终止。
- 当前第一阶段覆盖空对象写入拒绝、真实样本 TextAsset / Texture2D 对象级写入后读回、同目录临时文件提升、MD5 / SHA-256 记录、资源处理器 result JSON 解析和 AB 导出 manifest 原子写入。
- 后续补目标已存在 blocker、确认覆盖后的替换路径、Sprite / workspace replacement 的真实样本回归和更多 AssetsTools Writer 类型。

Presentation 回归优先覆盖：

- 工作区导出窗口提交命令时携带选中 bundle 和每个资源类型槽的 processor selection。
- 导出任务使用 `SaveRebuild` profile，并在执行中禁用重复提交。
- 当前导出完成后只由导出窗口消费执行结果；保存检查点和 `Committed` 状态落地前，不广播 workspace-wide 操作变更，避免主工作台误刷新旧资产标签。
- 后续检查点落地后，通过精确的已提交操作通知刷新保存预览 / 变更投影，而不是直接重扫 AB。

测试边界：

- 在真实 writer 端口落地前，不用测试伪造的 AB 二进制内容。
- 外部 CLI 集成测试默认使用受控测试脚本或 fake executor，不依赖机器全局工具。
- 完整跨多个 AB 的全局回滚不属于第一版契约，不应写成测试要求。

## 14. 落地顺序

建议按以下顺序实现：

1. [已落地] 扩展 Application 导出命令 DTO，携带资源包范围和资源处理器选择。
2. [已落地] 复用现有保存预览的 artifact 输入和输出 preflight 构建第一阶段导出计划。
3. [已落地] 定义资源处理器执行端口和 result DTO，并用 fake executor 覆盖 Application 结果透传与失败回填。
4. [已落地] 在 Infrastructure 接入外部 CLI 资源处理器执行与 result JSON 解析。
5. [已落地] 定义 `IWorkspaceBundleExporter`，Application 编排 bundle writer 有界并发。
6. [已落地首版] Infrastructure 已接入真实 AssetsTools 对象级写包、处理器输出消费、单 bundle 临时输出提升和写后 hash 记录。
7. [已落地] 新增 AB 导出 manifest store，写出 `_abt-bundle-export-manifest.json`。
8. [待补] 接入保存检查点和操作记录 committed 状态。
9. [已落地] 将工作区导出窗口的开始按钮接到真实 `SaveRebuild` 后台任务。
10. [进行中] 补 Presentation、本地化、自动化回归和端到端手动验证；当前 CLI 处理器端口与 TextAsset / Texture2D 对象级写包已补 Application / Infrastructure 回归。

## 15. 收口结论

真实导出不新增一套独立线程模型，也不让导出窗口直接执行处理器或写包。外层继续是统一后台任务调度，内层由 Application 用例按业务一致性拆成资源处理器 worker 池和 bundle writer worker 池。资源处理器并发解决“同类资源预处理慢”的问题，bundle 写包并发解决“多 AB 输出慢”的问题；两者都以有界并发、独立 staging、结构化诊断和可取消任务为边界。最终输出 `_abt-bundle-export-manifest.json`，把修改后的 AB 文件、处理器选择、hash、检查点和诊断稳定传递给后续应用或导出后处理插件。
