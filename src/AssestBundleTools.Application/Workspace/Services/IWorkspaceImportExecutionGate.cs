namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责在 Application 导入链路内部对同一工作区导入作业做互斥保护。
/// </summary>
public interface IWorkspaceImportExecutionGate
{
    Task<TResult> ExecuteExclusiveAsync<TResult>(
        string workspaceKey,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
