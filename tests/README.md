# Tests

当前已建立第三轮最小回归骨架：

- `AssestBundleTools.Domain.Tests`
- `AssestBundleTools.Application.Tests`
- `AssestBundleTools.Infrastructure.Tests`
- `AssestBundleTools.Localization.Tests`
- `AssestBundleTools.Presentation.Tests`

当前覆盖重点：

- `AppError / Result` 基础行为
- 工作区目录相关 `Application` Handler 的关键路径（当前通过零依赖控制台 runner 执行）
- 工作区资源包移除时的元数据修正与缓存失效链路
- 工作区导入来源登记时的路径去重、更新计数、类型统计与排除列表修正
- 工作台资源包列表拖放兼容契约：父面板接收已处理的子控件路由事件、DragEnter / DragOver 阶段提前声明 Copy、Drop 阶段延迟读取 StorageItems，并记录处理器环境、拖放会话、格式、读取异常与导入分发诊断；诊断中心日志在活动工作区下同步追加到 `logs/diagnostics.log`
- 主应用权限契约：Win32 manifest 显式使用 `asInvoker / uiAccess=false`，运行时检测实际管理员权限并显示拖放受限警告，不以消息过滤或 UIAccess 绕过 Windows 完整性级别隔离
- 工作区导入处理时的缓存复用命中、解密失败、Reader 拒绝、读取失败、标准化 bundle artifact 复用，以及陈旧 bundle artifact / index / preview 缓存清理与 `SourceRemoved` 诊断；自动识别覆盖每个 AB 独立 Probe、匹配包 / 标准包在同一工作区分别选择 codec / 直通、工作区持续保留自动策略，并在各 bundle artifact / index 记录实际 codec、由 artifact manifest 保存有界 codec metadata 供后续原样导出回写；内置 Arknights LZ4AK codec 覆盖 UnityFS 特征 Probe、合成包确定性回环和可用真实样本回环，内置 CrossCore codec 覆盖 duplicated-header v1 / prefixed-header v2 特征 Probe、合成回环、真实样本 AssetsTools 读回和原文件 SHA-256 回环；外部 CLI 适配器覆盖可选 Encrypt 命令、codec 参数透传与只读内置目录项
- 资源预览查询时的缓存命中、标准化 bundle artifact manifest 命中时跳过解密器、并发预览缓存 miss 时 artifact 生成 single-flight、文本预览完整载荷缓存、有界展示文本投影、PreviewCache / FileReplacement / EditDraft 三类完整文本 reference 的 chunk / lines / search 查询、文本行 / 字符索引 sidecar 保存 / 回退读取与 usage 统计、Texture2D 文件/工作区替换后的有效图片预览、Mesh / Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController 结构化载荷水化、AnimationClip binding / 曲线摘要展示、GameObject 自适应预览摘要卡片、组件引用信息卡与已解析资源跳转展示、MonoBehaviour 自适应预览摘要卡片、脚本 / 字段分组 / 序列化容器 / 字段 / 引用信息卡、TypeTree JSON 字段树与已解析资源跳转展示、AnimatorController 自适应预览摘要卡片、状态机容器 / 参数 / AnimationClip 引用信息卡与已解析资源跳转展示、`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建、音频摘要在新转码器可用时的升级重建，以及解密失败 / 生成失败摘要回退链路
- Spine 资源组第一版 Application 读模型中的 `.atlas / .skel / .skel.bytes / .json` 组识别、跨工作区 `Texture2D` page 候选匹配、atlas 完整预览文本 page / region 解析、page 到 Texture2D 组成资产映射、JSON skeleton 的 bone / slot / skin / animation 轻量计数摘要、`.skel / .skel.bytes` 的 `AssetPreviewKind.Binary` 预览载荷传递、多 skin region attachment 与基础 timeline 的实验性动画预览 DTO、runtime-neutral skin previews / setup frame / sampled frames / draw commands、draw order timeline、slot color/tint、`V21 / V34 / V35 / V36 / V37 / V38 / V40 / V41 / V42` runtime adapter 版本矩阵、runtime adapter 端口优先于 JSON fallback 的应用层调度、V38 / V42 官方 runtime JSON / binary adapter 的 atlas / skeleton 解析与 draw command 输出、V38 / V42 runtime playback session 当前帧 seek、setup pose、skeleton 复用、frame diagnostics 和取消结果、JSON fallback sampled playback session、固定 FPS 帧序列采样、序列帧导出计划的共享画布 / world-to-pixel 变换 / PNG 文件命名、Presentation PNG 序列帧导出入口、V38 / V42 mesh attachment world vertices / atlas UV / triangle draw command 输出、V38 / V42 mesh deform timeline 采样、V38 transform constraint timeline 采样、V38 / V42 IK constraint timeline 采样、官方 runtime 复杂 mesh 采样预算、二进制 skeleton 版本未知时尝试已注册 adapter、无匹配 adapter 的结构化诊断、缺失 atlas / texture page / atlas 预览不可用、atlas page 未匹配 Texture2D、Texture2D 预览不可用、page 尺寸与图片尺寸不一致诊断，JSON fallback mesh / weighted vertices / deform、slot 双颜色、event、约束和 Bezier 曲线等实验动画预览未支持特性的结构化诊断，以及 Presentation 资产窗格 `资源 / Spine 组` 视图切换、浏览状态持久化、主窗口右侧组文档去重打开、组文档轻量恢复、统一资源文档标签组内普通资产与 Spine 组混排 / 重排 / 移出、独立窗口轻量快照恢复、隐藏预览 inactive、详情回填、atlas page 选择切换、Texture2D region overlay、skeleton 摘要、实验性动画预览投影、逐三角 mesh 渲染与控件级帧缓存、动画选择、真实 skin 切换、速度、进度、播放时间与进度 slider / 时间读数展示时间拆分 / 节流、暂停 / 逐帧 / 独立 setup pose frame 的播放体验状态，以及 D3D 主线前置的 Presentation 渲染后端抽象、DTO 到 vertex batch 的 draw order / UV / tint / mesh 三角投影、clip-space 投影、预览视口平移 / 锚点缩放 / 重置状态、Texture2D 预览载荷解码 / page size 暴露、常见独立 alpha page 合成、premultiplied alpha 解码与 D3D / Win2D 载荷传递、默认 D3D11 shader resource 上传、可复用动态 vertex / index buffer 容量增长、D3D 渲染失败自动回退 Win2D、D3D 诊断输出、device lost 识别、小窗口 frame / texture version 批次缓存、runtime frame generation 诊断、batch renderer 预热和顶点 / 索引批次后端选择
- 编辑草稿恢复候选解析中的工作区范围校验、轻量引用标准化、可恢复 / 缺失 / 来源不匹配 / 过期 / 引用无效诊断分类，工作区变更暂存中的 `editDrafts[]` 描述读取，`staging/drafts` 文本草稿 payload 的保存、按块读取、按行读取、有界搜索、删除和非法路径保护，以及 Presentation 恢复弹窗的草稿候选预检摘要投影和可恢复标记筛选
- 工作区可恢复操作记录：`WorkspaceChangeSetDto.operations[] / schemaVersion / operationSequence` DTO 契约、操作日志追加与状态更新、当前生效操作筛选、`Pending` 到恢复计划 `Interrupted` 的基础分类、`JsonWorkspaceChangeStore` 对操作记录和字符串枚举的 JSON round-trip、文件/工作区替换、撤销替换、目标资源级重做替换成功后的操作记录追加、替换读模型按事实流物化当前有效替换、Sprite 裁切框 / pivot 几何保存为 `EditDraftApplied` 操作记录并在重开工作区时物化有效编辑状态、Sprite 编辑区关联 Texture2D 文件替换立即复用 Texture2D 替换链路、关联资源变更卡片撤销 / 重做时解析真实替换目标、单资源 / 批量 / Spine 资源组提取记录追加，资产详情从 `operations[]` 投影独立修改 / 最新提取记录，工作区操作通知携带目标资产时的局部详情 / 资产列表状态刷新，工作区级批量提取跨 AB 受控并行 / 同 AB 内顺序 / GC 压力退避 / 结构化进度快照，以及工作区重开时从 `operations[]` 物化当前生效替换状态
- 关联资源影响链路：运行时工作区依赖图可从被修改 Texture2D 查询到依赖它的可展示 Sprite；Texture2D 直接替换会在 Sprite 详情中投影为关联替换记录；Sprite 预览会复用关联 Texture2D 的当前有效替换载荷并保留 Sprite 裁切覆盖层；Presentation 操作通知会通过应用层关联影响查询把受影响资产从真实目标扩展到依赖资源
- 工作区级批量提取对话框第一版：顶部 `批量提取` 入口打开对话框协调器，对话框内容暴露资源包、类别、输出目录和计划摘要绑定；ViewModel 覆盖默认活动资源包选中、`All` 与显式类别互斥、输出目录校验、提交 DTO 和搜索过滤不破坏选择状态；Presentation formatter 覆盖工作区批量提取准备、当前资源、动态未知类型名和内存退避提示
- Presentation 统一后台任务调度第一阶段：`IBackgroundWorkScheduler` 会复用底层 runner、把可见任务进度投影到状态服务，并按 `BatchExtraction` profile 串行多选 / 批处理提取任务、按 `Maintenance` profile 串行缓存清理任务；调度器已提供 `StartAsync`、`BackgroundWorkHandle<T>`、当前任务快照、`TasksChanged` 和 `TryCancel`，状态条服务会对可取消当前任务暴露取消入口，并能回调调度器取消同一个后台任务；工作区导入处理阶段已通过 `WorkspaceImport` profile 运行，并把 Application 导入进度同步投影到状态条；普通预览 / 后台预热已通过 `PreviewForeground / PreviewPrewarm` profile 运行，前台资产预览、Spine 组详情和当前贴图页载荷会投影轻量 Shell 状态，后台预热保持静默，Spine runtime 当前帧已通过独立 `SpineRuntimeFrame` profile 运行，预览超时放弃后会等底层任务真实结束再释放 profile 闸门；快速切换资产详情标签时当前标签优先进入前台预览，旧活动标签降级为后台预热，后台预热按最近激活 / 最近打开优先，前台预览调用方取消可释放 `PreviewForeground` profile 槽；预览 worker 总数已接入设置页自动 / `1-4` 固定值；预览 `OutOfMemory` 会以专用摘要诊断回退并短期暂停同 bundle 后台预热，前台成功预览后恢复；`cache/bundles` 标准化 bundle artifact 已接入导入 / 预览 / 提取 / Spine 详情共享路径，Application artifact 生成阶段已按资源包键 single-flight，Infrastructure 内部 `AssetsToolsBundleLoadedSessionCache` 已接入 loaded-session single-flight / lease / 滑动 TTL / 默认容量上限 / 内存压力退避 / artifact 主动失效，并由 Reader、预览生成器和经预览管线导出的提取按标准化 artifact identity 共享，减少同一 bundle 的重复加载；标准化 artifact 当前支持显式清理和偏好驱动自动容量治理
- Shell 设置与诊断导航契约：四个设置分区由 Shell 左栏默认展开的父项下子菜单承载，首次进入默认选择“偏好”，返回时恢复当前窗口上次子项；分区切换复用当前设置页实例，页面内容区不再嵌套第二套 `NavigationView`；偏好页显示语言选项覆盖跟随系统、简体中文和 English，选择会持久化并同步到 Windows 应用语言覆盖，重启后完整生效
- 主窗口 chrome 契约：使用标准 `32px` 单行标题栏，仅显示产品名；不再启用 `TitleBarHeightOption.Tall`，并移除主窗口副标题与 Shell pane 页眉；Shell 导航采用可折叠的 `Left` / compact inline 模式，收缩后保留 `48px` 图标栏，展开后恢复占位布局而不覆盖内容
- 资源编辑窗口模式中的打开冲突解析：主窗口和独立资源编辑窗口已存在同一资源文档标签时，优先解析到可激活的已有宿主和目标标签，避免重复创建窗口或标签；独立窗口单标签拖出策略会阻止创建等价新窗口
- 替换链路中的候选类型解析、来源路径校验、缺失资源失败、类型不匹配、工作区候选空集失败、单资源文件/工作区替换、右键 / 检查器 `替换文件` 立即选择文件并应用、撤销替换、目标资源级重做替换、Texture2D 有效替换预览，以及批量替换 manifest/启发式计划、真实文件替换执行、支持/跳过混合结果摘要
- 保存链路中的保存前预览空态/资源包分组聚合、现有标准化 artifact 输入图与来源 codec 身份解析、目标输出路径 preflight blockers、保存执行的缺失工作区 / 无候选失败 / 执行摘要、选中资源包真实导出、processor selection 透传、文件替换载荷的资源处理器 work item 构建、AudioClip 处理器输出投影、音频写入模式透传、处理器结果透传、失败处理器阻断所属 bundle 写包、对象级写包项构建、TextAsset / Texture2D / Sprite / AudioClip 写后读回、Texture2D 原格式不可编码时的 RGBA32 回退、AudioClip 按原容器匹配写入 `.resS / .resource`、容器不匹配拒绝和强制写入分支、来源 codec 在标准写包后的可选 Encrypt、AB 导出 manifest 写出、输出 codec 身份、处理器结果摘要和 MD5 / SHA-256 记录
- 工作区目录相关 `Infrastructure` 持久化关键路径（当前也通过零依赖控制台 runner 执行）
- 缓存型工作区运行时依赖图中的反向引用、依赖闭包、Mesh 贴图关联，以及外部 `PPtr` 占位到当前工作区已导入资产的跨 Bundle 解析
- 缓存型工作区运行时资产列表中的动态类型 Facet、隐藏图节点排除、以及 `Unknown + Unity 类型名` 的资产级筛选
- 预览缓存描述文件 / 载荷文件 / 文本行 / 字符索引 sidecar 的保存回读、删除清理，编辑草稿文本 payload 描述文件 / 载荷文件 / 文本行 / 字符索引 sidecar 的保存回读、删除清理，以及缺失 payload / 非法 payload path / 缺失文本索引下的容错加载与清理
- 标准化 bundle artifact store 的派生 / PassThrough bundle 统一复制落盘、旧版 manifest-only 记录拒绝复用、`lastAccessedAtUtc` 初始化 / 回写、usage 统计、按大小 / 数量上限的 LRU prune、受保护 artifact prune、Application 缓存治理用例、自动 prune 策略触发、工作区级删除清理，loaded-session 对相同 artifact identity 的 in-flight 复用、等待者取消后保留可复用 in-flight 结果、不同 artifact identity 的隔离、容量上限 LRU 淘汰、内存压力退避和 artifact / workspace 主动失效，以及预览内存不足的 Application fallback 与 Presentation 后台预热退避
- `zh-CN / en-US` 资源键一致性与 `ResourceKeys.cs` 覆盖率
- `Presentation -> Infrastructure` 组合根边界检查
- `Presentation` 本地化调用不得直接写资源键字面量
- 应用偏好 JSON 的显示语言保存回读，以及旧快照缺少语言字段时回落到跟随系统
- 工作区进程占用链路中的 `pending` 预登记 / 晋升 / 清理行为
- 工作区加载失败后的页面级恢复与空态回滚链路

测试项目职责边界：

- `AssestBundleTools.Domain.Tests`
  - 只覆盖领域规则、值对象、实体和稳定领域错误/契约
- `AssestBundleTools.Application.Tests`
  - 覆盖 Handler / Use Case / `Result` 语义 / DTO 契约
  - 默认通过 fake / stub / in-memory double 驱动应用层端口
  - 不把 WinUI 或基础设施实现细节当作测试前提
- `AssestBundleTools.Infrastructure.Tests`
  - 覆盖持久化、序列化、目录布局和适配器行为
  - 可以使用受控临时目录 IO
  - 不承担页面级状态或窗口语义验证
- `AssestBundleTools.Localization.Tests`
  - 覆盖资源键完整性、`ResourceKeys`、架构 guardrail 静态检查
- `AssestBundleTools.Presentation.Tests`
  - 覆盖 ViewModel、协调器、窗口/导航/回滚语义，以及受控展示层边界检查
  - 不默认升级为“真实 Reader + 真实缓存 + 真实外部工具”的全链路集成测试

测试编写时机约束：

- 已稳定、已文档化的行为契约，以及已经确认的回归缺陷，应在同一变更中补齐或更新自动化测试。
- 对骨架、占位或过渡态流程，只测试当前已经实现且文档已承认的行为，不提前在测试中定义未来语义。
- 当前替换链路的测试边界先停留在候选解析、兼容性、摘要、已定义失败和当前命令结果。
- 当前保存链路的测试边界覆盖保存前预览、资源包聚合、现有 artifact 输入图解析、执行骨架结果、当前已定义失败，以及已落地对象类型的真实写包读回。
- 最终持久化失败细分、取消/回滚、不可替换等更深语义，等对应实现和文档契约落地后再继续补测试。

默认依赖与副作用限制：

- 不依赖网络访问
- 不依赖真实用户目录、真实工作区目录或机器全局状态
- 不依赖不可控外部工具安装状态或机器特定环境
- 不依赖脆弱的休眠等待、竞态时序或当前系统语言文案
- 需要文件系统时，优先使用受控临时目录并在测试内自行清理

命令行验证备注：

- `AssestBundleTools.Application.Tests` 和 `AssestBundleTools.Infrastructure.Tests` 当前都是零依赖控制台 runner。
  在受限终端或沙箱环境里执行 `dotnet run` / `dotnet test` 时，若默认用户目录不可写，先设置可写的 `DOTNET_CLI_HOME`，并建议同时设置 `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`。
- `AssestBundleTools.Presentation.Tests` 项目固定为 `x86 / win-x86`。
  若用 `MSBuild` 或 `dotnet test` 单独验证它，请显式使用 `Platform=x86`，否则容易因为引用链先产出 `x64` DLL 而触发 `MSB3270`。
- `AssestBundleTools.Infrastructure.Tests` 应通过 `ProjectReference` 引用 `src/AssestBundleTools.Infrastructure`，不要固定 `HintPath` 到 `src/*/bin/Debug/...`。
  固定路径在 `Platform=x86` 构建时可能引用旧 DLL，导致测试编译提示新 DTO 字段不存在；这类错误优先检查项目引用方式，而不是先怀疑生产代码。
- `AssestBundleTools.Infrastructure.Tests` 和 `Presentation.Tests` 都会带起同一组上游项目。
  因此这两类验证不要并行跑；并行时容易争用 `src/*/obj` 或 `src/*/bin`，导致 `CS2012`、`MSB3021`、`MSB3027`。
- `AssestBundleTools.Application.Tests` 也会在解析引用前构建 `src/AssestBundleTools.Application`。
  不要把它和解决方案级 `MSBuild` 同时跑；这次曾复现 `CS2012`，具体表现为无法写入 `src/AssestBundleTools.Application/obj/Debug/net8.0/AssestBundleTools.Application.dll`。
  串行重跑后测试通过时，应把前一次视为验证命令争用，而不是优先怀疑业务代码。
- `AssestBundleTools.Presentation.Tests` 在当前 WinUI / x86 测试链路下，`dotnet test` 可能出现退出码 `1` 但没有测试正文的静默失败。
  若项目已经用 `MSBuild /p:Platform=x86` 构建成功，建议用 VS `vstest.console.exe` 直接执行 `bin/x86/.../AssestBundleTools.Presentation.Tests.dll`，
  并带上 `/Platform:x86 /InIsolation`；只有当输出里出现明确断言、加载或编译错误时，再按真实测试失败处理。
- 如果 `Presentation.Tests` 为了避开 WinUI 输出锁而构建到独立 `OutDir`，运行 `vstest` 前需要检查该 `OutDir` 根部是否含有 `hostfxr.dll / hostpolicy.dll`。
  这两个 app-local host 文件会让 x86 `testhost` 把临时输出目录误判为 `.NET location`，随后报找不到 `Microsoft.NETCore.App 8.0.0 (x86)`。
  只删除临时 `OutDir` 根部的 `hostfxr.dll / hostpolicy.dll` 后，再用：
  `vstest.console.exe <OutDir>\AssestBundleTools.Presentation.Tests.dll /Platform:x86 /InIsolation /TestAdapterPath:<OutDir>`。
- 如果只是为了避开运行中进程或 DLL 文件锁，优先保留默认 `obj`，只在解决方案或项目构建时重定向 `OutDir`。
  不建议把 `BaseIntermediateOutputPath` 当成通用解法；这次验证中它曾引出 `NETSDK1004` 和 `CS0579` 之类与真实代码无关的噪声错误。

断言风格约束：

- 优先断言稳定契约和可观察结果，例如 `Result`、错误码、消息键、结构化参数、DTO 字段、摘要和页面级状态变化
- 避免把私有实现细节、偶发调用次数、临时日志句子或尚未文档化的完整本地化整句写成刚性断言
- 只有当“精确文本本身”就是正式契约时，才断言完整用户文案

后续仍待补齐：

- 工作区可恢复操作记录接入保存链路后，补来源缺失 / 指纹不匹配恢复计划、按目标资源撤销栈的当前会话精确回退、Sprite 编辑真实写包消费和保存检查点回归
- 提取主线已补单资源文本 / 图片真实写出、Mesh OBJ / 静态 FBX 导出、结构化 JSON 摘要导出、MonoBehaviour TypeTree JSON 字段树 / 摘要 JSON / YAML / 原始字段文本导出、单资源保存文件名保留、批量提取操作记录、Spine 资源组 skeleton / atlas / 主贴图 / alpha 贴图整组提取、文件写出端口和提取记录重启恢复回归；后续补命名冲突、取消、失败分类、输出文件缺失恢复诊断和批处理结果明细回归
- 替换主线已落地 Texture2D 的记录、有效预览、右键 / 检查器 `替换文件` 快捷入口一致性、独立变更记录、局部预览刷新、资产列表已修改状态即时刷新和目标资源级撤销 / 重做回归；后续继续补文件来源兼容性诊断、来源缺失 / 指纹不匹配恢复计划、批量替换结果明细、更多资源类型有效预览和真实写包消费回归
- 保存 / 重建已进入第一阶段真实导出：工作区导出窗口可选择导出到工作区输出目录或自选目录，`SaveRebuild` 后台任务会按选中资源包并发写出修改后的 AB 并生成 `_abt-bundle-export-manifest.json`；CLI 资源处理器端口、真实外部进程执行、result JSON 解析、非法输出路径拒绝、处理器结果摘要、失败回填、内置 FMOD 音频处理器协议、对象级 AssetsTools 写包、TextAsset / Texture2D / Sprite / AudioClip 写后读回、ASTC 等 Texture2D 原格式不可编码时的 RGBA32 回退，以及 AudioClip 按原容器匹配 / 容器不匹配拒绝 / 强制写入 `.resS / .resource` 回归已落地；后续按 `docs/design/workspace-export-save-pipeline.md` 继续补 workspace replacement 跨 bundle 真实样本回归、更多资源类型、最终冲突确认、缺失 artifact materialization、取消 / 回滚和保存检查点回归
- 更完整的工作区激活，以及替换/保存链路在真实写包与最终持久化语义落地后的深层回归
- 结构化错误与资源键的更细粒度自动校验
