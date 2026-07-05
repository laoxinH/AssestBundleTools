# 资源编辑窗口模式设计

## 1. 文档目的

本文档记录资源详情从“主窗口右侧检查器”扩展到“可独立资源编辑窗口”的设计约束，作为后续实现拖拽标签、独立窗口、窗口间标签组合、编辑 / 替换能力时的统一基线。

配套文档：

- [ui-final-spec.md](ui-final-spec.md)
- [workspace-management.md](workspace-management.md)
- [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md)
- [project-build-roadmap.md](project-build-roadmap.md)
- [code-architecture-guidelines.md](code-architecture-guidelines.md)

## 2. 设计结论

资源编辑窗口模式采用类似 VS Code 的文档窗口模型：

- 资产文档标签可以从主窗口右侧资源详情区拖出，形成独立资源编辑窗口。
- 主窗口右侧的整个资源详情窗格也可以拖出，形成包含多个资产文档标签的独立窗口标签页组。
- 独立资源编辑窗口之间可以通过拖拽资产文档标签组合成新的标签页组。
- 独立资源编辑窗口中的资产文档标签或整个标签组可以重新放回主窗口。
- 窗口模式下预览区升级为固定显示的“预览 / 编辑区域”，不再藏在 `详情` 分段内部。

该模式只改变资源文档的展示宿主，不改变工作区事实来源。一个工作区仍由一个主进程承载，独立资源编辑窗口共享同一活动工作区、运行时会话、索引缓存、预览缓存、依赖图、替换队列、变更队列和保存计划。

## 3. 目标

1. 让预览和编辑操作保持常驻，不被 `详情 / 依赖 / 替换 / 变更` 分段切换挤掉。
2. 支持用户把高频查看或编辑的资源放到独立窗口，减少主工作台三栏布局的横向压力。
3. 支持多资源并排或跨显示器查看，为贴图、材质、MonoBehaviour、AnimatorController、Spine 资源组等后续对比和替换确认留空间。
4. 保持工作区状态唯一，避免多窗口各自维护替换结果、预览缓存或变更队列。
5. 让标签拖拽、放回主窗口、窗口间合并和后续布局恢复具备清晰状态模型。

## 4. 非目标

当前设计不把以下能力作为第一阶段目标：

- 同一个工作区由多个进程同时编辑。
- 独立资源编辑窗口升级为新的工作区实体。
- 内层 `详情 / 依赖 / 替换 / 变更` 分段拖出成单独窗口。
- 完整多窗口布局恢复阻塞主窗口浏览状态恢复。
- 多窗口编辑冲突、真实写包、取消 / 回滚和最终持久化语义提前定死。
- 像 IDE docking 系统一样提供任意停靠区域、无限拆分窗格或复杂嵌套布局。

## 5. 术语

- **资产文档**：一个已打开资源的详情上下文，稳定身份为 `BundleId + AssetId`。
- **资产文档标签**：资产文档在某个标签组中的可视标签。
- **资产文档组**：承载多个资产文档标签的标签页组。主窗口右侧详情区和每个独立资源编辑窗口都各自拥有一个资产文档组。
- **资源编辑窗口**：独立 WinUI 窗口，用于承载一个资产文档组、固定预览 / 编辑区域和右侧信息区。
- **预览 / 编辑区域**：窗口模式下固定显示的主操作面，承载图片、Mesh、文本、结构化摘要和后续编辑器。
- **信息区**：窗口模式右侧区域，承载 `详情 / 依赖 / 替换 / 变更` 分段以及对应信息卡、依赖导航和替换状态。
- **Spine 资源组文档**：跨资产聚合详情上下文，稳定身份使用 `BundleId + GroupId` 或等价组文档键；不得为了复用当前资产文档标签而伪造 `AssetId`。当前已作为统一资源文档标签的一种，与普通 `BundleId + AssetId` 资产文档共享主窗口右侧标签组和独立资源编辑窗口宿主；标签头通过非文字强调标识区分 Spine 组。

## 6. 视觉结构

资源编辑窗口采用“左主操作 + 右信息”的稳定结构：

```text
┌──────────────────────────────┬──────────────────────┐
│ 资产文档标签组               │ 详情 / 依赖 / 替换 / 变更 │
├──────────────────────────────┤                      │
│                              │                      │
│ 预览 / 编辑区域              │ 信息区                 │
│                              │                      │
├──────────────────────────────┤                      │
│ 功能区                       │                      │
└──────────────────────────────┴──────────────────────┘
```

布局规则：

1. 左侧 `预览 / 编辑区域` 是窗口模式第一视觉重心，默认占窗口宽度的主要部分。
2. 左下 `功能区` 与预览 / 编辑区域同宽，用于放置当前资源的操作命令。
3. 右侧 `信息区` 固定承载内层分段，宽度可调整，但不应挤压到无法阅读信息卡。
4. 顶部资产文档标签组只表示“打开了哪些资源”。内层分段只表示“当前资源看哪类信息”，二者视觉层级必须明显区分。
5. 窗口过窄时，优先保持预览 / 编辑区域可操作；信息区允许进入最小宽度或后续折叠为抽屉式区域。
6. 默认资源编辑窗口尺寸下，左侧主操作区必须优先保留可见预览画布。Spine 编辑态的组成文件替换区归入右侧信息区 `替换` 分段；左侧不再用 `Auto` 组成文件面板参与主操作区测量，避免把动画预览视口压缩到不可见。

## 7. 预览 / 编辑区域

窗口模式下，原详情页顶部预览区升级为固定主区域：

