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
7. 可选工具缺失属于可恢复预览状态。前台预览可以通过结构化组件 id 交给统一在线组件安装链路，经用户确认、可信下载和校验安装后重新加载；预览生成器、ViewModel 和后台预热不得自行下载或弹窗。具体规则见 [online-component-installation.md](online-component-installation.md)。

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
- `GetAssetPreviewQuery` 默认继续返回当前有效内容；只有必须以不可变 AB 原始载荷为基线的校验用例，才允许显式请求 `Original` 内容范围。`Original` 范围必须绕过文件替换、工作区替换、文本编辑草稿、预览来源覆盖和有效内容投影，但仍复用同一套原始预览缓存与 generator 管线，调用方不得自行读取 AB 或缓存文件。
- 对所有 preview generator 输出做展示裁剪、集合截断、摘要降级和诊断补充。
- 提供 `LoadPreviewTextChunkQuery`、`LoadPreviewTextLinesQuery`、`SearchPreviewTextQuery`、后续 `FormatPreviewTextRangeQuery` 等按需读取契约。

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

当前 `AssetPreviewDto.TextContent` 已作为展示字段使用，应保持有界。`FullTextContent` 属于过渡字段，后续应逐步替换为 payload reference 或只在 Application 内部消费；Presentation 不得直接绑定该字段。Application 内部结构化解析应先把预览结果统一解析为 `AssetPreviewTextSourceDto`：小文本可以使用 inline 文本，截断文本必须优先使用 `FullContentReference` 并通过 Application query 背书的 `TextReader` / chunk reader 读取；`FullTextContent` 只作为 bounded / 兼容输入，不得成为大文本结构化解析的必要条件。当 `TextDisplay.IsTruncated=true` 且没有完整 reference 时，解析器必须返回结构化诊断或跳过解析，不能把截断样本当作完整 atlas / skeleton / JSON 输入。

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

当前已落地的过渡形态是 `AssetPreviewFullContentReferenceDto`：它记录 `Kind / ContentType / ContentLength` 以及按来源区分的引用字段。`PreviewCache` 使用 `WorkspaceKey / PreviewCacheKey` 指向 `cache/previews` payload；`FileReplacement` 使用 `SourcePath` 指向当前文件替换 / override 来源；`EditDraft` 使用 `WorkspaceKey / DraftId / DraftMode / DraftRevision` 指向 `staging/drafts` 中的文本草稿读模型。该读模型不再等价于单个完整 `text.txt` 文件：已物化小文本仍可由 `text.txt + *.lines.json` 背书，patch-backed 草稿则由完整 payload 或稳定 `text-base.txt` 快照加 `text-patches.json` manifest 合成当前内容；带 `DraftRevision` 的引用读取对应 patch revision，缺失 revision 的旧引用才兼容读取草稿当前头部；编辑器保存必须把打开时的 `DraftRevision` 作为 patch base revision 传回 Application，确保删除较新的操作记录后重新编辑时继续基于当前有效 revision，而不是草稿当前 head；`sourceReference` 只用于 deferred 草稿首次保存时定位源并写出稳定 base snapshot，不作为保存后长期读取的唯一依赖。工作区内“从另一个资产替换”的 TextAsset 预览不新增 reference kind，而是继承源资产 Text 预览的 `PreviewCache` reference。Presentation 仍只能把 reference 传回 Application query，不能自行读取缓存路径、替换文件或草稿目录。`LoadPreviewTextChunkQuery` 已经可以通过 Application gateway 读取有界文本片段，当前 offset / length 以规范化后的字符位置为单位；`LoadPreviewTextLinesQuery` 已经可以读取规范化行范围，服务后续行号、滚动和编辑器虚拟化；`SearchPreviewTextQuery` 已经可以返回有界匹配列表、匹配行列、有限行片段和 `HasMore`。`AssetPreviewTextSourceDto` 是 Application 内部消费完整文本的统一轻量源描述，Spine atlas 结构化详情、Spine JSON skeleton 摘要和官方 runtime preview / playback 都应优先携带该 source，而不是在 DTO 之间传递完整大字符串；`AssetPreviewTextSourceReader` 通过 `LoadPreviewTextChunkQuery` 为 reference-backed source 提供 `TextReader`，让 atlas 解析器按行读取，让 runtime adapter 在自身解析边界读取完整 atlas / skeleton。`IPreviewCacheStore.LoadTextChunk / LoadTextLines / SearchText` 是预览缓存完整文本读取边界，`IReplacementPreviewSourceProvider.LoadFileTextChunkAsync / LoadFileTextLinesAsync / SearchFileTextAsync` 是文件替换文本读取边界，`IAssetEditDraftTextStore.LoadTextChunk / LoadTextLines / SearchText` 是编辑草稿完整文本读取边界；真实 Infrastructure 实现必须优先分块 / 流式读取，并在 preview cache 和完整文本草稿 payload 写入时生成可重建的 `*.lines.json` 稀疏索引 sidecar，patch-backed 草稿则通过 composite reader 从 base snapshot 与 patch manifest 计算 chunk、行范围、搜索和统计。该 sidecar 当前为 schema v2，包含行锚点与规范化字符 offset 锚点，`LoadTextChunk / LoadTextLines / SearchText` 会从最近锚点继续扫描；sidecar 缺失、版本不匹配或 payload 身份不匹配时自动退回顺序扫描，避免为了普通分块、分行或搜索查询把整个 payload 拷贝进内存。

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

