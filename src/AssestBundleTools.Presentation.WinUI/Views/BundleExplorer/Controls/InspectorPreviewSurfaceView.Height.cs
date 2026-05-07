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

    public double FixedPreviewSurfaceHeight
    {
        get => (double)GetValue(FixedPreviewSurfaceHeightProperty);
        set => SetValue(FixedPreviewSurfaceHeightProperty, value);
    }

    public double PreviewSurfaceHeight =>
        ViewModel?.Preview.UsesFixedPreviewSurfaceHeight == true
            ? NormalizeFixedPreviewSurfaceHeight(FixedPreviewSurfaceHeight)
            : double.NaN;

    public double PreviewSurfaceMinHeight =>
        ViewModel?.Preview.UsesFixedPreviewSurfaceHeight == true
            ? NormalizeFixedPreviewSurfaceHeight(FixedPreviewSurfaceHeight)
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
}
