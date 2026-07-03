# AssestBundleTools 错误与异常处理规范

## 1. 文档目的

本文档用于统一项目中的错误、异常、日志和用户提示处理方式。

目标如下：

- 让错误处理方式在各层保持一致
- 明确什么时候返回 `Result`，什么时候抛异常
- 保证用户提示、诊断日志和异常堆栈彼此分离
- 和 Clean Architecture、多语言设计保持一致

本文档默认约束以下范围：

- `Domain`
- `Application`
- `Infrastructure`
- `Presentation.WinUI`

## 2. 基本原则

### 2.1 先区分“错误”和“异常”

本项目中，必须先把两个概念分开：

- `错误`：业务流程中可预期、可描述、可恢复或可提示给用户的问题
- `异常`：程序遇到了非预期状态、外部依赖故障、代码缺陷或无法在当前层直接处理的问题

例如：

- 选择了不存在的文件，这是可预期错误
- 拖拽了不兼容的资源类型，这是可预期错误
- 第三方解析库抛出未处理异常，这是异常
- 领域对象出现不可能状态，这是异常

### 2.2 预期失败不用异常驱动

以下情况不应依赖抛异常来驱动主流程：

- 参数校验失败
- 文件不存在
- 资源类型不兼容
- 用户取消操作
- 保存前校验不通过
- 资源未找到
- 输出文件冲突

这些情况应优先通过：

- `Result`
- `Result<T>`
- `ErrorCode`
- `AppError`

来表达。

### 2.3 异常只用于非预期问题

以下情况允许或应该抛异常：

- 程序 bug
- 领域不变量被破坏
- 第三方库抛出不可控异常
- 文件系统、序列化、网络等基础设施级故障
- 当前层无法恢复且必须中断执行

### 2.4 用户提示和开发诊断必须分离

用户看到的内容应该是：

- 已本地化
- 可理解
- 与当前操作相关

开发或诊断日志应该是：

- 技术化
- 可检索
- 包含错误码、上下文和异常细节

不能把异常消息直接弹给用户。

## 3. 统一决策规则

推荐统一按下面的判断表执行。

| 场景 | 建议处理方式 |
|---|---|
| 用户输入不合法 | 返回 `Result.Failure` |
| 资源类型不匹配 | 返回 `Result.Failure` |
| 文件不存在/路径错误 | 返回 `Result.Failure` |
| 用户取消文件选择 | 返回 `Cancelled` 或非错误状态 |
| 第三方库解析失败，但可识别原因 | 包装后返回 `Result.Failure` |
| 第三方库抛出未知异常 | 抛出 `InfrastructureException`，由上层转换 |
| 领域对象进入不可能状态 | 抛出 `DomainInvariantException` |
| UI 事件处理器内部未捕获异常 | 由全局异常处理统一兜底 |

## 4. 推荐的错误模型

建议项目内统一定义一套应用级错误模型，而不是到处返回字符串。

## 4.1 `AppError`

建议结构至少包含：

```csharp
public sealed record AppError(
    string Code,
    ErrorCategory Category,
    ErrorSeverity Severity,
    string MessageToken,
    IReadOnlyDictionary<string, object?> Parameters,
    bool IsUserActionable = true,
    bool IsRetryable = false);
```

字段含义建议如下：

- `Code`
  - 稳定错误码
- `Category`
  - 错误分类
- `Severity`
  - 严重级别
- `MessageToken`
  - 本地化消息键
- `Parameters`
  - 本地化格式化参数
- `IsUserActionable`
  - 用户是否能通过修改输入修复
- `IsRetryable`
  - 是否建议重试

## 4.2 `Result` / `Result<T>`

建议所有 Use Case 统一采用：

```csharp
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsCancelled { get; }
    public IReadOnlyList<AppError> Errors { get; }
}

public sealed class Result<T> : Result
{
    public T? Value { get; }
}
```

推荐约束：

- 成功时 `Errors` 为空
- 失败时至少有一个 `AppError`
- 用户取消不视为失败异常
- 不要混用 `null` + `bool` + `string message` 这种松散模式

## 4.3 错误分类

建议先定义以下基础分类：

- `Validation`
- `NotFound`
- `Conflict`
- `Unsupported`
- `PermissionDenied`
- `ExternalDependency`
- `IoFailure`
- `ParseFailure`
- `SerializationFailure`
- `Concurrency`
- `Cancelled`
- `Unknown`

## 4.4 严重级别

建议定义：

- `Info`
- `Warning`
- `Error`
- `Critical`

通常建议：

