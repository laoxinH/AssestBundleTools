# 工作区关联资源同步模型设计

## 1. 文档目的

本文定义工作区操作发生后，真实目标资源、关联资源、聚合资源文档和独立资源编辑窗口之间的通用同步模型。

该模型不是 Spine 专用机制。它应覆盖：

- `Texture2D` 修改影响 `Sprite`
- `TextAsset` 修改影响直接引用它的 Spine / MonoBehaviour / GameObject 等宿主资源，例如 `.atlas / .skel / .json` 文本资源
- `Material / Sprite / Mesh / TextAsset` 等可被直接引用资源修改后，影响所有可展示的直接引用宿主
- `Texture2D / Sprite` 修改影响 `Mesh / Material / GameObject / ParticleSystemRenderer` 等引用型预览
- Spine 资源组中的 skeleton / atlas / texture page 组成资产修改影响组文档
- 普通资产详情、聚合资产详情、主窗口和独立资源编辑窗口之间的双向撤销 / 重做 / 删除记录同步

目标是把“操作事实状态同步”和“富预览重载”拆开，避免重型预览流程阻塞变更记录按钮、状态标签和关联资源操作能力。

## 2. 问题背景

当前工作区操作已经通过 `operations[]` 形成事实流，Presentation 也已有操作通知和受影响资产扩展能力。但在复杂关联资源中，仍存在以下问题：

1. 变更记录按钮状态可能依赖完整资源详情重载。
2. 聚合资源如 Spine 组会把组成资产 `ChangeRecords` 和动画预览 details 一起刷新。
3. 重型预览加载、防抖、取消和 runtime 初始化会放大按钮状态延迟。
4. 主窗口与独立编辑窗口可能分别处在 busy / pending / refresh 状态，导致同一条操作记录的按钮反馈不一致。
5. 当前通知更多表达“资产受影响”，缺少一套稳定的“操作事实读模型已变化，应立即同步所有投影”的通用契约。

因此，后续不应继续为 Spine、Sprite、Mesh 分别补局部刷新 hack，而应建立统一关联资源同步模型。

## 3. 核心设计结论

关联资源同步分为三层：

```text
操作事实层
  operations[] / effective replacement / redo slot / record deletion

轻量操作投影层
  ChangeRecords / CanUndo / CanRedo / IsEffective / pending operation state

富预览投影层
  image / sprite crop / mesh texture overlay / material thumbnail / spine runtime preview
```

同步规则：

1. 操作事实层由 Application 和 Infrastructure 维护，是唯一事实来源。
2. 轻量操作投影层必须优先刷新，用于变更页卡片、撤销 / 重做 / 删除按钮、状态 badge 和关联记录投影。
3. 富预览投影层异步刷新，可以防抖、取消、降级和失败诊断，但不得阻塞轻量操作投影层。
4. Presentation 可以维护短生命周期的 pending/loading 状态，但该状态只表示“用户命令或读模型同步正在进行”，不得成为操作事实来源。
5. 同一 `operationId` 在真实目标资源、隐式依赖资源、聚合资源和多窗口中的所有可见投影必须共享同一 pending / disabled 语义。

## 4. 分层职责

### 4.1 Application

Application 负责定义稳定契约：

- 操作记录 DTO 和物化规则。
- 关联影响查询抽象。
- 轻量操作投影查询或刷新契约。
- 根据 `operationId` 解析真实操作目标。
- 校验关联资源是否确实受目标资源影响。

Application 不负责：

- WinUI 控件状态。
- 按钮 loading 动画。
- 预览控件刷新调度。
- 直接格式化用户文案。

### 4.2 Infrastructure

Infrastructure 负责从工作区运行时会话、依赖图、操作记录和缓存中提供数据：

- 物化当前有效替换和 redo 能力。
- 为单个资产构建 `AssetDetailsDto.ChangeRecords`。
- 通过运行时依赖图解析隐式依赖影响。
- 为聚合资源的组成资产提供轻量详情补水。

Infrastructure 不得直接通知 ViewModel，也不得暴露 WinUI 类型。

