# AssestBundleTools 项目构建路线图

## 1. 文档目的

本文档用于明确项目从当前空骨架状态到可用 MVP 工具的逐步构建顺序。

目标如下：

- 明确每个阶段先做什么、后做什么
- 控制范围，避免同时推进过多模块
- 保证 UI、架构、多语言、错误处理按统一顺序落地
- 为后续逐步实现提供执行基线

本文档是“实施顺序规划”，和以下文档配套使用：

- [ui-final-spec.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/ui-final-spec.md)
- [code-architecture-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/code-architecture-guidelines.md)
- [error-handling-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/error-handling-guidelines.md)
- [workspace-management.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/workspace-management.md)
- [asset-editor-window-mode.md](asset-editor-window-mode.md)

## 2. 当前状态

截至目前，项目已经完成以下基础工作：

- 已从单项目演进为多项目结构
- 已明确 `Domain / Application / Infrastructure / Presentation.WinUI` 分层
- 已完成 UI 信息架构与主工作区方案定稿
- 已完成代码架构规范文档
- 已完成错误与异常处理规范文档

当前仓库结构：

```text
src/
  AssestBundleTools.Domain/
  AssestBundleTools.Application/
  AssestBundleTools.Infrastructure/
  AssestBundleTools.Presentation.WinUI/

tests/
```

这意味着下一步不应直接开始写复杂业务逻辑，而应先补“基础能力层”。

同时，当前实现状态已经补充了以下基础能力：

- 已完成 WinUI 文本资源化与基础本地化服务
- 已完成应用级日志服务与“设置与诊断”日志承载页
- 已完成主工作区单窗格只读浏览链路
- 已完成 M7 的“资源包标签页浏览”稳定子集
- 已开始进入 M8 的“提取与替换流程骨架”稳定子集
- 已接入文件替换骨架与批处理变更摘要扩展
- 已接入批处理页中的保存前摘要与输出预览承载
- 已接入批处理页中的批量提取预览、类型筛选与执行骨架
- 已接入批处理页中的保存执行骨架与最近执行反馈
- 已接入批处理页中的批量替换预览、来源筛选与执行骨架
- 已接入批处理页中的结构化任务结果明细面板
- 已开始演进多工作区上下文骨架与资产列表多选快捷操作
- 已完成工作区目录元数据与 JSON 持久化骨架
- 已补充工作区数据根目录的“元数据 / 解析缓存 / 变更暂存”分层存储骨架
- 已接入资源包标签页、左侧资源包搜索与活动筛选状态的工作区恢复链路
- 已完成控制台页中的最近工作区真实列表、工作区删除骨架与打开策略收敛链路
- 已将工作区新建 / 打开改为单一可视化配置面板，避免连续目录选择器造成理解断层
- 已将工作区向导进一步收敛为“目录优先”表单，工作区名称位于目录输入区下方并按目录自动生成建议值
- 已补工作区目录检查链路：新建时若目录中已存在工作区则提示直接打开或重置，打开时若目录无有效工作区则留在当前面板继续修正
- 已将“打开工作区”表单职责收敛为“从目录导入并打开现有工作区”，工作区名称在该模式下只读显示
- 已支持通过目录直接打开未入索引的工作区，并在绑定成功后自动补登记到最近工作区目录
- 已把工作区名称提升为可配置且可持久化的元数据字段，不再强制等于来源目录名
- 已把“新建工作区”调整为空工作区创建流程，后续通过拖拽或导入入口补充真实资源来源
- 已把工作台顶部命令区与控制台页入口收敛到同一套工作区新建 / 打开流程
- 已把 Shell 收敛为“控制台态 / 工作台态”两态模型：默认从控制台进入，正式工作台也可显式返回控制台
- 已让一级导航第一入口位随窗口态显示“控制台 / 工作台”，避免额外分叉导航概念
- 已在资源包列表展开态与收缩态补入 `文件导入 / 目录导入` 入口骨架，真实 AB 导入流程仍后置
- 已将工作区统一导入入口扩展到 `文件 / 目录 / 拖拽` 三种入口，且工作台拖拽接收范围当前收敛在左侧资源包列表区域
- 已把批处理页和“设置与诊断”页统一接到活动工作区上下文刷新链路
- 已补齐工作区目录重载时对已保留会话状态的合并，避免刷新最近工作区目录时丢失活动会话摘要
- 已完成最近工作区的固定 / 取消固定与不可用状态表达
- 已完成“启动时恢复上次工作区”偏好设置与受控自动恢复链路
- 已将控制台最近工作区的打开方式收敛为设置项，支持默认当前、默认新建与每次询问
- 已将无需用户交互的轻量提示收敛为 Shell 级 toast 通知服务与通用展示控件，供控制台、工作台和设置页复用
- 已接入工作区级解密器选择、工作台“当前解密器”状态标签与“设置与诊断”中的解密器管理页
- 已开始推进 M9 第一子阶段：`文件 / 目录 / 拖拽` 导入已正式接到 `解密器 -> Reader -> BundleIndexCacheStore -> CachedWorkspaceSessionRegistry`
- 已将工作区运行时会话从假资源包列表切换为基于真实导入文件缓存构建，当前资源包列表已不再依赖假数据
- 已补充启发式 `IAssetBundleReader`，当前先稳定输出资源包级索引，资产级真实解析仍后置
- 已让工作区打开链路在进入浏览前自动复用或刷新导入缓存，避免 Reader/缓存版本升级后仍继续消费旧索引
- 已为左侧资源包列表补充右键上下文操作，支持打开所在位置与从工作区移除，并同步引入单资源包排除路径元数据
- 已补强资源包标签页的资产列表状态恢复链路，当前会缓存并恢复搜索、筛选与 `selectedAssetId`，切换标签页和重启后都能回到对应资源焦点
- 已将工作台资产类型筛选从 UI 固定六项推进为应用层驱动的动态类型 Facet：真实 Reader 可展示资产会保留 Unity 类型名，列表查询返回类型统计，Presentation 按 `All -> 固定优先类型 -> 动态真实类型` 投影下拉项并显示数量，动态项通过 `AssetType + AssetTypeDisplayName` 筛选键恢复和持久化
- 已把工作台右侧详情区扩展为“资源包标签页内的资产文档标签”结构，当前会缓存并恢复已打开资产、活动资产标签与检查器页签位置，避免不同资源共用单一检查器实例
- 已完成资源编辑窗口模式的设计收敛：后续资产文档标签和整个资源详情窗格可以独立为同一工作区进程内的资源编辑窗口，并支持窗口间标签组合与放回主窗口；该能力当前只进入设计文档，不改变现有主窗口资产文档标签实现，详细见 [asset-editor-window-mode.md](asset-editor-window-mode.md)
- 已完成预览展示链路第一轮资源化收口：`PreviewInfoText` 已被结构化 `PreviewInfoItems / PreviewMessage / Diagnostic` 取代，尺寸单位等公共展示值也已收敛到共享本地化格式化器
- 已完成工作台页面 ViewModel 第二轮拆分：工作区壳层、资源包左窗格、文档宿主与资产工作流已分别下沉到独立子 ViewModel，`BundleExplorerViewModel` 当前主要承担页面级协调与工作区恢复/持久化编排
- 已完成 `BundleExplorerViewModel` 的 D-04 新一轮收口：在工作区浏览状态恢复/持久化节流、页面级结果反馈、工作区加载/重绑定编排以及资源包标签打开/激活/关闭/重排编排分别下沉到 `BundleExplorerBrowserStateCoordinator / BundleExplorerOperationFeedbackBridge / BundleExplorerWorkspaceBindingCoordinator / BundleExplorerBundleTabCoordinator` 之后，`BundleExplorerPage` 也已直接迁回 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 子状态宿主绑定，旧页面兼容透传属性与 `BundleExplorerPageStateBridge` 已移除，主类体量已由 `1258` 行降到约 `426` 行
- 已完成 `BundleExplorerAssetWorkflowViewModel` 的 D-04 第六轮拆分：在 `Preview`、提取/替换动作、检查器投影、资产文档标签协调、资产文档宿主构造/清理以及主资产窗格加载/刷新/空态回退编排下沉之后，资产级命令入口与页面动作编排也已下沉到 `BundleExplorerAssetCommandCoordinator`，共享结果失败反馈继续统一复用 `BundleExplorerOperationFeedbackBridge`，主类当前约 `273` 行；当前 D-04 的主体偏差已完成收口，后续重点转向防止职责回灌与稳定页面内共享状态
- 已完成 `BundleExplorer` 页面级共享状态的 D-03 第三轮收口：`BundleExplorerPage.xaml / BundleExplorerPage.xaml.cs` 当前已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 的语义化状态宿主，旧的 `BundleExplorerPageStateBridge` 已移除，不再继续保留页面兼容桥接层
- 已完成 Material 结构化摘要预览：当前通过 `Infrastructure -> Application -> Presentation` 的现有预览管线输出 `Shader / TexEnv / Float / Color / Int` 的半可视化摘要，并会把 Shader PPtr 和 TexEnv 行按 `AssetDetailsDto.Dependencies` 定位到当前依赖图中的 `Shader / Texture2D / Sprite`；命中贴图时用现有 `GetAssetPreviewQuery` 管线加载缩略图和主纹理平面预览，主纹理卡仅叠加 `_Color` 基础 tint 并显示限制说明；预览还会基于常见 Unity 材质属性名展示主纹理、法线、自发光、Alpha 裁剪、金属 / 光滑度、溶解 / 扰动、透明 / 混合等语义候选提示；当前已补充简化材质样张，用基础色、Alpha、金属度、光滑度和自发光强度做受控近似展示，并在贴图槽区域展示已解析 / 外部未解析 / 内部未解析引用汇总；未命中时保留内部 / 外部未解析状态，仍不在 UI 层直接读取 AB、打开额外资产或模拟 Shader
- 已完成 AnimationClip 第一版半可视化摘要预览：Infrastructure 输出 `duration / sampleRate / frame estimate / loop settings / curve counts / animation events` 的 JSON sidecar，Application 缓存命中后水化为专用 DTO，Presentation 通过独立 Presenter / ViewModel / 控件渲染，不在 UI 层解析 AB
- 已完成 Font 第一版半可视化摘要预览：Infrastructure 输出字体指标、字符矩形、atlas 贴图引用和默认材质引用的 JSON sidecar，Application 缓存命中后水化为专用 DTO，Presentation 通过独立 Formatter / ViewModel / 控件渲染，不在 UI 层解析 AB
- 已完成 GameObject 第一版信息浏览预览：Infrastructure 输出对象基础字段、Transform 摘要、组件列表与常见组件引用的 JSON sidecar，Application 缓存命中后水化为专用 DTO，Presentation 通过独立 Formatter / ViewModel / 控件渲染，并复用详情依赖图进行资源跳转，不在 UI 层解析 AB 或渲染场景
- 已完成 MonoBehaviour 第一版信息浏览预览并按真实样本补强：Infrastructure 输出对象名、启用状态、MonoScript 摘要、字段分组、常见 Unity 序列化容器、前若干字段和 PPtr 引用的 JSON sidecar，Application 缓存命中后水化为专用 DTO，Presentation 通过独立 Formatter / ViewModel / 控件渲染，并复用详情依赖图进行资源跳转；MonoScript 类名无法解析时只显示未解析脚本 PathId，不在 UI 层解析 AB 或推断脚本业务语义
- 已完成 AnimatorController 第一版信息浏览预览：Infrastructure 输出控制器名、Layer / Parameter 计数、`m_Controller` 常量区中的 StateMachine / State / Transition / BlendTree / Value 摘要，以及 `m_AnimationClips` 等 PPtr 引用 JSON sidecar，Application 缓存命中后水化为专用 DTO，Presentation 通过独立 Formatter / ViewModel / 控件渲染，并复用详情依赖图进行资源跳转；真实样本中 `m_AnimatorLayers / m_AnimatorParameters` 常为空，首版以 `m_Controller` 常量区摘要为主，不做状态机画布或动画播放模拟

