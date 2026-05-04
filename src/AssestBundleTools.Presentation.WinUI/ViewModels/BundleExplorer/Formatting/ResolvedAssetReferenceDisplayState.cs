using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

public sealed record ResolvedAssetReferenceDisplayState(
    string Text,
    ResolvedAssetNavigationTarget Target,
    string OpenButtonText,
    string OpenButtonToolTipText);
