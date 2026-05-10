using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private const double WheelZoomInScale = 1.12d;
    private const double WheelZoomOutScale = 1d / WheelZoomInScale;

    private readonly SpineAnimationPreviewViewportState _viewportState = new();
    private bool _isViewportDragging;
    private Point _lastViewportPointerPosition;

    private void OnPreviewViewportSquareFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPreviewViewportFrameSizing();
    }

    private void RefreshPreviewViewportFrameSizing()
    {
        if (!UseSquareViewport)
        {
            if (!double.IsNaN(PreviewViewportSquareFrame.Height))
            {
                PreviewViewportSquareFrame.Height = double.NaN;
            }

            InvalidatePreviewSurface();
            return;
        }

        var squareSide = Math.Max(0d, PreviewViewportSquareFrame.ActualWidth);
        if (!double.IsFinite(squareSide) ||
            Math.Abs(PreviewViewportSquareFrame.Height - squareSide) < 0.5d)
        {
            return;
        }

        PreviewViewportSquareFrame.Height = squareSide;
    }

    private void OnPreviewViewportHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PreviewViewportHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(
                0d,
                0d,
                Math.Max(0d, PreviewViewportHost.ActualWidth),
                Math.Max(0d, PreviewViewportHost.ActualHeight))
        };
        InvalidatePreviewSurface();
    }

    private void OnPreviewViewportPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewViewportHost);
        if (!CanManipulateViewport() || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isViewportDragging = true;
        _lastViewportPointerPosition = point.Position;
        PreviewViewportHost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPreviewViewportPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isViewportDragging || !CanManipulateViewport())
        {
            return;
        }

        var currentPosition = e.GetCurrentPoint(PreviewViewportHost).Position;
        if (_viewportState.PanBy(
                currentPosition.X - _lastViewportPointerPosition.X,
                currentPosition.Y - _lastViewportPointerPosition.Y))
        {
            ApplyViewportState();
        }

        _lastViewportPointerPosition = currentPosition;
        e.Handled = true;
    }

    private void OnPreviewViewportPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isViewportDragging = false;
        PreviewViewportHost.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPreviewViewportPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!CanManipulateViewport())
        {
            return;
        }

        var point = e.GetCurrentPoint(PreviewViewportHost);
        var scale = point.Properties.MouseWheelDelta > 0 ? WheelZoomInScale : WheelZoomOutScale;
        if (_viewportState.ZoomBy(
                scale,
                point.Position.X,
                point.Position.Y,
                PreviewViewportHost.ActualWidth,
                PreviewViewportHost.ActualHeight))
        {
            ApplyViewportState();
        }

        e.Handled = true;
    }

    private void OnResetViewportButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewportState.Reset())
        {
            ApplyViewportState();
        }
    }

    private void OnZoomOutViewportButtonClick(object sender, RoutedEventArgs e)
    {
        ZoomViewportFromCenter(WheelZoomOutScale);
    }

    private void OnZoomInViewportButtonClick(object sender, RoutedEventArgs e)
    {
        ZoomViewportFromCenter(WheelZoomInScale);
    }

    private void ZoomViewportFromCenter(double scale)
    {
        if (!CanManipulateViewport())
        {
            return;
        }

        if (_viewportState.ZoomBy(
                scale,
                PreviewViewportHost.ActualWidth / 2d,
                PreviewViewportHost.ActualHeight / 2d,
                PreviewViewportHost.ActualWidth,
                PreviewViewportHost.ActualHeight))
        {
            ApplyViewportState();
        }
    }

    private void ResetViewportState()
    {
        _isViewportDragging = false;
        if (_viewportState.Reset())
        {
            ApplyViewportState();
        }
    }

    private void RestoreViewportStateFromViewModel(
        InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        _isViewportDragging = false;
        if (_viewportState.Restore(
                viewModel?.ViewportZoom,
                viewModel?.ViewportPanX,
                viewModel?.ViewportPanY))
        {
            ApplyViewportState(syncViewModel: false);
            return;
        }

        ApplyViewportState(syncViewModel: false);
    }

    private void ApplyViewportState(bool syncViewModel = true)
    {
        _renderBackend.SetViewport(_viewportState.Current);
        if (syncViewModel)
        {
            ViewModel?.UpdateViewportState(
                _viewportState.Current.Zoom,
                _viewportState.Current.PanX,
                _viewportState.Current.PanY);
        }

        InvalidatePreviewSurface();
    }

    private bool CanManipulateViewport() =>
        _isPreviewRunning && ViewModel?.HasSpineAnimation == true;
}
