using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorImagePreviewSurfaceView : UserControl
{
    private InspectorPreviewImageViewModel? _observedViewModel;

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
        view.RefreshPreviewImageViewport();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        RefreshPreviewImageViewport();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachObservedViewModel(_observedViewModel);
    }

    private void AttachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
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

    private void OnPreviewImageViewportHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPreviewImageViewport();
    }

    private void RefreshPreviewImageViewport()
    {
        PreviewImageViewportChanged?.Invoke(
            this,
            new PreviewImageViewportChangedEventArgs(
                PreviewImageViewportHost.ActualWidth,
                PreviewImageViewportHost.ActualHeight));
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
