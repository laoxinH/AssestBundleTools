# Spine D3D11 Performance Baseline

本文档记录 Spine D3D11 smoke 后端的真实样本性能观察。它不是默认启用标准，而是后续判断卡顿来源、缓存策略和默认切换条件的对照记录。

## 采集方式

启动前打开：

```powershell
$env:ABT_SPINE_D3D11_SMOKE = '1'
$env:ABT_SPINE_D3D11_DIAGNOSTICS = '1'
```

建议在同一资源组上连续记录以下操作：

- 首次打开并等待 texture decode / GPU upload 完成
- 正常播放至少 10 秒
- 窗口 resize
- 切 animation
- 切 skin
- 暂停 / 恢复
- 上一帧 / 下一帧
- 关闭并重新打开同一 Spine 组

重点读取这些日志字段：

- `texture-decode requested / decoded / payloadBytes / elapsedMs / textureAlpha / nonZero`
- `gpu-texture-upload uploaded / totalGpuPages / uploadedBytes / totalGpuBytes / elapsedMs`
- `frame batches / vertices / indices / gpuBytes / elapsedMs`
- `uploadMs / projectMs / clipMs / drawMs / presentMs`
- `spine.runtime-frame totalMs / reusedSkeleton / createMs / resetMs / applyMs / worldMs / commandsMs / commands / meshVertices / meshTriangles`
- `batch-renderer-prewarmed` / `batch-prewarm-completed`
- `interaction event / animation / skin / playing / time / speed`
- `device-lost recovery=queued` 或 `rendering-failed`

## 2026-05-09 基线

样本：`char_2026_yu_nian#12.ab`

环境：`ABT_SPINE_D3D11_SMOKE=1`、`ABT_SPINE_D3D11_DIAGNOSTICS=1`，WinUI 预览区域约 `393x234` 像素。

大贴图 / 大三角 Spine 组：

| 指标 | 观测值 |
| --- | --- |
| texture payload | `15,666,859` bytes |
| decoded pages | `1` |
| texture decode | `122.213 ms` |
| texture alpha | `0..255` |
| non-zero alpha | `55.009%` |
| GPU pages | `1` |
| GPU texture bytes | `42,510,400` |
| GPU upload | latest run `14.052 ms`; previous segmented run observed `53.904 ms` |
| first visible frame | latest run frame `1`, `8,845` vertices, `25,350` indices, `66.255 ms` |
| first frame timing | latest run `uploadMs=14.052`, `projectMs=3.479`, `clipMs=3.353`, `drawMs=15.904`, `presentMs=1.556` |
| cached duplicate frame | latest run frame `2`, `1.004 ms`, `projectMs=0.135`, `clipMs=0.141`, `drawMs=0.408`, `presentMs=0.267` |
| changed frame spike | latest run frame `3`, `11.735 ms`, `projectMs=10.128`, `clipMs=0.885`, `drawMs=0.585`, `presentMs=0.116` |
| changed frame typical | latest run frame `5`, `1.94 ms`, `projectMs=0.77`, `clipMs=0.56`, `drawMs=0.497`, `presentMs=0.083` |
| steady sample | latest run frame `60`, `0.309 ms`, `projectMs=0.001`, `clipMs=0.001`, `drawMs=0.264`, `presentMs=0.034` |
| later steady sample | frame `120`, `0.317 ms`, `projectMs=0`, `clipMs=0.001`, `drawMs=0.267`, `presentMs=0.041` |
| bounds sample | `-1029.775,6.337..1023.472,1921.976` |
| UV range | `0.001,0.001..1,0.999` |
| alpha range | `0..1` |

小贴图参考 Spine 组：

| 指标 | 观测值 |
| --- | --- |
| texture payload | `1,299,532` bytes |
| decoded pages | `1` |
| GPU texture bytes | `4,194,304` |
| first visible frame | `1,869` vertices, `5,244` indices, about `25-39 ms` |
| steady samples | often below `6 ms` after shader / texture warmup |

## 2026-05-09 实时播放验证

最新 `char_2026_yu_nian#12.ab` 交互日志覆盖播放、速度切换、animation 切换和进度拖动。用户观感已经达到流畅的 `60 FPS` 交互预览目标，当前不再把“提高 sampled frame 数量 / 全量 batch 预热”作为实时播放优化方向。

关键观察：

| 指标 | 观测值 |
| --- | --- |
| runtime skeleton reuse | 连续同 skin 帧稳定 `reusedSkeleton=True` |
| runtime create cost | 连续帧 `createMs=0` |
| runtime reset cost | 常见约 `0.07-0.3 ms` |
| runtime apply / world | 常见低于 `0.5 ms` |
| D3D projection | 小窗口 batch 命中时多为 `projectMs=0` |
| D3D draw | 稳态低，主要剩余成本常在 `presentMs` 或 runtime draw command build |
| commandsMs spike | 可见 `8-10 ms` 和少量更高尖峰，但当前没有形成用户可感知卡顿 |

当前结论：

- `60 FPS` 交互预览可以视为达标；后续不应为了压低偶发 `commandsMs` 尖峰牺牲结构边界或重新引入整段预采样，除非后续日志能复现用户可感知卡顿。
- 若目标提升到 `120 FPS`，`commandsMs`、XAML 绑定刷新和 present 节奏会重新变成需要一起验证的瓶颈；这属于下一阶段高刷新目标，不是当前 `60 FPS` 完成标准。
- 旧 sampled DTO 仍保留给 JSON fallback、兼容回退、测试稳定输入和未来固定 fps 导出，不再作为交互预览主线。

## 当前判断