## 3. 总体实施原则

整个项目建议遵循下面的实施原则：

1. 先打基础模型，再打 UI
2. 先打通假数据链路，再接真实 AB 解析
3. 先实现只读浏览，再实现提取、替换、保存
4. 先落实多语言和错误模型，再铺更多页面
5. 每一阶段都必须有明确完成标准

## 4. 分阶段构建总览

| 阶段 | 名称 | 状态 | 目标 |
|---|---|---|---|
| M0 | 解决方案与分层骨架 | 已完成 | 多项目结构和基础分层就位 |
| M1 | 核心公共模型 | 已完成 | 建立 `Result`、错误模型、ID/状态模型 |
| M2 | 应用层用例骨架 | 已完成 | 建立 Use Case、DTO、查询/命令接口 |
| M3 | 基础设施占位实现 | 待开始 | 建立 Reader/Writer/Store 等接口假实现 |
| M4 | 本地化与日志基础 | 已完成 | 建立资源文件、语言服务、日志接口 |
| M5 | WinUI 应用外壳 | 已完成 | 搭建 Shell、导航、主页面骨架 |
| M6 | 工作区只读链路 | 已完成 | 打通打开工作区、树、列表、详情 |
| M7 | 标签页浏览骨架 | 已完成 | 打通资源包标签页浏览、复用与关闭 |
| M8 | 提取与替换流程骨架 | 已开始 | 打通提取、工作区替换候选、文件替换候选、应用/撤销、资产多选快捷提取与变更摘要骨架 |
| M9 | 真实 AB 读取接入 | 已开始 | 用真实解析能力替换假实现 |
| M10 | 保存与重建能力 | 待开始 | 实现另存为、变更输出与校验 |
| M11 | 测试、诊断与打包 | 待开始 | 补测试、日志、设置、发布准备 |

## 5. 各阶段详细规划

## 5.1 M0 解决方案与分层骨架

### 目标

完成多项目结构和 Clean Architecture 基础边界。

### 当前状态

已完成。

### 当前产出

- 多项目解决方案
- `src` 结构
- `tests` 占位目录
- `AssemblyMarker` 基础占位

### M0 完成标准

- 可成功构建解决方案
- 各项目引用方向符合架构规范
- WinUI 项目能作为 `Presentation` 入口运行

## 5.2 M1 核心公共模型

### 目标

为后续所有模块建立统一的基础类型。

### 应实现内容

在 `Domain` / `Application` 中优先建立：

- `Result`
- `Result<T>`
- `AppError`
- `ErrorCategory`
- `ErrorSeverity`
- `WorkspaceId`
- `BundleId`
- `AssetId`

建议同时补充：

- `PaneId`
- `AssetType`
- `WorkspaceMode`

### 主要目录建议

```text
src/AssestBundleTools.Domain/
  Common/
  Workspace/
  Bundles/
  Assets/

src/AssestBundleTools.Application/
  Common/
```

### 完成标准

- 统一错误模型可以被所有 Use Case 使用
- 各种 ID 类型不再使用裸字符串随意传递
- 基础模型有最小单元测试

### 为什么先做这个阶段

因为后续：

- UI ViewModel
- Use Case
- 错误处理
- 本地化提示
- 工作区状态

都依赖这一层。

## 5.3 M2 应用层用例骨架

### 目标

建立应用层调用边界，让 UI 能通过标准 Use Case 获取数据。

### 当前状态

已完成。

### 应实现内容

优先建立以下 Use Case 骨架：

- `OpenWorkspace`
- `LoadBundleTree`
- `LoadAssetList`
- `GetAssetDetails`
- `OpenAssetInPane`

同时建立：

- `Command`
- `Query`
- `Handler`
- `Dto`

### 完成标准

- UI 不需要直接访问基础设施
- 所有页面数据都能通过 Use Case 获取
- Use Case 统一返回 `Result`

### 当前产出

- `OpenWorkspace`
- `LoadBundleTree`
- `LoadAssetList`
- `GetAssetDetails`
- `OpenAssetInPane`
- 按功能划分的 `Command / Query / Handler / Dto`
- 临时内存工作区注册表与假数据读模型

### 注意事项

- 这个阶段先允许返回假数据
- 不要求真实解析 AB

## 5.4 M3 基础设施占位实现

### 目标

为应用层接口提供最小可运行实现，但先不接入真实复杂逻辑。

### 应实现内容

先定义接口，并实现占位版本：

- `IAssetBundleReader`
- `IAssetBundleWriter`
- `IWorkspaceStore`
- `ILogService`
- `IClock`

建议优先提供：

- `MockAssetBundleReader`
- `InMemoryWorkspaceStore`
- `DebugLogService`
- `JsonBundleIndexCacheStore`
- `JsonPreviewCacheStore`
- `JsonWorkspaceChangeStore`

### 完成标准

- Application 层接口都有对应实现
- 可以在不接真实 AB 的情况下打通 UI 演示流程
- 工作区数据目录不再只有单一元数据文件，而是已为真实 AB 接入预留缓存层与暂存层

## 5.5 M4 本地化与日志基础

### 目标

在 UI 大规模铺开前完成多语言和日志基础设施。

### 当前状态

已完成。

### 应实现内容

在 `Presentation.WinUI` 中优先建立：

- `Strings/zh-CN/Resources.resw`
- `Strings/en-US/Resources.resw`
- `ILocalizationService`
- `IAppLanguageService`
- 基本日志展示格式

优先资源化以下文本：

- 导航项
- 按钮文本
- 页签标题
- 空状态提示
- 常见错误提示

### 完成标准

- UI 中不再出现硬编码用户可见字符串
- 能根据资源键获取本地化文本
- 日志文本与用户提示文本已分离

### 当前产出

- `Strings/zh-CN/Resources.resw`
- `Strings/en-US/Resources.resw`
- `ILocalizationService`
- `ResourceLocalizationService`
- `IAppLogService`
- `IAppNotificationService`
- 应用级日志集合与“设置与诊断”页面承载
- Shell 级通用 toast 宿主与复用展示控件

### 注意事项

- 这个阶段越早做，越能避免返工

## 5.6 M5 WinUI 应用外壳

### 目标

搭建主应用壳和核心页面骨架。

### 当前状态

已完成。

### 应实现内容

按 UI 定稿优先建立：

- `ShellPage`
- `HomePage`
- `BundleExplorerPage`
- `AssetPaneView`
- `InspectorPanelView`
- `LogPanel`

建议同时建立：

- 导航服务
- 页面基础 ViewModel
- 主窗口启动入口

### 完成标准

- 能正常进入主界面
- 能切换导航页
- 控制台页与工作台页面骨架可显示
- 日志区和详情区已具备占位结构

### 当前产出

- `ShellPage`
- `HomePage`
- `BundleExplorerPage`
- `AssetPaneView`
- `InspectorPanelView`
- `LogPanel`
- `BatchTasksPage`
- `SettingsDiagnosticsPage`
- Shell 导航与页面切换骨架
- WinUI 资源化文本与页面级 ViewModel 基础结构

### 当前限制

- 此时仍可使用假数据
- 暂不要求真实 AB 内容

## 5.7 M6 工作区只读链路

### 目标

优先打通“打开 -> 浏览 -> 查看详情”的只读流程。

### 当前状态

已完成。

### 应实现内容

- 打开工作区
- 加载 Bundle 树
- 加载资产列表
- 选中资产显示详情
- 搜索和基本筛选

优先只做：

- 控制台页到工作台的切换
- 单窗格工作区
- 右侧概览/预览基础页

### 完成标准

- 用户可以浏览工作区中的 Bundle 和资产
- 右侧详情区能随选中项更新
- 错误可通过统一 `Result` 显示

### 当前进展

- 已可通过工作区入口加载基于假数据链路的工作区
- 已可显示左侧资源包分组列表
- 已可显示主工作区单窗格资产列表
- 已可显示右侧详情区数据
- 已可通过顶部轻量提示反馈关键操作结果
- 已把详细运行日志集中到“设置与诊断”
- 搜索和类型过滤已接入基础刷新链路

### 这一阶段的重要性

只读链路稳定后，后续提取和替换才有稳定承载面。

## 5.8 M7 标签页浏览骨架

### 目标

在不破坏当前主工作区稳定性的前提下，实现资源包标签页浏览能力。

### 当前状态

已完成。

当前阶段明确采用以下收敛策略：

- 主工作区继续保持单窗格三栏布局
- 标签页能力优先作用于“资源浏览”
- 当前阶段不重新引入资源包浏览子窗口，也不再把双窗格作为本阶段目标；资源详情后续独立为资源编辑窗口属于资产文档宿主增强，单独按 [asset-editor-window-mode.md](asset-editor-window-mode.md) 分阶段推进

