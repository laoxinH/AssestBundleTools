# 资源预览载荷安全与文本编辑承载计划

## 1. 文档目的

本文档用于把资源预览从“后台执行已经足够安全”的阶段性理解，收束为一套更长期的载荷契约和阶段计划。

本次触发点是 atlas `TextAsset` 被替换为约 12 MB 任意 JSON 后，Spine 组预览长时间停留在加载态并影响主窗口交互。但同类风险不只存在于 atlas：

- `TextAsset / Shader / .json / .skel / .atlas / .bytes` 等文本或准文本资源可能被替换为巨大、错误格式或恶意构造内容。
- `MonoBehaviour` TypeTree、`AnimatorController`、`AnimationClip`、`GameObject` 等结构化摘要可能生成过大的字段树、列表或 JSON 文本。
- `Texture2D / Sprite / AudioClip / Mesh` 等媒体资源可能在解码、转码、几何投影或缩略图生成阶段触发超大内存、超长 CPU 或第三方库不可取消问题。
- 导出资源处理器和后续插件可能返回过大的 result JSON 或诊断文本。
- 后续若增加 `TextAsset / Shader` 直接编辑、JSON 美化、行号、搜索、差异和快速修改，必须既能查看完整内容，又不能把完整大文本直接绑定到 WinUI 控件。

因此本文档的目标不是只修复一个 Spine case，而是定义所有预览类型都应遵循的安全边界、DTO 形态、文本查看 / 编辑演进方向和分阶段执行顺序。

## 2. 设计结论

1. 后台任务是执行边界，不能替代预览载荷边界。`IBackgroundWorkScheduler` 负责把工作移出 UI 线程、调度并发、处理取消和状态条投影；它不负责保证返回给 Presentation 的 DTO 足够小。
2. Presentation 可绑定的预览数据必须有界。任何进入 WinUI 绑定、布局、文本控件、列表控件、画布或 renderer 的字符串、集合、二进制和几何数据都必须经过 Application 定义的预算策略裁剪或降级。
3. 完整内容与展示内容必须分离。展示字段只承载可直接绑定的小样本；完整文本、完整 TypeTree、原始媒体、外部工具原始输出等必须通过 sidecar、缓存 payload reference 或后续 chunk query 按需读取。
4. 第三方解析器和 codec 入口必须有前置 guard。不能假设第三方 Spine runtime、图片解码器、音频转码器、JSON parser 或 AB writer 能及时响应取消令牌。
5. 文本预览和文本编辑是两个模式。普通预览默认快速、轻量、有界；完整查看和编辑必须走虚拟化 / 分块 / 文件背书的文本工作面，避免把大文件整段放入 `TextBox` 或绑定字符串。
6. 富预览失败必须回退为结构化诊断。失败原因应进入本地化诊断 DTO 和详情信息卡，不应只留在日志或异常消息中。

## 3. 分层边界

### 3.1 Presentation

Presentation 负责：

- 调度预览请求，展示加载、取消、超时和诊断状态。
- 只绑定 Application 返回的有界展示模型。
- 在未来文本编辑器中请求文本块、行号范围、搜索结果和格式化片段，而不是直接读取文件或缓存目录。
- 对长列表、长文本、帧数据、字段树和缩略图使用虚拟化或分页控件形态。

Presentation 不负责：

- 直接读取 AB、替换源文件、预览缓存 sidecar 或工作区 staging 文件。
- 解析 atlas、skel、JSON skeleton、TypeTree、Mesh、图片或音频格式。
- 把 `FullTextContent`、原始 stdout / stderr 或完整插件 result JSON 直接绑定到 UI。

### 3.2 Application

Application 负责：

- 定义预览预算策略、DTO 契约、诊断语义和本地化消息 token。
- 统一解析“当前有效内容”：原始资源、工作区替换、文件替换、编辑草稿和 Spine 组组成文件草稿都应进入同一套 preview source resolution。
- 对所有 preview generator 输出做展示裁剪、集合截断、摘要降级和诊断补充。
- 提供后续 `LoadPreviewTextChunkQuery`、`SearchPreviewTextQuery`、`FormatPreviewTextRangeQuery` 等按需读取契约。

### 3.3 Infrastructure

