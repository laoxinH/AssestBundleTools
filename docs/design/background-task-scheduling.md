# AssestBundleTools 统一后台任务调度设计

## 1. 文档目的

本文档用于收束当前导入、预览、提取，以及后续导出 / 保存 / 重建等长耗时操作的后台多线程设计。

目标不是把所有业务流程改造成同一个“大任务类”，而是明确三层边界：

1. **执行原语**：把耗时工作从 WinUI UI 线程移到后台线程。
2. **调度策略**：统一管理优先级、并发槽、取消、进度、状态条和任务生命周期。
3. **业务内部并行**：由 Application 用例在自身一致性边界内决定是否继续拆 worker。

后续实现后台任务时，应优先对照本文档，避免导入、预览、提取和保存各自重新发明一套线程池、进度 UI、取消语义或刷新机制。

## 2. 适用范围

本文档覆盖：

- 工作区打开 / 导入刷新。
- 资源详情预览、Spine 资源组详情、Spine runtime 当前帧预览和后台预热。
- 单资源提取、普通资源批量提取、Spine 资源组整组 / 批量提取。
- 后续导出、保存为新包、真实写包、重建输出、序列帧导出等长耗时任务。
- Shell 底部状态条、任务进度、取消、日志摘要和操作记录通知之间的关系。

本文档不覆盖：

- 具体 AB 解析、Unity 字段树读取、外部解密器协议或图片 / 音频 / Mesh / Spine 预览生成细节。
- 工作区操作记录的事实模型，仍以 [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md) 为准。
- 标准化 bundle 物理缓存和 loaded-session 缓存的完整实现细节，仍以 [workspace-management.md](workspace-management.md) 的缓存边界为准。

## 3. 当前实现盘点

当前仓库已经具备多条后台能力，但它们还分散在不同协作对象中：

| 链路 | 当前承载点 | 已有能力 | 当前不足 |
| --- | --- | --- | --- |
| 工作区打开 / 导入 | `WorkspaceImportCoordinatorService`、`BundleExplorerWorkspaceBindingCoordinator`、`IBackgroundTaskRunner` | 外层任务已离开 UI 线程；Application 内部已有 `workspaceKey` 执行闸门和 bundle 粒度 worker 池 | 外层任务、导入进度覆盖层和 Shell 状态条尚未统一为同一个任务模型 |
| Application 导入 worker | `WorkspaceImportProcessingCoordinator -> WorkspaceImportWorkerPool -> WorkspaceImportCommitter` | `串行计划 -> 有界并行 worker -> 串行提交`，默认 worker 并发度 `1-4` | 该 worker 池属于业务一致性边界，不应被 Presentation 调度器接管 |
| 资源预览 | `AssetPreviewQueryExecutor` + `IBackgroundWorkScheduler` + `WorkspaceBundleArtifactResolver` + `AssetsToolsBundleLoadedSessionCache` | 前台槽优先服务活动详情标签，后台槽用于已打开标签预热；支持取消、超时、调度器 profile 闸门；导入 / 预览 / 提取 / Spine 详情会优先复用 `cache/bundles` 标准化 artifact；artifact 生成阶段按 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 串行化，标准化 artifact 已有 usage / LRU prune、设置页显式清理入口和偏好驱动自动容量治理；预览缓存已有 usage / prune，并纳入当前工作区缓存聚合统计、设置页显式清理和偏好驱动自动容量治理；AssetsTools loaded-session 再按标准化 artifact identity 做 single-flight / lease 复用，并在 artifact 删除 / prune 时主动失效；loaded-session 已有默认 6 个空闲会话上限和 GC 内存压力下 1 个空闲会话退避；预览生成 `OutOfMemoryException` 会映射为专用诊断，并让同 bundle 后台预热短期退避 | 完整可观测队列、任务详情和后续写包共享入口仍需后续补齐 |
| Spine 详情、贴图载荷和 runtime 帧 | `SpineAssetGroupDetailsLoader`、`InspectorSpineAnimationPreviewSurfaceView` + `IBackgroundWorkScheduler` | 组详情 / 贴图载荷通过 `PreviewForeground` 后台加载，runtime 当前帧通过独立 `SpineRuntimeFrame` profile 运行；初始组详情先用交互预览快路径加载可播放动画所需内容，播放失败且缺少明确诊断时才补跑完整诊断；均不让 UI 直接读 AB | Spine 序列帧导出和后续导出 profile 仍需继续统一 |
| 批量提取 | `BundleExplorerBatchExtractionBackgroundExecutor`、`BatchTasksViewModel.Execution` | 已通过后台 runner 执行，并向 Shell 状态条上报进度 | 任务取消、并发限制、队列排队和完成结果展示仍偏局部实现 |
| Shell 状态条 | `IAppBackgroundTaskStatusService` | 能显示当前任务、进度和不确定进度，并把刷新合并到 UI 线程 | 当前是手动 begin/report 模型，尚未和调度器的任务生命周期自然绑定 |

因此后续应把当前薄 `IBackgroundTaskRunner` 保留为执行原语，在其上增加统一的调度层，而不是让每条链路继续直接拥有独立线程、独立进度和独立闸门。

## 4. 总体原则

1. WinUI UI 线程只负责布局、输入、绑定更新和轻量投影，不运行 AB 读取、解密、导出、写包或批量循环。
2. Presentation 可以调度后台任务、展示瞬时进度、合并 UI 刷新、处理用户取消，但不得直接解析资源包或绕开 Application 用例。
3. Application 继续拥有用例语义、操作记录、导入一致性、保存一致性、恢复计划和 `Result / AppError`。
4. Infrastructure 继续拥有具体 IO、外部进程、AB Reader / Writer、预览生成器和缓存持久化实现，不反向通知 UI。
5. 调度器不是新的业务状态源。Shell 状态条是瞬时 UI；操作记录、变更页、保存预览和重启恢复仍由工作区读模型和操作记录驱动。
6. 调度器不是第二套物理线程池。它默认仍使用 .NET ThreadPool 或现有 runner，新增的是任务描述、优先级、并发闸门、进度、取消和观测策略。
7. 当前导入 worker 池不得上移到 Presentation。导入的 bundle 粒度并行属于 Application 内部用例实现，Presentation 只调度“处理当前工作区导入”这一外层任务。

