using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetPaneViewModel
{
    public void ConfigurePane(PaneId paneId, string paneTitle, string emptyStateText)
    {
        ConfigurePane(paneId, paneTitle, paneTitle, emptyStateText);
    }

    public void ConfigurePane(PaneId paneId, string paneTitle, string emptyStateTitleText, string emptyStateText)
    {
        PaneId = paneId;
        PaneTitle = paneTitle;
        EmptyStateTitleText = emptyStateTitleText;
        EmptyStateText = emptyStateText;
    }
}