- 用户可修复问题：`Warning`
- 当前操作失败：`Error`
- 可能导致工作区不可恢复或程序无法继续：`Critical`

## 5. 异常类型设计

不建议项目里到处随意定义异常类，但建议保留少量明确的异常基类。

推荐最小集合：

- `DomainInvariantException`
- `InfrastructureException`
- `ExternalDependencyException`

### 5.1 `DomainInvariantException`

用于表示：

- 领域对象被创建到非法状态
- 聚合内部状态被破坏
- 出现理论上不可能成立的条件

这类异常通常代表：

- bug
- 漏校验
- 领域规则实现错误

### 5.2 `InfrastructureException`

用于包装技术依赖错误，例如：

- 文件读写失败
- 目录访问失败
- 序列化失败
- 外部工具执行失败

要求：

- 必须保留 `InnerException`
- 必须带上足够上下文，例如路径、Bundle 名、操作类型

### 5.3 `ExternalDependencyException`

用于包装：

- 第三方 Unity 解析库
- 外部进程
- 外部服务

这样上层能区分：

- 是自己的逻辑问题
- 还是依赖组件失效

## 6. 分层处理规范

## 6.1 Domain 层

Domain 层规则：

- 不记录日志
- 不做本地化
- 不依赖 UI
- 不返回最终展示文案

推荐做法：

- 可预期规则失败优先通过 `Result` 或工厂方法失败返回
- 不变量破坏时抛 `DomainInvariantException`

示例：

- `AssetId.Create(...)` 参数非法，可返回失败结果
- `WorkspaceSession.ApplyChange(...)` 若内部状态不可能成立，可抛 `DomainInvariantException`

## 6.2 Application 层

Application 层是错误处理的主汇聚层。

职责：

- 组织 Use Case
- 将 Domain 失败转换为统一 `Result`
- 将基础设施异常包装或转换为统一 `Result`
- 决定哪些失败要向 UI 暴露，哪些需要中断

建议规则：

- 业务可预期失败统一返回 `Result.Failure`
- 对于 `OperationCanceledException`，统一转换为取消状态
- 对于已知基础设施异常，转换为标准 `AppError`
- 对于真正未知异常，记录上下文后继续抛出

Application 层不应做的事：

- 拼用户可见错误文本
- 直接弹窗
- 直接显示 MessageBox

## 6.3 Infrastructure 层

Infrastructure 层规则：

- 不做最终用户提示
- 不直接更新 UI
- 不吞异常

建议做法：

- 捕获第三方异常
- 包装为 `InfrastructureException` 或 `ExternalDependencyException`
- 附加操作上下文
- 继续抛给 Application

不要这样做：

- `catch (Exception) { return false; }`
- `catch (Exception ex) { throw ex; }`

必须这样做：

- `throw;`
- 或 `throw new InfrastructureException(..., ex);`

## 6.4 Presentation.WinUI 层

Presentation 层规则：

- 负责把 `Result` 转成可展示状态
- 负责把 `AppError` 映射为本地化消息
- 负责兜底未处理异常

推荐做法：

- ViewModel 调用 Use Case
- 对 `Result.Failure` 显示本地化提示
- 对 `Cancelled` 不提示错误，最多静默结束
- 对未处理异常走统一异常入口

补充约束：

- 对“无需用户进一步决策”的即时反馈，统一走 Shell 级 `IAppNotificationService`
- Toast 宿主和动画应集中在通用展示控件中维护，不要在各页面重复复制一套提示状态
- 需要用户确认、覆盖、删除、重置的流程仍使用 `ContentDialog` 或页面内联校验
- `ContentDialog` 内的表单校验不要再复用 Shell 级 toast，避免被弹窗层级遮挡
- 表单内联校验应直接落在具体字段上，例如红色边框、字段下方错误文本、只读展示框占位提示
- 交互提示音等轻量反馈应通过通用服务统一触发，不要在控件内部直接散落 `SystemSounds` 等实现细节

## 7. 什么时候 `try-catch`

必须强调：不是每一层、每个方法都要写 `try-catch`。

推荐原则：

- 只在能“处理、转换、补充上下文”的地方捕获异常
- 不能处理的异常不要捕获
- 不要写空 `catch`

适合捕获的位置：

- 基础设施适配器
- Use Case 边界
- UI 命令边界
- 全局异常入口

不适合到处捕获的位置：

- 普通 getter
- 简单状态转换
- 纯映射函数
- ValueObject 只读逻辑

## 8. 用户取消不是异常

本项目里很多操作都可能被用户取消：