## 5. 分层职责

### 5.1 Presentation

Presentation 层负责统一后台任务调度服务：

- 接收页面、ViewModel、Coordinator 或 ActionHandler 提交的后台任务请求。
- 根据任务类型分配 profile、优先级、并发槽和取消范围。
- 将任务状态投影给 Shell 底部状态条。
- 对高频进度做 UI 线程合并刷新。
- 对过期预览请求、关闭标签、切换工作区、关闭窗口等页面级生命周期触发取消。
- 在任务完成后触发已有 toast、日志、页面读模型失效通知或结果面板更新。

Presentation 不负责：

- 直接读取 AB 或缓存目录。
- 推导资产依赖、Spine 组匹配或导出格式。
- 将后台任务状态持久化为工作区事实。

### 5.2 Application

Application 层负责：

- 导入、预览、提取、替换、保存等用例契约。
- 业务一致性闸门，例如同一工作区导入只允许一个运行中作业。
- 导入内部的 `PlanBuilder -> WorkerPool -> Committer` 三段模型。
- 操作记录追加、状态更新、恢复计划、保存检查点。
- 返回稳定 DTO、进度 DTO、结构化诊断和 `Result / AppError`。

Application 可以保留内部 worker 池，但应遵循以下规则：

- worker 池必须服务于具体用例的一致性边界，而不是为了替代 Presentation 调度器。
- 缓存写入、预览清理、元数据回写和保存提交等共享状态修改默认串行。
- 需要用户交互的信息必须由 Presentation 先收集，再作为命令参数传入。

### 5.3 Infrastructure

Infrastructure 层负责：

- 解密、标准化 bundle、Reader / Writer、预览 sidecar 生成和文件写出。
- 标准化 bundle 物理缓存、预览缓存、变更暂存文件的具体读写。
- 外部进程协议和原始诊断捕获。

Infrastructure 不负责：

- 更新 ViewModel、状态条或 toast。
- 持有 WinUI 控件、窗口对象、DispatcherQueue。
- 决定页面级取消、预览优先级或状态条展示。

## 6. 目标服务形态

后续建议形成两层服务：

```text
IBackgroundTaskRunner
  只负责把一个 Func<CancellationToken, Task<T>> 放到后台执行。

IBackgroundWorkScheduler
  负责提交任务、选择 profile、排队、并发闸门、进度、取消、状态投影和任务观测。
```

建议的调度请求模型：

```text
BackgroundWorkRequest<T>
  id
  kind
  profile
  priority
  workspaceId / workspaceKey
  bundleCacheKey
  resourceKey
  titleToken / titleParameters
  progressMode
  cancellationPolicy
  operation
```

建议的任务运行模型：

```text
BackgroundWorkHandle<T>
  taskId
  status
  resultTask
  cancellation
  progressReporter
```

当前第一版实现中，`IBackgroundWorkScheduler.StartAsync` 已返回 `BackgroundWorkHandle<T>`，`RunAsync` 作为等待结果的兼容便捷入口继续保留；调度器会维护当前会话内的 `BackgroundWorkSnapshot` 列表，并通过 `TasksChanged` 通知观察者更新 UI 或后续任务面板。

这里的 `titleToken / titleParameters` 表示“可本地化的展示语义”，不要让调度器要求调用方传入硬编码用户文本。

## 7. 统一后台任务流程模型

统一调度器应把所有后台任务收敛到同一条生命周期外壳：

```text
Submit
  -> Normalize
  -> Register
  -> Enqueue
  -> Acquire Gates
  -> Execute
  -> Report Progress
  -> Complete
  -> Notify
  -> Cleanup
```

这条流程只统一“后台任务如何被管理”，不统一“业务用例如何实现”。导入、预览、提取、导出、保存仍然调用各自的 Application 契约；调度器只负责生命周期、资源闸门、进度和完成后的通用收尾。

### 7.1 流程阶段

| 阶段 | 责任 | 不应承担 |
| --- | --- | --- |
| `Submit` | 调用方提交 `BackgroundWorkRequest<T>`，声明 profile、工作区、资源包、标题 token、取消策略和操作函数 | 不直接启动散落的 `Task.Run` |
| `Normalize` | 调度器补齐默认优先级、进度模式、闸门需求和本地化展示元数据 | 不修改业务命令内容 |
| `Register` | 创建稳定 `taskId`、运行状态、取消源和可观测 handle | 不把任务状态写入工作区操作记录 |
| `Enqueue` | 按优先级和 profile 排队；可跳过低价值预热任务 | 不让后台预热抢占前台交互 |
| `Acquire Gates` | 申请全局、工作区、资源包和 memory-heavy bundle load 闸门 | 不替代 Application 内部一致性闸门 |
| `Execute` | 使用底层 `IBackgroundTaskRunner` 执行业务操作 | 不在 Presentation 中解析 AB 或写包 |
| `Report Progress` | 接收进度快照，并合并投影到 Shell 状态条或局部覆盖层 | 不逐项向 UI 线程排队大量刷新 |
| `Complete` | 统一归类成功、失败、取消、超时和未预期异常 | 不吞掉 `Result / AppError` 语义 |
| `Notify` | 按任务结果触发 toast、日志、读模型失效通知或结果面板更新 | 不用手动页面刷新替代操作变更通知 |
| `Cleanup` | 释放闸门、取消源、loaded-session lease，并观察遗弃任务异常 | 不提前释放仍在同步解包的资源包槽 |

### 7.2 任务语义分类

调度流程统一，但任务语义分三类处理：

| 分类 | 典型任务 | 持久化语义 | 完成通知 |
| --- | --- | --- | --- |
| `TransientRead` | 前台预览、后台预热、Spine 当前帧、轻量详情加载 | 不持久化；可取消、可丢弃、可重试 | 只更新当前页面状态或预览诊断 |
| `VisibleOutput` | 单资源提取、批量提取、Spine 组导出、序列帧导出 | 成功输出后按业务需要追加 `ExtractAsset / ExtractBatch` 或后续导出记录 | 状态条、toast、日志摘要、操作变更通知 |
| `WorkspaceMutation` | 替换、撤销、保存、重建 | 必须通过 Application 操作记录、恢复计划、检查点或保存结果表达 | 操作变更通知、保存预览刷新、结果摘要 |

