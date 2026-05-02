namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 按仓库既定规则解析工作区导入 worker 并发度。
/// </summary>
public sealed class WorkspaceImportExecutionOptionsResolver : IWorkspaceImportExecutionOptionsResolver
{
    private readonly Func<int>? _configuredWorkerCountProvider;

    public WorkspaceImportExecutionOptionsResolver(Func<int>? configuredWorkerCountProvider = null)
    {
        _configuredWorkerCountProvider = configuredWorkerCountProvider;
    }

    public WorkspaceImportExecutionOptions ResolveOptions()
    {
        return new WorkspaceImportExecutionOptions(
            ResolveBundleWorkerCount(Environment.ProcessorCount, _configuredWorkerCountProvider?.Invoke()));
    }

    public static int ResolveBundleWorkerCount(int processorCount, int? configuredWorkerCount = null)
    {
        if (configuredWorkerCount is > 0)
        {
            return Math.Clamp(configuredWorkerCount.Value, 1, 4);
        }

        if (processorCount <= 1)
        {
            return 1;
        }

        if (processorCount <= 4)
        {
            return 2;
        }

        if (processorCount <= 6)
        {
            return 3;
        }

        return 4;
    }
}