1. 图片 / Sprite：保留缩放、平移、透明棋盘格、裁切框、pivot 和 9-slice 辅助线；后续编辑能力优先在此区域扩展。
   - 当前 Sprite 修改第一阶段已经落地为窗口模式内的编辑工作面和保存链路：主窗口 `修改` 会打开或激活资源编辑窗口并请求编辑态；编辑态把当前有效源图锁定为带少量安全边距的自适应视口显示，隐藏普通缩放、平移和裁切结果切换入口，允许通过鼠标移动 / 缩放裁切框并拖拽 pivot。若关联 Texture2D 已被替换且尺寸与原贴图不同，编辑态必须展示替换后贴图的完整真实像素画布，不把新贴图压缩到原贴图尺寸；裁切框和 pivot 仍按当前 Sprite 几何叠加，便于用户按新图重新调整。编辑态顶部预览工具栏只提供轻量几何快捷动作：可一键复位到预览载入时的原始 Sprite 裁切 / pivot 参数，或一键把整张源图设置为裁切区域；全尺寸动作不重置用户当前记录的 pivot 相对比例；还可以选择替换 Sprite 关联的 Texture2D 图片文件，选择后立即复用 Texture2D 的 `ReplaceFromFile` 链路应用替换、追加 Texture2D 操作记录并通过关联影响刷新 Sprite 预览，不等待“保存 Sprite 编辑”；若刷新前本次编辑会话存在未保存裁切 / pivot 临时几何、保存基线或撤销 / 重做历史，刷新后必须继续保留这些临时状态，只替换底图和当前画布尺寸。pivot 自动夹在当前裁切框内部；裁切框通过拖拽或数值输入变化时，pivot 按当前记录的相对裁切框比例自动跟随，拖动框内部移动时裁切框和 pivot 一起平移；手动拖拽或输入 pivot 坐标后，会重新记录新的相对比例并用于下一次裁切框变化。拖动裁切框边 / 角时未参与拖动的边保持原位，拖动点时对角点保持固定，拖动边时对边保持固定。裁切框 X / Y 只限制最小值为 0，宽 / 高只限制最小可操作尺寸，不因当前宽高接近源图尺寸而锁定到 0 或移动锚点。拖拽会话开始后应冻结当前源图到视口的投影基准，窗口尺寸或布局变化只在拖拽完成后应用，避免窗口模式动态测量导致固定锚点抖动；编辑态裁切框用独立边线投影固定边 / 对角点，不依赖 `Left + Width` 组合绑定来表达固定边。预览框内显示轻量坐标轴、四个角点坐标和四条边长度数值；预览框下方、功能按钮上方的独立数值面板显示裁切框和 pivot 的源图坐标，手动修改裁切框数值时 pivot 按相对比例同步更新，手动修改 pivot 数值时同步刷新相对比例，整体保持双向同步且不覆盖预览；拖拽开始时必须实际退出数值输入编辑态，释放数值输入框焦点和文本选区，拖拽期间数值面板只能展示当前拖拽结果，程序化字段刷新不得反向写回裁切几何。窗口操作区在编辑态只显示保存、退出、撤销、重做四个轻量命令；退出使用返回预览语义图标，避免表现为关闭窗口或取消任务。保存按钮仅在当前几何相对上次保存基线存在未保存变化时启用，并只把当前几何追加为 `EditDraftApplied` 操作记录。退出编辑态时若存在未保存几何，必须提示用户保存或丢弃；编辑态撤销 / 重做只作用于本次窗口编辑会话内的临时几何历史，按时间顺序前进或回退，最多保留 20 步，不提前写入工作区操作记录。保存后刷新当前有效预览，并在 `变更` 分段显示独立记录卡片；实际 AB 写入留到后续保存 / 重建 / 统一导出阶段。
2. Mesh：保留线框 / UV / 贴图叠加 / 网格显隐 / 缩放旋转等工具。
3. 文本：以固定编辑面呈现，为后续 TextAsset / Shader 编辑或替换确认预留空间。
4. Material / Font / AnimationClip / GameObject / MonoBehaviour / AnimatorController：继续显示主要摘要和轻量可视化信息；详细字段仍进入右侧信息区。
5. AudioClip：保留试听工具面，后续波形或幅度摘要也应在此区域扩展。
6. Spine 资源组：当前已在主窗口组文档中展示 JSON skeleton 轻量结构摘要、atlas region overlay、atlas page 选择器和结构化诊断，诊断覆盖 page 未匹配 Texture2D、Texture2D 预览不可用、page 尺寸与图片尺寸不一致等情况；实验性 JSON fallback 动画预览当前支持多 skin 的 region attachment、bone rotate / translate / scale、slot attachment、draw order 和 slot color / rgb / rgba / alpha 时间线，linked mesh、权重顶点、deform、slot 双颜色、event、IK / transform / path / physics 约束、Bezier 曲线等尚未完整解释的特性仍投影为结构化诊断。SpineViewer 只作为基础播放、动画 / skin 切换和导出边界参考，本项目不复刻其完整查看器、调试渲染、多轨道混合、动态壁纸、PSD 或编辑器级能力。当前已完成 V38 / V42 JSON 与 binary skeleton adapter、runtime playback session、D3D11 渲染后端和固定 FPS 帧序列采样基础；Presentation 仍只消费 Application DTO / draw command / Texture2D 预览载荷，具体 runtime 对象必须封装在 Infrastructure adapter 内。播放体验层当前提供动画选择、真实 skin 切换、播放速度、进度拖动、播放 / 暂停、上一帧 / 下一帧、重播和 setup pose；后续优先把这些基础能力稳定化并产品化导出，不主动扩展更多官方 runtime 版本或复杂 runtime 行为。当前 Application 已提供 `LoadSpineAssetGroupsQuery / LoadSpineAssetGroupDetailsQuery` 的轻量组读模型，主窗口资产窗格已接入 `资源 / Spine 组` 切换用于浏览组摘要。选择 Spine 组会在主窗口右侧复用现有资源详情窗格打开组文档，身份为 `BundleId + GroupId`，检查器展示组摘要、组成资产跳转、skeleton 摘要、诊断摘要、atlas page / region、page 选择器、当前 page 的 overlay、runtime adapter 状态和可用时的实验性动画预览；Spine 组文档可与普通资产文档混在同一个资源文档标签组中，并可跟随统一资源编辑窗口进行会话内拖出、组合和放回。

Spine 修改第一阶段定位为“资源组替换装配台”，不是骨骼 / 动画编辑器。用户从 Spine 组行或组文档点击 `修改` 时，应打开或激活资源编辑窗口并进入 Spine 编辑态；左侧继续保留动画预览画布和基础播放工具，右侧信息区自动选中 `替换` 分段，并在该分段内展示组成文件替换装配台，按 `Skeleton`、`Atlas`、`Texture pages` 三组展示当前组成资产、草稿来源、匹配状态和诊断。组成文件替换装配台跟随信息区滚动，不再位于预览下方，也不得通过独立高度测量挤压动画预览画布；默认窗口高度下应露出标题和主要条目。Spine 编辑态下该分段进入专用组成文件替换模式，隐藏普通资源替换中的当前目标资源、替换来源、能力兼容性和未应用状态占位；退出编辑态后恢复普通替换内容。`Skeleton` 支持选择 `.skel / .json / .bytes` 等 skeleton 文件，`Atlas` 支持选择 atlas 文本文件，`Texture pages` 支持逐 page 选择图片文件，并提供按文件夹批量匹配 skeleton、atlas 和 page 图片的入口。选择文件后只进入当前 Spine 编辑草稿并立即以“原始组 + 当前草稿替换”重新投影预览；在点击保存前，不生成工作区操作记录，也不改变组成资产的已应用替换状态。若某个组成资产已经存在已应用文件替换，替换页行项在没有新的草稿来源时显示该已应用来源路径，路径过长时可截断显示但必须保留完整路径提示。编辑态功能区只显示 `保存 / 退出 / 撤销 / 重做`：保存仅在草稿相对上次保存基线存在修改时启用，保存时把本次草稿拆成目标为真实组成资产的多条 `ReplaceFromFile` 操作记录；退出若存在未保存草稿，复用保存 / 丢弃 / 取消确认；撤销和重做只重放当前编辑会话内最多 20 步的临时组成文件选择历史。