这里的分类决定默认取消、进度、状态条可见性和完成后通知方式。新增后台任务时，应先选择语义分类，再选择更具体的 profile。

### 7.3 标准执行上下文

调度器传给任务操作的上下文应至少包含：

```text
BackgroundWorkExecutionContext
  taskId
  profile
  workspaceId / workspaceKey
  cancellationToken
  progress
  acquiredGates
```

调用方不应直接持有 Shell 控件或 DispatcherQueue；需要展示进度时只调用 `progress.Report(...)`。需要业务结果时等待 `BackgroundWorkHandle<T>.ResultTask`，再按现有 coordinator / action handler 处理页面级状态。

### 7.4 标准完成结果

调度器内部应把任务完成统一归类为：

```text
BackgroundWorkCompletion
  Succeeded
  Failed
  Cancelled
  TimedOut
  Faulted
```

其中：

- `Succeeded / Failed / Cancelled` 优先来自 Application `Result`。
- `TimedOut` 通常只表示调用方停止等待，不代表底层同步工作已经结束。
- `Faulted` 表示未预期异常已被调度器观察并转为诊断，用户可见提示仍应优先通过 `AppError` 或本地化资源表达。

## 8. 任务类型与 Profile

统一调度器至少需要支持以下 profile：

| Profile | 典型任务 | 优先级 | 并发建议 | 取消策略 | 状态条 |
| --- | --- | --- | --- | --- | --- |
| `WorkspaceOpen` | 打开 / 恢复工作区 | 高 | 每窗口 1 | 工作区切换失败时回滚；用户关闭窗口可取消 | 显示 |
| `WorkspaceImport` | 手动导入、拖拽导入、自动导入刷新 | 高 | 每工作区外层 1；Application 内部 `1-4` worker | 用户可取消；同工作区串行 | 显示 |
| `PreviewForeground` | 当前活动资源详情预览、当前 Spine 详情 | 最高交互优先 | 前台槽 1 | 新请求替代旧请求；标签关闭取消 | 显示轻量加载状态 |
| `PreviewPrewarm` | 已打开但未激活标签的预览预热 | 低 | 使用剩余预览槽；压力大时可跳过 | 页面切换、内存压力、缓存未命中时可取消或降级 | 默认不抢占状态条 |
| `SpineRuntimeFrame` | Spine runtime 当前帧生成 | 交互 | 受预览 / runtime session 控制 | 播放停止、seek、skin / animation 切换时取消旧帧 | 默认不进入全局状态条 |
| `SingleExtraction` | 单资源提取、单 Spine 组提取 | 可见后台 | 每用户动作 1 | 用户可取消；已完成项保留结果 | 显示 |
| `BatchExtraction` | 普通资源多选提取、Spine 组批量提取、工作区级批量提取对话框执行 | 可见后台 | 每工作区 1；Application 内部可按 AB 资源包受控并行，默认 `CPU/2`、最大 `4`，GC 压力下退避到 `1`；同 AB 内顺序执行 | 用户可取消；继续处理可成功项 | 显示确定进度 |
| `BatchReplacement` | 后续真实批量替换 | 可见后台 | 每工作区 1 | 用户可取消；以操作记录恢复 | 显示 |
| `Export` | 序列帧导出、资源组导出、后续素材包导出 | 可见后台 | 每工作区或每资源组受控 | 用户可取消；已输出文件按结果记录 | 显示 |
| `SaveRebuild` | 另存为新包、真实写包、重建输出 | 最高可见后台 | 每工作区独占 | 强取消 / 回滚语义需由 Application 定义 | 显示并保留结果入口 |
| `Maintenance` | 缓存清理、低优先级诊断 | 最低 | 空闲或低并发 | 可被高优先级任务跳过或暂停 | 默认不显示 |

Profile 是调度策略，不是业务类型本身。同一个 Application 用例可以在不同入口下使用不同 profile，例如当前活动标签请求预览使用 `PreviewForeground`，后台预热同一预览用例使用 `PreviewPrewarm`。

## 9. 并发闸门

统一调度器应组合以下闸门，而不是每个页面手写一套 `SemaphoreSlim`。

### 9.1 全局闸门

- `CpuBound`：CPU 较重但内存压力较低的任务。
- `IoBound`：文件读写、导出、缓存读写等 IO 密集任务。
- `MemoryHeavyBundleLoad`：AB 解包、标准化、`LoadBundleFile` 等高内存峰值任务。

同一任务可以同时占用多个闸门。例如预览缓存未命中且需要解包时，应同时受到预览槽和 bundle load 闸门约束。

### 9.2 工作区闸门

- 同一 `workspaceKey` 的导入刷新外层任务必须串行。
- 保存 / 重建与同一工作区的替换提交、导入提交之间应互斥。
- 提取是只读外部输出操作，通常可以和预览并行，但必须共享 bundle load / IO 闸门，避免批量提取压垮预览。
- 工作区关闭或切换时，Presentation 应取消该工作区范围内的预览、预热和未确认后台任务；已经进入 Application 操作记录事实链路的任务按对应用例语义处理。

### 9.3 资源包闸门与 Single-flight

针对 `workspaceKey + bundleCacheKey` 应保留资源包级 single-flight 方向。当前已落地三层复用：第一层是 Application 侧 `WorkspaceBundleArtifactResolver` 对 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 做 artifact 生成闸门，避免并发预览、提取或导入同时重跑解密 / 标准化；第二层是 Application / Infrastructure 之间的 `cache/bundles` 标准化 artifact manifest，导入、预览、提取和 Spine 详情先复用同一可读 bundle 路径；第三层是 Infrastructure 内部基于标准化 artifact identity 的 loaded-session single-flight，并由 AssetsTools Reader、预览生成器和经预览管线导出的提取共享。没有 artifact identity 的直接 Reader / 测试调用继续回退到物理路径 key：

- 多个预览、提取或 Spine 组解析同时需要同一个 bundle 时，只允许一个标准化 / 加载请求进入物理读取，其余请求等待同一个结果或复用 loaded-session。
- UI 请求取消或超时后，如果底层同步解包已经开始，调度槽不应立刻释放给新的同类 bundle load。底层任务完成、失败或被真正取消后再释放，避免旧解包仍在运行时继续放入新解包导致内存峰值失控。
- 对已确认内存不足的 bundle，当前 `AssetPreviewQueryExecutor` 会短期退避后台预热，只保留用户主动前台请求；连续失败的更细分退避仍按真实样本再扩展。

