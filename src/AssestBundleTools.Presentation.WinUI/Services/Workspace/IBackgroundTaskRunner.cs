namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IBackgroundTaskRunner
{
    Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
