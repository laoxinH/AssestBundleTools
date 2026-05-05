using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetDocumentGroupViewModelTests
{
    [Fact]
    public void DetachAssetTab_ShouldPreservePreviewStateAndSelectFallback()
    {
        var firstTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var secondTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(firstTab, activate: true);
        group.AddAssetTab(secondTab, activate: false);
        _ = firstTab.BeginPreviewRequest();
        firstTab.Inspector.SetPreviewLoading(ResourceKeys.InspectorPreviewLoadingText);

        Assert.True(group.DetachAssetTab(firstTab));

        Assert.DoesNotContain(firstTab, group.AssetTabs);
        Assert.Same(secondTab, group.ActiveAssetTab);
        Assert.True(firstTab.Inspector.Preview.IsPreviewLoading);

        firstTab.CancelPreviewRequest();
    }

    [Fact]
    public void TryGetAssetTab_ShouldMatchBundleAndAssetIdentity()
    {
        var firstTab = CreateAssetDocumentTab("bundle-a", "shared-asset");
        var secondTab = CreateAssetDocumentTab("bundle-b", "shared-asset");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(firstTab, activate: true);
        group.AddAssetTab(secondTab, activate: false);

        Assert.True(group.TryGetAssetTab(new BundleId("bundle-b"), new AssetId("shared-asset"), out var resolvedTab));

        Assert.Same(secondTab, resolvedTab);
    }

    [Fact]
    public void BundleDocumentTabDetachAssetTab_ShouldNotCancelMovedPreview()
    {
        var bundleTab = new BundleDocumentTabViewModel(
            new BundleId("bundle-a"),
            displayName: "bundle-a.ab",
            logicalPath: "bundle-a.ab",
            isPreview: false);
        var firstTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var secondTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        bundleTab.AddAssetTab(firstTab, activate: true);
        bundleTab.AddAssetTab(secondTab, activate: false);
        _ = firstTab.BeginPreviewRequest();
        firstTab.Inspector.SetPreviewLoading(ResourceKeys.InspectorPreviewLoadingText);

        Assert.True(bundleTab.DetachAssetTab(firstTab));

        Assert.DoesNotContain(firstTab, bundleTab.AssetTabs);
        Assert.Same(secondTab, bundleTab.ActiveAssetTab);
        Assert.True(firstTab.Inspector.Preview.IsPreviewLoading);

        firstTab.CancelPreviewRequest();
    }

    private static AssetDocumentTabViewModel CreateAssetDocumentTab(string bundleId, string assetId)
    {
        var textSet = InspectorInformationTextSetFactory.Create(new TestLocalizationService());
        return new AssetDocumentTabViewModel(
            new BundleId(bundleId),
            new AssetId(assetId),
            assetId,
            textSet);
    }
}