### 4.4 资源大小语义

资产读取、预览和提取链路必须区分以下大小，不得用一个通用 `Size` 在不同阶段互相覆盖：

- **源数据大小**：资源在 Unity 中实际引用的载荷长度。`Texture2D` 优先使用 `m_StreamData.size`，再使用 `m_CompleteImageSize`；`AudioClip` 优先使用 `m_Resource.m_Size`，内联载荷使用 `m_AudioData` 长度。资产列表、大小排序和详情基础信息使用这一口径；无法取得载荷元数据时才回退到序列化记录大小。
- **序列化记录大小**：SerializedFile 中对象记录自身的字节长度。它用于解析诊断和存储结构说明，不代表图片或音频源载荷；当它与源数据大小不同时，详情基础信息应单独展示。
- **解码后大小**：例如纹理解码为 RGBA 后的像素缓冲长度。它只属于预览诊断，不作为列表大小或源数据大小。
- **预览 / 提取输出大小**：例如 FSB5 转码得到的 WAV、纹理编码得到的 PNG 文件长度。它描述派生文件，不得覆盖资产详情中的源数据大小或序列化记录大小。

`AssetPreviewDto.PreferredDisplaySizeInBytes` 仍可参与具体预览摘要，例如同时显示音频原载荷与转码输出；Presentation 在应用预览结果时不得用该字段反向改写资产详情基础信息。源数据大小应在 reader 建立资产索引时通过稳定元数据写入资产 DTO，避免为了列表展示触发完整预览或导出。新增或修改索引元数据语义时必须同步提升 reader parser version，使旧工作区缓存自动失效重建。

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

`TextAsset / Shader` 直接编辑应满足：

- 入口必须与当前文件替换快捷入口分离：当前检查器和普通资源行的 `替换文件` 只负责选择外部文件并追加 `ReplaceFromFile`；正式文本编辑使用独立 `编辑` 命令，打开或激活资源编辑窗口中的窗口化文本编辑模式。
- 编辑草稿使用工作区 staging 文件或专用 draft store 背书，不把完整文本放在 ViewModel 字符串属性里。
- UI 编辑控件必须支持虚拟化或至少有明确的大文件降级策略；普通 WinUI `TextBox` 不能作为大文本编辑的稳态承载。
- 编辑态保存时追加可恢复操作记录，仍由保存 / 重建链路消费；不直接覆盖原始 AB。
- JSON 美化、压缩、校验、atlas 校验等格式工具都是编辑器命令，必须走后台任务和预算策略。
- 格式化命令默认只对可承受范围执行；对大 JSON 提供范围格式化、结构折叠或只读结构索引等渐进能力。
- 文本编辑体验长期参考 VS Code / Monaco 的交互标准：行号 gutter、当前行高亮、搜索 / 替换、跳转行号、缩略滚动条、括号 / JSON 结构辅助、主题一致性和键盘快捷键都应作为正式编辑器方向；实现时仍必须保持仓库分层边界，编辑器只渲染当前可见范围或受控缓冲区，内容读取、搜索、格式化、保存和草稿生命周期继续通过 Application query / command。技术 spike 已在 [text-editor-technology-spike.md](text-editor-technology-spike.md) 收束：当前主实现采用 WebView2 + 离线 Monaco，并封装在 `TextDocumentSurfaceView` 内；原生 WinUI 行列表 / `TextBox` 只作为 fallback surface，不得绕过 Application query / command。
- 未保存草稿、撤销 / 重做、关闭确认和多窗口显示同一文本时的状态归属，应沿用资源编辑窗口的页面级状态和工作区操作记录规则。

