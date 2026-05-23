using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorImagePreviewSurfaceView : UserControl
{
    private const double WheelZoomInScale = 1.12d;
    private const double WheelZoomOutScale = 1d / WheelZoomInScale;

    private InspectorPreviewImageViewModel? _observedViewModel;
    private bool _isDragging;
    private bool _isEditingSpriteOverlay;
    private bool _isBindingsUpdateQueued;
    private bool _isUpdatingSpriteEditGeometryFields;
    private Point _lastPointerPosition;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewImageViewModel),
            typeof(InspectorImagePreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorImagePreviewSurfaceView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedText();
    }

    public InspectorPreviewImageViewModel? ViewModel
    {
        get => (InspectorPreviewImageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<PreviewImageViewportChangedEventArgs>? PreviewImageViewportChanged;

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorImagePreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewImageViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPreviewImageViewModel);
        view.UpdateBindingsNow();
        view.RefreshPreviewImageViewport();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        UpdateBindingsNow();
        RefreshPreviewImageViewport();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachObservedViewModel(_observedViewModel);
        _isDragging = false;
        _isEditingSpriteOverlay = false;
    }

    private void AttachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        _observedViewModel.DisplayOptions.PropertyChanged += OnObservedDisplayOptionsPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel.DisplayOptions.PropertyChanged -= OnObservedDisplayOptionsPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        QueueBindingsUpdate();

        if (e.PropertyName is not (
            nameof(InspectorPreviewImageViewModel.ImageSource) or
            nameof(InspectorPreviewImageViewModel.CanvasWidth) or
            nameof(InspectorPreviewImageViewModel.CanvasHeight)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshPreviewImageViewport();
            return;
        }

        DispatcherQueue.TryEnqueue(RefreshPreviewImageViewport);
    }

    private void OnObservedDisplayOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        QueueBindingsUpdate();
    }

    private void OnPreviewImageViewportHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPreviewImageViewport();
    }

    private void OnPreviewImagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewImageViewportHost);
        if (ViewModel?.Visibility != Visibility.Visible || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (ViewModel.IsSpriteEditModeActive)
        {
            if (!ViewModel.TryBeginSpriteEditDrag(point.Position.X, point.Position.Y))
            {
                return;
            }

            _isEditingSpriteOverlay = true;
            PreviewImageViewportHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _lastPointerPosition = point.Position;
        PreviewImageViewportHost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPreviewImagePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var currentPosition = e.GetCurrentPoint(PreviewImageViewportHost).Position;
        if (_isEditingSpriteOverlay)
        {
            ViewModel.UpdateSpriteEditDrag(currentPosition.X, currentPosition.Y);
            e.Handled = true;
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        ViewModel.PanBy(
            currentPosition.X - _lastPointerPosition.X,
            currentPosition.Y - _lastPointerPosition.Y);
        _lastPointerPosition = currentPosition;
        e.Handled = true;
    }

    private void OnPreviewImagePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ViewModel?.CompleteSpriteEditDrag();
        _isEditingSpriteOverlay = false;
        _isDragging = false;
        PreviewImageViewportHost.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPreviewImagePointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.Visibility != Visibility.Visible)
        {
            return;
        }

        if (ViewModel.IsSpriteEditModeActive)
        {
            e.Handled = true;
            return;
        }

        var point = e.GetCurrentPoint(PreviewImageViewportHost);
        ViewModel.ZoomBy(
            point.Properties.MouseWheelDelta > 0 ? WheelZoomInScale : WheelZoomOutScale,
            point.Position.X,
            point.Position.Y);
        e.Handled = true;
    }

    private void OnSpriteEditGeometryNumberBoxValueChanged(
        NumberBox sender,
        NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingSpriteEditGeometryFields ||
            ViewModel?.IsSpriteEditModeActive != true)
        {
            return;
        }

        ViewModel.UpdateSpriteEditGeometryFromInput(
            SpriteEditCropXNumberBox.Value,
            SpriteEditCropYNumberBox.Value,
            SpriteEditCropWidthNumberBox.Value,
            SpriteEditCropHeightNumberBox.Value,
            SpriteEditPivotXNumberBox.Value,
            SpriteEditPivotYNumberBox.Value);
    }

    private void RefreshPreviewImageViewport()
    {
        PreviewImageViewportHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(
                0d,
                0d,
                Math.Max(0d, PreviewImageViewportHost.ActualWidth),
                Math.Max(0d, PreviewImageViewportHost.ActualHeight))
        };
        PreviewImageViewportChanged?.Invoke(
            this,
            new PreviewImageViewportChangedEventArgs(
                PreviewImageViewportHost.ActualWidth,
                PreviewImageViewportHost.ActualHeight));
    }

    private void UpdateBindingsNow()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindingsNow);
            return;
        }

        _isBindingsUpdateQueued = false;
        _isUpdatingSpriteEditGeometryFields = true;
        try
        {
            Bindings.Update();
        }
        finally
        {
            _isUpdatingSpriteEditGeometryFields = false;
        }
    }

    private void QueueBindingsUpdate()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBindingsUpdate);
            return;
        }

        if (_isBindingsUpdateQueued)
        {
            return;
        }

        _isBindingsUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(UpdateBindingsNow))
        {
            _isBindingsUpdateQueued = false;
        }
    }

    private void ApplyLocalizedText()
    {
        SpriteEditGeometryTitleText.Text = UiText.Get(ResourceKeys.InspectorSpriteEditGeometryTitle);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropXLabelText,
            SpriteEditCropXNumberBox,
            ResourceKeys.InspectorSpriteEditCropXLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropYLabelText,
            SpriteEditCropYNumberBox,
            ResourceKeys.InspectorSpriteEditCropYLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropWidthLabelText,
            SpriteEditCropWidthNumberBox,
            ResourceKeys.InspectorSpriteEditCropWidthLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropHeightLabelText,
            SpriteEditCropHeightNumberBox,
            ResourceKeys.InspectorSpriteEditCropHeightLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditPivotXLabelText,
            SpriteEditPivotXNumberBox,
            ResourceKeys.InspectorSpriteEditPivotXLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditPivotYLabelText,
            SpriteEditPivotYNumberBox,
            ResourceKeys.InspectorSpriteEditPivotYLabel);
    }

    private static void ApplySpriteEditNumberBoxText(
        TextBlock label,
        NumberBox numberBox,
        string resourceKey)
    {
        var text = UiText.Get(resourceKey);
        label.Text = text;
        AutomationProperties.SetName(numberBox, text);
    }
}

public sealed class PreviewImageViewportChangedEventArgs : EventArgs
{
    public PreviewImageViewportChangedEventArgs(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}