Infrastructure 负责：

- 具体 AB reader、preview generator、sidecar materialization、转码、图片 / 音频 / runtime adapter 和缓存读写。
- 在调用第三方库前执行轻量 preflight guard，例如大小、维度、行数、深度、token 数、像素数、采样数、顶点数和已知错误外形检查。
- 捕获第三方异常并返回结构化技术诊断给 Application 包装，不把外部原始异常消息作为用户主提示。

## 4. 统一预览载荷契约

后续预览 DTO 应逐步收敛到下面的原则。

### 4.1 可绑定展示字段

所有可绑定字段必须满足：

- 字符串有最大字符数和最大行数。
- 集合有最大返回项数，并携带 `TotalCount / ReturnedCount / HasMore` 或等价摘要。
- 二进制只承载已确认可直接展示的小型 payload，例如缩略图、转码后音频片段或当前帧贴图；原始大文件通过 sidecar reference 读取。
- 几何数据只承载预览所需子集；过大 Mesh、过多动画曲线、过多 TypeTree 字段应回退为计数摘要和诊断。

当前 `AssetPreviewDto.TextContent` 已作为展示字段使用，应保持有界。`FullTextContent` 属于过渡字段，后续应逐步替换为 payload reference 或只在 Application 内部消费；Presentation 不得直接绑定该字段。

### 4.2 完整载荷引用

需要完整内容时，DTO 应返回类似下面的语义，而不是直接返回完整大对象：

```text
PreviewPayloadReference
  PayloadId / CacheKey
  ContentHash
  ContentLength
  ContentType
  Encoding
  SourceFingerprint
  GeneratorVersion
  BudgetPolicyVersion
  AvailableOperations: ReadChunk / Search / FormatRange / Export / EditDraft
```

payload reference 可以指向：

- `cache/previews` 中的预览 sidecar。
- 工作区 staging 中的替换源副本。
- 标准化 bundle artifact 派生出的临时只读 payload。
- 后续文本编辑草稿文件。

Presentation 只能把 reference 传回 Application query，不能自行拼路径读取。

### 4.3 诊断和预算结果

预算触发不应只表现为“预览不可用”。DTO 应明确表达：

- 展示内容是否被截断。
- 完整内容是否可按需读取。
- 是显示预算超限、结构化解析预算超限，还是第三方 runtime / codec 被 guard 跳过。
- 当前预览是富预览、摘要回退、文本样本，还是只显示诊断。

建议逐步统一诊断语义：

```text
Preview.DisplayTruncated
Preview.PayloadTooLarge
Preview.StructureBudgetExceeded
Preview.FormatMismatch
Preview.ParserSkippedByGuard
Preview.RuntimeSkippedByGuard
Preview.FullContentAvailableOnDemand
Preview.FullContentUnavailable
```

这些诊断应有本地化主提示；技术细节进入详情卡、日志或复制诊断入口。

## 5. 文本预览与未来文本编辑

### 5.1 普通文本预览

适用对象：

- `TextAsset`
- `Shader`
- Spine atlas 文本
- Spine JSON skeleton
- MonoBehaviour / TypeTree JSON 摘要
- 外部工具 result JSON 的可读摘要

普通预览目标是快速判断内容和状态：

- 默认只加载展示样本，例如前若干字符 / 行。
- 展示编码、换行类型、总大小、估计行数、是否截断、是否可完整打开。
- JSON / atlas / shader 等格式识别只做轻量检测；深度解析必须受预算控制。
- JSON 美化只在大小、深度、token 数和行数预算内执行；超过预算时显示原始样本和“可完整打开 / 可按块查看”入口，不做全量 pretty print。
- 行号展示必须支持虚拟化，不能因为显示行号而预先拆分全部行。

### 5.2 完整文本查看

完整查看不是普通预览的放大版，而是独立的按需模式：

- 通过 Application chunk query 按范围读取文本。
- 文本块可以按字节范围、字符范围或行范围请求；具体策略由实现阶段根据编码和换行索引决定。
- 首次打开完整查看时可以后台建立轻量行索引，但索引构建必须可取消、可缓存、可降级。
- 搜索应优先流式或分块执行，返回有限数量结果和 `HasMore`。
- 大文件跳转行号时允许先估算再校准，不要求一次性精确加载全部行。

