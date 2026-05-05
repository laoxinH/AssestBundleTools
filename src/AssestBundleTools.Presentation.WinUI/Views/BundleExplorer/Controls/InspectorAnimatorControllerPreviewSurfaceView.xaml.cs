using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorAnimatorControllerPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewAnimatorControllerViewModel),
            typeof(InspectorAnimatorControllerPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorAnimatorControllerPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewAnimatorControllerViewModel? ViewModel
    {
        get => (InspectorPreviewAnimatorControllerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
