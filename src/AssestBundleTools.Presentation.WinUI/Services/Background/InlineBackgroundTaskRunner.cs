namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class InlineBackgroundTaskRunner : IBackgroundTaskRunner
{
    public static InlineBackgroundTaskRunner Instance { get; } = new();

    private InlineBackgroundTaskRunner()
    {
    }

    public Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation(cancellationToken);
    }
}
