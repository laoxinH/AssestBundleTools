using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorGameObjectPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewGameObjectViewModel),
            typeof(InspectorGameObjectPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorGameObjectPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewGameObjectViewModel? ViewModel
    {
        get => (InspectorPreviewGameObjectViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