资源包 single-flight 与 loaded-session 缓存不改变操作记录事实语义；它们只负责性能和资源复用。

## 10. 优先级与排队

建议优先级从高到低：

1. `UserBlocking`：当前活动标签预览、当前用户刚触发的打开 / 定位 / 详情加载。
2. `Visible`：导入、批量提取、保存、导出等用户可见长任务。
3. `Background`：已打开标签后台预热、可跳过的辅助加载。
4. `Maintenance`：缓存清理、低优先级诊断。

调度规则：

- 前台预览不能被后台预热饿死。
- 批量提取不能无限压制用户当前详情预览，应让出少量交互槽。
- 保存 / 重建这类高风险任务可以独占工作区，但不应阻塞其他工作区的只读预览。
- 同 profile 内按提交时间 FIFO 即可；跨 profile 按优先级和资源闸门共同决定。
- 后台预热可以被取消或跳过，不应为了完成预热而拖慢用户当前操作。

## 11. 进度与 Shell 状态条

Shell 底部状态条只显示当前会话中的后台状态：

- 当前任务标题。
- 当前处理对象。
- 确定性进度或不确定进度。
- 简短运行状态。
- 当前调度器会话内后台任务数量。
- 对声明 `CanCancel` 的当前任务显示取消入口。
- 鼠标悬停在任务数量区域时直接显示轻量任务悬浮面板，列出 `IBackgroundWorkScheduler.CurrentTasks` 中的所有当前任务、排队 / 运行 / 正在取消状态、进度和单任务取消入口；鼠标离开数量区域与悬浮面板后自动关闭。数量区域同时作为可聚焦轻量按钮，支持点击 / 键盘激活同一面板；数量区域不叠加普通 tooltip，避免系统 tooltip 与任务面板争用同一次悬停。

工作区级批量提取的进度由 Application 通过结构化 `WorkspaceBatchExtractionProgressDto` 上报，Presentation 再格式化为本地化状态条 detail。该 detail 应显示准备阶段总项数 / 候选资源包数，运行阶段当前资源包、当前普通资源或 Spine 组、成功 / 失败项计数，并在 GC 内存压力退避时附加短提示；调度器仍只接收已经格式化好的瞬时展示文本，不把本地化规则下沉到 Application。

状态条不得替代：

- toast 成功 / 失败反馈。
- 应用日志。
- 批处理结果面板。
- 资源详情“变更”页。
- 工作区操作记录。

进度模型建议：

```text
BackgroundWorkProgress
  stage
  completedCount
  totalCount
  currentItemName
  detailToken / detailParameters
  isIndeterminate
```

刷新规则：

- 后台线程只写入最新进度快照。
- Shell 状态条在 UI 线程按最新快照合并投影。
- 不允许每个小文件写出都直接向 DispatcherQueue 排队一次 UI 更新。
- 批量任务完成后日志默认写摘要；失败明细应有限展示或进入可展开结果，不逐项刷爆日志集合。

当多个后台任务同时存在时，状态条主文本默认展示最近更新的可见任务；任务数量和悬浮面板负责展示完整调度器快照。悬浮面板是瞬时 Presentation 投影，不写入工作区事实，也不替代批处理结果面板或日志明细。

当前第一版 Shell 状态条已接入 `BackgroundTaskStatusRequest`：调度器在任务真正获得 profile 闸门后创建状态条 handle，并把可取消任务的 `taskId / cancelAction` 交给状态条服务。状态条取消按钮只触发调度器取消当前任务的 `CancellationToken`，不直接推断业务回滚；业务链路必须继续用自身结果、操作记录和恢复计划表达最终状态。

当前任务数量和悬浮面板直接订阅 `IBackgroundWorkScheduler.TasksChanged` 并读取 `CurrentTasks`，因此排队中但尚未获得 profile 闸门的任务也会出现在悬浮面板中；Shell 主状态文本仍只在任务获得闸门并创建状态条 handle 后展示该任务。

## 12. 取消、超时与异常观测

### 12.1 取消

- 预览请求可以被标签切换、资源变更、窗口关闭或新请求替代。
- 后台预热可以被任何更高优先级任务取消或跳过。
- 批量提取、导出、保存等可见任务应优先提供显式取消入口；取消后已经成功的项按用例结果保留，未完成项按 `Cancelled` 或后续恢复计划处理。
- 保存 / 重建进入真实写包后，取消和回滚必须由 Application 用例定义，不由 Presentation 直接推断。

### 12.2 超时

- 前台预览可以有展示层超时，避免 UI 长时间等待。
- 超时只代表调用方停止等待，不代表底层同步 AB 解包已经终止。
- 调度器必须观察被放弃的后台任务，避免未观测异常和槽位提前释放。

### 12.3 异常

- 预期失败返回 `Result / AppError`。
- 取消返回取消语义，不伪装成普通错误。
- 未预期异常由调度器观测并转为日志 / 诊断；具体用户提示仍应通过本地化资源和 `AppError` 优先表达。

## 13. 各链路接入规则

### 13.1 工作区打开与导入

接入方式：

1. Presentation 调度一个 `WorkspaceOpen` 或 `WorkspaceImport` 外层任务。
2. Application 执行 `OpenWorkspaceCommand` 或 `ProcessWorkspaceImportsCommand`。
3. Application 内部继续使用 `WorkspaceImportExecutionGate` 和 `WorkspaceImportWorkerPool`。
4. 导入进度 DTO 可同时投影到左侧资源包窗格覆盖层和 Shell 状态条。
5. 完成后通过既有工作区绑定、日志、toast 和资源包树重载更新页面。

禁止：

- 在 Presentation 中按 bundle 手写导入 worker。
- 让 Shell 状态条直接读取导入缓存或工作区元数据。
- 因为有统一调度器而移除 Application 的同工作区导入执行闸门。

### 13.2 预览与 Spine

接入方式：