Spine 编辑保存时不得把整个 Spine 组伪装成一个可写 Unity 资产。`Texture2D` page、atlas `TextAsset`、skeleton `TextAsset` 仍分别形成独立替换记录，并共享同一个 `batchId / relationId`，使 Spine 组 `变更` 分段能从组成资产当前读模型投影出同批次 / 同来源的修改卡片，同时组成资产自己的详情页也能看到真实目标记录并支持撤销 / 重做 / 删除。组级投影卡片上的撤销 / 重做 / 删除必须携带被投影记录的真实 `operationId` 与组成资产 `target`，动作结果和删除通知应刷新 Spine 组和组成资产两侧读模型。组级投影只能展示或聚合真实组成资产记录，不应创造一个目标为 Spine 组本身的虚假替换记录。保存后的有效预览通过工作区操作记录物化出的组成资产有效载荷重新生成：atlas parser、runtime adapter、D3D / Win2D 播放面仍只消费 Application 层聚合 DTO、预览 sidecar 和图片载荷，不在 Presentation 中直接读取资源包、解析外部文件或持有 runtime 对象。若草稿组合存在缺失 page、atlas 与图片尺寸不一致、skeleton 与 atlas 不匹配或 runtime adapter 不支持等问题，左侧预览区和右侧 `替换` 分段都应展示结构化诊断，并允许用户继续替换缺失组成文件；富预览必须稳定回退为结构化摘要，不能显示空白。atlas `TextAsset` 的有效预览允许保留完整文本供导出 / 结构化解析使用，但 UI 展示文本必须保持有界；当 atlas 文本明显是 JSON、超过结构化解析预算或无法形成有效 page / region 时，Application 应快速返回 `AtlasParseFailed` 诊断并跳过 Texture2D page 载荷、atlas overlay 和 runtime adapter 尝试，避免坏输入让前台预览长时间停留在加载态。

Spine 动画预览的长期播放路线不再继续扩大整段 `sampled frames` 和全量 D3D batch 预热，当前实时预览已接入 Application 定义、Infrastructure 实现的 runtime playback session；Presentation 按真实 elapsed delta 计算播放时间，并只接收当前帧 DTO 进行渲染。JSON fallback 也通过 sampled fallback playback session 接入同一 Presentation 路径。详细契约见 [spine-runtime-playback-session.md](spine-runtime-playback-session.md)。

预览 / 编辑区域只消费 Application 预览 DTO、缓存载荷和展示层状态，不直接读取 AB、遍历文件或调用 Infrastructure 解析实现。

修改阶段的预览 / 编辑区域必须显示当前有效内容：当普通资产应用工作区替换、文件替换或后续编辑草稿后，图片、Sprite、Mesh 贴图化视图、文本、音频和结构化摘要预览应刷新为替换后的结果；当 Spine 组的 skeleton、atlas 或 texture page 组成资产存在已应用替换或草稿时，组级预览也应使用这些有效载荷重新生成。该行为只影响预览和编辑工作面，右侧 `详情` 中的目标资产原始身份、Bundle、PathId、Unity 类型、导入索引和原始依赖仍保持不变；来源资产 / 文件的信息进入 `替换` 和 `变更` 分段。

## 8. 功能区

功能区分成两层概念：

1. **视图工具**：刷新预览、重置视图、打开所在位置、复制定位信息、缩放、背景切换、网格显隐等。
2. **资源动作**：提取、选择工作区来源、选择文件来源、应用替换、撤销、后续编辑草稿应用或丢弃。

规则：

- 高频视图工具优先用图标按钮，并提供 tooltip。
- 高价值资源动作使用文本或图标 + 文本按钮。
- 危险动作、不可用动作和会修改工作区状态的动作必须有明确禁用态、确认态或诊断入口。
- 功能区不应复制右侧信息区中的完整字段，只承载当前资源的操作入口和简短状态。
- 窗口模式左下功能区不再常驻展示 `移动到主窗口`、`全部移动到主窗口`、`在新窗口打开` 这类宿主搬运按钮；标签 / 标签组搬运继续通过拖拽、标签上下文命令和关闭策略承载，避免编辑动作区被窗口管理命令挤占。
- 功能区采用与主窗口检查器一致的轻量标签式按钮样式，不再使用独立卡片边框包裹整组命令；普通可文件替换资产只保留高频 `提取 / 替换文件` 入口，已有窗口化编辑能力的资源类型才显示 `编辑` 入口；替换应用、操作记录撤销 / 重做和删除继续在右侧 `替换 / 变更` 分段承载，避免同一动作在窗口底部重复出现。
- Sprite 编辑态功能区只显示 `保存 / 退出 / 撤销 / 重做`：保存只在有未保存几何时启用，退出在有未保存几何时弹出保存 / 丢弃 / 取消确认，撤销和重做只重放当前编辑会话的临时几何历史，历史上限为 20 步。用户直接关闭资源编辑窗口或关闭处于 Sprite 编辑态的资源标签时，也必须先复用同一未保存确认；选择保存或丢弃后先退出编辑态回到预览状态，再继续窗口关闭、标签关闭或放回主窗口流程，选择取消则保持编辑态并取消本次关闭。
- Spine 编辑态功能区同样只显示 `保存 / 退出 / 撤销 / 重做`：保存把当前组成文件草稿拆分成真实组成资产的替换操作记录，退出在有未保存草稿时确认保存或丢弃，撤销和重做只作用于当前编辑会话的文件选择草稿历史，不提前写入工作区操作记录。组成文件选择、批量匹配、匹配诊断和当前草稿摘要应放在右侧信息区 `替换` 分段，不塞进底部功能区。
- `TextAsset / Shader` 的文件替换与正式编辑必须保持入口分离：`替换文件` 只负责选择外部文件并追加 `ReplaceFromFile` 操作；`编辑` 命令负责打开或激活资源编辑窗口并进入窗口化文本编辑模式。当前第一阶段已经接入独立 `编辑` 命令和 draft-backed 文本工作面：编辑态隐藏普通预览面板，大文本和 Shader 显示只读行号视图、有限搜索和本地化状态提示，小型 TextAsset 在 `AssetTextEditPolicy.InlineEditableTextLength` 预算内显示可编辑正文。文本读取、搜索、草稿创建、恢复打开已有草稿、草稿更新、TextAsset 保存应用和退出 / 关闭丢弃都走 Application gateway 与后台 runner。TextAsset 保存先追加 `EditDraftUpdated` 再追加 `EditDraftApplied`，并进入当前有效读模型和导出写包计划；创建、更新、应用和恢复打开草稿时必须保留 descriptor 的 source fingerprint，并把它继续写回 `EditDraftReference`，保证窗口快照恢复可继续做来源校验；已应用 TextAsset 草稿再次进入编辑态时作为 baseline 打开，退出或丢弃本次未保存 UI 改动不得删除仍被导出写包引用的草稿 payload；Shader 当前只读查看，待写回适配器完成后再开放保存。退出、关闭标签或关闭窗口时，若文本编辑态存在未保存正文，必须提示保存 / 丢弃 / 取消。后续 JSON 美化 / 压缩、格式校验、行跳转、撤销 / 重做和大文本虚拟化编辑继续复用该窗口模式，不得回退到 `替换文件` 入口。
- 功能区命令仍走现有 Application 用例和 Presentation 协调器，不绕过替换 / 保存 / 预览管线。