- 取消打开文件
- 取消保存路径
- 取消替换确认
- 取消长任务

这些都不应视为错误异常。

建议规则：

- 用 `IsCancelled`
- 或专门的取消结果
- 不写错误日志
- 不弹错误提示

最多记录：

- `Info` 级日志
- 状态栏提示“已取消”

## 9. 批处理与部分失败

批处理不能只返回一个笼统的成功或失败。

建议为批处理定义独立结果模型，例如：

```csharp
public sealed record BatchResult<TItem>(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<TItem> SuccessItems,
    IReadOnlyList<BatchErrorItem> FailureItems);
```

适用场景：

- 批量提取
- 批量替换
- 批量保存

规范建议：

- 单项失败不应直接中断整个批处理，除非是致命错误
- 每项失败都要带错误码和上下文
- UI 最终显示汇总结果和失败明细

## 10. 日志规范

## 10.1 日志和提示分离

必须明确：

- 日志给开发和诊断
- 提示给用户

不能直接把异常 `Message` 当用户提示。

当前推荐分工：

- 非阻断即时反馈：`toast`
- 阻断确认与高风险决策：`dialog`
- 表单上下文修正：页面内联校验
- 表单校验提示音：通用交互反馈服务

## 10.2 结构化日志字段

建议日志至少带这些上下文：

- `OperationId`
- `WorkspaceId`
- `BundleId`
- `AssetId`
- `ErrorCode`
- `ExceptionType`
- `ActionName`

这样后续定位问题会容易很多。

## 10.3 日志级别建议

- `Trace`
  - 细粒度调试信息
- `Information`
  - 正常操作流
- `Warning`
  - 可恢复失败
- `Error`
  - 当前操作失败
- `Critical`
  - 程序无法继续或可能损坏工作区

## 10.4 日志内容建议

推荐：

```text
Operation=ReplaceAssetFromWorkspace
WorkspaceId=Session-01
BundleId=battle.ab
AssetId=102938
ErrorCode=Asset.TypeMismatch
Message=Asset replacement validation failed.
```

不推荐：

```text
替换失败了！！！
```

## 11. 本地化与用户提示

错误提示必须和多语言机制对齐。

推荐规则：

- `Application` 返回 `ErrorCode` / `MessageToken`
- `Presentation` 根据资源文件组装最终文本
- 参数格式化由 UI 层完成

例如：

```text
Code = Asset.TypeMismatch
MessageToken = Error.Asset.TypeMismatch
Parameters = { SourceType: Texture2D, TargetType: TextAsset }
```

然后由 UI 层读取资源：

- `Error.Asset.TypeMismatch`

生成最终提示。

### 11.1 当前已落地的 M8 错误码

当前在提取与工作区替换骨架中，已经正式使用以下错误码与消息键：

- `Asset.Replace.NoCompatibleSource`
  - `MessageToken = Error.Asset.Replace.NoCompatibleSource`
  - 用于当前资源在工作区中没有可用同类型替换源时
- `Asset.Replace.TypeMismatch`
  - `MessageToken = Error.Asset.Replace.TypeMismatch`
  - 用于替换源与目标资源类型不兼容时
- `Asset.Replace.NoAppliedChange`
  - `MessageToken = Error.Asset.Replace.NoAppliedChange`
  - 用于当前资源没有已应用替换、因此无法撤销时
- `Asset.Replace.SourceFilePathRequired`
  - `MessageToken = Error.Asset.Replace.SourceFilePathRequired`
  - 用于文件替换命令缺少来源路径时
- `Asset.Replace.NoBatchCandidates`
  - `MessageToken = Error.Asset.Replace.NoBatchCandidates`
  - 用于当前工作区在给定来源筛选条件下没有可执行批量替换骨架的候选资源时
- `Asset.Extract.NoBatchCandidates`
  - `MessageToken = Error.Asset.Extract.NoBatchCandidates`
  - 用于当前工作区在给定筛选条件下没有可执行批量提取的候选资源时
- `Asset.Extract.NotSupported`
  - `MessageToken = Error.Asset.Extract.NotSupported`
  - 用于当前资源存在但没有提取能力时
- `Asset.Extract.PayloadUnavailable`
  - `MessageToken = Error.Asset.Extract.PayloadUnavailable`
  - 用于预览 / 导出管线未能生成可写出的 payload 时
- `Asset.Extract.WriteFailed`
  - `MessageToken = Error.Asset.Extract.WriteFailed`
  - 用于提取 payload 写出到目标文件失败时