当前第一阶段已经落地独立 `编辑` 入口和窗口化编辑壳：`TextAsset / Shader` 可以从检查器或资源编辑窗口进入 draft-backed 文本工作面。打开编辑器会创建 `text-content` 草稿并写入 `editDrafts[]`；进入编辑前会重新查询当前资产详情，如果详情已有有效 `TextEditDraftId / TextEditDraftMode / TextEditDraftRevision`，则优先通过 `OpenTextAssetEditDraft` 打开该当前有效草稿 revision，并以 `IsAppliedBaseline=true` 标记该引用来自当前已应用状态；只有详情没有有效文本编辑时，标签携带的未应用 `EditDraftReference` 才作为恢复引用打开，不重新创建或覆盖草稿。独立资源编辑窗口中若已经存在干净的已应用文本编辑态，用户再次进入编辑前也必须先刷新目标资产详情；若当前详情已回退到其他 revision 或原始内容，窗口必须退出旧编辑态并按当前详情重新打开，不能因为窗口内残留的 `TextEditDraft` 直接复用上次保存的草稿 head。Presentation 侧已经集中用有效文本草稿引用解析器把详情投影变成 `EditDraftReference`；Application 打开已应用 baseline 草稿时必须重新从 `operations[]` 物化目标资产当前有效 `text-content` 编辑，并以该事实流 revision 作为最终读取 pin，调用方显式 `DraftRevision` 和会话详情 revision 只作为投影输入 / 旧引用兼容，不得覆盖事实流。若操作记录删除后目标资源已经没有有效文本编辑，已应用 baseline 打开必须失败并触发 Presentation 清理旧引用；若回退到上一条记录，则预览和编辑都读取上一条 `EditDraftApplied.result.draftRevision`。草稿创建、恢复打开、正文更新和保存应用必须保留同一个 descriptor `source fingerprint`，并把它投影回资产标签的 `EditDraftReference`，避免窗口快照恢复时把自身草稿误判为来源不匹配。小型 `TextAsset` 在 `AssetTextEditPolicy.InlineEditableTextLength` 预算内开放正文编辑，保存时先用 `UpdateTextAssetEditDraft` 提交 range patch 并追加 `EditDraftUpdated`，再用 `ApplyTextAssetEditDraft` 追加 `EditDraftApplied`；Application 会把补丁交给 `IAssetEditDraftTextStore.ApplyTextPatch`，由 store 校验基线、追加 patch manifest、更新 descriptor 统计并提供 composite reader。每条 `EditDraftApplied` 都记录本次应用的 `DraftRevision`，因此多次编辑可保留多条记录，删除最新记录时会回到上一条记录绑定的文本内容。预算内大文本进入编辑窗口后不再等待一次性全文字符串：初始化先显示首屏行号 / 搜索，`TextDocumentSurfaceView` 的 WebView bridge ready 后自动通过 `LoadPreviewTextChunkQuery` 分块填充 Monaco 模型并在完成时记录 `ContentLength + SHA-256` 保存基线；若 WebView 不可用或流式加载失败，仍保留只读分页和“加载完整编辑”手动入口。超过预算的文本和 `Shader` 仍降级为只读行号 / 分页加载 / 有限搜索视图。文本读取、分页、搜索、草稿创建、草稿恢复打开、草稿更新、保存应用和退出丢弃都通过 Application query / command 与后台 runner 执行。退出或关闭正在编辑的标签 / 窗口时，若存在未保存正文会提示保存 / 丢弃 / 取消；未应用草稿的丢弃会追加 `EditDraftDiscarded` 并清理对应草稿描述。已应用 TextAsset 草稿再次进入编辑态后会作为 `IsAppliedBaseline` 打开；用户无修改退出或选择丢弃本次未保存 UI 改动时，只退出编辑态并保留仍被当前有效 `EditDraftApplied` 引用的草稿引用和当前读模型，避免删除导出写包仍需消费的内容。若删除操作记录后目标资源已经没有有效文本编辑，Presentation 必须清理已应用 baseline 的 `EditDraftReference`；若回退到上一条记录，则引用和编辑 baseline 必须跟随详情投影更新到当前有效 `DraftRevision`，不能继续打开标签旧引用、窗口旧编辑态或草稿 head。`TextAsset` 保存后的草稿已经进入当前有效读模型、预览刷新策略和统一导出写包计划：`GetAssetPreview` 会在没有更高优先级文件替换 / 工作区替换时优先从 `operations[]` 物化当前有效 TextAsset 编辑，再读取对应 `DraftRevision` 的 `EditDraft` reference；普通详情和资源编辑窗口完整详情刷新会把 `TextEditDraftId / TextEditDraftMode / TextEditDraftRevision / TextEditAppliedAtUtc` 变化视为有效预览变化。`Shader` 当前只进入只读文本工作面，保存按钮按 `CanApply=false` 禁用，后续接入 Shader 字段写回适配后再允许应用。