## 9. 信息区

信息区继续采用 `详情 / 依赖 / 替换 / 变更` 分段：

1. `详情`：基础字段、预览补充信息、预览诊断和类型专属信息卡。
2. `依赖`：资产依赖、反向引用、依赖闭包、Bundle 外部依赖，以及已解析资源跳转。
3. `替换`：工作区来源、文件来源、候选校验、兼容性摘要和当前替换状态。
4. `变更`：当前资源变更摘要、待保存状态和后续真实写包前置诊断。

信息区不是资产文档标签的同级标签。用户拖拽或独立成窗口的是资产文档标签或资产文档组，不是 `详情 / 依赖 / 替换 / 变更` 这些内部分段。

## 10. 拖拽与窗口组合

### 10.1 可拖拽对象

可拖拽对象只有两类：

1. 资产文档标签。
2. 资产文档组。

不可拖拽对象：

- 内层 `详情 / 依赖 / 替换 / 变更` 分段。
- 单个信息卡。
- 单个依赖行。
- 单个预览工具栏按钮。

### 10.2 从主窗口拖出单个标签

用户从主窗口右侧资产文档组拖出一个资产文档标签时：

1. 创建新的资源编辑窗口。
2. 将该资产文档从主窗口资产文档组移到新窗口组。
3. 迁移页面级状态：内层分段位置、预览缩放 / 平移 / Mesh 工具状态、替换候选选择、编辑草稿标识。
4. 主窗口切换到剩余标签中的合理焦点；若没有剩余标签，显示资源详情空态。

### 10.3 从主窗口拖出整个详情窗格

用户拖出整个资源详情窗格时：

1. 创建新的资源编辑窗口。
2. 将当前资源包标签页下的资产文档组整体移动到新窗口。
3. 保留资产文档顺序、当前激活资产、内层分段位置和各资产预览状态。
4. 主窗口右侧显示空组或占位态，并保留资产列表选择，不强制关闭当前资源包标签。

### 10.4 窗口间合并

用户把一个独立窗口中的资产文档标签拖到另一个独立窗口时：

1. 目标窗口按拖入位置插入标签。
2. 源窗口移除该标签。
3. 源窗口若无剩余标签，自动关闭或显示空态；第一阶段可优先自动关闭。
4. 标签身份和页面级状态跟随标签移动。

### 10.5 放回主窗口

用户把资产文档标签或整个组放回主窗口时：

1. 根据资产文档所属 `BundleId` 打开或激活对应资源包标签页。
2. 将资产文档插入该资源包标签页下的主窗口资产文档组。
3. 恢复激活资产和内层分段位置。
4. 若同一资产在主窗口已有标签，优先激活已有标签并合并可恢复状态，不创建重复标签。

独立资源编辑窗口的关闭入口可以复用同一放回链路：当前“设置与诊断”提供资源编辑窗口关闭方式，默认关闭标签或窗口时先回到主窗口，用户也可以改为直接关闭。窗口关闭前必须在 `Closing` 阶段处理该选择；若当前活动 Sprite 文档处于编辑态，还必须先复用退出编辑的未保存确认，保存或丢弃后退出编辑态并回到预览状态，再继续按关闭偏好放回主窗口或直接关闭；用户取消确认时取消窗口关闭。用户关闭独立窗口内的单个资源标签时也必须执行同一前置确认：若被关闭标签处于 Sprite 编辑态，先激活该标签并解析未保存状态，确认继续后再按关闭偏好放回主窗口或直接关闭标签，取消则保留标签和编辑态。工作区切换、进程退出、拖拽后空组清理等内部生命周期关闭仍应直接关闭，不把失效窗口重新放回主窗口。

## 11. 状态模型

### 11.1 工作区级事实

以下状态只能有一份，归工作区运行时会话所有：

- 已导入资源包集合。
- 资产索引和基础详情。
- 资产依赖图、反向引用和依赖闭包。
- 预览缓存描述与载荷。
- 替换队列、变更队列和保存计划。
- 真实导入 / 预览 / 替换 / 保存用例结果。

资源编辑窗口不得复制或私有化这些状态。

多窗口同时显示同一资产或同一 Spine 组时，窗口之间共享同一套有效预览状态。任一窗口应用或撤销替换后，其他窗口应通过工作区操作变更通知重新投影预览；页面级缩放、平移、动画播放位置等观察状态可保留，但内容载荷必须跟随工作区有效状态更新。

关联资源也遵循同一通知链路。资源 A 被修改时，操作通知先通过 Application 层关联影响查询扩展到依赖 A 的可展示资源，再由主窗口和独立资源编辑窗口按受影响资产局部刷新。第一版已用于 Sprite 与 Texture2D：无论在 Sprite 编辑保存时应用关联贴图替换，还是直接在 Texture2D 资产侧修改贴图，Sprite 文档都会刷新变更记录和预览底图；从投影到 Sprite 侧的关联替换记录执行撤销 / 重做时，命令仍解析并修改真实 Texture2D 目标，通知也必须同时覆盖 Texture2D 与依赖它的 Sprite。后续 Spine skeleton / atlas / texture page、Mesh 贴图化视图和 Material 纹理预览继续复用这一路径，不在窗口模式中新增私有同步状态。

### 11.2 页面级窗口状态

以下状态属于页面级浏览状态，可随窗口和标签移动：

