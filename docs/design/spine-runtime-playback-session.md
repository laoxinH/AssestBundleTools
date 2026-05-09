# Spine Runtime Playback Session 设计

## 1. 文档目的

本文档定义 Spine 动画预览从“预生成 sampled frames + 全量 batch 预热”迁移到“runtime session 实时播放”的长期路线。

目标是：

- 降低长动画和复杂 mesh 在低速播放时的常驻内存占用。
- 避免为了改善流畅度继续把整段动画采样到 `60 / 120 FPS` 并全量缓存。
- 让实时预览按真实播放时钟推进，而不是受固定 sampled frame 数量限制。
- 保持现有分层边界：Presentation 仍不直接持有官方 Spine runtime，也不读取 AB。

## 2. 背景结论

当前 D3D11 后端日志显示，复杂样本稳定播放时 GPU draw / present 通常不是主要瓶颈；更大的风险来自：

- runtime adapter 预先生成大量 `SpineAnimationFrameDto.SampledFrames`。
- D3D 后端对当前 animation sampled frames 做 vertex batch 预热。
- 长动画在 `0.5x / 0.25x` 播放时，为了保持姿态密度需要更多 sampled frames，内存随 `时长 * 采样率 * mesh 复杂度` 增长。

参考 [SpineViewer](https://github.com/ww-rm/SpineViewer) 的实时预览策略：预览播放持有 runtime skeleton / animation state，每个 render loop 用真实 delta 推进并绘制当前姿态；固定 fps 逐帧推进只用于视频或序列帧导出。该方向更适合本项目的交互预览。

## 3. 设计原则

1. 实时预览优先使用 runtime session，不再依赖全动画 sampled frame 列表。
2. 导出、截图序列或确定性测试可以继续使用固定 fps 采样。
3. Application 只定义 runtime-neutral 契约；Infrastructure 封装官方 Spine runtime 对象。
4. Presentation 只消费 Application DTO、播放状态和 Texture2D 预览载荷；不得引用 `AssestBundleTools.Infrastructure` 或官方 runtime 命名空间。
5. D3D11 / Win2D 渲染后端仍属于 Presentation 实现细节，不进入 Application 契约。
6. 工作区浏览状态只保存 Spine 组轻量身份和 UI 状态，不持久化 runtime session、骨骼实例、GPU 资源或逐帧缓存。

## 4. 分层边界

### 4.1 Application

Application 新增一个面向实时播放的窄端口，建议命名为：

- `ISpineAnimationRuntimePlaybackService`

该端口负责创建、推进、seek、切换 animation / skin 和释放 runtime playback session。端口方法只能使用 Application DTO 和稳定标识，例如：

- `WorkspaceId`
- `BundleId`
- `SpineGroupId`
- skeleton / atlas / texture page 组成资产身份
- animation name
- skin name
- playback time 或 delta
- playback speed

端口可以选择两种实现形态之一：

- 返回 Application 定义的 session lease 接口，Presentation 只持有该接口并负责 dispose。
- 返回不透明 session id，Presentation 后续调用时携带 session id。

无论采用哪种形态，官方 Spine `Skeleton / AnimationState / Atlas` 等对象都不得穿过 Application 边界。

### 4.2 Infrastructure

Infrastructure 负责：

- 基于现有 Spine 资源组详情、预览载荷和 adapter key 创建对应版本 runtime session。
- 持有官方 Spine runtime 对象和 atlas / texture region 映射。
- 每次 update / seek 后，把当前姿态投影为 runtime-neutral `SpineAnimationFrameDto` 或等价 draw command DTO。
- 在 animation / skin 变化时重置或更新 runtime state，并返回新的当前帧。
- 在 session dispose、工作区失效或预览取消时释放 runtime 对象。

首批实现范围优先覆盖已 vendor 的 V38 / V42 runtime adapter。JSON fallback 可以继续走 sampled DTO 路径，直到其行为也有稳定 runtime session 实现。

### 4.3 Presentation

Presentation 负责：

- 创建和释放当前可见 Spine 预览对应的 playback session。
- 把播放 / 暂停、速度、seek、animation / skin 切换转换为 Application 端口请求。
- 用显示刷新驱动 runtime update，并把返回的当前帧交给现有 D3D11 / Win2D render backend。
- 继续负责 D3D texture 上传、swap chain、device lost 恢复、vertex batch 投影和渲染。

Presentation 不负责：

- 读取 AB。
- 解析 skeleton / atlas。
- 持有官方 runtime 对象。
- 把整段动画预热成常驻 D3D batch cache。

## 5. 播放时钟策略

实时播放使用真实 elapsed delta：

```text
render tick -> elapsed delta -> delta * playbackSpeed -> runtime session update -> current frame DTO -> render backend
```

当前首版实时播放已从 `33ms` 的 `DispatcherTimer` 切到 `CompositionTarget.Rendering`，播放时间按真实 elapsed delta 与当前播放速度推进。真实复杂样本已验证 `60 FPS` 交互目标可接受；若后续目标提升到 `120 FPS` 或更高刷新率，仍需在目标机器上验证实际 rendering tick、XAML binding 压力和 D3D present 节奏；可选机制包括：

- `CompositionTarget.Rendering`
- D3D 后端驱动的 render loop
- 更高精度的 `DispatcherQueueTimer`，但需要验证实际 jitter

`120 FPS` 只在高刷新率显示器和实际 update/render loop 能达到约 `8.33ms` tick 时才有意义。若播放循环、绑定刷新或 present 节奏达不到目标，提高采样率或缓存帧数不会带来真实 120fps 观感。

UI 绑定刷新不需要和渲染帧率完全一致。进度文本、slider 等可节流更新，避免高刷新播放时造成 XAML binding 压力。

## 6. 缓存策略

实时预览主路径缓存长期资源，而不是缓存整段动画姿态：

- 缓存 skeleton / atlas / animation state 所需 runtime 对象，生命周期绑定 playback session。
- 继续缓存 Texture2D 预览载荷和 D3D shader resource。
- 继续复用 D3D dynamic vertex / index buffer。
- 可保留小型 current / lookahead vertex batch cache，用于防止同一姿态或短时间回退重复投影。
- 不再默认对整个 animation 的所有 sampled frames 做 batch prewarm。

旧 sampled DTO 路径保留为：

- Win2D / runtime session 不可用时的兼容回退。
- JSON fallback 过渡实现。
- 未来导出、序列帧或固定 fps 离线生成。
- 自动化测试中需要稳定帧序列时的确定性输入。

## 7. 交互语义

### animation / skin 切换

切换 animation 或 skin 时，Presentation 应先向 playback session 提交切换请求，再用返回的当前帧刷新渲染后端。切换事件不得出现“新 animation 名称 + 旧播放时间 / 旧帧”的混合状态。

### seek

seek slider 使用绝对时间请求。session 必须支持把当前 animation 设置到指定时间并返回对应姿态。seek 不应通过连续累加大量 delta 来追赶目标时间。

### pause / step

暂停时不推进 delta，但可继续渲染当前帧。逐帧命令在 runtime session 主路径下应按目标显示帧率或 runtime 建议步长推进；固定 fps 导出路径仍使用导出 fps。

### setup pose

setup pose 继续作为独立语义。runtime session 可提供 setup pose 当前帧，不能把 animation 0 秒帧等同为 setup pose。

## 8. 诊断

新增诊断应能区分：

- session 创建耗时与 adapter key。
- runtime frame generation / skeleton create or reset / animation apply / world transform / draw command build 耗时。
- 当前帧 draw command / vertex / triangle 数量。
- render backend 投影、clip、draw、present 分段耗时。
- animation / skin / speed / seek / pause / dispose 交互事件。
- session 创建失败、adapter 不支持、载荷缺失、session 过期和取消。

建议日志事件前缀：

- `spine.runtime-session-created`
- `spine.runtime-session-disposed`
- `spine.runtime-frame`
- `spine.runtime-interaction`
- `spine.runtime-session-failed`

这些日志只作为诊断；用户可见错误仍走结构化错误和本地化资源。

当前实时预览已输出 `spine.runtime-frame` 诊断，字段包括 adapter key、setup pose 状态、animation / skin、播放时间、总耗时、skeleton 是否复用、skeleton create / reset、animation apply、world transform、draw command build、draw command 数量以及 mesh vertex / triangle 数量。D3D11 后端继续输出 `spine.d3d11 frame`，用于区分 runtime 生成帧成本和 GPU / DTO 投影 / present 成本。

## 9. 实施阶段

当前 2026-05-09 首版实现状态：

- Application 已新增 `ISpineAnimationRuntimePlaybackService`、播放 session 接口和 `SpineAnimationRuntimePlaybackSourceDto / FrameRequest / FrameResult` 等 DTO；runtime adapter 生成的预览 DTO 会携带 `PlaybackSource`，交互预览请求会传 `IncludeSampledFrames=false`，不再为实时播放预采样整段动画。
- Infrastructure 的 V38 / V42 官方 runtime adapter 已能创建 playback session，并按 animation / skin / setup pose / 绝对播放时间生成当前 `SpineAnimationFrameDto`。
- Presentation 的 Spine surface 已优先创建 runtime playback session，并由 `CompositionTarget.Rendering` 驱动播放；ViewModel 允许 runtime 预览在没有 `SampledFrames` 时仍展示 animation / skin。
- D3D11 后端不再默认对整段 animation sampled frames 做全量 batch prewarm；当前帧 batch 未命中时直接投影并绘制，只保留小窗口 world-space batch cache。
- Runtime frame result 已携带分段诊断，Presentation 调试输出会节流打印 `spine.runtime-frame`；V38 / V42 playback session 会在同 skin 连续帧之间复用 runtime skeleton 并在每帧重置 setup pose，正常 frame cancellation 已改为返回 `Cancelled` 结果，避免交互切换和 session dispose 时把预期取消表达为热路径异常。
- `char_2026_yu_nian#12.ab` 大量三角样本的最新交互日志已经覆盖播放、速度切换、animation 切换和进度拖动；连续帧显示 `reusedSkeleton=True`、`createMs=0`、`resetMs` 常见约 `0.07-0.3ms`，D3D 当前帧路径大多 `projectMs=0` 且 draw 成本低，主观观感已达到流畅的 `60 FPS` 交互预览目标。
- 旧 sampled DTO 路径仍保留为 JSON fallback、runtime session 不可用时的兼容回退、测试稳定输入和未来固定 fps 导出方案。
- 尚未完成：UI 绑定刷新节流、高刷新率 / `120 FPS` 实机验证、固定 fps 导出链路正式产品化、JSON fallback 的 runtime session 化、更多官方 Spine Runtime 版本，以及 linked mesh / path / physics 等高级行为的样本门控补强。D3D11 后端当前仍通过 `ABT_SPINE_D3D11_SMOKE` 显式启用，默认切换需要另行验收。

### 第一阶段：契约与文档

- 已完成 Application 端 runtime playback session 契约和 DTO。
- 已明确 session 生命周期、取消、dispose 和工作区失效规则。
- 已保留现有 sampled DTO 预览生成，不立即删除旧路径。

### 第二阶段：Infrastructure V38 / V42 session

- 已在 V38 / V42 adapter 内创建 runtime playback session。
- 已支持 create、绝对 seek、animation / skin 切换、setup pose 和 dispose；首版由 Presentation 计算播放时间，session 按绝对时间生成当前姿态。
- 继续复用现有 atlas / texture page 匹配诊断。
- 已输出与现有 D3D projector 兼容的 runtime-neutral frame DTO。

### 第三阶段：Presentation 接入实时播放

- Spine 预览 surface 已优先创建 runtime playback session。
- 播放循环已从 `33ms` timer 迁移到显示刷新驱动。
- D3D 后端已停止播放主路径的全量 sampled frame batch prewarm，只保留当前帧和小窗口缓存。
- runtime session 不可用时继续回退现有 sampled DTO 播放。

### 第四阶段：验证与默认策略

- 已用 `char_2026_yu_nian#12.ab` 复杂样本记录并验证 `60 FPS` 目标下的交互播放、速度切换、animation 切换和进度拖动观感。
- 在高刷新显示器上继续验证 `120 FPS` 目标是否真的受益，并决定进度文本 / slider 等 UI 绑定是否需要进一步节流。
- 固定 fps 导出、序列帧或视频输出仍需从实时预览主线中正式拆成产品化用例。
- 若 D3D runtime path 在更多设备、device lost 恢复和工作集基线下稳定，再考虑让 D3D 后端从 opt-in smoke 变为默认高复杂 Spine 路径。

## 10. 测试要求

- Application tests 覆盖 session 契约、无 adapter / 载荷缺失 / session 过期 / dispose 后调用等稳定语义。
- Infrastructure tests 覆盖 V38 / V42 session 的 create、update、seek、animation / skin 切换、setup pose 和 dispose。
- Presentation tests 覆盖 session 生命周期、隐藏预览 inactive、不重复创建 session、关闭标签释放 session，以及 Presentation 不直接引用 Infrastructure 的边界检查。
- 性能验证继续同时记录 `spine.runtime-frame` 与 `spine.d3d11 frame`，用来判断瓶颈来自 runtime 生成当前姿态、DTO 到 D3D batch 投影、clip / draw，还是 present。

## 11. 非目标

- 不把官方 Spine runtime 对象暴露给 Presentation。
- 不把 runtime session 写入工作区持久化状态。
- 不为了实时预览继续扩大 sampled frame 上限或全量 D3D batch cache。
- 不在第一阶段同时重写导出链路。
- 不把 WinUI 控件、D3D device、swap chain 或 GPU resource 放入 Application / Infrastructure 公共 DTO。
