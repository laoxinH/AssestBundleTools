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
| texture alpha | `0..255` |
| non-zero alpha | `55.009%` |
| GPU pages | `1` |
| GPU texture bytes | `42,510,400` |
| GPU upload | `14.263 ms` |
| first visible frame | frame `6`, `8,845` vertices, `25,350` indices, `37.14 ms` |
| steady sample | frame `60`, `8,845` vertices, `25,350` indices, `2.345 ms` |
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
- 大样本首帧主要包含 shader / buffer / draw warmup，后续帧已明显下降。
- 下一轮需要用 `uploadMs / projectMs / clipMs / drawMs / presentMs` 区分 CPU runtime 采样、DTO 到 vertex batch 投影、clip-space 投影、GPU draw 和 present 的占比。
- 若 `projectMs` 或 frame DTO 生成前耗时占主导，优先把缓存推进到 animation / skin / playback timestamp 维度；若 `drawMs / presentMs` 占主导，再考虑 D3D batch 合并、texture page 排序或更低层的渲染调度优化。
