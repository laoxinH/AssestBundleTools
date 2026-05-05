using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorMonoBehaviourPreviewSurfaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewMonoBehaviourViewModel),
            typeof(InspectorMonoBehaviourPreviewSurfaceView),
            new PropertyMetadata(null));

    public InspectorMonoBehaviourPreviewSurfaceView()
    {
        InitializeComponent();
    }

    public InspectorPreviewMonoBehaviourViewModel? ViewModel
    {
        get => (InspectorPreviewMonoBehaviourViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
