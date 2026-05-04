using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorMaterialAppearanceSampleView : UserControl
{
    private static readonly MaterialPreviewAppearanceViewModel EmptyViewModel = new();

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MaterialPreviewAppearanceViewModel),
            typeof(InspectorMaterialAppearanceSampleView),
            new PropertyMetadata(null));

    public InspectorMaterialAppearanceSampleView()
    {
        InitializeComponent();
    }

    public MaterialPreviewAppearanceViewModel ViewModel
    {
        get => (MaterialPreviewAppearanceViewModel?)GetValue(ViewModelProperty) ?? EmptyViewModel;
        set => SetValue(ViewModelProperty, value);
    }
}
