using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class MaterialPreviewSummaryCardViewModel
{
    public MaterialPreviewSummaryCardViewModel(MaterialPreviewSummaryCardDisplayState card)
    {
        TitleText = card.TitleText;
        ValueText = card.ValueText;
        DetailText = card.DetailText;
    }

    public string TitleText { get; }

    public string ValueText { get; }

    public string DetailText { get; }

    public Visibility DetailVisibility =>
        string.IsNullOrWhiteSpace(DetailText)
            ? Visibility.Collapsed
            : Visibility.Visible;
}