### 应实现内容

- 资源包标签页浏览
- 单击资源包列表直接打开标签页规则
- 同一资源包标签页复用规则
- 标签页关闭后的页面级状态回收
- 标签页切换时的资源详情同步

### 完成标准

- 主工作区可从左侧资源包列表或显式命令打开资源包标签页
- 资源包列表单击即可直接打开对应标签页
- 标签页可浏览当前资源包的资产列表与详情
- 同一资源包不重复开新标签页实例
- 标签页关闭后可正确回收页面级浏览状态
- 工作区状态仍统一管理

### 当前进展

- 已移除资源包浏览子窗口链路与窗口映射代码
- 已支持左侧资源包列表单击直接打开或激活资源包标签页
- 已支持同一资源包重复打开时直接激活已有标签页
- 已支持标签页关闭与活动标签页切换
- 已支持左侧资源包列表收缩为摘要窄条，并在收缩后让中间与右侧区域均分剩余宽度
- 当前实现范围继续保持单窗格三栏，不重新引入多窗口主路径

### 注意事项

- 这一阶段仍不必实现真实拖拽替换
- 重点是浏览、标签页复用和状态同步

## 5.9 M8 提取与替换流程骨架

### 目标

在 UI 和应用层层面打通提取与替换流程。

### 当前状态

已开始。

### 应实现内容

- 提取命令
- 替换命令
- 拖拽候选替换
- 替换确认
- 变更队列显示
- 保存摘要弹窗骨架

### 当前实现范围

为控制节奏，当前 M8 先收敛为一个稳定子集：

- 单资源提取命令骨架
- 文件替换候选查询骨架
- 外部文件替换命令骨架
- 工作区内同类型资源替换候选查询
- 主工作区右侧检查器中的“选择替换源 / 应用替换 / 撤销替换”
- 主工作区右侧检查器中的“选择文件来源 / 应用替换 / 撤销替换”
- 资源详情中的替换来源、替换状态、变更摘要展示
- 基于内存工作区的假提取时间、工作区替换与文件替换状态回写
- 当前工作区变更摘要查询与批处理页承载结构
- 主工作区到批处理页的“批量提取”导航入口
- 主工作区资产列表中的多选与“提取选中”快捷操作
- WinUI 文件选择服务与真实外部文件路径接入
- 主工作区“保存为新包”到批处理页保存前摘要的统一入口
- 批处理页中的保存前摘要、建议输出目录和资源包输出预览结构
- 批处理页中的批量提取范围预览、资源类型筛选、资源包级候选聚合和执行骨架
- 批处理页中的批量替换范围预览、来源筛选、资源包级候选聚合和执行骨架
- 批处理页中的保存执行骨架、执行结果反馈与应用日志写入
- 批处理页中的结构化任务结果明细承载

当前明确暂不实现：

- 真实文件导出
- 跨标签页拖拽替换
- 批量提取与保存摘要弹窗

### 完成标准

- 命令链路完整
- 失败和取消状态能被正确处理
- 右侧详情区能展示工作区替换候选、文件替换候选和校验结果

### 当前实现策略

- 可先使用假实现和假校验规则
- 暂不要求真实写回
- 顶部命令区“选择替换源”当前先自动选中第一个兼容候选
- 检查器替换页中的“选择文件来源”当前已接入真实文件选择器，并把所选路径带入候选校验链路
- 真正的应用与撤销动作统一收敛到右侧检查器完成
- 批处理页当前先以工作区变更摘要为主，不直接执行真实批量导出或保存
- 主工作区“保存为新包”当前先导航到批处理页中的保存前摘要承载，不执行真实写入
- 主工作区“批量提取”当前先导航到批处理页中的批量提取预览区域，并执行“回写提取记录”的骨架逻辑
- 批处理页中的“执行批量替换骨架”当前只校验并汇总已进入替换队列的资源，不重复执行真实替换动作
- 批处理页中的“执行保存骨架”当前只验证输出计划并记录结果，不执行真实写包
- 工作区替换与文件替换共用同一条“应用 / 撤销”动作链路，当前以工作区状态为单一事实来源
- 应用级工作区上下文开始按“活动工作区 + 已登记工作区列表”演进，为后续切换工作区做准备
- 已接入工作区目录索引与单工作区元数据文件的 JSON 存储骨架
- 已接入资源包标签页、活动标签页、左侧资源包搜索和活动资产筛选的恢复链路
- 统一导入协调服务当前已承载 `文件 / 目录 / 拖拽` 三类入口，工作台拖拽只桥接左侧资源包列表区域，避免侵占整页拖拽语义
- 工作区向导当前已显式处理“目录中已存在工作区 / 目录中无工作区 / 目录中存在多个候选工作区”三类目录状态
- 工作区向导当前已支持选择工作区默认解密器，打开已有工作区时只读展示该配置
- “设置与诊断”页当前已支持解密器的新增、编辑、删除与刷新
- 工作台顶部状态摘要当前已显示“当前解密器”

## 5.10 M9 真实 AB 读取接入

### 目标

将工作区浏览链路从假数据切换为真实 AB 数据。

### 应实现内容

- 第一子阶段：把 `导入来源 -> 解密器 -> Reader -> BundleIndexCacheStore -> WorkspaceSessionRegistry` 主链路接通
- 第一子阶段：先让资源包列表脱离假数据，基于真实导入文件构建缓存会话
- 第二子阶段：接入真实解析库，补齐资产列表与详情读取
- 第二子阶段：在读取链路中补齐 `BundleIndexCacheStore` 的失效判断与命中恢复
- 第三子阶段：在预览链路中补齐 `PreviewCacheStore` 的生成、命中与失效清理
- 第三子阶段：补齐解密自动识别、失败诊断与更完整的缓存失效策略

### 完成标准

- 第一子阶段完成时，资源包列表已能展示真实导入文件
- 第一子阶段完成时，导入后可直接刷新当前工作区并从缓存恢复资源包浏览会话
- 最终完成时，能读取真实 AB 资产与详情
- 最终完成时，能按工作区配置调用正确解密器
- 最终完成时，失败时能输出标准错误码和日志

### 风险点

- 第三方解析库能力边界
- 大文件性能
- 资产类型差异

### 当前进展补充

