# AssestBundleTools 设计文档索引

## 1. 文档目的

本文档用于收束 `docs/design` 下的文档状态，明确哪些文档是当前规范入口，哪些是专题记录，哪些仅作为历史兼容入口保留。

当前状态更新时间：2026-05-10。上一轮 Spine 预览、状态恢复、资源详情滚动和 UI 按钮样式已经完成阶段性提交，下一步开发重心回到提取、替换和保存输出主线。

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
- [asset-bundle-decryptor-extension.md](asset-bundle-decryptor-extension.md)
  解密器扩展点、外部进程协议和工作区缓存关系。
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
- M10 真实保存与重建尚未开始完整实现。
- 当前不再优先扩展新的预览类型或大改工作台布局。

## 6. 下一步开发主线

下一轮默认按“先让提取/替换顺畅，再进入真实写包”的顺序推进：

1. 提取流程顺滑化
   先按 [workspace-recoverable-operation-records.md](workspace-recoverable-operation-records.md) 建立可恢复操作记录，再明确单资源、多选、批量提取的输出目录、命名冲突、进度、取消、结果明细和日志入口。
2. 真实提取落地
   在 Application 用例后接 Infrastructure 导出实现，保持原始资源目录只读，输出目标必须由用户显式选择或确认。
3. 替换流程顺滑化
   补强文件来源选择、兼容性诊断、替换预览、撤销和批量替换结果明细，避免替换状态分散在多个页面。
4. 保存 / 重建前置设计
   在保存前预览、输出计划、覆盖保护、失败分类和回滚边界明确后，再进入真实写包。
5. 回归与文档同步
   每个小里程碑都同步补 Application / Infrastructure / Presentation 回归，并更新路线图中对应状态。