### 5.3 文本编辑模式

后续 `TextAsset / Shader` 直接编辑应满足：

- 编辑草稿使用工作区 staging 文件或专用 draft store 背书，不把完整文本放在 ViewModel 字符串属性里。
- UI 编辑控件必须支持虚拟化或至少有明确的大文件降级策略；普通 WinUI `TextBox` 不能作为大文本编辑的稳态承载。
- 编辑态保存时追加可恢复操作记录，仍由保存 / 重建链路消费；不直接覆盖原始 AB。
- JSON 美化、压缩、校验、atlas 校验等格式工具都是编辑器命令，必须走后台任务和预算策略。
- 格式化命令默认只对可承受范围执行；对大 JSON 提供范围格式化、结构折叠或只读结构索引等渐进能力。
- 未保存草稿、撤销 / 重做、关闭确认和多窗口显示同一文本时的状态归属，应沿用资源编辑窗口的页面级状态和工作区操作记录规则。

## 6. 各资源类型预算方向

下面是阶段性预算方向，具体数值后续应集中到统一 policy 中，避免散落在各 preview generator 内。

| 类型 | 主要风险 | 初始 guard 方向 |
| --- | --- | --- |
| `TextAsset / Shader` | 大文本绑定、错误编码、超大 JSON 美化 | 展示样本有界；完整文本走 reference；编码检测和换行统计有预算 |
| Spine atlas | 任意 JSON / 大文本被当作 atlas；region/page 过多；runtime parser 不可取消 | 已有大小、行数、page、region、JSON 外形 guard；后续迁入统一 policy |
| Spine skeleton JSON / `.skel` | 大 JSON、深层嵌套、动画 / attachment 过多、runtime parser 不可取消 | 增加大小、深度、token、skin、animation、attachment、mesh 顶点预算；runtime 前置 guard |
| `MonoBehaviour` / TypeTree | 字段树过深、数组过大、TypeTree JSON 过长 | 字段数、深度、数组项、JSON 输出长度有界；完整字段树走 sidecar |
| `AnimatorController` | 状态机、转场、参数过多 | Layer、Parameter、State、Transition 返回项数有界，细节分页 |
| `AnimationClip` | 曲线和 keyframe 过多 | binding、曲线、事件、keyframe 摘要有界，完整曲线后续按需查看 |
| `Mesh` | 顶点 / 三角面 / UV 过多导致投影和渲染压力 | 预览几何上限、抽样或摘要回退；贴图叠加前检查三角和 UV 预算 |
| `Texture2D / Sprite` | 超大像素、异常格式、解码内存峰值 | 解码前检查尺寸、像素数、格式支持；必要时缩略图 / 降采样；编辑态只保留当前所需画布 |
| `AudioClip` | 长音频转码、波形采样过大、格式异常 | 转码输入大小、时长、采样率、声道和波形采样点有界 |
| `Material / GameObject / Font` | 引用和字段过多 | 摘要卡有界，引用明细分页或截断诊断 |
| 外部 CLI / 插件 result JSON | 结果文件巨大、结构异常、stderr 过长 | result JSON 大小、深度和消息长度有界；原始 stdout / stderr 只作诊断明细 |

## 7. 后台任务与预览契约关系

后台任务继续遵循 [background-task-scheduling.md](background-task-scheduling.md)，但需要明确：

- 超时只是调用方停止等待，不保证底层同步解析已经终止。
- 取消只对观察 cancellation token 的代码有效，不应作为第三方 parser / codec 的主要保护。
- 前台预览槽只能保证当前可见标签优先，不保证返回内容适合 UI 绑定。
- 预览 prewarm 必须更保守；后台预热不应生成完整文本、完整 TypeTree 或大媒体 payload。
- 当调用方取消或标签失焦时，已返回但过期的预览结果不得继续投影到 UI。

因此每个 preview generator 都应先做轻量 preflight，再进入重型解析；每个 Presenter 都应假设 DTO 已经有界，并在调试 / 测试中拒绝绑定明显超过预算的内容。

## 8. 分阶段执行计划

### 阶段 0：文档和风险盘点

目标：

