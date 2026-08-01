# AssestBundleTools 设计文档索引

本目录保存仍对实现有约束力的设计契约。项目进度只在[当前路线图](project-build-roadmap.md)维护，已完成的阶段流水由 Git 历史和[版本说明](../releases/README.md)承担。

## 必读规范

修改对应区域前，按需阅读：

1. [代码架构规范](code-architecture-guidelines.md)：分层、目录、ViewModel 体量、本地化和测试边界。
2. [工作区管理](workspace-management.md)：工作区身份、元数据、导入、缓存和恢复规则。
3. [可恢复操作记录](workspace-recoverable-operation-records.md)：操作事实、草稿、撤销重做和检查点。
4. [UI 规范](ui-final-spec.md)：窗口、工作台、响应式布局和交互准线。
5. [错误处理规范](error-handling-guidelines.md)：`Result / AppError`、诊断和用户提示。
6. [当前偏差登记](implementation-deviation-register.md)：尚未关闭的架构偏差与体量风险。

构建命令和测试归属分别见[调试与构建](debug-setup.md)和 [tests/README.md](../../tests/README.md)。

## 工作区与任务

- [后台任务调度](background-task-scheduling.md)：profile、并发闸门、取消和状态投影。
- [关联资源同步](workspace-associated-asset-synchronization.md)：操作事实到列表、详情、预览和多窗口的刷新边界。
- [批量提取](workspace-batch-extraction-dialog.md)：选择模型、输出身份和提取 manifest。
- [批量替换](workspace-batch-replacement-dialog.md)：manifest 精确匹配、启发式匹配和拖入替换。
- [导出与保存重建](workspace-export-save-pipeline.md)：资源处理、写包、逐 AB codec 回写和导出 manifest。

## 浏览、预览与编辑

- [预览载荷契约](asset-preview-payload-contract.md)：预算、完整载荷引用、缓存和大小语义。
- [资源编辑窗口](asset-editor-window-mode.md)：标签移出、独立窗口、状态所有权和恢复。
- [文本编辑器技术决策](text-editor-technology-spike.md)：Monaco、WebView2、fallback 和草稿边界。
- [Spine 播放会话](spine-runtime-playback-session.md)：runtime session、时钟、缓存和交互。
- [Spine D3D11 性能基线](spine-d3d11-performance-baseline.md)：带日期的性能采样记录，不是产品进度文档。

## 插件、组件与发布

- [AssetBundle codec 扩展](asset-bundle-decryptor-extension.md)：逐 AB 识别、标准化 artifact 和来源格式回写。
- [插件管理页](plugin-management-page.md)：插件目录 UI、catalog 状态和应用集成边界。
- [在线组件安装](online-component-installation.md)：受信任运行时组件的下载、校验和原子安装。
- [更新与发布](application-update-and-release-distribution.md)：版本、产物矩阵、更新通道和发布门禁。
- [插件开发与安装指南](../plugin-guide.md)：三类插件唯一对外协议。

## 当前状态

- [当前路线图](project-build-roadmap.md)只维护仍有效的能力基线和下一阶段优先级。
- [当前偏差登记](implementation-deviation-register.md)只保留未关闭项；关闭项不继续堆积在正文。
- [版本说明](../releases/README.md)记录已经发布给用户的变化。

## 维护规则

- 稳定规范描述“必须怎样”，不记录逐提交完成日志。
- 专题文档只定义自己的所有权边界，公共协议通过链接复用。
- 对外插件字段和 CLI 语义只在 `docs/plugin-guide.md` 定义。
- 导出 manifest 的生产者契约只在 `workspace-export-save-pipeline.md` 定义。
- 路线图不复制测试清单；实际回归范围以 `tests/README.md` 为准。
- 仓库内链接使用相对路径，不写本机绝对路径。