- 资源编辑窗口位置、大小、显示器和最小化 / 最大化状态。
- 资产文档组顺序。
- 当前激活资产文档。
- 每个资产文档的内层分段位置。
- 图片缩放 / 平移、Sprite 显示模式、Mesh 显示模式、Spine 动画 / 皮肤 / 播放速度 / 视口缩放平移等预览浏览状态。
- 替换候选选择。
- 编辑草稿标识和草稿来源。

这些状态可以持久化，但第一阶段不要求独立资源编辑窗口随应用重启恢复；主窗口资源包标签和资产文档组恢复仍是优先级更高的稳定链路。

Spine 资源组文档属于页面级浏览状态。主窗口资源包标签会用 `openSpineGroupTabs[]` 保存 `GroupId / DisplayName / GroupKey` 这类轻量身份信息，以及动画名、皮肤名、播放速度、播放时间、播放/暂停、setup pose、预览背景、视口缩放/平移和 atlas page 选择这类轻量观察状态；独立资源编辑窗口通过 `openAssetEditorWindows[].openDocumentTabs[]` 保存普通资产文档和 Spine 组文档的统一轻量标签序列，并用 `ActiveDocumentKind / ActiveSpineGroupId` 恢复活动 Spine 组。窗口快照仍不保存 atlas 解析结果、Texture2D 预览载荷、region overlay 数据或诊断明细；真实组详情在恢复后激活时继续通过 `LoadSpineAssetGroupDetailsQuery` 和现有预览管线重新读取。`openAssetTabs[] / ActiveAssetId` 保留为普通资产标签和旧快照兼容字段。

### 11.3 编辑草稿状态

后续编辑能力落地时，编辑草稿必须绑定：

- 工作区身份。
- `BundleId + AssetId`。
- 资源类型和预期替换 / 编辑模式。
- 生成草稿的资源版本或来源指纹。

草稿不得只绑定某个 WinUI 控件实例。窗口关闭、标签移动或主窗口切换时，草稿必须能被确认、丢弃或恢复。

编辑草稿后续应用到工作区时，应通过 [工作区可恢复操作记录](workspace-recoverable-operation-records.md) 进入 `operations[]`，而不是只修改窗口或标签页状态。窗口快照仍只保存轻量 `EditDraftReference`，真实草稿内容和草稿应用 / 丢弃记录留在 `staging` 变更暂存层。

## 12. 架构边界

窗口模式实现必须遵守现有分层：

- Presentation 负责窗口、标签组、拖拽、焦点、展示状态和用户命令触发。
- Application 负责预览查询、替换候选、替换应用、撤销、保存预览和后续编辑用例。
- Infrastructure 负责真实 AB 读取、缓存、解密器和外部工具。

禁止事项：

- 独立资源编辑窗口直接读取 AB 文件。
- 窗口拖拽逻辑直接解析预览缓存或依赖图。
- 为了窗口模式引入 Presentation 到 Infrastructure 的新直接依赖。
- 把多窗口浏览状态写成工作区事实数据，导致保存 / 替换用例依赖 UI 布局。

推荐协作对象命名：

- `AssetEditorWindowCoordinator`：资源编辑窗口创建、关闭、激活与主窗口放回。
- `AssetDocumentGroupViewModel`：可复用资产文档组状态，供主窗口和独立窗口共用。
- `AssetDocumentMoveCoordinator`：标签 / 组在宿主之间移动、合并、去重和焦点落点。
- `AssetEditorWindowStateStore`：后续保存独立窗口位置和标签组状态。
- `AssetEditDraftCoordinator`：后续管理编辑草稿确认、恢复和丢弃。

Spine 动画的长期高性能渲染路线采用 `D3D11 + SwapChainPanel + vertex batch renderer` 组合，而不是在 Win2D 逐三角裁剪路径上继续堆叠近似策略：

- `vertex batch renderer` 是核心策略：把 `SpineAnimationFrameDto.DrawCommands` 转换为按 texture / blend / tint 可提交的顶点、UV、颜色和索引批次，保持 mesh 几何质量。
- `D3D11` 是首选渲染后端：负责 GPU 纹理采样、alpha blend、顶点 / 索引 buffer 提交和渲染目标管理。
- `SwapChainPanel` 是 WinUI 承载面：只负责把 D3D 输出嵌入资源详情预览区，不承载 runtime 解析、采样或资源组识别语义。
- 该渲染器属于 Presentation 控件实现细节，只能消费 Application DTO、Texture2D 预览载荷和展示层状态；不得让 Application 引用 D3D / WinUI 类型，也不得把 Infrastructure 官方 Spine runtime 对象泄露到 Presentation。
- 现有 Win2D 渲染器保留为兼容回退路径，用于 D3D 初始化失败、设备丢失恢复期间或尚未迁移的测试环境；回退路径不得再把复杂 mesh 降级为块状矩形近似。
- 所有用户可见的渲染状态、错误提示和降级提示仍走现有本地化资源；D3D 设备错误只作为诊断明细，不直接暴露原始异常文本作为主提示。

Spine 动画的长期高性能播放路线采用 runtime playback session，而不是继续把完整动画预采样并常驻缓存：

- Application 定义 `ISpineAnimationRuntimePlaybackService` 一类窄端口，用于创建、推进、seek、切换 animation / skin 和释放播放 session。
- Infrastructure 持有官方 Spine runtime 对象，按真实 delta 或绝对 seek 时间生成当前帧的 runtime-neutral draw command DTO。
- Presentation 的 Spine 播放循环已由 `CompositionTarget.Rendering` 显示刷新驱动；`char_2026_yu_nian#12.ab` 等真实复杂样本的播放、速度切换、animation 切换和进度拖动日志已确认 `60 FPS` 交互观感可接受。若目标提升到 `120 FPS`，仍需结合实际 rendering tick、present 节奏和绑定刷新压力继续验证。
- D3D 后端只缓存长期 GPU 资源、动态 buffer 和小窗口当前帧批次；不得默认预热整段动画所有 sampled frames。
- 固定 fps 采样仍可用于导出、序列帧和 runtime session 不可用时的兼容回退。
- 详细边界、诊断和测试要求见 [spine-runtime-playback-session.md](spine-runtime-playback-session.md)。

## 13. 与现有主窗口的关系

主窗口仍是活动工作区的主要导航和组织入口：

- 左侧资源包列表和中间资产列表不复制到资源编辑窗口。
- 独立资源编辑窗口中的资源打开操作应复用现有资产文档打开链路。
- 从依赖行打开资源时，默认在当前窗口组中打开；用户可后续通过设置或命令选择在主窗口或新资源编辑窗口打开。
- 主窗口关闭活动工作区时，必须先处理所有独立资源编辑窗口的未应用草稿和未完成动作。
- 删除工作区、切换活动工作区或进程退出时，资源编辑窗口必须跟随工作区会话失效，不保留悬空编辑窗口。