1. 当前活动详情标签使用 `PreviewForeground`。
2. 已打开但未激活标签使用 `PreviewPrewarm`。
3. 新打开或新激活的资产详情标签必须立即按 `PreviewForeground` 调度；快速切走的旧活动标签如果仍打开，则取消旧前台请求并重新排入 `PreviewPrewarm`，避免旧前台预览阻塞当前可见标签。
4. 预览 worker 总数由“设置与诊断”页的预览线程数量控制，支持自动和 `1-4` 固定值；其中前台槽固定为 1，其余槽用于后台预热。设置为 1 时后台预热可跳过。
5. 前台预览的调用方取消可以提前释放 `PreviewForeground` profile 槽，让新的活动标签快速进入前台调度；这只释放 Presentation profile 槽，已经进入底层 `LoadBundleFile` 的工作由 Infrastructure loaded-session single-flight / lease 控制，不能因为 UI 取消而提前卸载仍被使用或仍保留在缓存里的 bundle 上下文。
6. 前台资产预览、Spine 组详情和当前贴图页载荷会向 Shell 底部状态条投影轻量“正在加载预览”状态；后台预热不进入全局状态条，避免快速打开大量标签时刷屏。
7. Spine 组详情、Texture2D page 载荷和 alpha page 载荷进入 `PreviewForeground`；Spine runtime 当前帧进入独立 `SpineRuntimeFrame`，避免动画播放帧和普通详情预览互相挤占槽位。
8. 预览任务继续只消费 Application DTO 和预览载荷，不让 Presentation 读取 AB。
9. 预览缓存命中时可快速返回；缓存未命中时受 bundle single-flight 和 memory-heavy 闸门限制。
10. 预览生成阶段若返回 `OutOfMemory` 诊断，当前 bundle 的后台预热会短期退避；前台活动标签请求仍可继续显式发起，成功后会清除该退避。

禁止：

- 为了 Spine 组装直接在 WinUI 层解析 `.atlas / .skel` 或资源包。
- 后台预热抢占当前活动标签的前台槽。
- 取消 UI 请求后立即释放底层 bundle load 槽。
- 把加载中的提示文本当作稳定预览内容；加载态只能表示已有工作，不能阻止标签被降级或重新调度。

### 13.3 提取

接入方式：

1. 单资源提取在用户完成保存位置 / 格式选择后提交 `SingleExtraction`。
2. 普通资源多选和 Spine 组多选提交 `BatchExtraction`。
3. 提取内部继续复用 Application 提取命令和预览 / 导出载荷管线。
4. 每个成功导出的组成资源按操作记录模型追加 `ExtractAsset`，批量或 Spine 组入口追加 `ExtractBatch` 汇总。
5. 成功后由工作区操作变更通知触发读模型重读；不手动刷新资产列表。

禁止：

- 用状态条完成状态替代操作记录。
- 提取成功后为了更新“变更”页而重载资产列表。
- 批量提取逐文件插入大量日志。

### 13.4 后续导出、保存与重建

接入方式：

1. 序列帧导出、整组导出等使用 `Export` profile。
2. 真实保存 / 重建使用 `SaveRebuild` profile，并占用工作区写入独占闸门。
3. 保存前预览仍是 Application 查询，不应因为调度器存在而跳过保存计划。
4. 保存成功后通过检查点标记已消费操作；失败保留可恢复诊断。
5. 替换 / 编辑命令只追加可恢复操作记录并更新待保存状态；真实 AB 替换和写包只在 Export / SaveRebuild 消费操作记录时发生。
6. 覆盖、回滚、冲突、部分失败等语义由 Application 文档化后再实现。
7. Export / SaveRebuild 后续真实读包必须共享 `WorkspaceBundleArtifactResolver`，通过 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 取得标准化 artifact；不得重新从用户源路径、解密 staging 或历史 manifest-only 记录旁路读取。
8. Export / SaveRebuild 的 Application 命令上下文必须显式携带 `workspaceKey / workspaceDataRootPath / bundleCacheKey`，并在计划阶段形成“目标 bundle -> artifact identity”的只读输入图；当前保存预览和保存骨架已从现有 `bundle-index + artifact manifest` 解析该输入图，但仍不触发 artifact materialization。
9. 保存预览和保存骨架应通过 `IWorkspaceSaveOutputProbe` 做目标输出路径预检，把已存在目标文件、目标路径是目录、目录链被文件阻塞、非法路径等输出 blockers 暴露为结构化 DTO；该 preflight 不创建目录、不写文件，也不替代真实写包阶段的最终冲突确认。
10. Infrastructure 写包 / 导出适配器只接收标准化 bundle 路径、loaded-session identity、操作变更集和输出路径；不得反向依赖 Application store 或 Presentation 状态。
11. 成功复用或生成 artifact 后应回写 `lastAccessedAtUtc`，并按现有偏好触发容量治理；当前正在参与导出 / 写包的 artifact 必须进入 `ProtectedBundleCacheKeys`，避免任务中途被自身 prune 删除。
12. 如果真实写包需要可变 bundle 上下文，仍应先以标准化 artifact 作为只读基线，再在写包工作目录或 staging 中创建可变副本；不得把 loaded-session 中的第三方对象暴露为跨层共享编辑状态。

禁止：

- 在 Presentation 中直接修改 bundle 文件。
- Export / SaveRebuild 绕过 artifact resolver 重新解密、重新解包或直接打开用户源 AB。
- 将导出 / 保存任务完成状态只存在 Shell 状态条中。
- 用后台任务队列顺序代替保存冲突检测。

## 14. 与缓存设计的关系

后台调度和缓存是协作关系：

- 调度器决定什么时候允许任务进入解包 / 读取 / 写出。
- 标准化 bundle 物理缓存决定是否需要重新解密、解压或标准化文件。
- loaded-session 缓存决定短时间内是否复用已经加载的 AB 上下文。
- 预览缓存决定是否可以直接返回已有 payload / sidecar。

调度器不直接规定缓存格式，也不把缓存命中写成操作记录。

