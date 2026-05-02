namespace AssestBundleTools.Presentation.WinUI.Services;

public sealed class ThreadPoolBackgroundTaskRunner : IBackgroundTaskRunner
{
    public Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return Task.Run(() => operation(cancellationToken), cancellationToken);
    }
}
