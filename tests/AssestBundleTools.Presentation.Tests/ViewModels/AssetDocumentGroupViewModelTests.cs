using AssestBundleTools.Application.Assets.Dtos;
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
    public void AddSpineGroupTab_ShouldShareDocumentTabsWithAssetTabsAndActivateSpineDocument()
    {
        var assetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var spineGroupTab = CreateSpineGroupDocumentTab("bundle-a", "spine-hero");
        var group = new AssetDocumentGroupViewModel();

        group.AddAssetTab(assetTab, activate: true);
        group.AddSpineGroupTab(spineGroupTab, activate: true);

        Assert.Collection(
            group.DocumentTabs,
            tab => Assert.Same(assetTab, tab.AssetTab),
            tab => Assert.Same(spineGroupTab, tab.SpineGroupTab));
        Assert.Null(group.ActiveAssetTab);
        Assert.Same(spineGroupTab, group.ActiveSpineGroupTab);
        Assert.Same(spineGroupTab, group.ActiveDocumentTab?.SpineGroupTab);
        Assert.Single(group.AssetTabs);
        Assert.Single(group.SpineGroupTabs);
    }

    [Fact]
    public void ReorderDocumentTabs_ShouldUpdateKindSpecificCollections()
    {
        var firstAssetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var spineGroupTab = CreateSpineGroupDocumentTab("bundle-a", "spine-hero");
        var secondAssetTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(firstAssetTab, activate: true);
        group.AddSpineGroupTab(spineGroupTab, activate: false);
        group.AddAssetTab(secondAssetTab, activate: false);

        var orderedTabs = new[]
        {
            group.DocumentTabs.Single(tab => tab.Wraps(secondAssetTab)),
            group.DocumentTabs.Single(tab => tab.Wraps(spineGroupTab)),
            group.DocumentTabs.Single(tab => tab.Wraps(firstAssetTab))
        };

        Assert.True(group.ReorderDocumentTabs(orderedTabs));

        Assert.Collection(
            group.DocumentTabs,
            tab => Assert.Same(secondAssetTab, tab.AssetTab),
            tab => Assert.Same(spineGroupTab, tab.SpineGroupTab),
            tab => Assert.Same(firstAssetTab, tab.AssetTab));
        Assert.Collection(
            group.AssetTabs,
            tab => Assert.Same(secondAssetTab, tab),
            tab => Assert.Same(firstAssetTab, tab));
        Assert.Same(spineGroupTab, Assert.Single(group.SpineGroupTabs));
    }

    [Fact]
    public void DetachDocumentTabs_ShouldPreserveMixedOrderAndActiveDocument()
    {
        var assetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var spineGroupTab = CreateSpineGroupDocumentTab("bundle-a", "spine-hero");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(assetTab, activate: true);
        group.AddSpineGroupTab(spineGroupTab, activate: true);

        var transfer = group.DetachDocumentTabs();

        Assert.NotNull(transfer);
        Assert.Empty(group.DocumentTabs);
        Assert.Empty(group.AssetTabs);
        Assert.Empty(group.SpineGroupTabs);
        Assert.Null(group.ActiveDocumentTab);
        Assert.Collection(
            transfer.DocumentTabs,
            tab => Assert.Same(assetTab, tab.AssetTab),
            tab => Assert.Same(spineGroupTab, tab.SpineGroupTab));
        Assert.Same(spineGroupTab, transfer.ActiveDocumentTab.SpineGroupTab);
    }

    [Fact]
    public void DetachDocumentTab_ShouldActivateSpineFallbackWhenActiveAssetMovesOut()
    {
        var assetTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var spineGroupTab = CreateSpineGroupDocumentTab("bundle-a", "spine-hero");
        var group = new AssetDocumentGroupViewModel();
        group.AddSpineGroupTab(spineGroupTab, activate: false);
        group.AddAssetTab(assetTab, activate: true);

        var assetDocumentTab = group.DocumentTabs.Single(tab => tab.Wraps(assetTab));

        Assert.True(group.DetachDocumentTab(assetDocumentTab));

        Assert.DoesNotContain(assetTab, group.AssetTabs);
        Assert.Same(spineGroupTab, Assert.Single(group.SpineGroupTabs));
        Assert.Same(spineGroupTab, group.ActiveDocumentTab?.SpineGroupTab);
        Assert.Null(group.ActiveAssetTab);
        Assert.Same(spineGroupTab, group.ActiveSpineGroupTab);
    }

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
    public void DetachAssetTabs_ShouldPreserveOrderActiveAndPreviewState()
    {
        var firstTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var secondTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(firstTab, activate: true);
        group.AddAssetTab(secondTab, activate: true);
        _ = firstTab.BeginPreviewRequest();
        firstTab.Inspector.SetPreviewLoading(ResourceKeys.InspectorPreviewLoadingText);

        var transfer = group.DetachAssetTabs();

        Assert.NotNull(transfer);
        Assert.Empty(group.AssetTabs);
        Assert.Null(group.ActiveAssetTab);
        Assert.Collection(
            transfer.AssetTabs,
            tab => Assert.Same(firstTab, tab),
            tab => Assert.Same(secondTab, tab));
        Assert.Same(secondTab, transfer.ActiveAssetTab);
        Assert.True(firstTab.Inspector.Preview.IsPreviewLoading);

        firstTab.CancelPreviewRequest();
    }

    [Fact]
    public void AddAssetTabs_ShouldDeduplicateExistingTabsAndActivateTransferredActive()
    {
        var existingTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var duplicateTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var newTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        var group = new AssetDocumentGroupViewModel();
        group.AddAssetTab(existingTab, activate: true);
        _ = duplicateTab.BeginPreviewRequest();
        duplicateTab.Inspector.SetPreviewLoading(ResourceKeys.InspectorPreviewLoadingText);

        group.AddAssetTabs(new AssetDocumentGroupTransfer([duplicateTab, newTab], duplicateTab));

        Assert.Collection(
            group.AssetTabs,
            tab => Assert.Same(existingTab, tab),
            tab => Assert.Same(newTab, tab));
        Assert.Same(existingTab, group.ActiveAssetTab);
        Assert.False(duplicateTab.Inspector.Preview.IsPreviewLoading);
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

    [Fact]
    public void BundleDocumentTabDetachAssetTabs_ShouldPreserveOrderActiveAndPreviewState()
    {
        var bundleTab = new BundleDocumentTabViewModel(
            new BundleId("bundle-a"),
            displayName: "bundle-a.ab",
            logicalPath: "bundle-a.ab",
            isPreview: false);
        var firstTab = CreateAssetDocumentTab("bundle-a", "asset-1");
        var secondTab = CreateAssetDocumentTab("bundle-a", "asset-2");
        bundleTab.AddAssetTab(firstTab, activate: true);
        bundleTab.AddAssetTab(secondTab, activate: true);
        _ = firstTab.BeginPreviewRequest();
        firstTab.Inspector.SetPreviewLoading(ResourceKeys.InspectorPreviewLoadingText);

        var transfer = bundleTab.DetachAssetTabs();

        Assert.NotNull(transfer);
        Assert.Empty(bundleTab.AssetTabs);
        Assert.Null(bundleTab.ActiveAssetTab);
        Assert.Collection(
            transfer.AssetTabs,
            tab => Assert.Same(firstTab, tab),
            tab => Assert.Same(secondTab, tab));
        Assert.Same(secondTab, transfer.ActiveAssetTab);
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

    private static SpineAssetGroupDocumentTabViewModel CreateSpineGroupDocumentTab(string bundleId, string groupId)
    {
        var textSet = InspectorInformationTextSetFactory.Create(new TestLocalizationService());
        return new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                groupId,
                new BundleId(bundleId),
                groupId,
                groupId,
                Array.Empty<SpineAssetGroupPartDto>(),
                Array.Empty<SpineAssetGroupPartDto>(),
                Array.Empty<SpineAssetGroupPartDto>(),
                Array.Empty<SpineAssetGroupDiagnosticDto>()),
            textSet);
    }
}