当前 Presentation 承载也已完成 Monaco 主实现收口：检查器普通 `Text` 预览不再使用独立 `ScrollViewer + TextBlock` 直接显示展示字符串，而是把 Application 返回的有界展示文本交给 `TextDocumentSurfaceView`；资源编辑窗口的正文、查找 / 替换和快捷键也迁移到同一 surface。`TextDocumentSurfaceView` 当前以 WebView2 + 离线 Monaco 为主实现，原生 WinUI 行列表 / `TextBox` 只作为 WebView2 初始化失败、Runtime 缺失或静态资源缺失时的 fallback。Monaco 已承载统一行号、基础语法染色、Ctrl+F、Ctrl+H、Ctrl+S、撤销 / 重做和 minimap；`.json / .skel` 走 JSON 语言，`.atlas` 走类 ini 语言，Shader 临时映射到 C++ 风格高亮。编辑输入只通过 JS bridge 回传 dirty 状态，保存 / 搜索前才同步一次 range patch，避免每次键入都搬运整段文本。流式加载阶段会关闭大文档的 minimap、folding、occurrence / selection highlight、links 和 codeLens 等高成本 Monaco 功能，并把初始化 chunk 追加排除出 undo 栈。当前 store 已进入 patch-backed / chunk-backed 读取第一阶段，可以避免保存时重写完整草稿 payload；真正接近无限大文本编辑还需要继续处理 Monaco 前端分块/虚拟模型、逐输入 patch log、patch 压缩和 manifest compaction。

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

当前进度：