### 4.3 Presentation

Presentation 负责：

- 订阅操作变化通知。
- 将通知路由到打开的资产文档、聚合文档和独立窗口。
- 优先刷新轻量操作投影。
- 再按需要刷新富预览。
- 维护短生命周期 pending 状态并投影到按钮。
- 本地化展示变更记录文本和诊断。

Presentation 不得：

- 直接读取 `staging/changes`。
- 根据 UI 当前卡片猜测真实目标并绕过 Application 校验。
- 让预览加载完成成为撤销 / 重做事实状态切换的前置条件。

## 5. 通知契约演进

当前 `WorkspaceOperationChangedEventArgs` 已包含：

- `WorkspaceId`
- `ChangeKind`
- `AffectedAssets`
- workspace-wide 标记

长期应演进为操作读模型事件，建议补充：

```text
operationIds[]
primaryTargets[]
relatedTargets[]
implicitAffectedTargets[]
changeKind
reason
```

字段语义：

- `operationIds[]`
  本次事实变化涉及的操作记录 ID。撤销 / 重做 / 删除记录必须尽量携带。
- `primaryTargets[]`
  操作真实目标资产。
- `relatedTargets[]`
  `relations[]` 明确关联的对端资产。
- `implicitAffectedTargets[]`
  由依赖图扩展出的受影响资源，例如 Sprite / Mesh / Material / Spine 组组成关系。
- `reason`
  稳定枚举，例如 `ReplacementApplied / ReplacementUndone / ReplacementRedone / RecordDeleted / EditApplied / ExtractionRecorded / SaveCheckpointChanged`。

在未完成事件 DTO 演进前，现有 `AffectedAssets` 仍可作为兼容输入，但刷新协调器应在内部尽量归一为“受影响目标集合 + 相关 operationId 集合”。

## 6. 轻量操作投影刷新

应新增通用轻量刷新路径，职责是只刷新操作事实相关展示，不触发重型预览生成。

推荐能力：

```text
RefreshOperationProjectionAsync(workspaceId, affectedTargets, operationIds)
```

返回或应用内容包括：

- 单资产 `ChangeRecords`
- 单资产 `CanRedoReplacement`
- 当前有效替换 operation id
- 当前有效编辑 operation id
- 删除记录后的可见记录集合
- 聚合文档中组成资产的轻量 `ChangeRecords`

刷新规则：

1. 普通资产文档命中目标资产时，重读该资产轻量详情并重投影变更页。
2. 普通资产文档命中隐式依赖目标时，重读当前资产轻量详情，以便展示关联替换记录。
3. 聚合资源文档命中任一组成资产时，只先刷新组成资产轻量详情并更新聚合文档 `Parts[].ChangeRecords`。
4. 轻量刷新完成后立即更新按钮、badge 和卡片可见性。
5. 富预览是否刷新由后续 preview invalidation policy 判断。

对于 Spine 组，第一步不应等待 animation preview details 完整重载；只要 skeleton / atlas / texture page 组成资产的 `ChangeRecords` 更新，就应立即重投影组级 `变更` 分段。

## 7. 富预览刷新

富预览刷新继续按资源类型分发：

- `Texture2D`：有效图片预览。
- `Sprite`：有效底图 + 当前几何裁切投影。
- `Mesh`：有效贴图叠加或 UV 视图刷新。
- `Material`：有效贴图 / shader 引用摘要刷新。
- `Spine`：atlas page / texture upload / runtime playback details 刷新。

富预览刷新规则：

1. 只有有效预览输入签名变化时才刷新。
2. 允许防抖、取消旧请求、后台预热和降级。
3. 预览区可以显示 loading / stale / diagnostic。
4. 预览失败不得回滚操作按钮状态。
5. 预览刷新完成后不得覆盖已经更新过的轻量操作投影，除非携带更新后的同一版本操作投影。

## 8. Pending 状态模型

Presentation 应新增短生命周期 pending 状态，用于命令执行到轻量读模型刷新完成之间的窗口期。

推荐键：

```text
workspaceId
operationId?
targetBundleId
targetAssetId
actionKind
```

