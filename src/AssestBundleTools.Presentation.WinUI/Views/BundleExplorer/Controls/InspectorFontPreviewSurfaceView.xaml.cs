using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorFontPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewFontViewModel),
            typeof(InspectorFontPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorFontPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewFontViewModel? ViewModel
    {
        get => (InspectorPreviewFontViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
