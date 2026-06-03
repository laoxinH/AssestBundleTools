using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorPreviewSurfaceView
{
    public static readonly DependencyProperty FixedPreviewSurfaceHeightProperty =
        DependencyProperty.Register(
            nameof(FixedPreviewSurfaceHeight),
            typeof(double),
            typeof(InspectorPreviewSurfaceView),
            new PropertyMetadata(320d, OnFixedPreviewSurfaceHeightPropertyChanged));

    public static readonly DependencyProperty UseAvailablePreviewSpaceProperty =
        DependencyProperty.Register(
            nameof(UseAvailablePreviewSpace),
            typeof(bool),
            typeof(InspectorPreviewSurfaceView),
            new PropertyMetadata(false, OnUseAvailablePreviewSpacePropertyChanged));

    public static readonly DependencyProperty AvailablePreviewSurfaceMinHeightProperty =
        DependencyProperty.Register(
            nameof(AvailablePreviewSurfaceMinHeight),
            typeof(double),
            typeof(InspectorPreviewSurfaceView),
            new PropertyMetadata(0d, OnAvailablePreviewSurfaceMinHeightPropertyChanged));

    public double FixedPreviewSurfaceHeight
    {
        get => (double)GetValue(FixedPreviewSurfaceHeightProperty);
        set => SetValue(FixedPreviewSurfaceHeightProperty, value);
    }

    public bool UseAvailablePreviewSpace
    {
        get => (bool)GetValue(UseAvailablePreviewSpaceProperty);
        set => SetValue(UseAvailablePreviewSpaceProperty, value);
    }

    public double AvailablePreviewSurfaceMinHeight
    {
        get => (double)GetValue(AvailablePreviewSurfaceMinHeightProperty);
        set => SetValue(AvailablePreviewSurfaceMinHeightProperty, value);
    }

    public bool UseCompactSpineViewport => !UseAvailablePreviewSpace;

    public double PreviewSurfaceHeight =>
        !UseAvailablePreviewSpace && ViewModel?.Preview.UsesFixedPreviewSurfaceHeight == true
            ? NormalizeFixedPreviewSurfaceHeight(FixedPreviewSurfaceHeight)
            : double.NaN;

    public double PreviewSurfaceMinHeight =>
        !UseAvailablePreviewSpace && ViewModel?.Preview.UsesFixedPreviewSurfaceHeight == true
            ? NormalizeFixedPreviewSurfaceHeight(FixedPreviewSurfaceHeight)
            : UseAvailablePreviewSpace
                ? NormalizeAvailablePreviewSurfaceMinHeight(AvailablePreviewSurfaceMinHeight)
                : 0d;

    private static void OnFixedPreviewSurfaceHeightPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorPreviewSurfaceView view)
        {
            view.RefreshPreviewSurfaceHeightBinding();
        }
    }

    private static void OnUseAvailablePreviewSpacePropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorPreviewSurfaceView view)
        {
            view.RefreshPreviewSurfaceHeightBinding();
        }
    }

    private static void OnAvailablePreviewSurfaceMinHeightPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorPreviewSurfaceView view)
        {
            view.RefreshPreviewSurfaceHeightBinding();
        }
    }

    private void RefreshPreviewSurfaceHeightBinding()
    {
        UpdateBindingsNow();
    }

    private static double NormalizeFixedPreviewSurfaceHeight(double fixedPreviewSurfaceHeight)
    {
        return double.IsFinite(fixedPreviewSurfaceHeight) && fixedPreviewSurfaceHeight > 0d
            ? fixedPreviewSurfaceHeight
            : 320d;
    }

    private static double NormalizeAvailablePreviewSurfaceMinHeight(double availablePreviewSurfaceMinHeight)
    {
        return double.IsFinite(availablePreviewSurfaceMinHeight) && availablePreviewSurfaceMinHeight > 0d
            ? availablePreviewSurfaceMinHeight
            : 0d;
    }
}