- Infrastructure 已接入 `AssetsTools.NET`，当前真实 Reader 可读取 UnityFS 资源包中的序列化资产文件
- 当前真实 Reader 已可解析资产名、基础类型、字节大小与 `m_Container` 路径，并写入 `BundleIndexCacheStore`
- 工作区会话与工作台当前已能基于真实缓存恢复资产列表和基础详情，不再局限于资源包级空索引
- 当前保留启发式 Reader 作为回退实现，避免真实解析失败时直接丢失资源包浏览入口
- 当前导入处理已补上“导入源识别”层：目录、文件和拖拽来源展开后，会先执行轻量直检，尽早命中普通 AB 并筛除空文件等明显无效输入
- 当前导入处理已补齐 `BundleIndexCacheStore` 的按资源包失效判断与命中恢复，不再先整工作区清空缓存
- 当前缓存命中已纳入 `source fingerprint + decryptorId + decryptorVersion + parserVersion + toolVersion`
- 当前资源包索引失效或来源移除时，已同步清理对应 `bundleCacheKey` 下的预览缓存文件
- 资源包索引缓存当前已额外保存 `SerializedFileName + PathId`，用于把预览请求稳定定位到真实序列化对象
- 资源包索引缓存当前已额外保存 `SerializedFileNames` 集合，用于在工作区运行时把 `CAB-*` 外部依赖尽量还原为具体资源包文件名
- 工作台资源详情当前已正式接入 `GetAssetPreviewQuery -> PreviewCacheStore -> IAssetPreviewGenerator` 预览链路，并支持按资源指纹与生成器版本命中 / 失效
- 当前图片预览缓存已按资源独立落盘，避免多个资源反复覆写同一个 `payload` 文件
- 工作区解密器当前已补上第一轮“自动识别”链路：当工作区配置为自动识别时，导入处理会先按工作区级探测非回退解密器，再回退到直通解密器，并在成功建立或复用索引后把真实解密器固化回工作区元数据
- 解密后当前仍统一经 `IAssetBundleReader.CanRead(...)` 决定进入真实 Reader 还是在读取阶段拒绝，保持“Reader 不负责探测加密格式”的边界
- 当前导入处理已补上第一轮结构化失败诊断：会把自动识别探测摘要，以及“导入源筛除 / 解密失败 / 读取器拒绝 / 读取失败”四类问题明细回传到工作台日志链路
- 当前 M9 的自动识别主轴已明确收敛为四层：`导入源识别 -> 解密器识别 -> Reader 路径识别 -> 缓存命中识别`
- 当前导入处理已补上结构化缓存诊断：会把 `缓存未命中 / 来源指纹变化 / 解密器变化 / 解密器版本变化 / 工具版本变化 / 解析器版本变化 / 来源移除` 等原因回传到应用层结果，用于日志和后续诊断页复用
- 当前导入入口已经补上 `workspaceKey` 级执行闸门，工作区重新打开时的自动刷新也会复用同一互斥链路
- 手动导入与工作区打开/恢复触发的自动导入刷新当前已通过 Presentation 后台任务调度器执行，并在工作台左侧资源包窗格内显示正式导入进度覆盖层，避免导入解析期间窗口彻底卡住或整页被遮罩
- 外部解密 staging 目录当前已补上 `workspaceKey + decryptorId + bundleCacheKey` 唯一路径，避免不同来源下的同名 bundle 派生文件互相覆写
- 当前导入处理协调器已完成第二阶段收口：`Application` 内部的 `PlanBuilder -> Worker Pool -> Committer` 三段模型与 bundle 粒度有界并行已经落地，没有把并发扩散到 `Presentation` 或 `WorkspaceSessionRegistry`
- 多线程导入的默认 worker 并发度当前已通过 `WorkspaceImportExecutionOptionsResolver` 固化为：逻辑核心数 `<= 1` 用 `1`，`2-4` 用 `2`，`5-6` 用 `3`，`>= 7` 用 `4`；用户也可在“设置与诊断”中固定为 `1-4` 个 worker；第一版每个工作区导入上限不超过 `4`
- 当前导入结果已补上 `Planning / Screening / Worker / Commit / Total` 五段耗时诊断，并额外记录 `ProbeDuration / ProbeDecryptorCount / ProbeEvaluationCount`，可直接为后续 `Probe` 并发评估提供依据
- 当前预览查询链路已补上结构化预览诊断：会把预览缓存命中 / 失效原因，以及 `定位信息缺失 / 解密器不可用 / 解密失败 / 预览生成失败` 等摘要回退原因统一沉到应用层 DTO
- 当前预览诊断 detail 已从裸字符串进一步收口为 `LocalizedMessageDto` 结构化消息；检查器不再直接显示由 `Application / Infrastructure` 拼装的原始诊断句子
- 当前 Reader 直检与预览生成阶段的稳定失败原因已抬升为标准错误令牌，例如“来源资源包缺失 / 资源包格式无法识别 / 无兼容 Reader / 序列化文件定位失败 / 资产定位失败 / 字段树加载失败”，避免基础设施层继续拼接英文细节
- 当前解密器设置页中的保存 / 删除失败提示已去除 `ex.Message` 直透，并补上“保存写入成功但回读校验失败”的独立错误令牌；工作台预览显示层的兜底日志也已改为稳定本地化文本
- 当前外部工具执行失败已进一步收口：`vgmstream-cli` 转码、外部解密进程执行、解密器探测执行失败，以及 WinUI 壳层中的文件选择器 / 新进程启动 / 启动兜底页提示，均已改为资源化文本或稳定消息令牌，不再把宿主程序的 `ex.Message` 直接暴露到用户界面
- 当前预览展示层已不再依赖 `PreviewInfoText` 这类拼接文本；检查器、音频预览头部与工作台/批处理尺寸展示统一改为基于结构化 DTO 和共享本地化格式化器生成
- 当前真实 Reader 已补上第一轮依赖深读：会从序列化文件 `externals` 中提取资源包级外部依赖，并递归解析资源字段树中的 `PPtr` 引用，写入资产级依赖缓存
- 当前依赖页已扩展为 `资产依赖 / 反向引用 / 依赖闭包 / Bundle 外部依赖` 四块，并由运行时依赖图提供反向引用与闭包结果，避免把图遍历逻辑写到界面层
- 资产依赖项当前会展示目标资源的 `PathId`、Unity 类型名与闭包深度，便于把预览、依赖分析和后续替换动作稳定绑定到真实序列化对象
- 当前依赖解析已兼容 Unity 有符号 `PathId` 语义，不再把负数 `PathId` 误判为无效引用；对应 Reader 解析版本也会同步提升以触发旧缓存自动重建
- Bundle 外部依赖当前会对 `archive:/CAB-xxx/CAB-xxx` 解析为更友好的 `CAB-*` 展示名与依赖类型；这里的 `CAB-*` 属于 Unity 内部 archive / serialized file 标识，不等于用户导入的资源包文件名
- 当目标 serialized file 也存在于当前工作区已导入资源包中时，Bundle 外部依赖当前会优先显示具体 `xxx.ab` 文件名，并在副标题继续保留 `CAB-*` 标识
- 资产级外部 `PPtr` 占位当前也会在运行时工作区图中按稳定资产 ID 或 `SerializedFileName + PathId` 反查当前工作区已导入资产；命中时会把 `Material -> 外部 Texture2D/Sprite` 这类依赖提升为目标 Bundle 上的具体资产，供反向引用、闭包和 Mesh 贴图关联复用
- 资产友好路径当前优先使用 `m_Container` 路径，缺失时回退到 `SerializedFileName / 资产类型 / PathId` 组合摘要，避免不同资源仅靠名称难以区分
- 当前预览类型覆盖如下：
  - 图像预览：`Texture2D`、`Sprite`
    `Sprite` 当前已在源贴图预览上叠加裁切框、pivot 点和 9-slice border 辅助线，并额外提供裁切结果卡片；这些数据随预览缓存一起落盘和命中复用。
  - 文本预览：`TextAsset`、`Shader`
  - 音频预览：`AudioClip`
  - 半可视化摘要预览：`Material`、`AnimationClip`、`Font`、`GameObject`、`MonoBehaviour`、`AnimatorController`
    `Material` 当前会展示 Shader、TexEnv、颜色与数值属性；Shader PPtr 会按依赖图尝试定位到 `Shader` 资产并展示解析状态；TexEnv 行会按依赖图解析 `Texture2D / Sprite` 并通过现有预览查询管线加载前若干缩略图，同时把 `_MainTex` 或首个可用贴图提升为主纹理平面预览，并在存在 `_Color` 时做低透明度 tint 参考叠加；另会基于常见属性名补充材质语义候选提示，但不模拟 Shader 渲染，未匹配时展示内部或外部引用未解析状态。
    `AnimationClip` 当前会在自适应摘要预览区中只展示资源名、时长 / 采样率 / 帧数、曲线数量、binding 摘要和动画事件概览；更完整的时间、循环、曲线 binding 和事件明细通过通用可折叠信息卡展示。Infrastructure 输出 schema v3 JSON sidecar，Presentation 不在 UI 层解析 AB，也不在缺少骨骼上下文时做播放或骨骼驱动预览。
    `Font` 当前会展示基础字体指标、字符矩形、font data 大小、atlas 贴图引用和默认材质引用；引用只消费预览 sidecar 和依赖解析结果，不在 UI 层读取资源包。第一版明确未覆盖 atlas 贴图缩略图 / 字形样张、真实文本排版渲染、kerning、动态字体或系统字体 fallback、TextMeshPro 类资源，以及跨更多 Unity 版本 / 字段布局的样本验证；外部或内部引用未解析时继续保持 reference-only 呈现。
    `GameObject` 当前会展示对象名、激活 / Layer / Tag、组件摘要、Transform 摘要、层级摘要和紧凑摘要卡片；对象基础字段、Transform 坐标 / 父子关系、组件 PPtr / 类型 / 脚本 / 启用状态，以及常见组件的 `Mesh / Material / Sprite / Controller / Avatar / AudioClip / Script` 引用进入通用可折叠信息卡。组件和引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态。Infrastructure 输出 `payload.game-object.json`，Presentation 只消费预览 DTO，不在 UI 层读取 AB，也不做场景渲染或 Prefab 实例化。
    `MonoBehaviour` 当前会展示对象名、脚本摘要、启用状态、字段数、字段分组数、引用数和紧凑摘要卡片；对象基础字段、MonoScript 信息、字段分组、常见 Unity 序列化容器、前若干标量 / 数组字段和 PPtr 引用进入通用可折叠信息卡。脚本和引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态；MonoScript 类名无法解析时只显示未解析脚本 PathId。Infrastructure 输出 `payload.mono-behaviour.json`，Presentation 只消费预览 DTO，不在 UI 层读取 AB，也不做脚本业务语义推断、编辑或运行时模拟。
    `AnimatorController` 当前会展示控制器名、Layer / Parameter 总数、状态机 / 状态 / 转场 / BlendTree / Value 容器摘要和 `m_AnimationClips` 等 PPtr 引用；引用行会按 `AssetDetailsDto.Dependencies` 解析到统一资源跳转目标，未匹配时保留内部 / 外部引用未解析状态。真实样本中 `m_AnimatorLayers / m_AnimatorParameters` 可为空，预览以 `m_Controller` 常量区摘要为主。Infrastructure 输出 `payload.animator-controller.json`，Presentation 只消费预览 DTO，不在 UI 层读取 AB，也不做状态机画布或播放模拟。
  - 结构化摘要预览：`SpriteAtlas`
  - 聚合资源组预览：`Spine`
    Spine 后续按 2D 游戏资源组处理，不作为 `全部类型` 下拉里的 Unity 类型，也不作为 `仅已修改 / 仅可预览` 同级开关。检测到 `.atlas + .skel/.json + Texture2D` 可聚合关系时，中间资产列表应提供 `资源 / Spine 组` 视图切换；Spine 组视图展示聚合条目，右侧详情展示组匹配状态、atlas region overlay、动画 / skin / bone / slot 摘要、缺失 page / 外部引用未解析诊断和相关资产跳转。完整动画播放器、setup pose、skin 切换和 `.skel` 多版本细解析后置。
  - 通用摘要回退：其余可读取资源类型
- 工作台检查器当前已补充 `PathId` 展示，并为图片预览增加尺寸、裁切区域、像素编码与源格式标识等专属信息；预览表面已拆为独立控件，图片覆盖层、音频播放条和 Mesh 线框视口都各自落在独立控件中，避免继续把复杂预览细节堆回检查器主控件
- 工作台检查器当前已收敛为 `详情 / 依赖 / 替换 / 变更` 四页签；其中“详情”页统一承载预览画面、基础信息、预览补充信息与预览诊断，“依赖”页签会展示资产依赖、反向引用、依赖闭包和 Bundle 外部依赖
- 工作台右侧当前已补上资产详情标签层，不同资产会独立打开为标签并按资源包标签页归属保存状态，为后续资产级扩展功能预留稳定承载面
- 当前 M9 除“富预览扩展”外的主干代办已完成，下一步继续预览能力扩展阶段
- 当前剩余的高优先级架构收口已进一步缩小到“少量内部哨兵值、外部工具诊断回退与调试日志格式统一”，而不再是检查器、预览、启动兜底或 WinUI 壳层的用户可见文本主链路

### M9 预览能力下一阶段计划

在当前 `Texture2D / Sprite / TextAsset / Shader / AudioClip / Mesh` 已具备真实预览、`Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController` 已具备半可视化摘要或轻量信息浏览、其余类型仍停留在结构化摘要的前提下，
下一阶段不建议“平均推进所有类型”，而应按“识别价值 / 实现成本 / 架构稳定性”分批推进。

#### 预览类型优先级

第一梯队，当前已完成的下一步增强：

- `AnimationClip`
  已完成 binding / 曲线通道摘要增强：schema v3 sidecar 会补充曲线 binding 总数、前若干 binding 行、路径 / 属性 / ClassID / TypeID / 脚本引用 / 曲线数 / keyframe 数；`m_AnimationClipSettings` 缺失时回退读取 `m_MuscleClip` 的时间和循环字段，packed generic binding 的 keyframe 数在未解码前保留未知。自适应摘要预览区只展示主要信息，详细信息进入通用可折叠信息卡。后续仍不建议在缺少骨骼上下文时先做重型播放预览。
