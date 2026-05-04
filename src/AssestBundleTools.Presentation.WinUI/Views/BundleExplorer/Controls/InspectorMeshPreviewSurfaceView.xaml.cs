using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorMeshPreviewSurfaceView : UserControl
{
    private const double WheelZoomInScale = 1.12d;
    private const double WheelZoomOutScale = 1d / WheelZoomInScale;

    private InspectorPreviewMeshViewModel? _observedViewModel;
    private CanvasBitmap? _textureOverlayBitmap;
    private byte[]? _textureOverlayBitmapSource;
    private bool _isLoadingTextureOverlayBitmap;
    private bool _isDragging;
    private Point _lastPointerPosition;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewMeshViewModel),
            typeof(InspectorMeshPreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorMeshPreviewSurfaceView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public InspectorPreviewMeshViewModel? ViewModel
    {
        get => (InspectorPreviewMeshViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorMeshPreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewMeshViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPreviewMeshViewModel);
        view.InvalidateMeshCanvas();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        InvalidateMeshCanvas();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachObservedViewModel(_observedViewModel);
        ClearTextureOverlayBitmap();
        _isDragging = false;
    }

    private void AttachObservedViewModel(InspectorPreviewMeshViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewMeshViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InspectorPreviewMeshViewModel.TextureOverlayContent))
        {
            ClearTextureOverlayBitmap();
        }

        if (e.PropertyName is not (
            nameof(InspectorPreviewMeshViewModel.Geometry) or
            nameof(InspectorPreviewMeshViewModel.TextureOverlayContent) or
            nameof(InspectorPreviewMeshViewModel.DisplayMode) or
            nameof(InspectorPreviewMeshViewModel.IsTextureOverlaySelected) or
            nameof(InspectorPreviewMeshViewModel.IsMeshGridSelected) or
            nameof(InspectorPreviewMeshViewModel.RotationX) or
            nameof(InspectorPreviewMeshViewModel.RotationY) or
            nameof(InspectorPreviewMeshViewModel.Zoom)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            InvalidateMeshCanvas();
            return;
        }

        DispatcherQueue.TryEnqueue(InvalidateMeshCanvas);
    }

    private void OnMeshCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateMeshCanvas();
    }

    private void OnMeshCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.IsWireframeSelected != true)
        {
            return;
        }

        _isDragging = true;
        _lastPointerPosition = e.GetCurrentPoint(MeshCanvas).Position;
        MeshCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMeshCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || ViewModel is null || !ViewModel.IsWireframeSelected)
        {
            return;
        }

        var currentPosition = e.GetCurrentPoint(MeshCanvas).Position;
        ViewModel.Rotate(
            currentPosition.X - _lastPointerPosition.X,
            currentPosition.Y - _lastPointerPosition.Y);
        _lastPointerPosition = currentPosition;
        e.Handled = true;
    }

    private void OnMeshCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        MeshCanvas.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnMeshCanvasPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var wheelDelta = e.GetCurrentPoint(MeshCanvas).Properties.MouseWheelDelta;
        ViewModel?.ZoomBy(wheelDelta > 0 ? WheelZoomInScale : WheelZoomOutScale);
        e.Handled = true;
    }

    private void OnResolvedAssetOpenButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ResolvedAssetNavigationTarget target })
        {
            ResolvedAssetOpenRequested?.Invoke(this, new ResolvedAssetOpenRequestedEventArgs(target));
        }
    }

    private void OnMeshCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var viewModel = ViewModel;
        var geometry = viewModel?.Geometry;
        if (geometry is null || geometry.Vertices.Count == 0)
        {
            return;
        }

        var width = (float)Math.Max(0d, sender.ActualWidth);
        var height = (float)Math.Max(0d, sender.ActualHeight);
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        if (viewModel?.IsUvSelected == true)
        {
            var textureOverlayBitmap = ResolveTextureOverlayBitmap(sender);
            InspectorMeshPreviewRenderer.DrawUv(
                args.DrawingSession,
                geometry,
                width,
                height,
                viewModel.Zoom,
                textureOverlayBitmap,
                viewModel.ShouldDrawMeshGrid);
        }
        else
        {
            var meshTextureBitmap = ResolveTextureOverlayBitmap(sender);
            InspectorMeshPreviewRenderer.DrawWireframe(
                args.DrawingSession,
                geometry,
                width,
                height,
                viewModel?.RotationX ?? 0d,
                viewModel?.RotationY ?? 0d,
                viewModel?.Zoom ?? 1d,
                meshTextureBitmap,
                viewModel?.ShouldDrawMeshGrid ?? true);
        }

        InspectorMeshOrientationGizmoRenderer.Draw(
            args.DrawingSession,
            width,
            height,
            viewModel?.RotationX ?? 0d,
            viewModel?.RotationY ?? 0d);
    }

    private CanvasBitmap? ResolveTextureOverlayBitmap(CanvasControl canvas)
    {
        if (ViewModel?.ShouldDrawTextureOverlay != true ||
            ViewModel.TextureOverlayContent is not { Length: > 0 } textureOverlayContent)
        {
            return null;
        }

        if (_textureOverlayBitmap is not null &&
            ReferenceEquals(_textureOverlayBitmapSource, textureOverlayContent))
        {
            return _textureOverlayBitmap;
        }

        if (!_isLoadingTextureOverlayBitmap)
        {
            _ = LoadTextureOverlayBitmapAsync(canvas, textureOverlayContent);
        }

        return null;
    }

    private async Task LoadTextureOverlayBitmapAsync(
        CanvasControl canvas,
        byte[] textureOverlayContent)
    {
        _isLoadingTextureOverlayBitmap = true;
        try
        {
            using var stream = await CreateTextureOverlayStreamAsync(textureOverlayContent);
            var bitmap = await CanvasBitmap.LoadAsync(canvas, stream);
            if (ViewModel?.TextureOverlayContent is not { } currentContent ||
                !ReferenceEquals(currentContent, textureOverlayContent))
            {
                bitmap.Dispose();
                return;
            }

            ClearTextureOverlayBitmap();
            _textureOverlayBitmap = bitmap;
            _textureOverlayBitmapSource = textureOverlayContent;
            InvalidateMeshCanvas();
        }
        catch
        {
            ClearTextureOverlayBitmap();
        }
        finally
        {
            _isLoadingTextureOverlayBitmap = false;
        }
    }

    private static async Task<IRandomAccessStream> CreateTextureOverlayStreamAsync(byte[] textureOverlayContent)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(textureOverlayContent);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }

    private void ClearTextureOverlayBitmap()
    {
        _textureOverlayBitmap?.Dispose();
        _textureOverlayBitmap = null;
        _textureOverlayBitmapSource = null;
    }

    private void InvalidateMeshCanvas()
    {
        MeshCanvas.Invalidate();
    }
}
