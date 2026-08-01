# AssestBundleTools 当前实现偏差登记

## 1. 文档职责

本文只登记仍存在、能够从当前代码验证的架构偏差或体量风险。产品功能缺口进入[当前路线图](project-build-roadmap.md)，测试覆盖明细进入 [tests/README.md](../../tests/README.md)，已关闭记录由 Git 历史保留。

登记项必须包含证据、约束和退出条件。问题关闭后直接从当前表删除，不在正文继续维护完成流水。

## 2. 当前总览

当前没有已知的分层反向依赖或组合根越界。以下两项属于需要在后续修改中持续收口的文件体量风险。

| ID | 范围 | 状态 | 风险 |
| --- | --- | --- | --- |
| D-01 | 草稿文本持久化 | 活跃 | 单个 Infrastructure store 接近强制拆分阈值 |
| D-02 | BundleExplorer 页面协作对象 | 观察 | 多个页面级类型位于软上限附近，容易重新聚合职责 |

## 3. D-01 草稿文本持久化体量

证据：

- `JsonAssetEditDraftTextStore.cs` 当前约 790 行，已经接近手写业务类 800 行的强制拆分阈值。
- 文件同时承载描述文件、payload、文本索引、分块/行读取、搜索、删除和路径保护。

约束：

- 新增独立变化原因时，应先拆为明确的 storage、index 或 query collaborator。
- 不允许把 JSON、路径或文件系统细节上移到 Application 或 Presentation。

退出条件：

- 主 store 回到协调职责，持久化、索引和查询边界能够独立测试；或经审查确认其内容属于单一机械存储职责并为其记录明确豁免。

## 4. D-02 BundleExplorer 体量观察

证据：

- `BundleExplorerAssetWorkflowViewModel.cs` 当前约 600 行，位于页面级 facade 软上限。
- `BundleExplorerViewModel.cs`、提取 ActionHandler、标签 Coordinator 和插件管理 ViewModel 也处于约 570-600 行区间。

约束：

- 新状态优先进入已有子状态宿主或显式 `Coordinator / Presenter / ActionHandler`。
- 不恢复父级兼容透传属性、页面 bridge 或 sibling concrete coupling。
- 触达这些文件时必须重新检查行数和职责，不以“仍低于 800 行”为继续聚合的理由。

退出条件：

- 活跃页面 facade 稳定低于 600 行，或其职责被证明保持单一且不再增长。

## 5. 持续守护项

以下规则已有自动化或代码结构保护，不重复登记为偏差：

- `Presentation.WinUI` 只允许 `Composition/` 直接引用 Infrastructure。
- 用户可见文本通过本地化资源提供。
- 工作区打开保持 pending -> promote -> rollback 语义。
- 预览通过 Application 管线和缓存模型加载。
- 自动识别按 AB 独立选择 codec，来源 codec 元数据随 artifact 保存并用于导出回写。

若这些规则再次被破坏，应新增带具体文件和复现证据的偏差项，而不是在本页追加泛化提醒。
