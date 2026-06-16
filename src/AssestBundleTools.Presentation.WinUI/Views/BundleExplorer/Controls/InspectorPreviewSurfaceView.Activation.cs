using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorPreviewSurfaceView
{
    public static readonly DependencyProperty IsPreviewActiveProperty =
        DependencyProperty.Register(
            nameof(IsPreviewActive),
            typeof(bool),
            typeof(InspectorPreviewSurfaceView),
            new PropertyMetadata(true, OnIsPreviewActiveChanged));

    public bool IsPreviewActive
    {
        get => (bool)GetValue(IsPreviewActiveProperty);
        set => SetValue(IsPreviewActiveProperty, value);
    }

    private static void OnIsPreviewActiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorPreviewSurfaceView view)
        {
            view.UpdateBindingsNow();
            view.RefreshAudioPreviewSourceBinding();
        }
    }
}