当前已引入标准化 `cache/bundles` artifact、`cache/previews` 预览缓存和进程内 `AssetsToolsBundleLoadedSessionCache`：导入 / 预览 / 提取 / Spine 详情会先通过 `WorkspaceBundleArtifactResolver` 取得同一可读 bundle 路径；resolver 会对同一 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 的 artifact 生成请求做 single-flight 串行化，后续等待者在首个请求写入 manifest 后直接复用结果，并在保存或成功复用 manifest 时回写 `lastAccessedAtUtc`。`IBundleArtifactStore.GetUsage(...)` 已能统计 artifact 数量、字节数和访问时间，`Prune(...)` 已能按 `MaximumSizeInBytes / MaximumArtifactCount` 使用 LRU 删除最旧 artifact；`IPreviewCacheStore.GetUsage(...) / Prune(...)` 已能统计并清理预览描述、主 payload 和 `imageFocus` payload。`WorkspaceCacheMaintenanceGateway` 的 UI 聚合入口会同时汇总 / 清理标准化 bundle artifact 与预览缓存，工作台顶部状态标签也读取同一个聚合 usage。清理任务通过 `Maintenance` profile 投影到 Shell 状态条。导入、预览或提取首次保存 artifact 后，会按偏好设置触发当前工作区容量治理，并通过 `ProtectedBundleCacheKeys` 保护刚生成的 artifact，避免生成后立刻被自身 prune 删除；`GetAssetPreviewHandler` 保存预览 payload 后也会通过 `IPreviewCachePrunePolicy` 触发预览缓存容量治理，并用 `ProtectedPreviewCacheKeys` 保护刚生成的预览项。随后 `WorkspaceBundleArtifactResolver` 会从 manifest 派生 `bundle-artifact | workspaceKey | bundleCacheKey | normalizerVersion | bundleFingerprint` 形态的 loaded-session identity，Reader 和预览生成器把该 identity 传给 `AssetsToolsBundleLoadedSessionCache`；同一 identity 的 `LoadBundleFile / LoadAssetsFileFromBundle` 通过 single-flight 进入一次加载任务，成功后以 lease 方式短时复用，并用 30 秒滑动 TTL 驱逐。loaded-session 默认最多保留 6 个空闲会话，GC 内存压力达到阈值时退避到 1 个空闲会话；DEBUG 包会输出 `Acquire miss / in-flight / hit`、`Load start / completed / failed`、空闲过期和容量 / 内存压力淘汰事件，用于验证多预览、批量提取和 Spine 组提取是否复用同一加载会话；若调用方没有 artifact identity，Infrastructure 仍回退到物理路径 key。`AssetsToolsAssetPreviewGenerator` 会把 `OutOfMemoryException` 映射成 `Bundle.Preview.OutOfMemory`，`GetAssetPreviewHandler` 进一步落到 `AssetPreviewFallbackReasonKind.OutOfMemory`，Presentation 据此暂停同 bundle 的后台预热并在 DEBUG 输出 `preview.prewarm retreat-*` 诊断。`AssetsToolsAssetBundleReader` 和 `AssetsToolsAssetPreviewGenerator` 默认共享同一个进程内 cache；提取当前通过 `WorkspaceAssetExtractionExporter -> GetAssetPreviewHandler` 获取 payload，因此也会复用同一 loaded-session。

artifact 删除、prune、资源包移除、工作区记录删除、导入刷新失效和标准化版本失效时，Application 通过 `IBundleArtifactSessionInvalidator` 通知 Infrastructure 主动摘除对应 loaded-session 条目；新请求不会继续命中旧 identity，已经取得 lease 或正在底层同步读取的旧任务会自然完成，最后一个 lease 释放后再 dispose。

Export / SaveRebuild 共享 artifact resolver 已进入保存计划骨架的只读输入阶段：`GetWorkspaceSavePreviewQuery / SaveWorkspaceAsNewBundleCommand` 会携带 `workspaceKey / workspaceDataRootPath`，Application 通过 `bundle-index` 找到每个目标 bundle 的 `bundleCacheKey`，再用 `WorkspaceBundleArtifactResolver.TryResolveExistingInput(...)` 读取已有标准化 artifact manifest、回写 `lastAccessedAtUtc` 并派生 loaded-session identity。保存计划同时通过 `IWorkspaceSaveOutputProbe` 对目标输出路径做无副作用 preflight，并把输出路径可写、未探测、已存在文件、目标为目录、目录链被文件阻塞、非法路径和探测失败投影为结构化状态与诊断码。缺失、陈旧或物理路径不可解析的 artifact 只会在计划中标记诊断，不会在保存预览 / 保存骨架中触发解密、复制或 materialization；输出 preflight 也不会创建目录或写入文件。真实写包的最终冲突确认、取消、回滚和可变副本语义仍未定稿；后续实现时，Infrastructure 导出 / 写包适配器应直接消费这份只读输入图，并在需要时由真实计划阶段显式 materialize 缺失 artifact。当前已经解决同一进程内、同一 bundle artifact 生成、artifact 访问时间回写、artifact usage / LRU prune、显式 / 自动清理入口、同一 artifact identity loaded-session 重复加载、artifact 删除后的主动失效、loaded-session 容量上限、内存压力退避、预览 OOM 专用诊断、后台预热短期退避、SaveRebuild 保存计划读取现有 artifact identity，以及保存输出路径 preflight；下一步真实写包入口不得新增平行读取路径。

该段中的“未定稿”仅指最终冲突确认、取消、回滚和可变副本细节；核心边界已经固定为：修改阶段只记录和物化操作，真实 AB 替换在统一导出 / 保存重建阶段消费操作记录后执行。

## 15. 状态持久化边界

后台任务状态分三类：

| 状态 | 是否持久化 | 来源 | 用途 |
| --- | --- | --- | --- |
| Shell 运行中状态 | 否 | 调度器 / 状态条服务 | 当前会话反馈 |
| 页面浏览状态 | 是，轻量 | 工作区元数据 | 恢复标签、滚动、预览缩放等页面上下文 |
| 操作记录 | 是 | Application 操作记录 | 提取、替换、编辑草稿、保存检查点、撤销 / 恢复 |

应用重启后不恢复“正在跑的线程”。如果重启前任务已经写入 `Pending / Planned` 操作记录，则由操作记录恢复计划判断为 `Interrupted` 或其他可恢复状态；如果只是后台预热或状态条任务，则直接丢弃。

## 16. 当前落地状态

截至 2026-05-17，统一后台任务流程已完成第一阶段最小落地，并开始进入可观察任务模型：

