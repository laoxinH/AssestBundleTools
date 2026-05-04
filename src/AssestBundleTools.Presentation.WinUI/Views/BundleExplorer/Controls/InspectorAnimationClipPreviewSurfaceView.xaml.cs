using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorAnimationClipPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewAnimationClipViewModel),
            typeof(InspectorAnimationClipPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorAnimationClipPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewAnimationClipViewModel? ViewModel
    {
        get => (InspectorPreviewAnimationClipViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
