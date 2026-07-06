# VS Code 风格文本编辑器技术 spike

## 1. 背景

`TextAsset / Shader / atlas / skel / json / bytes` 等文本或准文本资源已经进入正式预览和编辑链路。现有实现具备 Application 侧分块、分行、搜索和 draft-backed 草稿读写边界，但 Presentation 侧仍需要一套长期文本 surface，承载：

- 普通文本预览。
- 大文本只读查看、行号、搜索和跳转。
- 小文本编辑。
- 后续大文本 patch 草稿编辑。
- JSON 美化 / 压缩 / 校验 / 定位。
- 后续 Shader 文本写回。

本 spike 比较三条路线：`WebView2 + Monaco`、原生 WinUI 虚拟化文本控件、自研 WinUI 文本面，并确定阶段性迁移策略。

## 2. 官方约束参考

- Microsoft WebView2 WinUI 入门文档：`https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui`
- Microsoft WebView2 Runtime 分发：`https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution`
- Microsoft WebView2 User Data Folder：`https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder`
- Monaco Editor 官方站点：`https://microsoft.github.io/monaco-editor/`
- Monaco Editor `IStandaloneCodeEditor` / `ITextModel` API：`https://microsoft.github.io/monaco-editor/typedoc/`
- Microsoft WinUI `ListView / GridView` 与集合性能文档：`https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/listview-and-gridview-data-optimization`
- Microsoft WinUI `ItemsRepeater` 文档：`https://learn.microsoft.com/en-us/windows/apps/design/controls/items-repeater`

## 3. 对比结论

| 路线 | 优点 | 风险 / 成本 | 结论 |
| --- | --- | --- | --- |
| WebView2 + Monaco | 最接近 VS Code；成熟行号、搜索、跳转、语法高亮、JSON 语言能力、撤销栈、选择和键盘体验；后续 patch / range edit 模型更自然 | 需要处理 WebView2 Runtime 分发、用户数据目录、离线 Monaco 静态资源打包、JS bridge、启动失败诊断、焦点 / 输入法、主题同步、测试自动化和安全边界 | 正式目标方案。必须先封装在独立 text surface 内，并保留原生 fallback；不允许 UI 直接读取文件或绕过 Application query / command |
| 原生 WinUI 虚拟化文本控件 | 与当前 Application 分行 / 搜索契约天然匹配；启动快、依赖少、可测试性较好；适合先承载普通预览和只读大文本 | 编辑体验、选区、多光标、语法高亮、JSON 格式化、撤销栈和大文本 patch 都要逐步补；很难达到完整 VS Code 体验 | 当前阶段采用为共享 fallback surface，把现有文本预览 / 编辑入口先收束到同一控件边界 |
| 自研 WinUI 文本面 | 控制力最高，可严格按仓库缓存和 patch 模型定制 | 成本最高；需要自研文本布局、滚动、选择、输入法、撤销、搜索、语法高亮和可访问性；容易拖慢主线 | 不作为主路线。仅保留少量辅助能力，例如 fallback line list、诊断只读视图和极端故障降级 |

## 4. 决策

1. 长期正式编辑器目标采用 `WebView2 + Monaco`，追求 VS Code 风格的大文本编辑体验。
2. 立即阶段不直接把业务写死到 WebView2；先建立 `TextDocumentSurfaceView` 作为 Presentation 统一文本 surface。
3. 当前 `TextDocumentSurfaceView` 已采用 WebView2 + 离线 Monaco 作为主实现，原生 WinUI 行列表 + 有预算的 `TextBox` 只作为故障 fallback。
4. Monaco 只作为 `TextDocumentSurfaceView` 内部实现接入，不改变 Application 侧 `LoadPreviewTextChunk / LoadPreviewTextLines / SearchPreviewText / draft command` 边界。
5. WebView2 启动失败、Runtime 缺失、Monaco 静态资源缺失、JS bridge 初始化失败时必须降级到原生 fallback surface，并显示本地化诊断。

## 5. 当前迁移边界

已建立的边界：