其中 `operationId` 可能在命令开始前为空，例如“撤销当前有效替换”场景；命令成功并返回或通知到达后应尽快绑定真实 operation id。

Pending 状态影响：

- 命中的撤销 / 重做 / 删除按钮禁用。
- 可显示轻量 loading indicator。
- 同一条操作在关联资源上的投影也同步禁用。
- 不影响无关记录和无关资源操作。

Pending 清除规则：

1. 命令失败：清除 pending，保留旧事实状态。
2. 命令成功且轻量投影刷新完成：清除 pending，展示新事实状态。
3. 命令成功但富预览仍在刷新：清除按钮 pending，仅预览区继续 loading。
4. 页面关闭或窗口销毁：清理本宿主 pending 投影；全局 tracker 不应泄漏窗口引用。

## 9. 关联影响规则

关联影响分为两类：

### 9.1 显式关联

由 `WorkspaceOperationRecordDto.relations[]` 表达，适合“同一个用户确认动作拆成多条强关联操作”的情况。

例子：

- Spine 编辑保存时，对 skeleton / atlas / texture page 组成资产生成多条替换记录。
- 未来某些复合编辑同时写入多个真实资产。

显式关联规则：

- 每条操作仍有自己的真实 `target`。
- 聚合资源只能投影真实操作记录，不能伪造组级资产目标。
- 撤销 / 重做 / 删除必须携带真实 `operationId`。

### 9.2 隐式依赖影响

由运行时依赖图和关联影响 registry 解析，适合“资源 A 被修改后，依赖 A 的资源需要刷新展示”的情况。

例子：

- Texture2D 修改影响 Sprite。
- Texture2D / Sprite 修改影响 Mesh 贴图叠加。
- Texture2D 修改影响 Material / GameObject 摘要。
- TextAsset 修改影响直接引用它的 Spine / MonoBehaviour / GameObject 等宿主资源，例如 `.atlas / .skel / .json`。
- Material / Sprite / Mesh / TextAsset 等直接引用目标变化时，所有可展示直接引用宿主都应进入轻量操作投影刷新。
- Spine texture page 修改影响 Spine 组动画预览。

隐式依赖规则：

- 不新增操作记录。
- 不改写原操作 `relations[]`。
- 依赖资源可以投影目标资源的替换记录为关联记录。
- 关联记录上的撤销 / 重做仍解析并执行真实目标操作。

## 10. 页面和窗口刷新流程

统一流程：

```text
用户点击操作按钮
  -> Presentation 标记 pending
  -> Application 命令执行
  -> operations[] 追加 / 删除 / 物化
  -> 操作通知发布
  -> 所有宿主按 workspaceId 过滤
  -> 轻量操作投影刷新
  -> 清除 pending，更新按钮与卡片
  -> 按预览输入变化异步刷新富预览
```

主窗口和独立窗口必须消费同一通知契约。不同窗口可以独立调度预览，但轻量操作投影应尽量保持同一事实状态。

如果某窗口处于全局 busy：

- 不应完全阻塞 pending 显示。
- 可以延后重型预览刷新。
- 轻量操作投影刷新应尽量允许在命令成功后尽快执行，避免按钮状态长时间停留旧值。

## 11. 与现有实现的迁移路径

### 第一阶段：稳定 pending 状态（已落地基础实现）

- 已新增 Presentation 层 `WorkspaceOperationPendingTracker`，以 `workspaceId + operationId` 作为短生命周期键。
- `InspectorChangeRecordViewModel` 已增加 `IsPending / PendingVisibility / IsActionEnabled` 投影字段；检查器变更卡片的撤销、重做、删除按钮在 pending 时显示轻量 `ProgressRing` 并禁用。
- 资源详情、Spine 聚合详情、主窗口和独立资源编辑窗口共用同一 pending tracker；同一 `operationId` 在真实目标、直接关联资源、隐式依赖资源和聚合资源中的可见卡片会同步进入 / 退出 pending。
- 操作按钮触发时，撤销 / 重做 / 删除记录按真实 `operationId` 标记 pending；普通“应用新替换”尚未产生可见记录，不进入 operationId pending。
- 当前清除时机由命令作用域驱动：命令失败会清除 pending 并恢复旧事实状态；命令成功返回后清除 pending，随后轻量投影刷新继续通过现有操作通知同步新事实状态。富预览仍独立 loading，不再阻塞变更卡片按钮状态。

