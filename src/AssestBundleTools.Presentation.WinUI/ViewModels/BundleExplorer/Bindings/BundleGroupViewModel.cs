using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示左侧工作区树中的一组 Bundle 节点。
/// </summary>
public sealed class BundleGroupViewModel : ObservableObject
{
    public BundleGroupViewModel(string displayName, IEnumerable<BundleNodeViewModel> bundles)
    {
        DisplayName = displayName;
        Bundles = new List<BundleNodeViewModel>(bundles);
    }

    public string DisplayName { get; }

    public IReadOnlyList<BundleNodeViewModel> Bundles { get; }
}