## 14. 恢复与持久化

推荐分层恢复策略：

1. 第一优先级：恢复主窗口资源包标签、资产列表筛选、资产文档组和活动资产文档。
2. 第二优先级：恢复资产文档内部预览浏览状态和内层分段位置。
3. 第三优先级：恢复独立资源编辑窗口的位置、大小和标签组。
4. 第四优先级：恢复未应用编辑草稿。

第一阶段只要求不破坏既有主窗口恢复。独立资源编辑窗口恢复可以等窗口创建、标签移动和草稿模型稳定后再落地。

## 15. 实施阶段

### 第一阶段：命令式独立窗口

完成标准：

- 资产文档标签菜单提供 `在新窗口中打开`。
- 新资源编辑窗口可承载单个资产文档。
- 独立窗口内可通过依赖跳转或资源打开链路继续打开多个资产文档，形成自己的标签组。
- 提供 `移动到主窗口` 命令。
- 不实现鼠标拖拽出窗，先验证状态迁移和窗口生命周期。

### 第二阶段：标签拖拽

完成标准：

- 主窗口和独立窗口之间支持拖拽单个资产文档标签。
- 独立窗口之间支持拖拽单个资产文档标签并合并标签组。
- 源窗口空组时有明确关闭或空态规则。
- 标签移动不会重复加载已完成预览，也不会丢失替换候选状态。

当前实现：

- 主窗口资产文档标签、独立资源编辑窗口资产文档标签均接入 `TabView` 单标签拖拽。
- 标签拖到另一组标签栏、主窗口资源详情宿主区域或独立资源编辑窗口任意可接收区域时，移动同一个资产文档标签实例；已加载或加载中的预览、内层分段、替换候选和编辑态随标签移动。
- 单标签迁出或窗口化时，源宿主若需要激活相邻 fallback 标签，只能异步刷新源宿主剩余标签；迁出动作本身不得等待 fallback 标签的详情查询、富预览加载或后台预热完成，也不得在同一个迁出调用栈内立即启动 fallback 详情 / 预览加载。否则被迁出的标签会先离开原标签组，却要等待无关预览任务完成后新窗口才显示，破坏“移动同一个标签实例”的窗口化体验。
- 主窗口标签拖到标签栏外时，创建新的独立资源编辑窗口；独立窗口有多个标签时，标签拖到标签栏外才创建新的独立资源编辑窗口。若独立窗口只有一个标签，拖到窗口外视为取消拖出，保留原窗口并回到该标签。若目标资源已在其他主窗口标签或独立资源编辑窗口中打开，则优先激活已有标签并置前所在窗口，不再创建新的独立窗口。
- 独立窗口之间拖拽会把标签合并到目标窗口标签组；目标窗口的整窗区域都参与单标签拖入检测，源窗口移动后若为空，按第一阶段规则自动关闭。
- 同一标签组内拖拽仍只重排当前标签组顺序，不触发跨宿主移动。

### 第三阶段：详情窗格拖出 / 放回

完成标准：

- 主窗口右侧整个资产文档组可拖出成独立资源编辑窗口。
- 独立窗口整个标签组可放回主窗口。
- 组级移动保留顺序、激活标签和各资产文档页内状态。

当前实现：

- 主窗口右侧资源详情标题栏提供组级命令和组级拖拽手柄，可将当前活动资源包下的整个资产文档组打开到新的资源编辑窗口。
- 独立资源编辑窗口提供组级命令和组级拖拽手柄，可将当前窗口标签组整体移动回主窗口、移动到另一个资源编辑窗口，或重新打开到新的资源编辑窗口。
- 独立资源编辑窗口左下功能区已移除组级 `移动到主窗口 / 全部移动到主窗口 / 在新窗口打开` 按钮；用户关闭窗口或标签时会根据设置选择回到主窗口或直接关闭，组级拖拽和标签上下文命令继续保留。
- 组级移动复用 `AssetDocumentGroupTransfer`，迁移同一组 `AssetDocumentTabViewModel` 实例，保留标签顺序、激活标签、已加载 / 加载中的预览状态和替换候选等页面级状态。
- 放回主窗口时按每个资产文档的 `BundleId` 打开或复用目标资源包标签；若目标资产已经存在于主窗口资产文档组，则激活已有标签并取消被合并标签的预览请求，避免重复标签。
- 当前阶段已落地轻量“拖动整个详情窗格 / 标签组”手势：拖到另一个资源详情宿主时整组合并，拖到窗口外时创建新的独立资源编辑窗口；在原宿主内释放只结束拖拽，不移动标签组。有效目标宿主会显示轻量落点高亮预览；任意停靠布局和独立窗口位置 / 标签组恢复仍留到后续阶段。

### 第四阶段：编辑草稿与布局恢复

完成标准：

- 编辑草稿与窗口生命周期解耦。
- 关闭窗口、移动标签、关闭工作区时有明确草稿确认 / 丢弃 / 恢复流程。
- 独立资源编辑窗口位置、大小和标签组可恢复。
- 多窗口同时展示同一资产时，替换 / 编辑状态能通过共享协调器同步。

当前实现：