- D3D11 后端已经能真实采样贴图并显示大 mesh，不再是空白或块状矩形近似。
- 大样本首帧主要消耗在 Texture2D GPU upload、device / shader / buffer warmup 和首帧 draw；后续 GPU draw / present 通常低于 `1 ms`。`Vortice.D3DCompiler.dll` 在首帧前首次加载，首帧 `drawMs` 稳定在约 `16 ms`，下一步先通过 batch renderer shader / pipeline 预热确认这部分能否移出首个真实动画帧。
- 新动画帧的 Presentation 投影成本主要在 `projectMs / clipMs`，当前大样本约 `1.4-3 ms`；同一帧重复渲染时缓存命中后基本归零。
- 当前迹象不支持“持续 GPU 绘制是主要瓶颈”的判断。有界多帧 batch cache 已让回环 / 重复帧路径的 `projectMs / clipMs` 基本归零。
- 复杂样本切入时曾出现 `projectMs=40.633 ms` 的冷投影尖峰，说明仍需继续观察大 mesh 首次投影。当前实时播放主路径不再等待整段 sampled frame batch prewarm；当前帧 batch 未命中时直接投影并绘制，避免“等待预热完成”本身造成切换后的可见跳帧。
- D3D 投影尖峰消除后，复杂样本仍不流畅时优先看 `spine.runtime-frame` 的 runtime frame generation 分段、播放 tick 频率和 XAML binding 压力。真实样本曾只有 `22` 个 sampled frames，说明旧采样预算会限制低速播放观感；V38 / V42 的 `60 FPS`、`1024` 帧采样策略现在只作为 `IncludeSampledFrames=true` 的兼容 / 导出路径能力，交互预览主线不再请求整段 sampled frames。
- 长期策略不再继续提高全动画 sampled frame 上限或 D3D 全量 batch cache。实时预览已接入 [Spine Runtime Playback Session](spine-runtime-playback-session.md)：Infrastructure 持有官方 Spine runtime state，按绝对 seek 时间生成当前帧 DTO；Presentation 只渲染当前帧并保留小窗口缓存。固定 fps 采样保留给导出、序列帧和 runtime session 不可用时的兼容回退。
- `60 FPS` 交互目标已经由真实复杂样本确认；若目标提升到 `120 FPS`，仍需要同时验证播放 tick、D3D present 和 XAML binding 压力。继续增加 `120 FPS` 采样或预热不再是实时预览主路径。
- 最新真实样本日志已经确认当前 runtime playback session + D3D 当前帧路径满足 `60 FPS` 交互目标；下一轮性能工作只应在高刷新目标、可感知卡顿或默认启用策略需要时继续推进。
- 默认动画连续播放平稳、但切换 animation / skin 时出现跳帧感时，优先检查 `animation-changed` / `skin-changed` 诊断中的 `time` 是否还停留在旧动画位置。此前 ViewModel 在重置播放时间和当前帧前先发出选择变化通知，导致控件短暂看到“新动画名称 + 旧播放时间 / 旧当前帧”，后台预热也可能把旧帧加入新动画批次；当前已改为先同步 `PlaybackTime` 与 `CurrentFrame`，再通知渲染后端刷新；预热也会在当前帧同步到渲染后端后启动，避免 `requestedFrames` 混入旧动画帧。
- 若 `interaction`、`texture-decode` 或 `batch-prewarm-completed` 诊断成对出现，并伴随切换后播放速度异常或跳帧，应优先检查是否有隐藏预览控件也在监听同一个 Spine ViewModel。独立资源编辑窗口右侧详情面板会复用 `InspectorPanelView`，但其详情预览区是隐藏的；当前隐藏详情预览会向内层 Spine surface 传递 inactive 状态，避免隐藏副本创建 runtime session、启动 rendering loop、重复 texture decode 和 batch prewarm。
- 若有用户可感知卡顿但 D3D 分段耗时仍低，应对照 `spine.runtime-frame` 判断是否来自 skeleton create / reset、animation apply、world transform 或 draw command build；若 runtime 与 D3D 分段都低，再继续检查播放 tick 与 XAML binding 刷新压力，而不是继续压缩 D3D draw。

## 2026-05-09 内存优化记录

- `SpineAnimationMeshVertexDto` / `SpineAnimationMeshTriangleDto` 已从引用类型 record 改为只读值类型 record struct，避免复杂 mesh 的 sampled frames 产生大量小对象；官方 V38 / V42 runtime 采样继续受 draw command / mesh triangle 总预算约束，硬上限回升到 `1024` 帧用于改善长动画低速播放和非默认动画切换后的可见姿态密度。
- D3D Texture2D page 成功上传为 shader resource 后，会释放 CPU 侧 BGRA 解码页；后续仍保留 texture page size 供 vertex batch 投影使用。
- D3D Texture2D 上传不再把 BGRA `byte[]` 复制成额外 `uint[]`；当前通过 span 视图提交像素数据，避免大贴图上传期间再产生一份同尺寸临时数组。
- D3D 后端不再把每帧 clip-space batch 常驻在缓存中；当前只缓存 texture-version 保护下的 world-space vertex batch，渲染时用池化数组直接投影并上传动态 vertex buffer。
- 因此新日志里的 `clipMs` 可能不再长期接近 `0.001 ms`，但它不再代表一份常驻 clip-space 顶点缓存；实际是否接受应以播放流畅度和工作集下降共同判断。
- 继续扩大 sampled frame 和全量 batch cache 已不再作为主要内存优化方向；实时预览主路径已改为只缓存 runtime session、Texture2D GPU 资源、动态 buffer 和当前帧 / 小窗口批次。