- `GameObject`
  已完成第一版轻量信息浏览预览并补上组件引用导航：schema v1 sidecar 会读取对象名、Layer、Tag、激活状态、静态标记、组件 PPtr、组件类型 / 名称 / 启用状态、MonoBehaviour 脚本引用、常见组件二级资源引用和 Transform 坐标 / 父子摘要。自适应摘要预览区只展示主要信息，并用摘要卡片补充组件构成、引用状态与常见组件专属字段；完整字段进入通用可折叠信息卡，组件和引用行复用统一已解析资源跳转链路。后续仍不建议在缺少场景上下文时先做重型场景渲染。
- `MonoBehaviour`
  已完成第一版轻量信息浏览预览并按真实样本补强：schema v1 sidecar 会读取对象名、启用状态、`m_Script` 指向的 MonoScript 摘要、字段分组、常见 Unity 序列化容器、前若干可展示标量 / 数组字段，以及前若干 PPtr 引用。自适应摘要预览区只展示脚本、启用状态、字段数、字段分组数和引用数等主要信息；脚本、字段分组、容器、字段和引用明细进入通用可折叠信息卡。若脚本类名无法从当前导入资源中解析，只显示未解析脚本 PathId；若脚本或字段引用 PathId 已被详情依赖图解析到当前工作区资产，信息卡行复用统一“打开资源”链路；该预览仍不做 MonoBehaviour 业务语义推断、编辑或运行时模拟。
- `AnimatorController`
  已完成第一版轻量信息浏览预览：schema v1 sidecar 会读取控制器名、Layer / Parameter 总数、状态机 / 状态 / 转场 / AnyState 转场 / BlendTree / Value 等容器摘要，以及前若干动画片段等 PPtr 引用。自适应摘要预览区只展示资源名、Layer / Parameter、状态 / 转场和引用状态等主要信息；Layer、Parameter、容器摘要和引用明细进入通用可折叠信息卡。真实样本显示不少 2D 资源的 `m_AnimatorLayers / m_AnimatorParameters` 数组为空，首版会继续从 `m_Controller.m_StateMachineArray / m_StateConstantArray / m_TransitionConstantArray / m_AnimationClips` 等字段提取可浏览信息；已解析引用复用统一“打开资源”链路，仍不做状态机画布、条件表达式编辑或运行时播放模拟。

第二梯队，按样本继续增强已落地预览：

- `AudioClip`
  当前已完成标准音频容器播放、`FSB5` 外部转码和摘要回退；后续若继续增强，优先补波形或幅度摘要，不改变音频转码与预览缓存边界。
- `Mesh`
  当前已扩展到 `线框 + UV0 + 贴图关联提示 + UV 关联贴图叠加 + 第一版逐三角贴图预览`：Infrastructure 从非压缩 Mesh 读取位置通道、UV0 通道、索引边、顶点通道摘要与 AABB，Application 以 `payload.mesh.json` 结构化缓存，Presentation 通过 Win2D 在线框视口和 UV 视图间切换；贴图关联优先使用运行时工作区依赖图派生的 `TextureAssociations`，主路径为 `Mesh <- MeshFilter / Renderer / ParticleSystemRenderer / GameObject -> Material -> Texture2D / Sprite`，找不到时才兜底 `Mesh -> Texture2D / Sprite` 直接依赖，并通过现有预览查询管线加载首个可预览的关联图片作为 UV 叠加载荷，不在 WinUI 侧解析资源包。后续若继续增强，应先评估压缩 Mesh / 多 stream 布局 / 法线或真实材质采样的样本价值，缺少三角、UV 或可预览贴图时仍回退纯结构显示。
- `Material`
  当前已完成 `Shader + Shader PPtr 解析状态 + TexEnv + 颜色 / 数值属性摘要 + 贴图依赖定位 + 前若干贴图缩略图 + 主纹理平面预览 + _Color tint 参考叠加 + 常见属性语义候选提示 + 简化材质样张 + 贴图引用解析汇总`；`Mesh` 和 `Material` 共享同一个贴图预览 payload loader，继续复用现有预览缓存、命名和失效链路。后续若继续增强，应保持样本驱动，不直接追求 Unity Editor 级完整 shader 仿真。
- `Font`
  当前已完成 `字体指标 + atlas 贴图引用 + 默认材质引用 + 前若干字符矩形摘要`。已知遗漏包括 atlas 贴图缩略图、可读字形样张、kerning、动态字体或系统 fallback、TextMeshPro 类资源，以及更多 Unity 版本 / 字段布局样本验证；后续增强应先基于真实样本确认字段稳定性，再通过现有预览管线联动 atlas 贴图生成受控样张，不直接追求完整字体渲染引擎。
- `SpriteAtlas`
  与现有 `Texture2D / Sprite` 图片预览、预览缓存和依赖解析链路最接近，但当前缺少足够验证资源，暂时降为样本门控项；后续遇到稳定样本后再补 `整张图集引用 + packed sprite 数量 + 前若干区域矩形 + 可解析贴图缩略图`，不按字段名猜测实现。
- `Spine` 资源组
  面向 2D 游戏的跨资产预览，优先级高于继续平均扩展弱上下文类型，但实现方式必须是 `资源 / Spine 组` 视图切换和聚合 DTO，而不是新增 Unity 资产类型。首版目标是识别 `.atlas + .skel/.json + Texture2D` 组合、解析 atlas page / region、在可预览 Texture2D 上叠加 region、展示 skeleton / atlas / texture page 匹配诊断，并提供跳转到组成资产的入口。完整动画播放、skin 切换、setup pose 和 `.skel` 二进制多版本兼容应在样本充足后再评估。

第三梯队，当前继续保留结构化摘要：

- 其余 `Unknown` 或强依赖上下文资源的类型

这些类型当前更适合做 `层级 / 字段 / 状态机 / 引用关系` 的信息浏览，
不应为了“有预览”而过早把它们硬塞进重型渲染链路。

#### 推荐实施顺序

建议按以下步骤推进：

1. 已完成 `AnimationClip` binding / 曲线摘要增强
   - 当前完成标准是 `payload.animation-clip.json` schema v3、缓存水化、自适应摘要预览区、通用信息卡明细，以及 Presentation 回归覆盖。
2. 已完成 `GameObject` 第一版信息浏览预览
   - 当前完成标准是 `payload.game-object.json` schema v1、缓存水化、自适应摘要预览区与摘要卡片、对象 / Transform / 组件 / 组件引用通用信息卡、已解析资源跳转，以及 Presentation 回归覆盖。
3. 已完成 `MonoBehaviour` 第一版信息浏览预览和真实样本补强
   - 当前完成标准是 `payload.mono-behaviour.json` schema v1、缓存水化、自适应摘要预览区与摘要卡片、脚本 / 字段分组 / 序列化容器 / 字段 / 引用通用信息卡、未解析脚本 PathId fallback、已解析资源跳转，以及 Application / Infrastructure / Presentation 回归覆盖。
4. 已完成 `AnimatorController` 第一版信息浏览预览
   - 当前完成标准是 `payload.animator-controller.json` schema v1、缓存水化、自适应摘要预览区与摘要卡片、Layer / Parameter / 状态机容器 / AnimationClip 引用通用信息卡、已解析资源跳转，以及 Application / Infrastructure / Presentation 回归覆盖。
5. 对 `SpriteAtlas / Font` 先补样本验证清单，再决定是否增强
   - `SpriteAtlas` 缺少验证资源时不实现字段猜测版，继续作为样本门控项。
   - Font 第一版已经可用，但 atlas 缩略图、样张、kerning、动态字体 / 系统 fallback、TextMeshPro 类资源都依赖更多真实样本，不建议紧接着在缺少样本的情况下扩成完整字体渲染。
6. `Spine` 资源组作为后续 2D 游戏聚合预览方向
   - 首先实现资源组识别、列表视图切换、组摘要 DTO、atlas region overlay、缺失资源诊断和组成资产跳转。
   - 不把 Spine 作为 `AssetType` 筛选项，不让 Presentation 直接读取 AB 或文本文件；所有组合关系与预览 sidecar 都由 Application / Infrastructure 管线输出。
   - `.skel` 二进制细解析、setup pose 和动画播放暂不作为首版完成标准。
7. 对已落地的 `Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController` 做样本驱动增强
   - `Material` 后续重点是基于已解析 TexEnv 的贴图定位、更友好的外部引用呈现，以及在样本足够时评估受控的简化材质球；现有属性语义提示只表达候选含义，不代表 Shader 仿真结果。
   - `AnimationClip` 后续可继续补更细的 binding 分类和曲线统计；不建议在缺少骨骼上下文时先做重型播放预览。
   - `Font` 后续可继续补 atlas 贴图联动和可读样张；不建议让 WinUI 侧绕过预览管线读取字体或贴图资源。
   - `GameObject` 后续可在真实样本足够时补组件引用跳转、Prefab / Scene 层级关系阅读和 MonoBehaviour 脚本友好名增强；不建议让 WinUI 侧绕过预览管线重读场景。
   - `MonoBehaviour` 已按真实样本补字段分组、常见 Unity 序列化容器摘要和未解析脚本 PathId fallback；后续若继续增强，应优先依赖更多 MonoScript / Spine 样本验证，不建议在没有明确脚本协议时推断业务含义或加入编辑器。
   - `AnimatorController` 后续若继续增强，应优先样本驱动补状态名 / 条件摘要 / Clip 到状态的对应关系；不建议在缺少 Avatar / Animator / 场景上下文时先做重型状态机画布或播放预览。

#### 实施约束

后续实现必须遵守以下约束：

- 不允许让 WinUI 直接读取或解析 AB，所有预览内容都必须经 `Application -> IAssetPreviewGenerator -> PreviewCacheStore` 链路输出。
- 不允许把音频波形、atlas 区域、模型默认相机等结构化数据继续塞进 `previewInfoText`；
  这类富预览元数据应进入独立 sidecar metadata。
- 预览缓存的失效仍必须统一依赖 `source fingerprint + decryptorId + decryptorVersion + generatorVersion + audioTranscoderId + audioTranscoderVersion (+ metadata schema version)`。
- 富预览阶段也必须保留结构化摘要回退，避免某个类型一旦无法解码就完全失去可浏览性。

