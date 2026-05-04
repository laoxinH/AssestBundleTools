using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed class ResolvedAssetOpenRequestedEventArgs : EventArgs
{
    public ResolvedAssetOpenRequestedEventArgs(ResolvedAssetNavigationTarget target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public ResolvedAssetNavigationTarget Target { get; }
}
