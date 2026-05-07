using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private async void OnInspectorSpineAtlasPageSelectionRequested(
        object sender,
        SpineAtlasPageSelectionRequestedEventArgs e)
    {
        await ViewModel.SelectActiveSpineGroupAtlasPageAsync(e.Option);
    }
}