### M9 导入吞吐扩展建议

多线程导入当前已经按工作区导入主链路的正式扩展落地，
而不是作为局部循环优化散落实现。

当前已完成的收口顺序如下：

1. `workspaceKey` 级执行闸门已落地
   手动导入、工作区重新打开时的自动刷新，以及后续缓存重建入口当前都会复用同一互斥链路。
2. 导入协调器已拆成三段
   `WorkspaceImportProcessingPlanBuilder -> WorkspaceImportWorkerPool -> WorkspaceImportCommitter`
   已成为当前稳定主链路，避免了在旧串行循环上直接叠 `Parallel.ForEachAsync(...)`。
3. staging 唯一路径已落地
   解密派生目录当前固定带上 `workspaceKey + decryptorId + bundleCacheKey`
   这一组唯一段，不再只依赖源文件名。
4. bundle 粒度有界并行 worker 已落地
   默认并发度规则与
   [`workspace-management.md`](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/workspace-management.md)
   保持一致，并通过 `WorkspaceImportExecutionOptionsResolver` 固化；用户设置只覆盖自动推导值，每个工作区导入上限保持 `4`。
5. 并发回归已同步补齐首轮关键路径
   当前至少已覆盖缓存复用、解密失败、Reader 拒绝、读取失败、陈旧缓存清理、
   staging 输出路径隔离，以及“同一工作区不允许双重导入作业”这几条关键路径。

当前剩余增强项：

- 工作区级解密器自动识别 `Probe` 仍保持串行；只有在后续 profiling 确认成为瓶颈时，才单独评估低并发 Probe 池。
- 若未来需要继续提高大工作区吞吐，优先补性能采样与更细粒度回归，不直接放宽每工作区 `4` 个 worker 的上限。

## 5.11 M10 保存与重建能力

### 目标

实现从变更队列到输出文件的完整链路。

### 应实现内容

- 变更汇总
- 保存前校验
- 另存为新包
- 冲突处理
- 保存日志

### 完成标准

- 能基于工作区变更输出结果文件
- 默认不覆盖原包
- 失败时可定位具体阶段

### 注意事项

- 这个阶段属于高风险操作
- 必须在前面的读取、替换和错误处理稳定后再开始

## 5.12 M11 测试、诊断与打包

### 目标

补充质量保障和发布准备。

### 应实现内容

- `Domain` 单元测试
- `Application` Use Case 测试
- 错误模型测试
- 本地化资源完整性测试
- 设置页基础功能
- 发布配置检查

### 测试编写时机

- 已稳定、已文档化的行为契约，以及确认过的回归缺陷，应在同一变更中补齐或更新自动化测试。
- 仍处于骨架、占位或过渡态的流程，只测试当前已经实现并且文档已承认的行为，不提前冻结未来语义。
- 当前替换与保存链路仍有部分能力停留在骨架阶段；在 `M10` 真实写包与最终持久化语义落地前，测试先覆盖候选解析、兼容性、摘要、保存前预览、资源包聚合、执行骨架结果和已定义失败即可。
- 当真实写包、冲突处理、取消/回滚或更细粒度失败分类真正进入实现阶段时，需要在同一轮变更中同步补齐对应测试，而不是把未来契约提前写死在当前骨架测试里。

### 测试分层与依赖限制

- `Domain / Application / Infrastructure / Localization / Presentation` 五个测试项目应继续按分层职责各自收口，不用测试去绕过生产代码中的边界。
- `Application.Tests` 默认通过 fake / stub / in-memory double 覆盖用例契约，不把 WinUI 或具体基础设施实现当作前提。
- `Infrastructure.Tests` 可以使用受控临时目录验证持久化和适配器行为，但不承担页面级交互语义。
- `Presentation.Tests` 覆盖 ViewModel、协调器、窗口/导航/回滚语义，不默认升级为真实外部工具或真实全链路端到端测试。
- 默认最小回归集合不依赖网络、真实用户目录、机器全局状态或不可控外部工具安装状态；若未来需要更高层级集成验证，应单独显式定义。

### 完成标准

- 核心模块有基础自动化测试
- 能输出可诊断日志
- 发布前设置和资源完整

## 6. 当前建议的实际开工点

当前最推荐的实际推进点不是重新扩展主界面，而是在已经稳定的 M8 子集基础上继续把工作区机制补成可持续扩展的基础层：

1. 继续补活动工作区在更多依赖页面中的状态摘要承载
2. 为真实 AB 导入链路保留统一工作区入口
3. 评估导入入口与工作区状态摘要的协同方式
4. 再评估是否需要顶层工作区切换器
5. 最后再进入真实 AB 读取接入

原因是：

- 主工作区布局已经稳定，不宜再次大改
- 当前主窗口已经具备稳定承载面，适合继续往真实操作骨架和多工作区上下文推进
- 这条路径能为后续文件替换、批处理和保存能力打基础

## 7. 建议的逐步执行方式

为了避免范围失控，建议每次只推进一个小里程碑。

推荐方式：

- 第一步：完成 M8 中的工作区机制与标签页状态收口
- 第二步：补工作区目录元数据与 JSON 持久化骨架
- 第三步：完成控制台页中的工作区新建、打开、删除与最近工作区入口
- 第四步：把同一套工作区入口同步到工作台和其他依赖活动工作区的页面
- 第五步：补固定工作区、不可用状态和自动恢复链路
- 第六步：继续补活动工作区在批处理页、“设置与诊断”页之外的摘要承载与导入入口协同
- 第七步：继续推进 M9，补齐自动识别、失败诊断与更细粒度缓存策略
- 第八步：推进保存输出与校验能力
- 第九步：补测试、诊断完善与发布收口

## 8. 每阶段完成后应更新的文档

建议每完成一个阶段，同步维护：

- 本路线图文档
- 架构规范文档中的实现偏差
- UI 文档中的结构变更
- 错误处理文档中的新增错误码
- 对本阶段实际触达的代码范围执行一轮架构/约束合规检查，而不是只确认功能可用或项目可编译
- 若检查发现的违规项已经在仓库文档中明确定义了修正方向，则应在同一阶段收口，不应把“已知超限/越界”当作默认可接受尾差

## 9. 当前执行结论

后续实施默认按以下节奏推进：

- 每个功能或重构里程碑在宣告完成前，都应包含一轮针对体量阈值、依赖方向、目录归属、本地化边界和既有工作区/预览 guardrail 的收尾检查
- 若某次变更触达区域已落入文档明确定义的超限/越界状态，则该工作项默认尚未完成，除非同一变更已完成修正，或用户在规则存在歧义时明确接受延后处理

