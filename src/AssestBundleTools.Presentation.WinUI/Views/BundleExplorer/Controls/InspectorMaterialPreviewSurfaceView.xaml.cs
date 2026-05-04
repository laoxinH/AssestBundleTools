using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorMaterialPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewMaterialViewModel),
            typeof(InspectorMaterialPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorMaterialPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewMaterialViewModel? ViewModel
    {
        get => (InspectorPreviewMaterialViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