- 独立资源编辑窗口已具备会话内轻量位置 / 大小记忆：新窗口按上一个可用窗口 bounds 级联打开，关闭当前工作区时清空该页面级状态。
- `AssetEditorWindowStateStore` 已跟踪当前会话内的独立窗口状态快照，包含窗口 bounds、资产文档标签顺序、活动资产身份、检查器分段和图片 / Mesh / Spine 预览浏览选项。
- 工作区浏览状态 DTO 与工作区元数据已具备 `openAssetEditorWindows[]` 持久化形状；窗口创建、布局变化、标签组变化和关闭会随浏览状态回写该快照。
- 工作区重新打开时当前会读取 `openAssetEditorWindows[]` 并弹出一次可控恢复提示；如果快照里包含轻量草稿引用，提示会显示带草稿标记的标签数量，并提供“同时恢复草稿标记”的轻量选项；如果待恢复标签已在主窗口或现有独立窗口中打开，提示和工具栏入口会显示冲突标签数量，并提示关闭对应标签后可继续恢复。用户选择稍后时，主窗口工具条仍保留手动恢复入口。只有用户主动确认时，才会按保存的轻量标签组和窗口布局重建独立资源编辑窗口。
- 恢复时会先按当前工作区资源包可用性和已打开资产身份拆分快照；可恢复的窗口会打开，不可恢复或与当前已打开资产冲突的窗口 / 标签会继续保留在待恢复入口中，用户也可在恢复提示中显式忽略这些已保存窗口。
- 恢复窗口布局时会先校验最小尺寸，并在应用到 WinUI 窗口前按当前显示器工作区做安全归位，避免窗口恢复到屏幕外或过大的不可操作位置。
- 当前只落盘轻量窗口 / 标签恢复元数据，不保存真实详情内容、预览载荷、替换候选、编辑草稿内容或运行时资源数据；资产文档标签已具备轻量 `EditDraftReference` 挂点，只允许保存草稿 id、模式和来源指纹等恢复引用；Spine 组标签只保存动画 / 皮肤 / 播放 / 背景 / 视口 / atlas page 选择等观察状态。
- Application 层已新增 `Editing` 草稿恢复候选契约：`IAssetEditDraftStore` 只读取草稿元数据，`GetAssetEditDraftRestorePlanHandler` 按工作区、草稿 id、模式和来源指纹把轻量引用解析为 `Restorable / Missing / SourceMismatch / Expired / InvalidReference`。资源编辑窗口文本入口已能把恢复成功并挂回标签的 `EditDraftReference` 交给 `OpenTextAssetEditDraft`，由 Application 校验 descriptor、source fingerprint、过期时间和文本 payload 后重新进入文本编辑态；该打开动作不追加新的 `EditDraftCreated`，也不覆盖草稿 payload，并会把 descriptor 的 source fingerprint / source description 重新投影到标签引用。
- Presentation 恢复弹窗当前已在显示前调用 `AssetEditDraftRestorePlanGateway` 做轻量预检，并在弹窗中显示可恢复、缺失、来源不匹配、已过期和引用无效的草稿引用数量；该预检会刷新提示文案、重复弹窗判定和后续可保留的草稿标记集合，不自动恢复草稿内容，也不改变用户是否主动恢复窗口的控制权。组合根已接入 `WorkspaceChangeSetAssetEditDraftStore`，从 `staging/changes/<workspaceKey>.json` 的 `editDrafts[]` 描述列表读取草稿元数据。
- 当前仍不自动弹出独立资源编辑窗口，也不恢复最大化 / 最小化或显示器；当预检计划可用时，草稿选项只决定本次成功打开的标签是否带回预检为可恢复的 `EditDraftReference` 标记，缺失、来源不匹配、已过期和引用无效的草稿标记不会重新挂回新标签。文本类真实草稿内容会在用户进入编辑态时按需打开；其它类型的真实草稿确认 / 丢弃 / 恢复队列仍留给后续编辑器模型。
- 当前打开资源详情、依赖跳转、Spine 组文档和新窗口打开入口会先检查主窗口与所有独立资源编辑窗口：如果目标文档已打开，则直接激活并切换到已有标签，再把所在窗口置前；只有未命中现有标签时才继续创建新的文档标签或独立窗口。
- Spine 资源组当前作为统一资源文档标签打开到普通独立资源编辑窗口；会话内混合标签、拖拽合并、放回主窗口、冲突激活和 `openAssetEditorWindows[]` 快照恢复均复用统一文档标签链路。Spine 组恢复只持久化 `BundleId + GroupId + DisplayName + GroupKey` 与轻量预览观察状态，不持久化解析详情或预览载荷。组识别以 `.skel / .skel.bytes / .json + .atlas` 后缀组合为快路径；当 Unity AB 去掉 TextAsset 文件后缀时，会通过 `SkeletonData` / `Atlas` MonoBehaviour 依赖树把无扩展 TextAsset 回填为 skeleton / atlas，并优先按 `SkeletonData -> Atlas -> Material -> Texture2D` 依赖链拆分多骨架、多 atlas、多贴图组。同目录同 basename 的无扩展 TextAsset 与 `.atlas` 配对仍作为缺少依赖详情时的保守兼容 fallback。
- 主窗口中间资产窗格的普通资源行和 Spine 组行已接入右键菜单入口；`在新窗口中打开` 会先把目标行同步为当前资源文档，再复用统一的新窗口打开逻辑，因此不会生成绕过资源详情标签组的临时预览宿主。

### 第五阶段：Spine GPU 批量渲染

完成标准：

- 新增 Presentation 内部的 Spine 渲染后端抽象，使现有播放控件可以在 Win2D 回退后端和 D3D11 后端之间切换。
- 当前已完成第一步：`InspectorSpineAnimationPreviewSurfaceView` 已改为消费 `ISpineAnimationPreviewRenderBackend`，现有 Win2D 绘制、贴图加载、帧缓存和 invalidation 已下沉到 `Win2DSpineAnimationPreviewRenderBackend`，播放控件本身不再直接持有 Win2D 帧绘制状态。
- D3D11 后端通过 `SwapChainPanel` 承载，并实现按 texture / blend 分组的 vertex batch renderer，批量提交 Spine region / mesh 三角形。
- 顶点批次只由 Application 的 runtime-neutral DTO 投影而来，保持 `world position / UV / tint / alpha / draw order` 语义；不在 Presentation 中持有官方 Spine runtime 对象，也不读取 AB。
- 当前已完成第二步：Presentation 内部已新增 DTO 到顶点批次的纯投影器，覆盖 region / mesh 顶点、UV、tint / alpha、无效三角过滤和按连续 texture page 保持 draw order 的批次切分；该投影器暂不接触 D3D 类型。
- 当前已完成第三步并默认启用 D3D 后端：播放控件优先使用 `SwapChainPanel` 承载的 D3D11 后端，创建 device / swap chain / render target，并把顶点批次投影到 clip space 后通过 D3D11 顶点 / 索引 buffer 提交；需要强制回退时可设置 `ABT_SPINE_D3D11=win2d`。
- 当前已完成第四步的第一段：Texture2D 预览载荷会在 Presentation 内解码为 BGRA8，并在常见 companion 命名下合成独立 alpha page 后统一转为 premultiplied alpha，再上传为 D3D texture / shader resource，并按 page name 缓存；顶点批次会通过 shader 采样真实 page texture 并应用 DTO 中的 tint / alpha。
- 当前已完成第五步的第二段并接入实时播放主线：D3D11 后端已把每个 batch 每帧临时创建 vertex / index buffer 的提交方式替换为可复用动态 buffer，按容量增长后使用 `WriteDiscard` 上传当前 batch；播放控件会在 Unloaded 后短暂保留 D3D / Win2D 后端资源，若没有快速重新挂载才释放，D3D resize 通过短防抖合并，Texture2D 解码和上传带 generation 保护，DXGI device removed / reset / hung / internal error 会先尝试释放并重建一次，仍失败时才自动切回 Win2D 后端并重放当前帧与贴图状态。D3D 后端按 `SpineAnimationFrameDto` 引用和 texture page version 做小窗口 vertex batch 缓存，clip-space 投影改为渲染上传阶段使用池化数组即时完成；当前帧 batch 未命中时直接投影并绘制，不再为了等待整段 sampled frame prewarm 而延迟显示。V38 / V42 官方 runtime adapter 仍保留复杂 mesh sampled DTO 作为 JSON fallback、兼容回退和未来固定 fps 导出基础；实时预览则优先通过 playback session 按 animation / skin / setup pose / 绝对 seek 时间生成当前帧。动画和 skin 切换会先同步播放时间与当前帧再通知控件刷新，避免切换瞬间把新动画名称与旧动画时间 / 旧帧混用。暂停或当前帧未变化时减少重复 invalidation；batch renderer 会在后台预编译 shader bytecode，并在 device 创建后预热 shader / input layout / sampler / blend 等管线资源，避免首个真实 draw 才加载 D3DCompiler。D3D 诊断可通过 `ABT_SPINE_D3D11_DIAGNOSTICS=1/true/d3d11` 额外输出 device / resize、batch renderer prewarm、texture decode、GPU upload、runtime frame generation、frame batch / vertex / index / elapsed、upload / projection / clip / draw / present 分段耗时、播放交互事件和 fallback / device lost 异常类型，便于真实样本基线记录；默认路径已切为 D3D11，Win2D 保留为强制回退和自动 fallback 后端。
- 独立资源编辑窗口的右侧详情面板会复用 `InspectorPanelView`，但详情预览区在该布局中隐藏；隐藏详情预览必须向内层 Spine surface 传递 inactive 状态，避免不可见副本监听同一 ViewModel、创建 runtime session、启动 rendering loop、重复 texture decode / batch prewarm，并让可见预览看起来跳帧或速度异常。
- 保留现有 Win2D 帧缓存作为回退；D3D 可用时大量三角动画不再走逐三角 `CreateLayer` 裁剪路径。
- 已给真实高复杂样本补受控性能验证记录，覆盖首帧纹理上传、连续播放、速度切换、animation 切换和进度拖动；窗口缩放、skin 切换、设备丢失回退和更多设备工作集基线仍继续按 [`spine-d3d11-performance-baseline.md`](spine-d3d11-performance-baseline.md) 补充。