- 落本文档，建立长期契约。
- 盘点当前所有 `AssetPreviewDto` 字段、preview generator、presenter、Spine group details、replacement effective preview 和 preview cache hydration 路径。
- 标记仍可能把完整文本、完整集合或大二进制带到 Presentation 的过渡点。

验收：

- 文档索引已加入本文。
- 有一份实现清单列出每个预览类型的现状、风险和下一阶段处理顺序。

### 阶段 1：统一展示预算 policy

目标：

- 在 Application 层建立统一 `PreviewPayloadBudgetPolicy` 或等价服务 / 常量集。
- 把文本展示裁剪从单点实现迁到统一 helper，覆盖原始预览、文件替换预览、工作区替换预览、编辑草稿预览和缓存恢复。
- DTO 增加截断、完整大小、估计行数、格式识别和完整内容可用性等元信息。
- Presentation presenter 增加防线：即使异常路径返回大文本，也不直接绑定。

验收：

- 大文本 `TextAsset / Shader / atlas / skel JSON` 在普通预览中都只显示有界样本。
- 原始预览、替换预览和缓存命中路径行为一致。
- Application / Presentation 回归覆盖长文本、错误编码、缓存恢复和替换来源。

### 阶段 2：结构化解析与第三方入口 guard 扩面

目标：

- 将 Spine atlas 现有 guard 迁入统一 policy 并保留现有行为。
- 给 JSON skeleton、binary skeleton runtime adapter、MonoBehaviour TypeTree、AnimatorController、AnimationClip、Mesh、图片和音频 generator 增加预算。
- 给外部 CLI result JSON 解析和诊断文本采集增加大小 / 深度 / 消息长度限制。

验收：

- 任意大 JSON 替换 atlas、skel、普通 TextAsset 或 Shader 都不会让主窗口长期加载。
- 错误图片、超大图片、错误音频、超大 Mesh 和异常 TypeTree 都能回退为结构化诊断。
- Infrastructure 测试覆盖第三方 parser / codec 被 guard 跳过的路径。

### 阶段 3：完整文本 reference 和分块读取

目标：

- 为文本预览引入 payload reference，不再依赖 Presentation 可见 DTO 携带完整文本。
- 增加按需读取文本块、估计行数、建立行索引、搜索片段的 Application query。
- 预览缓存和工作区 staging 记录 payload hash、长度、编码、generator version 和 budget policy version。

验收：

- 普通预览不会传递完整大文本。
- 用户可显式打开完整文本查看，并能在大文件中滚动、跳转和搜索而不冻结主界面。
- cache prune / workspace close 后，payload reference 失效能返回可理解诊断。

### 阶段 4：文本格式化和直接编辑工作面

目标：

- 实现 `TextAsset / Shader` 编辑态的文件背书草稿。
- JSON 美化、压缩、校验和行号展示走后台任务与预算策略。
- 对大 JSON 提供降级能力：原始查看、范围格式化、分块搜索、结构索引或只读摘要。
- 保存仍追加工作区可恢复操作记录，不直接写 AB。

验收：

- 小型 JSON 可以美化编辑；大型 JSON 不会一次性 pretty print 到 UI。
- 编辑态撤销 / 重做、未保存确认、关闭窗口确认和多窗口同资产刷新符合资源编辑窗口规则。
- 文本修改后，普通预览、Spine 组有效预览和导出写包都消费同一当前有效内容。

### 阶段 5：Presenter 与控件承载加固

目标：

- 检查 `InspectorPreviewSurfaceView`、文本预览控件、信息卡、结构摘要卡、Spine overlay、Mesh renderer 和 D3D / Win2D fallback 的输入大小假设。
- 长列表、长字段树、长诊断、长路径和长 stdout / stderr 使用折叠、分页、复制诊断或按需展开。
- 增加 Presentation 层测试，验证 presenter 不绑定超过预算的大字符串或大集合。

验收：

- 大载荷回归测试中 UI 仍可切换标签、关闭窗口、取消任务和打开设置页。
- 状态条和 toast 只显示用户友好摘要，不显示原始英文异常码或外部工具长输出。

### 阶段 6：观测、压力样本和回归基线

目标：