- preview-cache-backed 的 `Text` 预览已返回 `FullContentReference`，普通展示仍通过 `TextContent / TextDisplay` 保持有界。
- 已新增 `LoadPreviewTextChunkQuery / LoadPreviewTextLinesQuery / SearchPreviewTextQuery` 和 Application gateway 入口，可在不暴露缓存路径给 Presentation 的前提下读取最大预算内文本块、行范围或有限搜索结果。
- `JsonPreviewCacheStore` 已实现 `LoadTextChunk / LoadTextLines / SearchText` 流式读取：常规新缓存会优先使用 `TextFullCharacterCount / TextEstimatedFullLineCount` 元数据作为总长度 / 总行数，旧缓存缺失该元数据时才扫描剩余文本计算；搜索返回有限匹配和 `HasMore`，匹配行片段本身也有长度上限。新写入的 `Text` preview payload 会在同目录生成可重建的 `*.lines.json` 稀疏索引 sidecar，例如 `payload.txt -> payload.lines.json`；schema v2 同时记录行锚点和规范化字符 offset 锚点，`LoadTextChunk / LoadTextLines / SearchText` 可先跳到最近锚点再继续顺序读取，sidecar 缺失、版本不匹配或 payload 身份不匹配时会自动退回原顺序扫描。
- 文件替换 `TextAsset` 预览已返回 `FileReplacement` reference；`LoadPreviewTextChunkHandler / LoadPreviewTextLinesHandler / SearchPreviewTextHandler` 会通过 `IReplacementPreviewSourceProvider` 读取替换文件的规范化文本块、行范围或搜索结果，真实文件实现会保留 `.skel` 非 JSON 视为二进制的保护语义。
- 工作区内 TextAsset 替换源会先读取源资产有效预览，并继承源 Text 预览的 `PreviewCache` reference；因此替换后的目标资产也可以通过同一组 chunk / lines / search query 按需读取源文本，不需要 Presentation 识别源资产身份。
- staging / draft store 背书的文本草稿已新增 `EditDraft` reference 类型；`LoadPreviewTextChunkHandler / LoadPreviewTextLinesHandler / SearchPreviewTextHandler` 会通过 `IAssetEditDraftTextStore` 读取 `workspaceKey + draftId + draftMode` 对应草稿 payload。当前第一阶段已经把 `TextAsset / Shader` 的独立 `编辑` 命令、窗口化文本编辑壳、只读大文本行号 / 分页加载 / 搜索视图接入该 reference：UI 创建草稿时写入 `editDrafts[]`，从窗口快照恢复出的可用 `EditDraftReference` 会通过 `OpenTextAssetEditDraft` 校验 descriptor、source fingerprint、过期时间和 payload 后进入编辑态，预算内 TextAsset 正文保存先写 `EditDraftUpdated` 再写 `EditDraftApplied`，创建、更新和应用阶段都会保留 descriptor 原始 source fingerprint，当前有效读模型、预览刷新策略和导出写包都会消费 `text-content` 草稿，退出或关闭未应用文本编辑态时追加 `EditDraftDiscarded` 并清理草稿描述；已应用 baseline 草稿退出编辑态时必须保留 payload。`UpdateTextAssetEditDraft` 的 range patch 路径已委托 `IAssetEditDraftTextStore.ApplyTextPatch`，由 store 负责基线校验、range 合并、payload 写回和索引 sidecar 更新。大文本创建草稿已改为懒物化：超过 `AssetTextEditPolicy.AutoMaterializeTextLength` 的文本不会立刻复制完整 payload 到 `staging/drafts`，而是在 `editDrafts[].sourceReference` 保存源完整内容引用并标记 `isPayloadMaterialized=false`；编辑窗口先通过同一组 query 读取首屏行和搜索，随后在 WebView ready 后自动按 chunk 填充 Monaco，首次保存时仍由 store 物化稳定 base snapshot 和 patch manifest。物化后 payload 缺失不允许回退旧源引用。现有检查器文本预览和编辑窗口已迁移到统一 `TextDocumentSurfaceView`，并以 WebView2 + 离线 Monaco 作为主实现；JSON 格式化编辑、真正虚拟化大文本保存和 Shader 写回仍在阶段 4 后续。
- 当前 preview cache 和 edit draft 文本 payload 已具备稀疏行锚点和字符 offset 锚点索引；它能降低大 line index 读取、大 offset chunk 读取和从指定行开始搜索时的前置跳过成本。尚未覆盖搜索倒排索引、后台可取消重建、文件替换来源的持久索引或 JSON 范围格式化索引；全文搜索仍可能顺序扫描，内存安全但还不是最终的高频搜索形态。

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

当前进度：

