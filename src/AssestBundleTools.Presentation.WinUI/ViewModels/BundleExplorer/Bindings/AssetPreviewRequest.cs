namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed class AssetPreviewRequest : IDisposable
{
    private readonly CancellationTokenSource _cancellationSource = new();
    private bool _disposed;

    public AssetPreviewRequest(long version)
    {
        Version = version;
    }

    public long Version { get; }

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public void Cancel()
    {
        if (!_disposed && !_cancellationSource.IsCancellationRequested)
        {
            _cancellationSource.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellationSource.Dispose();
        _disposed = true;
    }
}