- `TextDocumentSurfaceView`
  Presentation 统一文本承载控件，当前以 WebView2 + 离线 Monaco 承载预览和编辑，并保留只读行列表、搜索结果列、小文本编辑框、状态栏和加载更多事件作为 fallback。普通文本预览的“加载更多”也经该控件发出事件，由页面 / 窗口协调器调用 `LoadPreviewTextLinesQuery` 追加行范围，和编辑器的分块加载共享同一 Application query 边界。
- `TextDocumentLineViewModel`
  普通预览和编辑器共享的行显示模型。
- `TextDocumentSearchMatchViewModel`
  编辑器搜索结果共享模型。

当前迁移状态：

- 检查器普通 `Text` 预览已从 `ScrollViewer + TextBlock` 迁移到 `TextDocumentSurfaceView`，并在展示文本被截断且存在 `FullContentReference` 时通过 `LoadPreviewTextLinesQuery` 支持按行追加预览内容。
- `TextAsset / Shader` 编辑窗口已从独立 `ListView / TextBox` 组合迁移到 `TextDocumentSurfaceView`。
- `TextDocumentSurfaceView` 已接入 WebView2 + 离线 Monaco 静态资源作为主实现，原生 WinUI 行列表 / `TextBox` 只作为 WebView2 初始化失败、Runtime 缺失或静态资源缺失时的 fallback。
- Monaco 主实现已统一普通预览和编辑窗口的行号、语法染色、查找 / 替换入口、Ctrl+F、Ctrl+H、Ctrl+S、撤销 / 重做和 VS Code 风格文本面。`.json / .skel` 走 JSON 语言，`.atlas` 走类 ini 语言，Shader 临时映射到 C++ 风格高亮。
- 当前保存已进入 `RangeEdit + patch-backed draft store` 阶段：`UpdateTextAssetEditDraft` 仍保留“全文正文”兼容入口，但资源编辑窗口保存统一提交范围补丁；范围补丁携带 `ContentLength + SHA-256` 基线并委托 `IAssetEditDraftTextStore.ApplyTextPatch` 校验、合并、更新 descriptor 和追加 `EditDraftUpdated` 操作记录。`JsonAssetEditDraftTextStore` 当前以完整 payload 或稳定 `text-base.txt` 快照为基线，写入 `text-patches.json` patch manifest，不再为每次保存重写当前完整 `text.txt`；读取 chunk、行范围、搜索、重新打开、有效预览和导出消费同一个 composite reader，因此保存后的当前文本可以在不物化当前完整 payload 文件的情况下被后续链路读取。流式大文本由 Monaco host 在 `onDidChangeModelContent` 中维护基于保存基线坐标的合并脏区间，保存时只回传该区间文本；若流式模型无法取得 range edit，Presentation 保存入口拒绝退回全文同步。
- 大文本打开已完成“快速进入 + 自动流式全文编辑”第一层长期边界：`CreateTextAssetEditDraft` 对超过 `AssetTextEditPolicy.AutoMaterializeTextLength` 的文本不再立即复制全文到 `staging/drafts`，而是在 `editDrafts[].sourceReference` 中记录源 `PreviewCache / FileReplacement / EditDraft` 引用并标记 `isPayloadMaterialized=false`；编辑窗口初始化只读取首屏行，WebView bridge ready 后复用 `LoadPreviewTextChunkQuery` 按 chunk 追加到 Monaco 模型，并在完成时以 `ContentLength + SHA-256` 作为保存基线。未物化草稿首次保存会用 `sourceReference` 定位源并流式写出规范化 `text-base.txt`，再追加 `text-patches.json`，不再强制走全文 `TextContent` 生成当前完整 draft payload；“加载完整编辑”按钮仅保留为 WebView 未 ready、fallback 或流式失败后的手动补救入口。当前仍是 Monaco 完整前端模型，不是无限大虚拟编辑器。
- 当前 WebView2 host 第一阶段使用默认环境初始化；显式 user data folder 和更细初始化诊断保留到下一阶段补齐。

## 6. Monaco 工程化条件

Monaco 主实现已经进入 `TextDocumentSurfaceView`，后续工程化收口必须满足：