- `Presentation.WinUI/Services/Background` 已新增 `BackgroundWorkRequest<T>`、`BackgroundWorkExecutionContext`、`IBackgroundWorkProgress`、`IBackgroundWorkScheduler`、任务 profile、语义分类和优先级枚举。
- `BackgroundWorkScheduler` 当前复用既有 `IBackgroundTaskRunner` 作为执行原语，并把可见任务生命周期投影到 `IAppBackgroundTaskStatusService`；`WorkspaceOpen / WorkspaceImport / PreviewForeground / PreviewPrewarm / SpineRuntimeFrame / BatchExtraction / BatchReplacement / SaveRebuild` 已有第一版 profile 闸门。
- `BackgroundWorkScheduler` 已提供 `StartAsync`、`BackgroundWorkHandle<T>`、`CurrentTasks`、`TasksChanged` 和 `TryCancel(taskId)`；任务快照包含 profile、语义分类、优先级、工作区 / bundle / resource 关联、标题、进度、状态、是否状态条可见和是否可取消。
- 当前运行中任务状态由 `BackgroundWorkTaskRegistry` 统一维护；调度器只负责执行原语、profile 闸门、取消源串联和状态条投影，避免后续导入、预览、提取、导出继续各自维护一套可观察任务表。
- Shell 底部状态条已支持当前可取消任务的图标取消按钮；当前接入的可取消任务包括工作区导入处理阶段、资源列表普通资源批量提取和 Spine 资源组批量提取。
- 设置与诊断页已通过 Application 的 `WorkspaceCacheMaintenanceGateway` 展示当前工作区聚合缓存 usage，统计范围包含 `cache/bundles` 标准化 bundle artifact 和 `cache/previews` 预览缓存；显式清理通过 `Maintenance` profile 同时清理两类可重建派生产物，不写操作记录，也不刷新资产列表。设置页还提供自动容量治理偏好，当前自动策略作用于标准化 bundle artifact 和预览缓存：导入 / 预览 / 提取首次生成 artifact 后会按当前工作区容量上限自动 prune，预览 payload 保存后也会按同一上限 prune 最旧预览项。
- 组合根已创建单例调度器，`App` 暴露同一 `BackgroundWorkScheduler` 给工作台、批处理页和独立资源编辑窗口使用，避免同类任务各自临时创建调度器。
- 资源列表多选批量提取、Spine 资源组多选批量提取、批处理页批量提取和工作区级批量提取对话框已迁移到 `BatchExtraction` profile；工作区顶部入口不再跳转批处理页执行；批处理页批量替换骨架已迁移到 `BatchReplacement` profile；保存骨架已迁移到 `SaveRebuild` profile。
- 工作区打开外层已迁移到 `WorkspaceOpen` profile；文件 / 目录 / 拖拽导入的处理阶段已迁移到 `WorkspaceImport` profile。Application 内部导入 worker 池仍保持在应用层一致性边界内。
- 普通资产详情预览已迁移到 `PreviewForeground / PreviewPrewarm` profile；前台资产预览、Spine 资源组详情和当前贴图页载荷会在 Shell 底部状态条显示轻量加载状态，后台预热保持静默；Texture2D / alpha page 载荷已接入 `PreviewForeground` profile；Spine 组详情初始打开使用 `InteractivePreview` 模式，跳过 skeleton 摘要和 Texture2D 尺寸诊断，无法形成可显示动画贴图载荷且快路径没有明确诊断时才补跑 `InteractiveDiagnostics`；Spine runtime 当前帧已接入独立 `SpineRuntimeFrame` profile。
- 快速切换资产详情标签时，旧活动标签会被显式降级为 `PreviewPrewarm`，新活动标签始终重新按 `PreviewForeground` 调度；前台预览请求在调用方取消后会释放前台 profile 槽，避免后台旧请求压住当前可见标签。后台预热候选会按最近激活、最近打开优先，而不是单纯按标签集合顺序。
- 预览 worker 总数已接入应用偏好设置和“设置与诊断”页，自动模式继续按 CPU 推导并限制在 `1-4`，手动值会同步影响调度器和预览查询执行器。
- Infrastructure 内部已接入 `AssetsToolsBundleLoadedSessionCache`，对同一标准化 artifact identity 提供 single-flight、lease、串行访问保护、30 秒滑动 TTL、默认 6 个空闲会话容量上限和 GC 内存压力下 1 个空闲会话退避；没有 artifact identity 的直接调用回退到物理路径 key。`AssetsToolsAssetBundleReader`、`AssetsToolsAssetPreviewGenerator` 和经预览管线导出的提取共享该 cache，减少导入读取、详情预览、Spine 贴图载荷和批量提取对同一 AB 的重复 `LoadBundleFile`；artifact 删除 / prune 时会主动失效对应 identity，避免物理缓存被清理后新请求继续使用旧 loaded session。
- 导入进度现在通过 `WorkspaceImportBackgroundProgressBridge` 同时投影给左侧导入遮罩和 Shell 状态条；操作记录与变更页刷新仍走既有 `IWorkspaceOperationChangeNotifier`，没有恢复手动刷新资产列表。
- 当前实现还没有完整任务详情弹层、全局 CPU/IO/memory 闸门或完成结果中心；loaded-session 容量上限和内存压力退避已先在 Infrastructure cache 内落地；SaveRebuild 保存计划已能读取现有 artifact identity，并能预检目标输出路径 blockers，但仍不执行真实写包或自动 materialize 缺失 artifact。

当前设计判断：后台调度、标准化物理缓存、loaded-session、预览缓存和保存输出 preflight 的职责边界已经清晰，足以继续承载提取 / 预览 / Spine 详情复用；容量治理已经补上显式清理、偏好驱动自动清理、artifact 删除后的 loaded-session 主动驱逐、loaded-session 容量上限和内存压力退避。缓存命中与清理细节本轮先通过 DEBUG 日志体现，其中 loaded-session 会直接记录 `Acquire miss / in-flight / hit` 和真实 `Load start / completed / failed`，不新增诊断面板；Export / SaveRebuild 已在保存骨架中形成只读 artifact 输入图和输出路径 blocker 预检，真实写包仍需在 M10 补最终冲突确认、取消、回滚、缺失 artifact materialization 和可变副本语义。

对应回归：