- 已完成 M0
- 已完成 M1，基础错误模型、结果模型和核心 ID 类型已就位
- 已完成 M2，应用层用例骨架和临时只读假数据链路已就位
- 已完成 M4，本地化资源、语言服务与基础日志承载已就位
- 已完成 M5，WinUI Shell、导航和核心页面骨架已就位
- 已完成 M6，基于假数据链路的工作区只读浏览已经稳定接入页面
- 已完成 M7，资源包标签页浏览、复用和关闭回收已稳定接入
- 已开始 M8，单资源提取、工作区替换候选、真实文件选择器接入、文件替换候选、应用/撤销、变更摘要、保存前摘要与批处理页承载结构已进入实现阶段
- 已把 M8 继续推进到批量提取预览、类型筛选和执行骨架阶段，真实文件导出仍后置
- 已把 M8 继续推进到保存执行骨架阶段，真实文件写包仍后置
- 已把 M8 继续推进到批量替换预览、执行骨架与结构化结果明细阶段
- 已开始把 M8 进一步收敛到“资产列表多选提取 + 多工作区上下文骨架”这条支线
- 已完成工作区目录元数据、JSON 持久化骨架与标签页状态恢复接线
- 已补充工作区数据目录中的解析缓存层与变更暂存层骨架，并把删除工作区记录的清理范围扩展到这些工具生成文件
- 已完成控制台页中的工作区新建、打开、删除与最近工作区入口骨架
- 已将工作区新建 / 打开流程收敛为集中式可视化配置面板
- 已把工作区向导调整为目录优先的工作流，并补齐目录内已有工作区的打开 / 重置分支
- 已补齐工作区重置后重建的运行时会话边界：重置打开会绕开同 `workspaceKey` 的旧窗口/进程激活与页面恢复点，成功绑定后再恢复普通去重语义
- 已将工作台顶部命令区与控制台页工作区入口对齐
- 已将窗口导航收敛为“控制台态 / 工作台态”双态模型，并允许从工作台显式返回控制台
- 已把活动工作区感知继续同步到批处理页与“设置与诊断”页，并补齐工作区目录刷新时的会话状态保留
- 已完成最近工作区的固定 / 取消固定、不可用状态表达与自动恢复上次活动工作区
- 已把 `文件导入 / 目录导入` 入口抽到共享导入协调服务，并同步承载到批处理页与“设置与诊断”页的活动工作区摘要区
- 已把工作台拖拽导入桥接到与文件/目录导入相同的共享协调服务，并把拖拽接收范围限制在左侧资源包列表区域
- 已把统一导入入口继续推进到 Application 层的“工作区导入来源登记”命令，并将轻量来源描述写入工作区元数据
- 已将 `WorkspaceBrowserService / WorkspaceCatalogService` 的 Handler 组装收口到 `App` 组合根，避免在 Presentation 门面内部继续 `new Handler`
- 已完成 `WorkspaceBrowserService / WorkspaceCatalogService` 的第一轮边界收口：Presentation 门面只依赖 `Application.Workspace.Services` 抽象，具体 Handler 扇出已统一回收到 Application
- 已完成 `WorkspaceBrowserService` 的 D-02 第二轮收口：底层单一 `WorkspaceBrowserGateway` 已拆为 `WorkspaceExplorerGateway / WorkspaceAssetInspectionGateway / WorkspaceAssetOperationGateway / WorkspaceBatchOperationsGateway` 四个能力簇网关，`WorkspaceImportCoordinatorService`、`BatchTasksViewModel` 与多组 `BundleExplorer` 协调器也已改为依赖对应窄接口
- 已完成 D-02 第三轮收口：`WorkspaceBrowserService / WorkspaceCatalogService` 已从主注入链移除，`App.xaml.cs` 当前直接暴露 `WorkspaceExplorer / AssetInspection / AssetOperation / BatchOperations / Catalog` 五组窄网关，`BundleExplorerPage`、`BatchTasksPage` 与 `HomePage` 也已按需直接消费对应接口
- 已完成资源包解密扩展基础框架、工作区级解密器选择与解密器管理页，为“加密 AB -> 解密 -> Reader 读取”链路预留稳定插入点，并支持外部进程解密器清单的新增、编辑、删除与重新加载
- 已开始把工作区已选解密器真正接到真实导入处理链路，并将真实导入文件写入 `BundleIndexCacheStore` 后再由缓存型会话驱动资源包列表
- 已完成 M9 第二子阶段的第一轮落地：`AssetsTools.NET` 已接入到 Infrastructure Reader，资产列表与基础详情已来自真实 AB 解析结果
- 已完成 M9 第二子阶段的第二轮落地：资源包索引缓存已支持按资源包命中复用、失效重建与缺失清理，并纳入解密器版本维度
- 已完成 M9 第三子阶段的第一轮落地：工作台资源详情已接入预览缓存生成 / 命中链路，并支持多类型资源预览
- 已完成 M9 当前子阶段中的第一轮工作区级解密器自动识别：新建工作区默认可选“自动识别”，导入时会解析并固化真实工作区解密器
- 已把 M9 当前子阶段的自动识别进一步细化为“导入源识别 / 解密器识别 / Reader 路径识别 / 缓存命中识别”四层，并在导入前补上前置筛选与结构化诊断
- 已完成 M9 当前子阶段中的失败诊断 / 更细粒度缓存诊断 / 依赖深读第一轮收口，导入与预览主链路已具备结构化原因表达
- 已完成 M9 当前子阶段中的失败诊断第二轮收口：导入自动识别 sample message、Reader 直检原因、预览诊断 detail 与预览生成稳定失败原因已进一步统一到结构化消息 / 标准错误令牌，不再依赖基础设施层直接拼装面向用户的说明文本
- 已完成 M9 当前子阶段中的依赖视图扩展：检查器依赖页已接入 `PathId` 展示、`CAB-*` 友好解析、反向引用与依赖闭包四类视图
- 已完成 M9 当前子阶段中的工作区级 CAB 反查第一轮收口：检查器中的 Bundle 外部依赖在命中当前工作区上下文时，已可把 `CAB-*` 还原为具体资源包文件名
- 已将“新窗口打开工作区”从同进程多窗口改为“新主进程承载工作区”，并补上启动参数解析、跨进程激活、同进程既有工作区直达工作台与启动期工作区重定向
- 已完成 D-08 第二轮窗口模型收口：当前工作区打开链路已补上“formal 正式注册 + pending 预登记 + 成功晋升 + 失败回滚”，工作区加载失败时不再把原有工作区上下文一并清空
- 已为 D-08 主链路补上首轮 `Presentation` 回归：`WorkspaceProcessCoordinatorServiceTests` 当前覆盖 pending 预登记 / 晋升 / 清理，`BundleExplorerWorkspaceBindingCoordinatorTests` 当前覆盖工作区加载失败时的恢复与空态回滚
- 已完成 M9 富预览扩展的 `AudioClip` 第二阶段：预览链路已补齐 `AssetPreviewKind.Audio`，支持 `WAV / OGG / MP3 / FLAC / M4A(MP4) / AAC` 的可播放载荷透传，并对 `FSB5` 补上独立外部转码层；当前会优先通过 `vgmstream-cli` 转为 `WAV` 后播放，缺失转码器或转码失败时稳定回退到结构化摘要
- 已完成一轮架构高优先级收口：`Application` 侧已移除 `InMemoryWorkspaceSessionRegistry.Shared` 默认回退，并把工作区导入链路中的文件系统探测能力抽象为应用层端口，由 `Infrastructure` 统一提供实现
- 已完成 `WorkspaceContextService` 持久化边界第一轮收口：最近工作区目录快照、工作区元数据与浏览状态已统一下沉到 `Application.Workspace.Services.WorkspaceContextPersistenceService`，`Presentation` 不再直接触碰工作区仓储与 `Directory.Exists(...)`
- 已完成 `WorkspaceContextService` 的 D-01 第三轮收口：最近工作区目录恢复、可用性映射、“上次活动工作区”恢复请求推导、激活元数据补齐与浏览状态回写编排已进一步下沉到 `Application.Workspace.Services.WorkspaceContextCatalogService`，主类体量已由约 `545` 行降至约 `261` 行
- 已完成一轮用户文本资源化高优先级收口：工作区导入自动识别、Reader 直检与预览诊断 detail 已改为展示层统一本地化；当前剩余热点主要是外部工具原始执行输出与少量异常分支
- 已完成一轮用户文本资源化第三阶段收口：解密器设置页保存/删除失败与工作台预览显示兜底日志已移除原始异常直透；当前剩余热点主要是外部工具原始执行输出与启动期异常展示
- 已完成 `BundleExplorerViewModel` 第一轮拆分：左侧资源包窗格状态与资源包/资产标签工作区状态已分别下沉到独立子 ViewModel，主工作台 ViewModel 开始收缩为页面级外观与流程编排层
- 已完成 `BundleExplorerViewModel` 的 D-04 后续收口：浏览状态恢复/持久化节流、页面级结果反馈、工作区加载/重绑定编排以及资源包标签打开/激活/关闭/重排已分别下沉到 `BundleExplorerBrowserStateCoordinator / BundleExplorerOperationFeedbackBridge / BundleExplorerWorkspaceBindingCoordinator / BundleExplorerBundleTabCoordinator`，`BundleExplorerPage` 也已直接迁回子状态宿主绑定，旧页面兼容透传属性与 `BundleExplorerPageStateBridge` 已移除，主类体量已从 `1258` 行降至约 `426` 行
- 已完成 `BundleExplorerAssetWorkflowViewModel` 的 D-04 第六轮收口：预览加载/呈现、提取/替换/撤销动作、检查器投影、资产文档标签同步、主资产窗格加载/刷新/空态回退以及资产级命令入口/页面动作编排已分别拆为独立协作对象，共享结果失败反馈继续统一复用 `BundleExplorerOperationFeedbackBridge`，主类当前约 `273` 行；当前 D-04 的主要强风险已完成收口，后续更适合把注意力转向防止职责回灌，而不是重新扩大页面主类
- 已完成 `BundleExplorer` 页面级共享状态的 D-03 第三轮收口：`BundleExplorerPage.xaml / BundleExplorerPage.xaml.cs` 已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 的语义化状态宿主，旧的 `BundleExplorerPageStateBridge` 已移除，检查器同步、资产命令态、左栏搜索/折叠态与文档宿主兼容属性刷新都已继续按状态宿主边界显式通知
- 已继续完成 `BundleExplorerPage.xaml.cs` 的页面协作收口：工作区导航/窗口注册、资源包窗格布局动画、资产文档标签同步与页面静态 chrome 当前已分别下沉到 `BundleExplorerPageNavigationCoordinator / BundleExplorerPagePaneLayoutCoordinator / BundleExplorerPageTabCoordinator / BundleExplorerPageChromeCoordinator`，页面主 code-behind 当前约 `362` 行；后续重点转为防止新的页面视图流程重新长回单一巨型 code-behind
- 已继续完成资源编辑窗口模式的一轮实现闭环：命令式 `在新窗口中打开 / 移动到主窗口`、标签拖拽、整组拖出 / 放回、窗口快照持久化，以及“启动读取独立窗口快照后弹出可控恢复确认，并保留稍后手动恢复入口”当前已打通。恢复执行已补上不可用资源包的部分失败保留、恢复提示中的显式忽略入口，以及恢复 bounds 的显示器工作区安全归位；后续重点转向更完整的冲突处理与编辑草稿恢复。窗口模式仍不得绕过 Application 预览 / 替换 / 保存用例，也不得引入 Presentation 到 Infrastructure 的新直接依赖
- 已完成 `BatchTasksViewModel` 的一轮页面投影收口：工作区摘要、批量提取/替换/保存预览、结果卡片与静态筛选项初始化当前已下沉到 `BatchTasksPresentationPresenter`，页面主 ViewModel 当前约 `533` 行；后续重点转为防止新的展示映射和文本拼装重新回灌回主类
- 已完成 D-05 第一轮边界显式化：`Presentation -> Infrastructure` 的直接引用当前只允许出现在 `src/AssestBundleTools.Presentation.WinUI/Composition/` 目录，`App.xaml.cs` 与普通壳层服务已退回为消费组合结果或依赖 Presentation 窄抽象，并补上仓库级检查防止新的越界引用
- 已完成 D-06 第一轮最小测试骨架：`tests/AssestBundleTools.Domain.Tests / AssestBundleTools.Application.Tests / AssestBundleTools.Localization.Tests` 已分别覆盖 `AppError`、`Result + 工作区目录关键 Handler` 以及 `zh-CN / en-US` 资源键一致性与 `ResourceKeys` 覆盖校验；后续测试重点转向扩展更多 `Application / Infrastructure` 关键路径
- 已完成 D-06 第二轮最小测试骨架：`tests/AssestBundleTools.Infrastructure.Tests` 已建立与 `Application.Tests` 一致的零依赖控制台 runner，当前覆盖 `JsonWorkspaceCatalogStore / JsonWorkspaceMetadataStore` 的缺失文件空载入、保存回读与删除清理；`Localization.Tests` 也补上了 `Presentation -> Infrastructure` 与 `ResourceKeys` 集中管理边界检查，四层最小回归入口当前已经齐备
- 已完成 D-06 第三轮回归补强：`tests/AssestBundleTools.Presentation.Tests` 已加入解决方案，并补上工作区进程占用与工作区恢复回滚两条 `Presentation` 关键路径，最小自动化回归当前已从四层骨架扩展到五层关键入口
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前已补 `RemoveWorkspaceBundleHandler` 的元数据修正与缓存失效链路，`tests/AssestBundleTools.Infrastructure.Tests` 当前已补 `JsonPreviewCacheStore` 的预览缓存落盘/删除回归
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前也已补 `RegisterWorkspaceImportHandler` 的路径去重、更新计数、路径类型统计与排除列表修正
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前也已补 `ProcessWorkspaceImportsHandler` 的缓存复用命中、解密失败、Reader 拒绝、读取失败，以及陈旧 bundle / preview 缓存清理与 `SourceRemoved` 诊断
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前也已补 `GetAssetPreviewHandler` 的预览缓存命中、`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建、音频摘要在新转码器可用时的升级重建，以及预览解密失败、预览生成失败摘要回退链路
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Infrastructure.Tests` 当前也已补 `JsonPreviewCacheStore` 在缺失 payload / 非法 payload path 下的容错加载、路径解析与描述文件清理
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前也已补替换链路中的文件候选类型解析、来源路径校验、缺失资源失败、类型不匹配、工作区候选空集失败、单资源文件/工作区替换、撤销替换，以及批量替换预览/执行摘要链路
- 已继续扩展 D-06 第三轮回归：`tests/AssestBundleTools.Application.Tests` 当前也已补 `GetWorkspaceSavePreviewHandler / SaveWorkspaceAsNewBundleHandler` 的保存预览空态、资源包分组聚合、缺失工作区、无候选失败与执行摘要链路
- 已完成 D-07 第二轮收口：宿主程序自身的启动/壳层异常提示与页面代码中残留的资源键字面量已继续收口到资源键常量和标准错误令牌；当前仅保留外部工具原始输出作为诊断回退文本，而不再作为主提示文案
- 当前 M9 富预览扩展已完成 `AudioClip`、`Sprite` 增强、`Mesh` 线框 / UV0 / 贴图关联 / 第一版逐三角贴图预览、`Material` 半可视化摘要、`AnimationClip` binding / 曲线摘要增强、`Font` 第一版半可视化摘要、`GameObject` 第一版信息浏览预览与 `MonoBehaviour` 第一版信息浏览预览及真实样本补强；`SpriteAtlas` 继续按样本资源可用性回补，Spine 作为后续资源组预览方向
- 当前 Sprite 预览已完成一轮增强：源贴图预览上叠加裁切框、pivot 点与 9-slice border，检查器额外展示裁切结果卡片；Infrastructure 已把 Sprite 专属生成逻辑拆入独立 builder，检查器预览表面也已拆为独立控件，避免后续富预览继续回灌到巨型生成器或检查器控件
- 当前 Mesh 预览已完成线框 / UV0 / 贴图关联 / 第一版逐三角贴图预览，并补上贴图化线框与 UV 视图共用的 Mesh/UV 网格显隐开关：`AssetsToolsMeshPreviewBuilder` 负责从真实 Mesh 字段读取轻量几何、三角面索引、UV0 和顶点通道摘要并生成 schema v3 JSON sidecar，`GetAssetPreviewHandler` 负责缓存命中后的结构化反序列化，`CachedWorkspaceSessionRegistry` 负责从运行时依赖图派生 `TextureAssociations`，`InspectorMeshPreviewSurfaceView` 只消费 DTO 并通过 Win2D 绘制线框、UV 或逐三角贴图化线框，不承担 AB 解析；贴图关联优先走 `Mesh <- MeshFilter / Renderer / ParticleSystemRenderer / GameObject -> Material -> Texture2D / Sprite`，再兜底 `Mesh -> Texture2D / Sprite`。当前贴图化预览用于表达几何 / UV / 贴图的基础对应关系，不模拟 Shader、Material tiling/offset、多材质 submesh 分配或光照效果。
- 当前 AnimationClip 预览已完成 binding / 曲线摘要增强：`AssetsToolsAnimationClipPreviewBuilder` 负责从真实 AnimationClip 字段读取采样率、起止时间、循环设置、曲线数量、前若干 animation events、曲线 binding 总数与前若干 binding 行并生成 schema v3 JSON sidecar；当 `m_AnimationClipSettings` 缺失时，时间和循环字段回退读取 `m_MuscleClip`，packed generic binding 的 keyframe 数在未解码前显示为未知。`GetAssetPreviewHandler` 负责缓存命中后的结构化反序列化，`AssetPreviewResultPresenter` 再把 DTO 交给独立 AnimationClip formatter / ViewModel / 控件渲染；自适应摘要预览区只放主要摘要，时间 / 循环 / binding / 事件明细进入通用信息卡，不在缺少上下文时把播放或骨骼驱动预览硬塞进 UI。
- 当前 Font 预览已完成第一版半可视化摘要：`AssetsToolsFontPreviewBuilder` 负责从真实 Font 字段读取基础指标、font data 大小、atlas 贴图引用、默认材质引用与前若干字符矩形并生成 schema v1 JSON sidecar，`GetAssetPreviewHandler` 负责缓存命中后的结构化反序列化，`AssetPreviewResultPresenter` 再把 DTO 交给独立 Font formatter / ViewModel / 控件渲染；后续增强优先考虑 atlas 贴图联动和受控样张，不在 WinUI 侧直接读取 AB。
- 当前 GameObject 预览已完成第一版轻量信息浏览：`AssetsToolsGameObjectPreviewBuilder` 负责读取对象基础字段、Transform 摘要、组件 PPtr、MonoBehaviour 脚本引用和常见组件二级资源引用并生成 schema v1 JSON sidecar；自适应摘要预览区只放主要摘要和紧凑摘要卡片，对象 / Transform / 组件 / 引用明细进入通用信息卡，已解析 PathId 复用统一资源跳转，不做场景渲染。
- 当前 MonoBehaviour 预览已完成第一版轻量信息浏览和真实样本补强：`AssetsToolsMonoBehaviourPreviewBuilder` 负责读取对象名、启用状态、MonoScript 摘要、字段分组、常见 Unity 序列化容器、前若干标量 / 数组字段和 PPtr 引用并生成 schema v1 JSON sidecar；自适应摘要预览区只放脚本、字段和引用摘要，脚本 / 字段分组 / 容器 / 字段 / 引用明细进入通用信息卡，已解析 PathId 复用统一资源跳转，未解析 MonoScript 只显示 PathId fallback，不做脚本业务语义推断、编辑或运行时模拟。
- 当前导入主链路在功能上已完成第二阶段多线程收口：`workspaceKey` 级执行闸门、唯一 staging 路径、`PlanBuilder -> Worker Pool -> Committer` 三段模型与 bundle 粒度有界并行都已落地；后续进入“导入吞吐增强”支线时，更适合转向 profiling、Probe 并发评估与更深层并发回归
- 当前资产预览查询已从单槽后台闸门调整为前台优先的有界并发执行队列，默认并发度为 CPU 核心数一半并限制在 `1-4`；其中 1 个前台槽专门服务当前活动资产详情标签，其余槽用于已打开资产标签的后台预热。若并发度为 1，后台预热不与前台竞争；切换到尚未完成的标签会取消该标签旧后台请求并以前台优先级重开，已完成预览则直接复用。