- `Bundle.Save.NoCandidates`
  - `MessageToken = Error.Bundle.Save.NoCandidates`
  - 用于当前工作区没有可执行保存 / 导出任务的资源包输出计划时
- `Asset.EditDraft.WorkspaceKeyRequired`
  - `MessageToken = Error.Asset.EditDraft.WorkspaceKeyRequired`
  - 用于草稿恢复候选解析缺少工作区范围时
- `Asset.EditDraft.ReferenceRequired`
  - `MessageToken = Error.Asset.EditDraft.ReferenceRequired`
  - 用于草稿恢复候选解析遇到空草稿引用时
- `Asset.EditDraft.IdRequired`
  - `MessageToken = Error.Asset.EditDraft.IdRequired`
  - 用于草稿恢复候选解析遇到缺少草稿 id 的引用时
- `Asset.EditDraft.ModeRequired`
  - `MessageToken = Error.Asset.EditDraft.ModeRequired`
  - 用于草稿恢复候选解析遇到缺少草稿模式的引用时
- `Asset.EditDraft.NotFound`
  - `MessageToken = Error.Asset.EditDraft.NotFound`
  - 用于轻量草稿引用指向的草稿元数据不存在时
- `Asset.EditDraft.SourceMismatch`
  - `MessageToken = Error.Asset.EditDraft.SourceMismatch`
  - 用于轻量草稿引用的来源指纹与草稿元数据不一致时
- `Asset.EditDraft.Expired`
  - `MessageToken = Error.Asset.EditDraft.Expired`
  - 用于轻量草稿引用指向的草稿元数据已过期时

另外，M8 当前已接入真实文件选择器，相关约束如下：

- 用户在文件选择器中主动取消：
  - 视为取消，不写错误日志，不弹错误提示
- 文件选择器初始化失败：
  - 由 Presentation 层记录本地化错误日志并显示通用失败提示
  - 不把底层异常消息直接展示给用户

不允许：

- 在 Use Case 中直接返回“资源类型不匹配”
- 在基础设施层直接抛出中文异常作为用户提示

## 12. 全局异常入口

WinUI 项目建议至少统一兜底以下入口：

- `App.UnhandledException`
- `TaskScheduler.UnobservedTaskException`
- `AppDomain.CurrentDomain.UnhandledException`

这些入口的职责：

- 记录 `Critical` 日志
- 生成诊断信息
- 向用户展示通用错误页或通用错误提示
- 防止异常静默丢失

统一提示建议：

- 使用本地化通用消息
- 提供“查看日志”或“复制诊断信息”
- 不直接显示原始堆栈

## 13. ViewModel 规范

ViewModel 中建议统一包装一个安全执行方法，例如：

```csharp
await RunSafeAsync(
    operationName: "OpenWorkspace",
    action: () => _openWorkspaceHandler.HandleAsync(command, ct));
```

这个包装方法负责：

- 设置忙碌状态
- 清理旧错误
- 接收 `Result`
- 显示本地化错误
- 记录异常日志
- 对未知异常走统一兜底

但注意：

- 不要把所有业务判断塞进这个包装器
- 包装器只负责边界行为

## 14. 建议的错误码命名方式

推荐使用稳定的分段式命名：

- `Workspace.Open.FileNotFound`
- `Workspace.Open.ParseFailed`
- `Asset.Replace.TypeMismatch`
- `Asset.Replace.ValidationFailed`
- `Asset.Extract.OutputConflict`
- `Bundle.Save.OutputConflict`
- `Bundle.Save.WriteFailed`
- `System.ExternalDependency.UnityParserFailed`

命名原则：

- 先领域，再动作，再原因
- 不要混入展示文案
- 不能频繁改名

## 15. 测试建议

至少应覆盖以下测试：

- `Result` 成功失败行为测试
- 错误码映射测试
- 批处理部分失败测试
- 异常包装测试
- 用户取消流程测试
- 本地化消息键存在性测试

尤其要测：

- 未知异常是否能被正确记录
- 已知错误是否不会误走全局异常
- `Cancelled` 是否不会被当作错误提示

## 16. 推荐的最终执行规则

后续实现默认遵循下面这套规则：

1. 可预期失败优先返回 `Result`
2. 非预期问题才抛异常
3. `Application` 层负责把失败统一收口
4. `Infrastructure` 层负责包装技术异常，不吞掉异常
5. `Presentation` 层负责本地化提示和全局兜底
6. 用户取消不是异常
7. 日志和用户提示必须分离
8. 用户可见错误文本不得硬编码
