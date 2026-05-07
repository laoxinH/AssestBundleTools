using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class SpineAtlasPageSelectorView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SpineAtlasPageSelectorViewModel),
            typeof(SpineAtlasPageSelectorView),
            new PropertyMetadata(null));

    public SpineAtlasPageSelectorView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public SpineAtlasPageSelectorViewModel? ViewModel
    {
        get => (SpineAtlasPageSelectorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<SpineAtlasPageSelectionRequestedEventArgs>? SelectionRequested;

    private void ApplyLocalizedText()
    {
        AtlasPageSelectorLabelText.Text = UiText.Get(ResourceKeys.InspectorSpineGroupAtlasPageSelectorLabel);
        ToolTipService.SetToolTip(
            AtlasPageSelectorComboBox,
            UiText.Get(ResourceKeys.InspectorSpineGroupAtlasPageSelectorToolTip));
    }

    private void OnAtlasPageSelectorComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: SpineAtlasPageOptionViewModel option } ||
            ReferenceEquals(option, ViewModel?.SelectedOption))
        {
            return;
        }

        SelectionRequested?.Invoke(this, new SpineAtlasPageSelectionRequestedEventArgs(option));
    }
}

public sealed class SpineAtlasPageSelectionRequestedEventArgs : EventArgs
{
    public SpineAtlasPageSelectionRequestedEventArgs(SpineAtlasPageOptionViewModel option)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
    }

    public SpineAtlasPageOptionViewModel Option { get; }
}