建议开发顺序：

1. 已完成：抽出 `SpineAnimationPreviewSurface` 的渲染后端接口和 Win2D 适配器，现有播放功能继续走兼容回退后端。
2. 已完成：实现 DTO 到 batch 的纯 Presentation 投影器，单元测试验证 draw order、UV、tint、alpha 和 texture 分组。
3. 已完成：接入 `SwapChainPanel` 与 D3D11 设备 / swap chain 生命周期，并默认绘制按 tint 着色的 Spine 顶点批次。
4. 已完成：接入 Texture2D 预览载荷解码、D3D texture 上传和 page texture 缓存，并在 D3D shader 中采样真实贴图。
5. 已完成：把当前每帧临时创建 vertex / index buffer 的提交流程升级为可复用动态 buffer / 批次提交器。
6. 已完成：补 D3D 后端生命周期和低风险缓存优化，包括 Unloaded 延迟释放、resize 防抖、device lost 一次重建、贴图 generation 过期保护、暂停状态降低刷新频率、vertex batch 缓存和池化 clip-space 投影。
7. 已完成：用 `char_2026_yu_nian#12.ab` 这类大量三角样本配合 D3D / runtime session 诊断记录首帧纹理上传、连续播放、速度切换、animation 切换和进度拖动基线，并确认当前 `60 FPS` 交互观感可接受。下一步只在高刷新目标、设备恢复、工作集或可感知卡顿需要时继续围绕 runtime frame generation、绑定刷新节流和小窗口 batch cache 优化，不再扩大 sampled frame / batch cache 或回到块状矩形近似。

### 第六阶段：Spine runtime 实时播放

完成标准：

- 已新增 Application 层 runtime playback session 契约，所有方法只使用稳定身份、播放请求 DTO 和 runtime-neutral 当前帧 DTO。
- Infrastructure 已在 V38 / V42 adapter 内创建和持有官方 runtime session，支持绝对 seek、animation / skin 切换、setup pose 和 dispose；播放 delta 由 Presentation 计算后以绝对时间请求当前帧。
- Presentation 可见预览已优先走 runtime session；隐藏预览 inactive 时不得创建或推进 session。
- 播放循环已从 `33ms` timer 迁移到显示刷新驱动，UI 绑定刷新仍可继续节流。
- D3D 后端已停止默认全量 sampled frame batch prewarm，仅保留当前帧 / 小窗口缓存和长期 GPU 资源缓存。
- runtime session 不可用或 adapter 不支持且没有 sampled fallback source 时，稳定回退现有 sampled DTO 播放路径。

建议开发顺序：

1. 已完成 Application 契约、DTO 和回归覆盖，现有 sampled UI 保留为回退。
2. 已在 Infrastructure V38 / V42 adapter 中实现 session create / seek / switch / setup pose / dispose，并复用现有 atlas / texture 诊断。
3. 已让 Spine 预览 ViewModel / surface 创建 session，并用返回的当前帧驱动现有 render backend。
4. 已改造播放 tick 为显示刷新驱动；当前 `60 FPS` 交互观感已通过真实复杂样本验证，进度绑定刷新节流留到高刷新目标或实测 XAML 压力出现时再决定。
5. 已收缩 D3D batch prewarm 和 cache 上限，只保留当前帧、小窗口或按需投影。
6. 已用真实复杂样本记录 runtime frame generation、D3D project / draw / present 和交互切换基线；是否把 D3D runtime path 升为默认仍需补更多设备、device lost 恢复、窗口缩放、skin 切换和工作集变化验证。

## 16. 测试建议

后续实现时建议补以下回归：

- 资产文档从主窗口移动到独立窗口后，主窗口标签组移除目标标签，独立窗口激活目标标签。
- 独立窗口标签移动回主窗口后，目标资源包标签页被打开或激活。
- 同一资产重复放回主窗口时去重并激活已有标签。
- 移动标签不丢失检查器分段位置、预览浏览状态和替换候选选择。
- 独立窗口关闭时，剩余标签和未应用草稿按设计规则处理。
- 工作区关闭或删除时，独立资源编辑窗口同步失效。
- Presentation 架构边界测试继续保证窗口模式不引入 Presentation 到 Infrastructure 的直接引用。
- Spine 组主窗口恢复只重建轻量组文档身份，重新激活后通过 Application 详情查询加载 atlas / texture 诊断和 overlay。
