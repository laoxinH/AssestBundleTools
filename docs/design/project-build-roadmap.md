# AssestBundleTools 当前路线图

## 1. 文档职责

本文只回答三个问题：当前已经具备什么、接下来优先完成什么、哪些能力明确延后。详细设计由专题文档维护，已完成历史由 Git 和版本说明维护。

## 2. 当前能力基线

以下能力已经进入主线，不应在新实现中重新建立平行链路：

- 工作区目录、最近工作区、pending 预登记、成功晋升和失败回滚。
- 文件与目录导入、常见非 AB 筛除、逐文件 codec 自动识别、标准 Unity 资产回退识别和增量索引。
- `cache/bundles` 标准化 artifact、逐包 codec 元数据、预览缓存、loaded-session 复用和容量治理。
- 资产列表、依赖与反向引用、资源详情标签、独立资源编辑窗口和窗口尺寸恢复。
- Texture2D、Sprite、AudioClip、文本、Mesh、Material、AnimationClip、Font、GameObject、MonoBehaviour、AnimatorController 与 Spine 预览主线。
- 单资源、批量资源和 Spine 组提取；文件、工作区及混合拖入替换；可恢复变更记录和资源级复原。
- 修改后 AB 的真实写出，包含 TextAsset、Texture2D、Sprite、AudioClip，压缩策略、逐 AB 来源 codec 回写和 schema v2 导出清单。
- AssetBundle codec、资源处理器和导出后处理三类外部 CLI 插件，以及文件夹/ZIP 包校验安装。
- Git tag 版本、三架构发布、MSI/Portable 分发、启动检查和关于页更新入口。

## 3. 活跃工作流

### P0 保存与恢复可靠性

- 补齐保存检查点和 `Committed` 状态，让导出成功与工作区事实恢复具有明确边界。
- 定义取消、部分成功、冲突确认和失败重试语义，避免把已写出的 AB 与未完成操作混为一体。
- 扩展真实样本回归，重点覆盖跨 Bundle 替换、来源 artifact 缺失和 codec 回写失败。

权威设计：[导出与保存重建](workspace-export-save-pipeline.md)、[可恢复操作记录](workspace-recoverable-operation-records.md)。

### P1 提取与替换完整性

- 完善命名冲突、取消结果、输出缺失和批处理结果明细。
- 继续以真实样本扩展可写资源类型，不按字段名猜测未验证格式。
- 保持单文件快捷替换、批量匹配和 Spine 组匹配共享正式 Application 链路。

权威设计：[批量提取](workspace-batch-extraction-dialog.md)、[批量替换](workspace-batch-replacement-dialog.md)。

### P1 大工作区性能与诊断

- 继续减少工作区重开时对未变化来源的扫描、Probe、Reader 和预览重复工作。
- 用阶段耗时、缓存命中和失败分类定位瓶颈，再调整并发度或缓存预算。
- 保持同一工作区单导入作业和“串行计划 -> 有界并行 worker -> 串行提交”边界。

权威设计：[工作区管理](workspace-management.md)、[后台任务调度](background-task-scheduling.md)。

### P2 扩展与发布加固

- 为插件包补充可选签名、来源信任和兼容版本策略之前，继续明确提示插件以当前用户权限运行。
- 完成安装版和便携版跨版本更新的设备矩阵验证，逐步接入代码签名。
- 在线组件下载与离线插件包导入继续共享安全原语，但不混为同一产品流程。

权威设计：[插件管理页](plugin-management-page.md)、[在线组件安装](online-component-installation.md)、[更新与发布](application-update-and-release-distribution.md)。

## 4. 交付顺序

默认按以下顺序选择下一项工作：

1. 已确认的数据损坏、崩溃和不可恢复回归。
2. 保存、替换和恢复链路的正确性与诊断。
3. 大工作区的可测性能问题。
4. 已有功能的可用性和一致性。
5. 新资源类型、在线市场等扩展能力。

每项变更都应同步对应专题文档和测试；不要把实现明细追加回本路线图。

## 5. 延后范围

以下内容没有明确样本、协议或产品门禁前不进入主线：

- Unity Editor 级完整 Shader、材质、动画状态机或场景编辑能力。
- 未经样本验证的资源字段推断和写回。
- 主程序进程内加载不受信任插件代码。
- 无签名、无兼容性约束的在线插件市场。
- 为历史讨论保留第二套工作区、预览、替换或保存架构。

## 6. 更新规则

- 能力完成后更新专题契约和版本说明，不在本文堆积完成日志。
- 优先级变化时只修改“活跃工作流”和“交付顺序”。
- 发现架构偏差时登记到[当前偏差登记](implementation-deviation-register.md)，关闭后从当前表移除并由 Git 历史保留。
