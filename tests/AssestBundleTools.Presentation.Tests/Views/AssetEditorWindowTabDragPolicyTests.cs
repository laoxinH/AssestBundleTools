using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowTabDragPolicyTests
{
    [Fact]
    public void CanCreateNewWindowFromDroppedTab_ShouldRejectSingleTabWindow()
    {
        var assetTab = CreateAssetDocumentTab("asset-1");
        var sourceGroup = new AssetDocumentGroupViewModel();
        sourceGroup.AddAssetTab(assetTab, activate: true);

        var canCreateNewWindow = AssetEditorWindowTabDragPolicy.CanCreateNewWindowFromDroppedTab(
            sourceGroup,
            assetTab);

        Assert.False(canCreateNewWindow);
    }

    [Fact]
    public void CanCreateNewWindowFromDroppedTab_ShouldAllowMultiTabWindow()
    {
        var assetTab = CreateAssetDocumentTab("asset-1");
        var sourceGroup = new AssetDocumentGroupViewModel();
        sourceGroup.AddAssetTab(assetTab, activate: true);
        sourceGroup.AddAssetTab(CreateAssetDocumentTab("asset-2"), activate: false);

        var canCreateNewWindow = AssetEditorWindowTabDragPolicy.CanCreateNewWindowFromDroppedTab(
            sourceGroup,
            assetTab);

        Assert.True(canCreateNewWindow);
    }

    private static AssetDocumentTabViewModel CreateAssetDocumentTab(string assetId)
    {
        var textSet = InspectorInformationTextSetFactory.Create(new TestLocalizationService());
        return new AssetDocumentTabViewModel(
            new BundleId("bundle-a"),
            new AssetId(assetId),
            assetId,
            textSet);
    }
}