### 第二阶段：拆轻量操作投影刷新

- 已在 Presentation 层新增 `AssociatedAssetOperationProjectionRefreshCoordinator`，作为轻量刷新协调器，独立于预览加载协调器。
- 已让普通资产文档在命中操作通知时先刷新 `ChangeRecords / CanRedoReplacement / IsModified / replacement source` 等轻量操作投影，再走原有完整详情和预览刷新流程。
- 已让 Spine 聚合资源文档在操作通知后优先刷新组成资产 `ChangeRecords`，并重新投影组级变更页按钮状态。
- 已让主窗口和独立资源编辑窗口在 Spine 组命中操作通知时先执行轻量操作投影刷新，再排 animation details 重载。
- 已为 Spine 预览调度拆出“仅刷新富预览”的入口，避免预览重载前用主面板旧快照覆盖已经更新的轻量操作投影。
- 当前关联影响扩展已覆盖所有可展示直接反向引用资源，`Texture2D` 只是首个稳定验证类型；`.atlas / .skel / .json` 等 `TextAsset` 以及其他直接引用目标也应通过同一通知扩展和轻量投影刷新路径同步。
- 统一通知 DTO 演进仍属于后续阶段；pending tracker 已在 Presentation 层先行落地为瞬时 UI 状态，不进入 Application 操作事实流。

### 第三阶段：演进通知 DTO

- 通知事件补充 `operationIds / primaryTargets / relatedTargets / implicitAffectedTargets / reason`。
- `NotifyingWorkspaceAssetOperationGateway` 从命令结果中优先提取真实 affected targets。
- 删除记录、撤销、重做场景尽量携带被影响 operation id。
- 保留旧 `AffectedAssets` 兼容一段时间。

### 第四阶段：统一预览失效策略

- 让 Texture2D、Sprite、Mesh、Material、Spine 都通过同一 preview invalidation policy 判断是否刷新富预览。
- 预览刷新不得重置轻量操作投影。
- 对聚合文档建立“轻量 parts refresh”和“full details refresh”的明确 API 边界。

## 12. 测试建议

Application / Infrastructure：

- 操作通知结果能给出真实目标和关联目标。
- 隐式依赖查询能从 Texture2D 找到 Sprite / Mesh / Material / Spine 组组成影响，并能从 TextAsset 等直接引用目标找到直接引用宿主。
- 关联资源撤销 / 重做通过 `operationId` 解析真实目标。
- 删除记录后 affected targets 覆盖真实目标和关联目标。

Presentation：

- 点击关联记录撤销后，真实目标和关联资源卡片同时进入 pending。
- 命令成功后，即使 Spine 预览仍 loading，变更卡片按钮已切换到新状态。
- 命令失败后，两侧 pending 清除且按钮恢复旧状态。
- 主窗口和独立窗口同时打开同一目标 / 关联资源时，轻量投影同步刷新。
- 预览刷新失败不回滚按钮状态。
- busy 状态只延后重型预览，不长期阻塞轻量操作投影。

回归范围应优先覆盖稳定契约：DTO 字段、按钮状态、pending 状态、刷新调用边界和预览失败后的状态保持；不要断言具体 loading 文案或偶发调度顺序。

## 13. 当前执行结论

后续实现关联资源同步时，默认按以下原则执行：

1. 操作事实优先，预览展示其次。
2. 变更页按钮状态必须来自轻量操作投影，不来自重型预览完成事件。
3. 关联资源、聚合资源和多窗口共享同一操作事实与 pending 语义。
4. Spine 只是这套模型中的复杂聚合资源案例，不应拥有独立的同步规则。
5. 新增资源类型的关联预览必须接入同一同步模型，而不是在 ViewModel 中手写局部互刷。