- Monaco 静态资源随应用离线打包，不依赖公网 CDN；应用输出只复制项目文件中的 `MonacoEditorAsset` 白名单，避免完整语言包或 worker 在资源更新后被误带入发布产物。
- WebView2 用户数据目录由应用显式管理，避免污染用户默认浏览器状态；当前 WinUI 投影未暴露桌面 `CreationProperties / CoreWebView2Environment.CreateAsync(...)` 环境重载，`TextDocumentSurfaceView` 在首次初始化前通过进程级 `WEBVIEW2_USER_DATA_FOLDER` 绑定到应用私有目录。
- JS bridge 只传递有界 payload、range、patch 和命令结果，不传递缓存路径、替换源路径或 staging 路径。
- 初始打开只加载受控范围或受控模型；预算内大文本在 WebView ready 后自动流式填充 Monaco，超过 `AssetTextEditPolicy.InlineEditableTextLength` 的文本仍保持只读 / 搜索。patch-backed / chunk-backed draft store 第一阶段完成后，是否继续放宽正文保存预算必须以真实压力样本验证 Monaco 全文模型、bridge 同步和格式化成本为前置条件。
- 搜索、跳转、格式化和保存通过 Application query / command；前端只展示结果和提交 patch。
- Runtime 缺失、初始化失败和 bridge 异常都有本地化主提示和可复制诊断；当前已提供 fallback 诊断复制入口和 Runtime 缺失安装 / 修复指引。
- 保留原生 fallback surface，保证预览和只读查看在 WebView2 不可用时仍可工作。
- WebView2 会缓存本地 host 页面引用的 JS 资源；修改 `abt-editor-host.js` 时必须同步更新 `abt-editor-host.html` 中的 `?v=` 查询版本，或在调试环境显式清理 WebView2 用户数据目录。不要把“重启后才恢复正常”的现象误判为保存链路状态 bug。

## 7. 后续阶段计划

### 阶段 A：统一 surface 收口

目标：

- 所有普通文本预览和文本编辑窗口都走 `TextDocumentSurfaceView`。
- 清理旧的专用文本控件命名和重复 XAML。
- 保持现有 Application 分块 / 搜索 / draft 生命周期不变。

验收：

- 普通 TextAsset / Shader / atlas / skel 文本预览不再直接绑定整段 `TextBlock`。
- 小 TextAsset 编辑保存、重新打开、退出和导出仍消费同一草稿。
- 大文本只读分页、搜索和加载更多行为保持不退化。

### 阶段 B：Monaco 离线原型

目标：

- 新增 Monaco 静态资源包和 WebView2 host 原型。
- 完成 editor 初始化、主题同步、只读模型加载、搜索、跳转和错误降级。
- 接入 Ctrl+S 到现有保存命令，替换默认 surface，同时保留原生 fallback。

验收：

- 打开 12 MB JSON / atlas 样本不阻塞主 UI。
- Runtime / 静态资源缺失时回退到原生 surface。
- 所有内容读取仍经 Application query。

当前进度：

- 已完成离线 Monaco 资源打包与 `TextDocumentSurfaceView` WebView2 host。
- 已把检查器文本预览和资源编辑窗口统一到 Monaco 主实现。
- 已接入 Ctrl+F、Ctrl+H、Ctrl+S、撤销 / 重做、行号、minimap、JSON / atlas / Shader 基础染色。
- 已把编辑输入桥接改为 dirty 信号 + 保存 / 搜索前按需同步正文，避免 12 MB 级文本在每次键入时都完整回传到 .NET。
- 已把文本编辑模式拆成“打开草稿”和“流式填充 Monaco”两步：大文本创建草稿只保存源引用，打开编辑窗口不再等待完整文本物化；首屏行号 / 搜索立即可用，预算内全文编辑由 WebView ready 事件自动触发，“加载完整编辑”按钮只保留为 fallback / 重试入口。
- 已补 WebView2 用户数据目录显式绑定、离线 Monaco 资源缺失、WebView2 初始化失败、host 页面加载失败和 JS bridge 超时的本地化 fallback 诊断；在当前 WinUI WebView2 投影限制下，用户数据目录通过进程级 `WEBVIEW2_USER_DATA_FOLDER` 在初始化前设置。
- 已补 Monaco light / dark 主题定义，并由 `TextDocumentSurfaceView` 按 WinUI `ActualTheme` 通过 JS bridge 同步；WebView2 进程异常退出会回退原生文本视图，fallback surface 已提供复制诊断入口。
- 已补 WebView2 Runtime 预探测：Runtime 不可用时显示安装 / 修复指引并回退原生文本视图；复制诊断会包含 Runtime 版本或失败信息、Monaco 资源目录、WebView2 用户数据目录、主题、语言和当前文档状态。
- 已完成 Monaco 资源裁剪：当前仓库保留 host、license、editor core、JSON language service、通用 editor worker、JSON worker、`cpp / ini` basic-language token、默认 NLS 与 `zh-cn` NLS；`AssestBundleTools.Presentation.WinUI.csproj` 在构建时先清理输出目录中的 `Assets\Monaco`，再通过 `MonacoEditorAsset` 白名单复制资源，防止未用语言、CSS/HTML/TS worker、多余 NLS 包或增量构建残留进入输出目录。
- 待补：主功能完成后统一做真实 UI 压力样本验证，以及后续若需要的更细运行时安装入口。