- `BackgroundWorkSchedulerTests` 覆盖调度器会复用底层 runner、把进度投影到状态服务，以及 `BatchExtraction` profile 会串行执行。
- `BackgroundWorkSchedulerTests` 覆盖 `StartAsync` 会暴露运行中任务快照、任务完成后清理快照、可取消 handle 会取消任务，以及状态条服务会暴露当前任务取消入口。
- `BackgroundWorkSchedulerTests` 覆盖被预览超时放弃的任务仍会持有 profile 闸门直到后台任务真实结束，避免旧请求仍在读取时新请求提前进入同一槽。
- `BackgroundWorkSchedulerTests` 覆盖前台预览请求在调用方取消时可以按请求策略释放 profile 槽。
- `BackgroundWorkSchedulerTests` 覆盖 `SpineRuntimeFrame` 与普通 `PreviewForeground` 使用独立 profile 槽。
- `BundleExplorerAssetDocumentTabCoordinatorTests` 覆盖快速切换时当前标签优先进入前台预览、旧标签降级为后台预热。
- `BundleExplorerAssetPreviewSchedulingTests` 覆盖前台资产预览 / Spine 组详情会投影到 Shell 状态服务、后台预热不投影状态服务，以及后台预热按最近激活标签优先调度。
- `SaveWorkspaceHandlersTests` 覆盖保存计划会暴露输出路径 preflight blockers，`FileSystemWorkspaceSaveOutputProbeTests` 覆盖不存在目标、已存在文件和目录链文件阻塞三类基础探测。

## 17. 建议迁移计划

### 第一阶段：抽象统一调度契约

- 已在 `Presentation.WinUI/Services/Background` 下建立调度请求、任务 profile、任务状态、进度模型和 `IBackgroundWorkScheduler`。
- 已保留现有 `IBackgroundTaskRunner` 作为底层执行原语。
- `IAppBackgroundTaskStatusService` 已先由调度器统一 begin/report/complete，后续再演进为更完整的状态投影订阅者。

### 第二阶段：接入当前可见后台任务

- 资源列表多选批量提取、Spine 资源组多选批量提取和批处理页批量提取已改为通过调度器提交 `BatchExtraction`。
- 批处理页批量替换骨架和保存骨架已分别通过调度器提交 `BatchReplacement / SaveRebuild`。
- 保留现有操作记录通知链路，不把页面刷新塞回任务完成回调。

### 第三阶段：接入预览执行队列

- `AssetPreviewQueryExecutor` 的前台 / 后台闸门已迁入调度器 `PreviewForeground / PreviewPrewarm` profile。
- Spine 详情和贴图载荷加载已改走 `PreviewForeground` profile；Spine 组初始交互预览先加载播放所需 skeleton / atlas / runtime 载荷，播放不可用时再补跑完整诊断，避免打开标签时默认执行 Texture2D 尺寸校验和 atlas overlay 解码。
- 预览 profile 已区分取消 / 超时后的槽位释放语义：超时放弃的任务仍会持有 profile 闸门直到后台任务真实结束；前台预览在调用方取消时可释放 `PreviewForeground` profile 槽，用于快速切换场景。
- 快速切换资产详情标签时，旧活动标签取消前台请求并降级为 `PreviewPrewarm`，新活动标签始终按 `PreviewForeground` 调度。
- 后台预热已改为按最近激活、最近打开优先提交候选；已不再把单纯集合顺序当作预热顺序。
- Spine runtime 当前帧已接入 `SpineRuntimeFrame` profile，与普通详情预览分槽。
- 预览 worker 总数已接入应用偏好设置，自动或手动 `1-4` 会同时影响调度器预热槽和预览查询执行器。
- Infrastructure 内部已引入 loaded-session single-flight，用于在 Reader、预览生成器和经预览管线导出的提取之间按标准化 artifact identity 复用同一 bundle 的已加载上下文；当前已补默认容量上限和 GC 内存压力退避，memory-heavy 全局闸门仍留到后续阶段。

### 第四阶段：接入工作区打开与导入外层任务

- `WorkspaceImportCoordinatorService` 和 `BundleExplorerWorkspaceBindingCoordinator` 已改为提交 `WorkspaceOpen / WorkspaceImport`。
- Application 内部导入 worker 池保持不变。
- 导入进度已同时投影到左侧覆盖层和 Shell 状态条。

### 第五阶段：完善缓存 single-flight 与导出 / 保存

- 当前基于标准化 bundle artifact 身份的 AssetsTools loaded-session single-flight 已补容量上限和内存压力退避；SaveRebuild 已补保存计划只读 artifact 输入图，后续补真实导出 / 写包执行入口。
- 序列帧导出、Spine 导出、真实保存 / 重建按 `Export / SaveRebuild` profile 接入。
- 保存 / 重建取消、回滚和检查点语义先在 Application 文档和测试中固化，再实现真实写包。

## 18. 测试建议

Presentation 测试优先覆盖：

- 高优先级前台预览不会被后台预热饿死。
- 批量提取进度会合并投影到状态条，不逐项直接刷 UI。
- 任务取消后旧任务异常被观测。
- 同工作区可见任务按 profile 进入正确闸门。
- 操作完成后通过操作变更通知刷新读模型，而不是手动刷新资产列表。

Application 测试继续覆盖：

- 导入执行闸门和 bundle worker 并发度。
- 导入 `串行计划 -> 有界并行 worker -> 串行提交` 顺序。
- 提取 / 替换 / 保存操作记录和恢复计划。
- 保存 / 重建进入真实写包后的取消、失败和回滚语义。

Infrastructure 测试优先覆盖：

- 标准化 bundle artifact 的失效与清理。
- loaded-session single-flight、lease、滑动 TTL 和容量上限。
- 预览缓存命中 / 失效与 payload 缺失容错。
- 文件写出、外部进程和缓存目录布局。

## 19. 收口结论

后续后台任务默认按以下规则实现：

- 页面只提交任务，不直接创建散落的线程池策略。
- `IBackgroundTaskRunner` 只保留为执行原语，不再承载优先级、状态条和并发策略。
- 统一调度器负责 Presentation 层的 profile、闸门、取消、进度和状态投影。
- Application 内部 worker 池只在业务一致性需要时存在，并继续由 Application 自己测试和维护。
- Shell 状态条只表达当前会话瞬时进度，不替代操作记录、日志、toast、变更页或保存检查点。
- 预览、提取、Spine 解析和后续导出应共享 bundle single-flight 与 loaded-session 缓存，避免同一 AB 被多线程重复物理解包。
