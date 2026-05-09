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

## 当前判断

- D3D11 后端已经能真实采样贴图并显示大 mesh，不再是空白或块状矩形近似。
- 大样本首帧主要消耗在 Texture2D GPU upload、device / shader / buffer warmup 和首帧 draw；后续 GPU draw / present 通常低于 `1 ms`。`Vortice.D3DCompiler.dll` 在首帧前首次加载，首帧 `drawMs` 稳定在约 `16 ms`，下一步先通过 batch renderer shader / pipeline 预热确认这部分能否移出首个真实动画帧。
- 新动画帧的 Presentation 投影成本主要在 `projectMs / clipMs`，当前大样本约 `1.4-3 ms`；同一帧重复渲染时缓存命中后基本归零。
- 当前迹象不支持“持续 GPU 绘制是主要瓶颈”的判断。有界多帧 batch cache 已让回环 / 重复帧路径的 `projectMs / clipMs` 基本归零。
- 复杂样本切入时曾出现 `projectMs=40.633 ms` 的冷投影尖峰，说明仍需避免 UI render 路径同步投影大 mesh。当前策略改为 texture page 解码完成后后台预热当前动画 sampled frames 的 vertex batch，回写前校验 texture page version；未命中且预热可用时，D3D 后端会延迟该帧绘制而不是在 UI 线程同步投影。
- 若有用户可感知卡顿但 D3D 分段耗时仍低，应补 Application / Infrastructure 侧预览生成、runtime 采样和 DTO 投影的加载期诊断，而不是继续压缩 D3D draw。