### 阶段 C：大文本虚拟化编辑和 patch 草稿

目标：

- 建立 `TextPatch` / `RangeEdit` 草稿命令。
- Monaco 提交 patch，Application 负责验证、写 draft payload、更新 descriptor 和操作记录。
- 小文本编辑也切到统一 patch 保存路径。

验收：

- 大文本编辑不需要整段进入 ViewModel 字符串。
- 保存后普通预览、资源编辑窗口和导出写包都读取已应用草稿。
- 未保存状态、关闭确认、恢复打开和多窗口同资产策略一致。

当前进度：

- 已完成 `TextAssetEditDraftRangeEditDto` 与 `UpdateTextAssetEditDraftCommand.RangeEdits` 第一阶段契约；命令校验要求 `TextContent` 与 `RangeEdits` 二选一，补丁范围使用 .NET / Monaco 一致的 UTF-16 字符索引。
- 已完成 store 级 patch-backed 写入边界：Application 命令通过 `IAssetEditDraftTextStore.ApplyTextPatch` 提交 range patch，store 按 `ContentLength + SHA-256` 校验基线，再把补丁追加到 `text-patches.json` manifest；descriptor 的长度、行数、hash、摘要和 `isPayloadMaterialized` 由 composite reader 计算后更新。已有 `text.txt` payload 不会被本次 patch 保存重写；未物化草稿会先根据 `editDrafts[].sourceReference` 流式写出稳定 `text-base.txt`，后续保存和恢复只依赖 draft 目录内的 base snapshot 与 manifest。handler 只把 store 状态映射为 `Asset.EditDraft.TextPatchBaselineMismatch / TextPatchRangeInvalid / TextLengthExceeded / NotFound` 等结构化错误，并复用原有 `EditDraftUpdated` 操作记录链路。stale patch 不写 manifest 或操作记录。
- 已完成 UI 保存入口切换：当前窗口按钮保存和 Ctrl+S 会走同一 surface 同步路径，优先提交 Monaco 基于保存基线生成的 range patch；非流式 fallback surface 或无法取得最小 patch 时可退回“从 0 到已保存全文长度”的全文替换 range patch，但仍通过 `RangeEdits` 进入 patch store，不再用 `TextContent` 物化路径作为常规保存。流式大文本不会走全文同步 fallback。
- 已完成 Monaco bridge 第一阶段：host 保存基线由 `setDocument / markSaved` 管理，保存时通过 `getPendingRangeEdits` 返回 `start / length / text / versionId / baseLength`，Presentation 只把 range patch 放进草稿 VM 的短期状态，不再在 Ctrl+S 消息里携带全文。对流式加载文档，host 当前保留完整保存基线并在保存时与当前 Monaco model 做最小单区间 diff，保证删除操作记录、重新进入编辑和保存后 baseline 一致；这仍是 Monaco 完整前端模型方案，不是最终虚拟文本模型。
- 已完成自动流式加载第一阶段：`TextDocumentSurfaceView` 暴露 streaming sink，资源编辑窗口进入预算内 TextAsset 编辑态后在 WebView ready 时自动从 Application chunk query 读取并追加到 Monaco；Presentation 不持有完整 `_lastSavedText`，保存基线由 Application/store 返回的 patch baseline 维护。大文档加载时会关闭 Monaco 高成本展示功能，初始化 chunk 不进入 undo 栈。
- 已补应用层回归测试覆盖 store 级 patch 委托、非法 range 映射、stale patch 基线拒绝、长度预算和操作记录保存；已补 Infrastructure 回归测试覆盖 `JsonAssetEditDraftTextStore.ApplyTextPatch` 对 payload、descriptor、行 / 字符索引 sidecar 的一致更新，以及 stale baseline 不改写 payload。
- 已补懒物化草稿边界：`editDrafts[]` 可保存 `sourceReference / isPayloadMaterialized`，`OpenTextAssetEditDraft` 只在 payload 尚不存在且 descriptor 明确未物化时恢复为 deferred draft；Presentation 初始化 deferred draft 只读首屏行，并在 WebView ready 后自动按 chunk 填充 Monaco。首次保存 deferred draft 时由 Application 把 descriptor 的 `sourceReference` 传给 store，store 流式写出稳定 base snapshot 并生成 patch manifest，避免为了首个补丁先生成当前完整 payload，同时避免保存后继续依赖 preview cache 或外部源。

