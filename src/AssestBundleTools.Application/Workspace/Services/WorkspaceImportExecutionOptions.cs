namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 表示一次工作区导入执行所使用的运行时选项。
/// </summary>
public sealed record WorkspaceImportExecutionOptions(
    int BundleWorkerCount);