## 10. 架构代办事项

更细的现状登记、受影响文件与建议收口顺序，统一见
[`implementation-deviation-register.md`](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/implementation-deviation-register.md)。
本节只保留与路线图推进直接相关的高优先级收口项。

以下事项当前已不再构成阻塞性偏差，但仍作为后续功能推进时需要持续守住的架构 guardrail：

1. `WorkspaceContextService` 已完成第三轮收口；最近目录项恢复、可用性语义与“上次活动工作区”恢复请求已下沉到 `Application.Workspace.Services.IWorkspaceContextCatalogService`，后续重点转为防止新的目录编排语义重新回流到 Presentation。
2. 工作区浏览能力注入面已完成第三轮收口；后续重点转为防止新的聚合总入口重新长回 Presentation，并继续保持页面根对象只依赖自己真正需要的能力簇接口。
3. 工作区进程占用链路已完成 D-08 第二轮收口，并已补首轮 `Presentation` 回归；后续重点转为继续扩展跨进程窗口激活与重定向细节回归，而不是继续变更这条语义主线。
4. “窗口级 `WorkspaceContextService` 重构”当前已不再作为主路线；若未来确实要支持“一进程多工作区并行”，应单独立项做窗口作用域上下文，而不是在现有单工作区进程模型上继续叠兼容。
5. 当前 `AudioClip` 的 `FSB5` 播放能力优先依赖外部 `vgmstream-cli`；若未来确实需要“完全无外部依赖”的部署形态，应在保留现有独立音频转码端口的前提下，单独补一轮内建 `FSB5` 部分 codec 支持，而不是把编解码逻辑直接塞回 Preview UI 或 Reader。
6. `BundleExplorerViewModel` 当前约 `426` 行；在页面兼容透传属性面与 `BundleExplorerPageStateBridge` 一并移除之后，主类已重新回到常规手写业务类 `200-500` 行目标内，后续重点应转为“避免再把新的页面流程或兼容面回灌进主类”，而不是继续把它当作 D-04 的主要热点。
7. 当前资产工作流子 `ViewModel` 已完成 D-03 第三轮 sibling / 页面状态收口与 D-04 第六轮体量收缩：`Preview / Modification Action / Inspector Projection / Asset Document Coordination / Asset Document Host / Asset Pane Coordination / Asset Command Coordination` 已分别下沉到 `BundleExplorerAssetPreviewCoordinator / BundleExplorerAssetModificationActionHandler / BundleExplorerAssetInspectorPresenter / BundleExplorerAssetDocumentTabCoordinator / BundleExplorerAssetDocumentHostCoordinator / BundleExplorerAssetPaneCoordinator / BundleExplorerAssetCommandCoordinator`；共享失败反馈继续统一复用 `BundleExplorerOperationFeedbackBridge`；`BundleExplorerPage` 也已直接绑定或订阅 `WorkspaceShell / BundlePane / DocumentWorkspace / AssetWorkflow` 的语义化状态宿主，旧页面兼容桥接层已移除；主类当前约 `273` 行，后续重点应转向防止职责回灌，而不是继续把命令逻辑堆回资产工作流主类。
8. 工作区导入多线程扩展当前已完成第二阶段主链路收口：`WorkspaceImportExecutionGate`、唯一 staging 路径、`PlanBuilder -> Worker Pool -> Committer` 与串行提交模型都已落地；后续重点转为守住并发边界，并在有明确收益时再评估 Probe 并发与更细粒度吞吐优化。

这些事项当前不阻塞 M9 主线，但后续在缓存失效、预览链路和保存输出继续推进前，应择机完成后续收口。