- 已新增正式文本编辑第一阶段：独立 `编辑` 命令、资源编辑窗口中的文本编辑模式、draft-backed 文本工作面、预算内正文编辑和本地化状态提示。
- WebView2 + 离线 Monaco 已作为主 surface 承载普通文本预览和 TextAsset 编辑，提供行号、基础语法染色、Ctrl+F / Ctrl+H / Ctrl+S、撤销 / 重做和 minimap；编辑输入只回传 dirty 状态，保存 / 搜索前按需同步正文；原生 WinUI 行号 / 搜索 / `TextBox` 工作面只作为 WebView2 或 Monaco 不可用时的 fallback。
- `TextDocumentSurfaceView` 已补 WebView2 用户数据目录显式绑定、主题同步、Runtime 预探测和 fallback 诊断：当前 WinUI WebView2 投影不暴露桌面 `CreationProperties / CoreWebView2Environment.CreateAsync(...)` 环境重载，因此在首次初始化前用进程级 `WEBVIEW2_USER_DATA_FOLDER` 指向应用私有目录；Runtime 缺失、离线 Monaco 资源缺失、WebView2 初始化失败、host 页面加载失败、JS bridge 超时和 WebView2 进程异常退出都会回退到原生文本视图并显示本地化主提示，fallback surface 可复制包含 Runtime / 路径 / 主题 / 文档状态的诊断。
- 文本编辑模式不复用 `替换文件` 入口；`替换文件` 仍只负责外部文件替换，`编辑` 负责打开或激活窗口化文本编辑壳。
- `CreateTextAssetEditDraft / UpdateTextAssetEditDraft / ApplyTextAssetEditDraft / DiscardTextAssetEditDraft` 已接入工作区命令网关；创建写 `editDrafts[]`，小型 TextAsset 正文保存或 range patch 保存写 `EditDraftUpdated` 后再写携带 `result.draftRevision` 的 `EditDraftApplied`，退出或关闭未应用编辑态写 `EditDraftDiscarded`。
- `OpenTextAssetEditDraft` 已接入工作区命令网关；编辑入口会先刷新当前资产详情。若详情已有有效文本编辑，则按 `TextEditDraftId / TextEditDraftMode / TextEditDraftRevision` 打开当前应用 baseline；若详情没有有效文本编辑但资产标签携带未应用 `EditDraftReference`，才按 `workspaceKey + draftId + draftMode` 打开既有草稿，校验 descriptor、source fingerprint、过期时间和 payload 后返回同一个 `EditDraft` reference，并把 descriptor 的 source fingerprint / source description 回传给资产标签，不追加新的 `EditDraftCreated`。若引用存在但 payload 缺失或诊断失败，UI 显示结构化失败，不静默创建新草稿覆盖问题。
- 文本编辑读取、搜索和草稿生命周期命令均通过 Application gateway 和 Presentation 后台 runner 执行，Presentation 不直接读取 staging 或 preview cache 路径。
- TextAsset 已进入保存 / 导出写包计划，导出对象写包会按当前有效 `EditDraftApplied.result.draftRevision` 从 `text-content` 草稿读取并写入 `m_Script`；Shader 当前作为只读大文本查看和搜索进入窗口壳，正文应用等待 Shader 写回适配器。
- TextAsset 正文编辑当前仍有明确的全缓冲安全预算，只承载 `AssetTextEditPolicy.InlineEditableTextLength` 以内内容；超过预算的 TextAsset 与 Shader 均保持只读查看 / 搜索。预算内大文本进入编辑态后会自动流式填充 Monaco，但不进入长期 ViewModel 字符串，也不通过单次 WebView 消息传输全文。range patch 命令和 store 级 `ApplyTextPatch` 边界已落地，当前 JSON store 已进入完整 payload 或稳定 `text-base.txt` 加 `text-patches.json` 的 patch-backed 读写形态；后续仍不得把超大文本整段绑定到 ViewModel 或单次 WebView 消息。
- 当前 `TextBox` 已收在 `TextDocumentSurfaceView` 内部，属于 WebView2 / Monaco 不可用时的 fallback；常规文本预览和 TextAsset 编辑已由 Monaco 主实现承载。Monaco 资源裁剪已完成：仓库和构建输出只保留当前 host 实际需要的 editor core、JSON、`cpp / ini` token、通用 / JSON worker、默认与 `zh-cn` NLS。store 级 patch-backed 保存边界已落地，下一步不应继续扩展 fallback 的编辑能力，而应围绕 12 MB+ atlas / skel / json 做真实压力样本记录，并继续推进 JSON / atlas / skel 格式工具、Monaco 大文件模型优化和 patch manifest compaction。
- 文本编辑态退出、关闭标签或关闭资源编辑窗口时，若存在未保存正文，必须复用保存 / 丢弃 / 取消确认；未应用草稿干净退出可直接丢弃未应用草稿描述。已应用 baseline 草稿重新打开后，退出或丢弃本次未保存 UI 改动只退出编辑态并保留草稿引用与 payload，不能调用 `DiscardTextAssetEditDraft` 删除仍被已应用操作和导出写包引用的内容。

验收：

