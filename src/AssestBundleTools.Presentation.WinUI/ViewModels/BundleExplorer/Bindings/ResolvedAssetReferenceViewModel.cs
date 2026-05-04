using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class ResolvedAssetReferenceViewModel
{
    public ResolvedAssetReferenceViewModel(ResolvedAssetReferenceDisplayState reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        Text = reference.Text;
        Target = reference.Target;
        OpenButtonText = reference.OpenButtonText;
        OpenButtonToolTipText = reference.OpenButtonToolTipText;
    }

    public string Text { get; }

    public ResolvedAssetNavigationTarget Target { get; }

    public string OpenButtonText { get; }

    public string OpenButtonToolTipText { get; }

    public Visibility OpenButtonVisibility => Visibility.Visible;
}
