using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 负责资产名称列的短延迟 Tooltip，避免把指针细节留在控件主类中。
/// </summary>
internal sealed class AssetPaneAssetNameToolTipCoordinator
{
    private const int AssetNameToolTipDelayMilliseconds = 320;

    private CancellationTokenSource? _assetNameToolTipDelayCancellationSource;
    private ToolTip? _currentAssetNameToolTip;
    private TextBlock? _currentAssetNameToolTipTarget;

    public async void OpenAfterDelay(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        Close();

        var cancellationSource = new CancellationTokenSource();
        _assetNameToolTipDelayCancellationSource = cancellationSource;

        try
        {
            await Task.Delay(AssetNameToolTipDelayMilliseconds, cancellationSource.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationSource.IsCancellationRequested ||
            !ReferenceEquals(_assetNameToolTipDelayCancellationSource, cancellationSource))
        {
            return;
        }

        Open(textBlock);
    }

    public void CloseFor(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        if (!ReferenceEquals(_currentAssetNameToolTipTarget, textBlock))
        {
            CancelPendingDelay();
            return;
        }

        Close();
    }

    public void Close()
    {
        CancelPendingDelay();

        if (_currentAssetNameToolTip is not null)
        {
            _currentAssetNameToolTip.IsOpen = false;
            _currentAssetNameToolTip = null;
        }

        _currentAssetNameToolTipTarget = null;
    }

    private void Open(TextBlock textBlock)
    {
        if (ToolTipService.GetToolTip(textBlock) is not ToolTip toolTip)
        {
            return;
        }

        _currentAssetNameToolTip = toolTip;
        _currentAssetNameToolTipTarget = textBlock;
        toolTip.IsOpen = true;
    }

    private void CancelPendingDelay()
    {
        if (_assetNameToolTipDelayCancellationSource is null)
        {
            return;
        }

        _assetNameToolTipDelayCancellationSource.Cancel();
        _assetNameToolTipDelayCancellationSource.Dispose();
        _assetNameToolTipDelayCancellationSource = null;
    }
}