- 建立一组本地压力样本：大 atlas、大 skel JSON、大普通 TextAsset、大 Shader、大 TypeTree、大 Mesh、坏图片、坏音频和超大 CLI result JSON。
- 给预览路径增加轻量 telemetry 或日志摘要：输入大小、输出大小、是否截断、是否 guard 跳过、耗时、是否命中缓存。
- 把预算 policy version 纳入 preview cache 失效条件。

验收：

- 每次预算策略变更能让旧 preview cache 正确失效。
- 压力样本测试可以在开发机稳定复现并验证 UI 不冻结。
- 文档记录当前预算默认值和调整原则。

## 9. 测试策略

### 9.1 Application 测试

- 长文本原始预览、文件替换预览、工作区替换预览和缓存恢复都返回有界展示文本。
- `FullTextContent` 过渡字段不被 Presentation 必需路径依赖；后续 reference query 能读取完整内容。
- atlas、skel JSON、普通 JSON、Shader 和错误编码文本都有结构化诊断。
- TypeTree、AnimatorController、AnimationClip 等摘要集合超过预算时返回计数、截断标记和诊断。

### 9.2 Infrastructure 测试

- 第三方 Spine runtime adapter 在超大 / 错误 atlas 或 skeleton 前被 guard 跳过。
- 图片、音频、Mesh 和 TypeTree generator 在预算外输入上返回摘要或失败诊断，不抛未包装异常。
- CLI result JSON 超限时保留稳定错误码和有限诊断明细。

### 9.3 Presentation 测试

- Presenter 不把超预算文本传入文本控件。
- 长诊断、长路径和长信息卡不会造成布局异常或明显卡顿。
- 过期预览请求返回后不会覆盖当前标签。
- 完整文本查看 / 编辑工作面在后续实现后覆盖滚动、跳转、搜索、取消和关闭确认。

### 9.4 手动压力测试

推荐样本：

- 用 12 MB JSON 替换 Spine atlas。
- 用大 JSON 替换 Spine skeleton。
- 用 20 MB 文本替换普通 `TextAsset` 或 `Shader`。
- 用错误格式图片替换 `Texture2D`。
- 用超大图片替换 `Texture2D` 并切换 Sprite 编辑态。
- 打开包含大量字段的 `MonoBehaviour` 和大量状态的 `AnimatorController`。
- 让 CLI 资源处理器返回超大 result JSON 和 stderr。

验收目标：

- 主窗口可继续切换资源、关闭标签和取消任务。
- 预览在预算内完成，或在可接受时间内返回诊断。
- 用户看到的是本地化摘要和可操作建议，详细技术信息可复制但不挤占主 UI。

## 10. 当前状态与下一步建议

当前已经完成：

- atlas `TextAsset` 替换为巨大 / JSON 错输入时，Spine 组详情会快速返回诊断并跳过 texture page、overlay 和 runtime 尝试。
- 文件替换 `TextAsset` 的普通展示文本已做有界裁剪，同时保留完整内容供 Application 内部结构化处理和导出链路使用。
- Spine runtime adapter 已在 V3.8 / V4.2 的预览和 playback session 前加入 atlas 输入 guard。

推荐下一步按阶段 1 开始：

1. 盘点当前所有 `AssetPreviewDto` 生产和消费路径，列出仍携带完整 payload 的字段。
2. 提取统一 preview text budget helper，先覆盖所有文本展示路径。
3. 给 DTO 增加 `IsTruncated / FullLength / EstimatedLineCount / FullContentReference` 方向的过渡字段。
4. 补齐 Application 与 Presentation 回归，确认普通预览、替换预览和缓存恢复行为一致。

阶段 1 完成后，再推进阶段 2 的各类型 parser / codec guard 扩面。

## 11. 待决问题

- 预算默认值是否允许用户在“设置与诊断”中调整，还是仅作为开发常量。
- 完整文本查看的 chunk 单位采用字节、字符还是行；不同编码下如何快速建立稳定行索引。
- 未来文本编辑控件选型和大文本虚拟化能力，需要在实现前做单独技术 spike。
- JSON 美化是否需要支持真正流式格式化，还是第一阶段只支持预算内全量格式化和预算外只读摘要。
- 完整文本草稿和 preview sidecar 的缓存、prune、恢复和操作记录引用关系如何细化。
