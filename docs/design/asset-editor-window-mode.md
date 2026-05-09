# 资源编辑窗口模式设计

## 1. 文档目的

本文档记录资源详情从“主窗口右侧检查器”扩展到“可独立资源编辑窗口”的设计约束，作为后续实现拖拽标签、独立窗口、窗口间标签组合、编辑 / 替换能力时的统一基线。

配套文档：

- [ui-final-spec.md](ui-final-spec.md)
- [workspace-management.md](workspace-management.md)
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

## 7. 预览 / 编辑区域

窗口模式下，原详情页顶部预览区升级为固定主区域：

1. 图片 / Sprite：保留缩放、平移、透明棋盘格、裁切框、pivot 和 9-slice 辅助线；后续编辑能力优先在此区域扩展。
2. Mesh：保留线框 / UV / 贴图叠加 / 网格显隐 / 缩放旋转等工具。
3. 文本：以固定编辑面呈现，为后续 TextAsset / Shader 编辑或替换确认预留空间。
4. Material / Font / AnimationClip / GameObject / MonoBehaviour / AnimatorController：继续显示主要摘要和轻量可视化信息；详细字段仍进入右侧信息区。
5. AudioClip：保留试听工具面，后续波形或幅度摘要也应在此区域扩展。
6. Spine 资源组：当前已在主窗口组文档中展示 JSON skeleton 轻量结构摘要、atlas region overlay、atlas page 选择器和结构化诊断，诊断覆盖 page 未匹配 Texture2D、Texture2D 预览不可用、page 尺寸与图片尺寸不一致等情况；实验性 JSON fallback 动画预览当前支持多 skin 的 region attachment、bone rotate / translate / scale、slot attachment、draw order 和 slot color / rgb / rgba / alpha 时间线，linked mesh、权重顶点、deform、slot 双颜色、event、IK / transform / path / physics 约束、Bezier 曲线等尚未完整解释的特性仍投影为结构化诊断。完整播放器参考 [SpineViewer](https://github.com/ww-rm/SpineViewer) 的“多版本 runtime 适配器 + 窄 skeleton / animation 抽象 + 独立渲染层”思路推进；当前 Application 已先建立并回归覆盖 `V21 / V34 / V35 / V36 / V37 / V38 / V40 / V41 / V42` 版本矩阵，按 skeleton 的 major/minor 版本解析目标 adapter key，预览 DTO 会携带 skeleton 版本、目标 adapter key、适配状态以及 runtime-neutral sampled frames / draw commands，未知或暂无 adapter 的版本会返回结构化 runtime 诊断。Application 现在已提供 `ISpineAnimationRuntimePreviewService` 端口：已知 adapter key 会先交给 Infrastructure runtime adapter 尝试生成同一套中立 DTO，未处理时继续回退到实验 JSON 预览；`.skel / .skel.bytes` TextAsset 会先通过统一预览缓存生成 `AssetPreviewKind.Binary` 载荷，再交给 runtime adapter 尝试解析，未处理时才保留二进制 skeleton 未支持诊断。当前 Infrastructure 已 vendor 官方 `spine-csharp` 3.8 / 4.2 源码到 `ThirdParty/Spine/V38` 和 `ThirdParty/Spine/V42`，并完成 V38 / V42 JSON 与 binary skeleton adapter；V38 / V42 adapter 会把 region attachment 直接输出中立 draw command，mesh attachment 输出官方 runtime 计算出的 world vertices、atlas UV 和 triangle indices，mesh deform timeline 会通过官方 runtime 采样反映到 mesh world vertices，V38 transform constraint timeline 与 V38 / V42 IK constraint timeline 已有最小样本回归，runtime draw order 与 slot / attachment / skeleton tint 会合成到 draw command。Presentation 通过 Win2D 按 DTO 应用 alpha 与 RGB tint，并保持 mesh 几何逐三角贴图绘制；复杂帧使用控件级渲染目标缓存复用完整高质量画面，只有 mesh 数据不可绘制时才退回到 region 矩形，避免播放时出现块状近似。播放体验层当前提供动画选择、真实 skin 切换、播放速度、进度拖动、播放 / 暂停、上一帧 / 下一帧、重播和 setup pose；setup pose 由 Application DTO 的独立 `SetupFrame` 承载，skin 切换通过 `SkinPreviews[]` 切换对应的 setup frame 与 sampled frames，不再把动画 0 秒帧等同于 setup pose；当前帧选择在 ViewModel 内用缓存索引和二分查找，采样端会按 setup frame 的 draw command / mesh 三角复杂度限制最大采样帧数，避免大动画一次性生成过重帧缓存。具体 runtime 对象必须封装在 Infrastructure adapter 内，Presentation 仍只消费 Application DTO / draw command / Texture2D 预览载荷。当前 Application 已提供 `LoadSpineAssetGroupsQuery / LoadSpineAssetGroupDetailsQuery` 的轻量组读模型，主窗口资产窗格已接入 `资源 / Spine 组` 切换用于浏览组摘要。选择 Spine 组会在主窗口右侧复用现有资源详情窗格打开组文档，身份为 `BundleId + GroupId`，检查器展示组摘要、组成资产跳转、skeleton 摘要、诊断摘要、atlas page / region、page 选择器、当前 page 的 overlay、runtime adapter 状态和可用时的实验性动画预览；Spine 组文档可与普通资产文档混在同一个资源文档标签组中，并可跟随统一资源编辑窗口进行会话内拖出、组合和放回。

预览 / 编辑区域只消费 Application 预览 DTO、缓存载荷和展示层状态，不直接读取 AB、遍历文件或调用 Infrastructure 解析实现。

## 8. 功能区

功能区分成两层概念：

1. **视图工具**：刷新预览、重置视图、打开所在位置、复制定位信息、缩放、背景切换、网格显隐等。
2. **资源动作**：提取、选择工作区来源、选择文件来源、应用替换、撤销、后续编辑草稿应用或丢弃。

规则：

- 高频视图工具优先用图标按钮，并提供 tooltip。
- 高价值资源动作使用文本或图标 + 文本按钮。
- 危险动作、不可用动作和会修改工作区状态的动作必须有明确禁用态、确认态或诊断入口。
- 功能区不应复制右侧信息区中的完整字段，只承载当前资源的操作入口和简短状态。
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

### 11.2 页面级窗口状态

以下状态属于页面级浏览状态，可随窗口和标签移动：

- 资源编辑窗口位置、大小、显示器和最小化 / 最大化状态。
- 资产文档组顺序。
- 当前激活资产文档。
- 每个资产文档的内层分段位置。
- 图片缩放 / 平移、Sprite 显示模式、Mesh 显示模式、文本滚动位置等预览浏览状态。
- 替换候选选择。
- 编辑草稿标识和草稿来源。

这些状态可以持久化，但第一阶段不要求独立资源编辑窗口随应用重启恢复；主窗口资源包标签和资产文档组恢复仍是优先级更高的稳定链路。

Spine 资源组文档属于页面级浏览状态。主窗口资源包标签会用 `openSpineGroupTabs[]` 只保存 `GroupId / DisplayName / GroupKey` 这类轻量身份信息；独立资源编辑窗口通过 `openAssetEditorWindows[].openDocumentTabs[]` 保存普通资产文档和 Spine 组文档的统一轻量标签序列，并用 `ActiveDocumentKind / ActiveSpineGroupId` 恢复活动 Spine 组。窗口快照仍不保存 atlas 解析结果、Texture2D 预览载荷、region overlay 数据或诊断明细；真实组详情在恢复后激活时继续通过 `LoadSpineAssetGroupDetailsQuery` 和现有预览管线重新读取。`openAssetTabs[] / ActiveAssetId` 保留为普通资产标签和旧快照兼容字段。

### 11.3 编辑草稿状态

后续编辑能力落地时，编辑草稿必须绑定：

- 工作区身份。
- `BundleId + AssetId`。
- 资源类型和预期替换 / 编辑模式。
- 生成草稿的资源版本或来源指纹。

草稿不得只绑定某个 WinUI 控件实例。窗口关闭、标签移动或主窗口切换时，草稿必须能被确认、丢弃或恢复。

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
- `AssetEditorWindowStateStore` 已跟踪当前会话内的独立窗口状态快照，包含窗口 bounds、资产文档标签顺序、活动资产身份、检查器分段和图片 / Mesh 预览浏览选项。
- 工作区浏览状态 DTO 与工作区元数据已具备 `openAssetEditorWindows[]` 持久化形状；窗口创建、布局变化、标签组变化和关闭会随浏览状态回写该快照。
- 工作区重新打开时当前会读取 `openAssetEditorWindows[]` 并弹出一次可控恢复提示；如果快照里包含轻量草稿引用，提示会显示带草稿标记的标签数量，并提供“同时恢复草稿标记”的轻量选项；如果待恢复标签已在主窗口或现有独立窗口中打开，提示和工具栏入口会显示冲突标签数量，并提示关闭对应标签后可继续恢复。用户选择稍后时，主窗口工具条仍保留手动恢复入口。只有用户主动确认时，才会按保存的轻量标签组和窗口布局重建独立资源编辑窗口。
- 恢复时会先按当前工作区资源包可用性和已打开资产身份拆分快照；可恢复的窗口会打开，不可恢复或与当前已打开资产冲突的窗口 / 标签会继续保留在待恢复入口中，用户也可在恢复提示中显式忽略这些已保存窗口。
- 恢复窗口布局时会先校验最小尺寸，并在应用到 WinUI 窗口前按当前显示器工作区做安全归位，避免窗口恢复到屏幕外或过大的不可操作位置。
- 当前只落盘轻量窗口 / 标签恢复元数据，不保存真实详情内容、预览载荷、替换候选、编辑草稿内容或运行时资源数据；资产文档标签已具备轻量 `EditDraftReference` 挂点，只允许保存草稿 id、模式和来源指纹等恢复引用。
- Application 层已新增 `Editing` 草稿恢复候选契约：`IAssetEditDraftStore` 只读取草稿元数据，`GetAssetEditDraftRestorePlanHandler` 按工作区、草稿 id、模式和来源指纹把轻量引用解析为 `Restorable / Missing / SourceMismatch / Expired / InvalidReference`。该契约只用于后续恢复提示和诊断分流，不读取或暴露真实草稿内容。
- Presentation 恢复弹窗当前已在显示前调用 `AssetEditDraftRestorePlanGateway` 做轻量预检，并在弹窗中显示可恢复、缺失、来源不匹配、已过期和引用无效的草稿引用数量；该预检会刷新提示文案、重复弹窗判定和后续可保留的草稿标记集合，不自动恢复草稿内容，也不改变用户是否主动恢复窗口的控制权。组合根已接入 `WorkspaceChangeSetAssetEditDraftStore`，从 `staging/changes/<workspaceKey>.json` 的 `editDrafts[]` 描述列表读取草稿元数据。
- 当前仍不自动弹出独立资源编辑窗口，也不恢复最大化 / 最小化、显示器或真实编辑草稿内容；当预检计划可用时，草稿选项只决定本次成功打开的标签是否带回预检为可恢复的 `EditDraftReference` 标记，缺失、来源不匹配、已过期和引用无效的草稿标记不会重新挂回新标签。真实草稿确认 / 丢弃 / 恢复队列仍留给后续编辑器模型。
- 当前打开资源详情、依赖跳转、Spine 组文档和新窗口打开入口会先检查主窗口与所有独立资源编辑窗口：如果目标文档已打开，则直接激活并切换到已有标签，再把所在窗口置前；只有未命中现有标签时才继续创建新的文档标签或独立窗口。
- Spine 资源组当前作为统一资源文档标签打开到普通独立资源编辑窗口；会话内混合标签、拖拽合并、放回主窗口、冲突激活和 `openAssetEditorWindows[]` 快照恢复均复用统一文档标签链路。Spine 组恢复只持久化 `BundleId + GroupId + DisplayName + GroupKey`，不持久化解析详情或预览载荷。

### 第五阶段：Spine GPU 批量渲染

完成标准：

- 新增 Presentation 内部的 Spine 渲染后端抽象，使现有播放控件可以在 Win2D 回退后端和 D3D11 后端之间切换。
- 当前已完成第一步：`InspectorSpineAnimationPreviewSurfaceView` 已改为消费 `ISpineAnimationPreviewRenderBackend`，现有 Win2D 绘制、贴图加载、帧缓存和 invalidation 已下沉到 `Win2DSpineAnimationPreviewRenderBackend`，播放控件本身不再直接持有 Win2D 帧绘制状态。
- D3D11 后端通过 `SwapChainPanel` 承载，并实现按 texture / blend 分组的 vertex batch renderer，批量提交 Spine region / mesh 三角形。
- 顶点批次只由 Application 的 runtime-neutral DTO 投影而来，保持 `world position / UV / tint / alpha / draw order` 语义；不在 Presentation 中持有官方 Spine runtime 对象，也不读取 AB。
- 当前已完成第二步：Presentation 内部已新增 DTO 到顶点批次的纯投影器，覆盖 region / mesh 顶点、UV、tint / alpha、无效三角过滤和按连续 texture page 保持 draw order 的批次切分；该投影器暂不接触 D3D 类型。
- 当前已完成第三步的受控 D3D 后端：`ABT_SPINE_D3D11_SMOKE=1/true/d3d11` 时，播放控件会切到 `SwapChainPanel` 承载的 D3D11 后端，创建 device / swap chain / render target，并把顶点批次投影到 clip space 后通过 D3D11 顶点 / 索引 buffer 提交。
- 当前已完成第四步的第一段：Texture2D 预览载荷会在 Presentation 内解码为 BGRA8、上传为 D3D texture / shader resource，并按 page name 缓存；顶点批次会通过 shader 采样真实 page texture 并应用 DTO 中的 tint / alpha。
- 当前已完成第五步的第二段：D3D11 后端已把每个 batch 每帧临时创建 vertex / index buffer 的提交方式替换为可复用动态 buffer，按容量增长后使用 `WriteDiscard` 上传当前 batch；播放控件会在 Unloaded 时释放 D3D / Win2D 后端资源，D3D resize 通过短防抖合并，Texture2D 解码和上传带 generation 保护，DXGI device removed / reset / hung / internal error 会先尝试释放并重建一次，仍失败时才自动切回 Win2D 后端并重放当前帧与贴图状态。D3D 后端还会按 `SpineAnimationFrameDto` 引用、texture page version 和 viewport 缓存 vertex batch / clip-space batch，暂停或当前帧未变化时减少重复 invalidation。D3D smoke 可通过 `ABT_SPINE_D3D11_DIAGNOSTICS=1/true/d3d11` 额外输出 device / resize、texture decode、GPU upload、frame batch / vertex / index / elapsed、upload / projection / clip / draw / present 分段耗时、播放交互事件和 fallback / device lost 异常类型，便于真实样本基线记录；默认路径仍是 Win2D，D3D 后端仍需通过环境变量主动启用。
- 保留现有 Win2D 帧缓存作为回退；D3D 可用时大量三角动画不再走逐三角 `CreateLayer` 裁剪路径。
- 给真实高复杂样本补受控性能验证记录，至少覆盖首帧纹理上传、连续播放、窗口缩放、skin / animation 切换、设备丢失回退和恢复。

建议开发顺序：

1. 已完成：抽出 `SpineAnimationPreviewSurface` 的渲染后端接口和 Win2D 适配器，现有播放功能继续走兼容回退后端。
2. 已完成：实现 DTO 到 batch 的纯 Presentation 投影器，单元测试验证 draw order、UV、tint、alpha 和 texture 分组。
3. 已完成：接入 `SwapChainPanel` 与 D3D11 设备 / swap chain 生命周期，并以 opt-in smoke 绘制按 tint 着色的 Spine 顶点批次。
4. 已完成：接入 Texture2D 预览载荷解码、D3D texture 上传和 page texture 缓存，并在 D3D shader 中采样真实贴图。
5. 已完成：把当前每帧临时创建 vertex / index buffer 的 smoke 提交流程升级为可复用动态 buffer / 批次提交器。
6. 已完成：补 D3D 后端生命周期和低风险缓存优化，包括 Unloaded 释放、resize 防抖、device lost 一次重建、贴图 generation 过期保护、暂停状态降低刷新频率、vertex batch 缓存和 clip-space batch 缓存。
7. 下一步：用 `char_2026_yu_nian#12.ab` 这类大量三角样本配合 D3D 诊断记录首帧纹理上传、连续播放帧时间、窗口缩放、skin / animation 切换和内存占用基线；若卡顿主要来自 Application / Infrastructure 的 runtime 采样或 DTO 投影，再把缓存推进到动画时间戳 / skin / animation 维度，而不是回到块状矩形近似。

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