仍未完成：

- Monaco 端尚未提交完整逐输入 patch log；非流式文本仍是在保存时由 host 对基线文本和当前文本做最小差异，生成单个 range patch。流式文本已改为逐输入维护单个合并脏区间，但远距离多处修改仍会合并成一个较大的区间，而不是多个独立 range edits。
- 当前 patch-backed store 采用 base + patch manifest + composite reader，可以避免每次保存重写完整 draft payload；但 Monaco 仍需要形成前端完整文本模型。后续若要接近 VS Code 大文件体验，需要继续推进前端分块/虚拟模型、多 range patch log 压缩和 manifest compaction / chunk rewrite。
- 已完成 patch-backed draft 的 offset chunk 读取优化：`EditDraft` reference 的 composite reader 会从 piece table 定位到目标字符 offset，并复用文本 sidecar 的字符锚点跳读 `text.txt / text-base.txt`，避免保存后的大文件再次进入编辑时每个 chunk 都从文件头扫描。
- 大文本正文保存仍受 `AssetTextEditPolicy.InlineEditableTextLength` 预算保护；当前优化目标是先把草稿持久化和读取链路从全文 payload 重写中解耦，而不是立即开放无限大文本编辑保存。

下一步：

1. 做 12 MB+ atlas / skel / json 真实压力样本记录，统一验证打开、搜索、替换、保存、重新打开、导出写包和恢复草稿。
2. 针对预算内大文本仍需要 Monaco 完整模型的问题，继续评估 Monaco 模型初始化、语言服务 / tokenization、bridge 传输和 patch 生成分别的耗时，再决定前端分块模型、可见范围模型或 worker 策略。
3. 后续如需要更细粒度撤销 / 合并，再让 Monaco host 收集 `onDidChangeModelContent` 的逐输入 range changes 并按版本压缩成 patch log，并在 store 侧补 manifest compaction / chunk rewrite。

### 阶段 D：搜索 / 跳转 / JSON 格式化

目标：

- 搜索结果分页和跳转行号接入 Monaco。
- JSON 格式化 / 压缩 / 校验走后台任务和预算策略。
- 大 JSON 优先支持范围格式化、错误定位和只读结构索引。

验收：

- 小 JSON 可完整格式化编辑。
- 大 JSON 不做无预算全量 pretty print。
- 格式错误能定位到行列并保持可编辑状态。

## 8. 风险和守护规则

- 不允许 Presentation 直接读取 preview cache、替换文件或 staging draft 路径。
- 不允许 WebView2 前端持有真实本地文件路径。
- 不允许进入编辑模式时自动把完整大文本放入长期 ViewModel 字符串属性；预算内文本允许自动流式填充 Monaco 模型，但 Presentation 保存态必须继续使用 patch baseline / range edits，不回退为长期完整字符串。
- 不允许把 WebView2 初始化失败变成整个编辑窗口不可用；必须 fallback。
- 不允许在未完成 patch-backed / chunk-backed draft store 前开放超过 `InlineEditableTextLength` 的文本正文保存。
