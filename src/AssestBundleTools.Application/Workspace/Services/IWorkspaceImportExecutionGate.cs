namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责对同一工作区的导入处理、打开扫描和来源移除等互斥操作做串行化保护。
/// </summary>
public interface IWorkspaceImportExecutionGate
{
    Task<TResult> ExecuteExclusiveAsync<TResult>(
        string workspaceKey,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