- 第一阶段验收：`TextAsset / Shader` 有独立 `编辑` 入口；打开编辑器不会阻塞主 UI；大文本只读视图支持行号和有限搜索；小型 TextAsset 可以编辑正文并在保存时写入 `EditDraftUpdated -> EditDraftApplied`；退出和关闭会确认或丢弃未应用草稿；TextAsset 保存会在 `变更` 分段显示文本编辑记录，并在导出写包中消费同一草稿内容。
- 后续完整验收：小型 JSON 可以美化编辑；大型 JSON 不会一次性 pretty print 到 UI；正文编辑态撤销 / 重做、未保存确认、关闭窗口确认和多窗口同资产刷新符合资源编辑窗口规则；文本修改后，普通预览、Spine 组有效预览和导出写包都消费同一当前有效内容。当前普通资产预览、资源编辑窗口和 Spine 组详情已识别已应用 TextAsset 草稿；Spine atlas / skeleton 结构化读取和 runtime 请求通过同一套 preview source / reference query 获取当前有效文本，后续仍需用真实 atlas / skeleton 编辑样本继续做压力验证和 runtime 兼容性记录。

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
- `FullTextContent` 过渡字段不被 Presentation 必需路径依赖；preview-cache-backed reference query 能读取完整内容片段。
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
- 完整文本查看 / 编辑工作面覆盖打开、搜索、取消、退出丢弃和关闭确认；后续正文编辑实现后继续覆盖编辑、保存、撤销 / 重做、跳转和格式化。

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
- 普通文本预览已开始收敛到 Application 层统一 `AssetPreviewTextDisplayPolicy`：原始生成 / 缓存恢复 / 文件替换文本预览共享同一字符数与行数展示预算，并输出 `TextDisplay` 元数据；Presentation 文本 presenter 也保留同一预算兜底，避免异常路径把超长文本直接绑定到 UI。检查器普通 `Text` 预览现在只默认展示首段有界内容；当预览返回 `FullContentReference` 且文本被截断时，`TextDocumentSurfaceView` 会显示本地化“加载更多”入口，并经 `LoadPreviewTextLinesQuery` 追加下一批行，继续复用 Application gateway，而不是 Presentation 直接读取 preview cache / 替换文件 / draft。
- `TextDisplay` 元数据已投影为稳定 `AssetPreviewInfoItemDto`：详情信息卡可以显示预览字符数、完整字符数、预览行数、估计完整行数和是否截断；原始生成、缓存恢复和文件替换预览路径共享同一投影 helper。
- preview-cache-backed 的文本预览已带 `FullContentReference`，并提供 `LoadPreviewTextChunkQuery / LoadPreviewTextLinesQuery / SearchPreviewTextQuery`；Presentation 可以把 reference 传回 Application 读取有界文本块、行范围或有限搜索结果，而不需要自行解析缓存路径。真实 JSON preview cache store 已覆盖 `LoadTextChunk / LoadTextLines / SearchText` 为流式读取，并为新 Text payload 写入 payload 文件名派生的 `*.lines.json` 稀疏索引 sidecar；schema v2 同时保存行锚点和字符 offset 锚点，chunk / lines / search 都会优先从最近锚点继续扫描，缺失或失效时仍可顺序扫描回退，避免常规完整文本查看和搜索为了分块重新载入全量 payload。
- 文件替换 `TextAsset` 的普通展示文本已做有界裁剪，并已返回 `FileReplacement` 完整内容 reference；完整文本查看可通过同一组 query 按块、按行或搜索读取替换来源文件。工作区内 TextAsset 替换源已复用源资产 `PreviewCache` reference，目标资产无需暴露完整源文本给 Presentation 也能继续分块、分行和搜索。编辑草稿文本已新增 `EditDraft` 完整内容 reference，并由 `JsonAssetEditDraftTextStore` 把规范化文本、描述文件和 `*.lines.json` sidecar 存入 `staging/drafts/<workspaceKey>/<draftId>/<draftMode>/`；完整文本查看可通过同一组 query 读取草稿块、行范围或搜索结果。正式文本编辑第一阶段已使用该 reference 承载 `TextAsset / Shader` 的独立窗口化编辑壳、行号、搜索、创建草稿、恢复打开已有草稿、小型 TextAsset 正文更新、TextAsset 保存应用和未应用草稿退出丢弃；草稿 source fingerprint 会随 `EditDraftReference` 保存，已应用 baseline 草稿退出不会删除 payload。`FullTextContent` 仍暂时保留给 Application 内部 bounded 兼容和部分导出链路兼容，JSON 格式化编辑、大文本虚拟化编辑和 Shader 写回仍属于后续范围。
- Spine 组详情已改为 source-backed 结构化读取：atlas 页面 / region 解析可以从 `FullContentReference` 通过 chunk-backed `TextReader` 按行读取；JSON skeleton 摘要和官方 runtime preview / playback 会把 `SkeletonJsonSource / AtlasTextSources` 传给 runtime adapter，runtime adapter 在自身解析边界通过 `IAssetPreviewTextSourceReader` 打开完整 source。因此普通大文本预览仍保持首段展示和按需加载，Spine 需要完整 atlas / skel 时也不会要求 Application / Presentation 先拼接 `FullTextContent`。第三方 Spine runtime 自身可能在 JSON parser 内部物化 skeleton 数据，这是 runtime 解析边界，外层 DTO / ViewModel 不携带整块文本。
- Spine runtime adapter 已在 V3.8 / V4.2 的预览和 playback session 前加入 atlas 输入 guard，并能在 runtime key 未预解析时尝试可处理 JSON / binary source 的官方 adapter。

