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
  Presentation 统一文本承载控件，当前以 WebView2 + 离线 Monaco 承载预览和编辑，并保留只读行列表、搜索结果列、小文本编辑框、状态栏和加载更多事件作为 fallback。
- `TextDocumentLineViewModel`
  普通预览和编辑器共享的行显示模型。
- `TextDocumentSearchMatchViewModel`
  编辑器搜索结果共享模型。

当前迁移状态：

- 检查器普通 `Text` 预览已从 `ScrollViewer + TextBlock` 迁移到 `TextDocumentSurfaceView`。
- `TextAsset / Shader` 编辑窗口已从独立 `ListView / TextBox` 组合迁移到 `TextDocumentSurfaceView`。
- `TextDocumentSurfaceView` 已接入 WebView2 + 离线 Monaco 静态资源作为主实现，原生 WinUI 行列表 / `TextBox` 只作为 WebView2 初始化失败、Runtime 缺失或静态资源缺失时的 fallback。
- Monaco 主实现已统一普通预览和编辑窗口的行号、语法染色、查找 / 替换入口、Ctrl+F、Ctrl+H、Ctrl+S、撤销 / 重做和 VS Code 风格文本面。`.json / .skel` 走 JSON 语言，`.atlas` 走类 ini 语言，Shader 临时映射到 C++ 风格高亮。
- 当前保存仍沿用 `UpdateTextAssetEditDraft -> ApplyTextAssetEditDraft` 的全量文本命令；Monaco 输入变更只通过 JS bridge 通知 dirty 状态，保存 / 搜索前才同步一次正文，避免每次按键跨 WebView 边界搬运整段文本。应用层预算已从旧 `TextBox` 小文本上限提升到 Monaco 全缓冲编辑安全上限。真正无限大文本编辑仍需阶段 C 的 patch 草稿命令后才能彻底放开。
- 当前 WebView2 host 第一阶段使用默认环境初始化；显式 user data folder 和更细初始化诊断保留到下一阶段补齐。

## 6. Monaco 工程化条件

Monaco 主实现已经进入 `TextDocumentSurfaceView`，后续工程化收口必须满足：

- Monaco 静态资源随应用离线打包，不依赖公网 CDN。
- WebView2 用户数据目录由应用显式管理，避免污染用户默认浏览器状态；当前 WinUI 投影未暴露桌面 `CreationProperties / CoreWebView2Environment.CreateAsync(...)` 环境重载，`TextDocumentSurfaceView` 在首次初始化前通过进程级 `WEBVIEW2_USER_DATA_FOLDER` 绑定到应用私有目录。
- JS bridge 只传递有界 payload、range、patch 和命令结果，不传递缓存路径、替换源路径或 staging 路径。
- 初始打开只加载受控范围或受控模型；当前 TextAsset 保存仍使用 Monaco 全缓冲安全预算，超过预算的大文本必须等 patch 草稿命令落地后再开放正文保存。
- 搜索、跳转、格式化和保存通过 Application query / command；前端只展示结果和提交 patch。
- Runtime 缺失、初始化失败和 bridge 异常都有本地化主提示和可复制诊断；当前已提供 fallback 诊断复制入口和 Runtime 缺失安装 / 修复指引。
- 保留原生 fallback surface，保证预览和只读查看在 WebView2 不可用时仍可工作。

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
- 已补 WebView2 用户数据目录显式绑定、离线 Monaco 资源缺失、WebView2 初始化失败、host 页面加载失败和 JS bridge 超时的本地化 fallback 诊断；在当前 WinUI WebView2 投影限制下，用户数据目录通过进程级 `WEBVIEW2_USER_DATA_FOLDER` 在初始化前设置。
- 已补 Monaco light / dark 主题定义，并由 `TextDocumentSurfaceView` 按 WinUI `ActualTheme` 通过 JS bridge 同步；WebView2 进程异常退出会回退原生文本视图，fallback surface 已提供复制诊断入口。
- 已补 WebView2 Runtime 预探测：Runtime 不可用时显示安装 / 修复指引并回退原生文本视图；复制诊断会包含 Runtime 版本或失败信息、Monaco 资源目录、WebView2 用户数据目录、主题、语言和当前文档状态。
- 待补：Monaco 资源裁剪、真实 UI 压力样本验证，以及后续若需要的更细运行时安装入口。

### 阶段 C：大文本虚拟化编辑和 patch 草稿

目标：

- 建立 `TextPatch` / `RangeEdit` 草稿命令。
- Monaco 提交 patch，Application 负责验证、写 draft payload、更新 descriptor 和操作记录。
- 小文本编辑也切到统一 patch 保存路径。

验收：

- 大文本编辑不需要整段进入 ViewModel 字符串。
- 保存后普通预览、资源编辑窗口和导出写包都读取已应用草稿。
- 未保存状态、关闭确认、恢复打开和多窗口同资产策略一致。

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
- 不允许把完整大文本放入长期 ViewModel 字符串属性。
- 不允许把 WebView2 初始化失败变成整个编辑窗口不可用；必须 fallback。
- 不允许在未完成 patch 草稿前开放大文本正文保存。
