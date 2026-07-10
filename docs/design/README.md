# AssestBundleTools 设计文档索引

## 1. 文档目的

本文档用于收束 `docs/design` 下的文档状态，明确哪些文档是当前规范入口，哪些是专题记录，哪些仅作为历史兼容入口保留。

当前状态更新时间：2026-07-11。当前开发重心已经进入 M10 真实导出 / 保存重建与文本编辑器收口并行阶段：后台任务、资源处理器并发、AB 写包并发和输出 manifest 契约已固化，首版对象级 AssetsTools 写包已经接入；来源 bundle codec 的识别、解密和导出回写协议已落地，并提供内置 Arknights LZ4AK CLI。文本预览 / 编辑已开始向统一 text surface 收束，正式 VS Code 风格编辑器目标采用 WebView2 + Monaco，下一步继续补保存检查点、Committed 状态、取消 / 部分成功恢复语义、更多资源类型和 Monaco 原型。

## 2. 当前规范入口

日常开发优先按下面顺序查阅：

1. [code-architecture-guidelines.md](code-architecture-guidelines.md)
   分层、目录、ViewModel 体量、本地化和测试边界的最高约束。
2. [project-build-roadmap.md](project-build-roadmap.md)
   当前阶段、下一步计划和里程碑顺序。
3. [workspace-management.md](workspace-management.md)
   工作区、缓存、导入、浏览状态和恢复规则。
4. [ui-final-spec.md](ui-final-spec.md)
   当前 UI 结构、工作台布局、检查器、预览和资源文档标签规则。
5. [error-handling-guidelines.md](error-handling-guidelines.md)
   `Result / AppError`、异常、诊断和用户提示规则。
6. [implementation-deviation-register.md](implementation-deviation-register.md)
   已知偏差、阶段性收口状态和后续 guardrail。

测试范围与命令注意事项见 [../../tests/README.md](../../tests/README.md)。

## 3. 专题文档

这些文档仍然有效，但只在触达对应专题时阅读：

- [asset-editor-window-mode.md](asset-editor-window-mode.md)
  资源编辑窗口、资产文档标签、独立窗口恢复和编辑草稿边界。
- [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md)
  工作区提取、替换、编辑草稿和保存输出的可恢复操作记录、撤销与重启恢复设计。
- [workspace-associated-asset-synchronization.md](workspace-associated-asset-synchronization.md)
  工作区操作后真实目标资源、隐式依赖资源、聚合资源文档和独立编辑窗口之间的通用同步模型。
- [background-task-scheduling.md](background-task-scheduling.md)
  导入、预览、提取、导出和后续保存 / 重建的统一后台任务调度、并发闸门、进度与状态条边界。
- [asset-preview-payload-contract.md](asset-preview-payload-contract.md)
  所有资源预览的载荷预算、DTO 安全边界、完整文本按需查看 / 编辑承载和阶段性加固计划。
- [text-editor-technology-spike.md](text-editor-technology-spike.md)
  VS Code 风格文本编辑器技术选型、WebView2 + Monaco 目标方案、原生 fallback surface 和后续 patch / JSON 格式化阶段计划。
- [workspace-export-save-pipeline.md](workspace-export-save-pipeline.md)
  工作区真实导出 / 保存重建的执行计划、资源处理器并发、AB 写包并发、检查点和 AB 输出 manifest 契约。
- [workspace-batch-extraction-dialog.md](workspace-batch-extraction-dialog.md)
  工作台顶部批量提取入口从跳转批处理页改为当前工作区内资源包 / 类型选择对话框的设计与第一版落地状态。
- [workspace-batch-replacement-dialog.md](workspace-batch-replacement-dialog.md)
  工作区级批量替换窗口、导出 manifest 回读、size-first / MD5 精确匹配、启发式回退和旧批量替换骨架移除方向。
- [asset-bundle-decryptor-extension.md](asset-bundle-decryptor-extension.md)
  解密器扩展点、外部进程协议和工作区缓存关系。
- [plugin-management-page.md](plugin-management-page.md)
  侧边栏一级插件管理页、导入解密 CLI 插件迁移、导出资源处理 CLI 插件目录和导入 / 导出整合逻辑。
- [spine-runtime-playback-session.md](spine-runtime-playback-session.md)
  Spine runtime playback session、播放时钟、缓存和诊断边界。
