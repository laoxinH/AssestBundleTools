using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private bool _isPlaybackSliderScrubbing;

    private void RegisterPlaybackSliderInputHandlers()
    {
        PlaybackSlider.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPlaybackSliderPointerPressed),
            handledEventsToo: true);
        PlaybackSlider.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnPlaybackSliderPointerReleased),
            handledEventsToo: true);
        PlaybackSlider.AddHandler(
            UIElement.PointerCanceledEvent,
            new PointerEventHandler(OnPlaybackSliderPointerReleased),
            handledEventsToo: true);
    }

    private void OnPlaybackSliderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPreviewRunning || ViewModel is null)
        {
            return;
        }

        _isPlaybackSliderScrubbing = true;
        _lastTickUtc = DateTimeOffset.UtcNow;
    }

    private void OnPlaybackSliderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CompletePlaybackSliderScrub();
    }

    private void CompletePlaybackSliderScrub()
    {
        if (!_isPlaybackSliderScrubbing)
        {
            return;
        }

        _isPlaybackSliderScrubbing = false;
        _lastTickUtc = DateTimeOffset.UtcNow;
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.DisplayPlaybackTime = PlaybackSlider.Value;
        if (ViewModel.HasRuntimePlayback)
        {
            QueueRuntimeFrameRefresh();
            return;
        }

        if (SyncRenderFrame())
        {
            InvalidatePreviewSurface();
        }
    }

    private void CancelPlaybackSliderScrub()
    {
        _isPlaybackSliderScrubbing = false;
    }
}