推荐下一步继续沿阶段 4 的 Monaco 文本编辑器工程化和大文本编辑能力推进，然后回到阶段 2/3 做更广的预览边界收口：

1. 补 WebView2 + Monaco 工程化剩余项：主功能完成后统一记录真实 UI 压力样本，以及后续如有需要的更细 Runtime 安装入口。
2. 在当前 Monaco 全缓冲编辑基础上接入 patch 草稿保存：分块缓冲 / range edit 命令、保存前差异摘要、未保存状态恢复，并保证超大文本不需要整段进入 ViewModel 字符串或单次 WebView 消息。
3. 给 TextAsset 编辑器补保存前差异摘要、撤销 / 重做和多窗口同资产冲突策略；当前已能按正文脏状态提示保存 / 丢弃 / 取消，后续需要把该状态接入恢复队列和多窗口同步。
4. 接入 JSON 美化 / 压缩 / 校验、行跳转和格式化错误定位；大型 JSON 先提供只读结构索引、范围格式化或预算外诊断，不做全量 pretty print。
5. 为 Shader 增加真实写回适配器，再把 `CanApply=false` 的只读入口升级为可保存入口。
6. 在当前 `*.lines.json` 行 / 字符稀疏索引基础上继续补可取消的后台索引重建和索引辅助搜索：现有全文搜索仍可能顺序扫描，后续应把高频搜索、搜索结果缓存和 JSON 范围格式化逐步切到索引辅助。
7. 继续盘点其它 staging-backed 文本载荷、结构化大集合和诊断文本，优先关注 TypeTree 字段树、CLI result JSON、外部工具 stdout / stderr 和插件诊断，确保它们也有预算和 reference / sidecar 边界。
8. 在文本链路边界稳定后，进入阶段 2：把 Spine atlas 现有 guard 迁入统一 policy，并扩展到 skel JSON、TypeTree、图片、音频、Mesh 和 CLI result JSON。

当前阶段性建议：下一步进入 patch 草稿保存与 JSON 格式化 / 校验。主要功能完成后再统一做 Monaco 工程化手动测试：确认正常启动后 `%LocalAppData%\AssestBundleTools\WebView2\TextEditor` 会生成 WebView2 用户数据、切换应用浅色 / 深色主题后 Monaco 背景和语法色跟随变化、临时移除输出目录中的 `Assets\Monaco` 会显示本地化兼容模式提示且“复制诊断”包含 Runtime / 路径 / 文档状态、12 MB JSON / atlas / skel 样本打开和关闭仍不阻塞主窗口。

## 11. 待决问题

- 预算默认值是否允许用户在“设置与诊断”中调整，还是仅作为开发常量。
- 完整文本查看的 chunk 对外继续采用规范化字符 offset 和行范围；后续需要细化不同编码、超大文本和编辑草稿下的索引重建 / 复用策略。
- 文本编辑控件选型已经完成第一轮 spike：长期目标为 WebView2 + Monaco，当前 `TextDocumentSurfaceView` 原生实现是 fallback 和迁移边界；待决点转为 Monaco 静态资源裁剪 / 打包方式、WebView2 Runtime 缺失修复指引、JS bridge 消息契约和 patch 草稿命令粒度。
- JSON 美化是否需要支持真正流式格式化，还是第一阶段只支持预算内全量格式化和预算外只读摘要。
- 完整文本草稿和 preview sidecar 的缓存、prune、恢复和操作记录引用关系如何细化。