- [spine-d3d11-performance-baseline.md](spine-d3d11-performance-baseline.md)
  Spine D3D11 性能采样记录和后续基线扩展。
- [debug-setup.md](debug-setup.md)
  Visual Studio、命令行构建、WinUI x64/x86 验证和调试配置。

## 4. 已合并的同类文档

以下文档仍保留文件名，避免旧链接失效，但内容已并入当前规范入口：

- [ui-design.md](ui-design.md)
  已合并到 [ui-final-spec.md](ui-final-spec.md)。后续不再作为独立 UI 规范维护。
- [ui-wireframes.md](ui-wireframes.md)
  已合并到 [ui-final-spec.md](ui-final-spec.md)。后续只作为历史文本原型入口，不再维护独立线框状态。

如果 UI 规则冲突，统一以 [ui-final-spec.md](ui-final-spec.md) 为准。

## 5. 当前阶段结论

当前项目状态可以收束为：

- M0-M7 已完成。
- M8 提取与替换流程骨架已完成第一轮：候选、替换状态、撤销、批量预览和保存骨架已经有稳定承载面。
- M9 真实读取、缓存、依赖图和富预览主线已完成阶段性收口；Spine 预览达到基础播放、动画 / skin 切换、状态恢复和 D3D 默认渲染的可继续使用状态。
- M10 真实保存与重建已完成首版真实导出链路：CLI 资源处理器、对象级 AssetsTools 写包、来源 codec 可选回写、AB 输出 manifest 和基础回归已接入；保存检查点、Committed 状态、取消 / 部分成功恢复语义和更广资源类型仍在推进中。
- 当前不再优先扩展新的预览类型或大改工作台布局；文本相关工作优先沿统一 `TextDocumentSurfaceView` 和 Monaco 原型推进，避免检查器预览、资源编辑窗口和后续大文本编辑继续分叉。

## 6. 下一步开发主线

下一轮默认按“继续收口后台任务与真实导出，再补保存检查点和恢复语义”的顺序推进：

1. 统一后台任务流程第一阶段
   按 [background-task-scheduling.md](background-task-scheduling.md) 建立 Presentation 层统一调度契约、profile、进度和状态条投影；当前已迁移工作区打开 / 导入外层任务、预览队列、Spine 详情载荷、批量提取、批量替换骨架和保存 / 导出执行，并接入 `cache/bundles` 标准化资源包物理缓存、artifact 生成 single-flight、usage / LRU prune、设置页显式清理入口、偏好驱动自动容量治理、`cache/previews` 预览缓存 usage / prune / 自动容量治理、当前工作区聚合缓存统计 / 清理、基于 artifact identity 的 loaded-session 复用 / 主动失效 / 容量退避，以及预览 `OutOfMemory` 专用诊断和同 bundle 后台预热短期退避。SaveRebuild 已读取现有标准化 artifact、loaded-session identity 和来源 codec 身份，通过输出路径 preflight 暴露覆盖 blockers，并接入 CLI 资源处理器、对象级 AssetsTools 写包、来源格式回写和 AB 输出 manifest；后续继续沿这份只读输入图补齐 materialization、最终冲突确认、取消和回滚语义。
2. 提取流程顺滑化
   已按 [workspace-batch-extraction-dialog.md](workspace-batch-extraction-dialog.md) 把工作台顶部批量提取收敛为当前工作区内的资源包 / 类型选择对话框；后续继续补命名冲突、取消结果明细、输出文件缺失诊断和批处理页历史摘要收口。
3. 真实提取落地
   在 Application 用例后接 Infrastructure 导出实现，保持原始资源目录只读，输出目标必须由用户显式选择或确认。
4. 替换流程顺滑化
   补强文件来源选择、兼容性诊断、替换预览、撤销和批量替换结果明细，避免替换状态分散在多个页面。
5. 保存 / 重建真实导出收口
   按 [workspace-export-save-pipeline.md](workspace-export-save-pipeline.md) 继续扩展首版对象级写包覆盖面，补保存检查点、Committed 状态、取消 / 部分成功恢复语义、更多资源类型和 `_abt-bundle-export-manifest.json` 后处理契约。
6. 回归与文档同步
   每个小里程碑都同步补 Application / Infrastructure / Presentation 回归，并更新路线图中对应状态。
