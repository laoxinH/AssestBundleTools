using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerActiveDocumentLocatorTests
{
    [Fact]
    public async Task LocateActiveDocumentAsync_ShouldSwitchToAssetsAndSelectActiveAsset()
    {
        var bundleId = new BundleId("bundle-ui");
        var workspace = CreateWorkspace(bundleId, out var bundleTab);
        var pane = new AssetPaneViewModel
        {
            ViewMode = AssetPaneViewMode.SpineGroups
        };
        var targetAsset = CreateAssetEntry(bundleId, "asset-2");
        var assetTab = new AssetDocumentTabViewModel(
            bundleId,
            targetAsset.Item.AssetId,
            targetAsset.DisplayName,
            CreateTextSet());
        bundleTab.AddAssetTab(assetTab, activate: true);
        workspace.NotifyAssetDocumentHostStateChanged();
        var refreshCallCount = 0;
        var preserveCurrentDocumentTab = false;
        var captureCallCount = 0;
        var locator = new BundleExplorerActiveDocumentLocator(
            workspace,
            pane,
            preserve =>
            {
                refreshCallCount++;
                preserveCurrentDocumentTab = preserve;
                pane.ReplaceItems("ui.ab", [targetAsset], "empty");
                return Task.CompletedTask;
            },
            () => captureCallCount++);

        var located = await locator.LocateActiveDocumentAsync();

        Assert.True(located);
        Assert.Equal(AssetPaneViewMode.Assets, pane.ViewMode);
        Assert.Same(targetAsset, pane.SelectedAsset);
        Assert.Equal(1, refreshCallCount);
        Assert.True(preserveCurrentDocumentTab);
        Assert.Equal(1, captureCallCount);
    }

    [Fact]
    public async Task LocateActiveDocumentAsync_ShouldSwitchToSpineGroupsAndSelectActiveGroup()
    {
        var bundleId = new BundleId("bundle-ui");
        var workspace = CreateWorkspace(bundleId, out var bundleTab);
        var pane = new AssetPaneViewModel
        {
            ViewMode = AssetPaneViewMode.Assets
        };
        var targetGroup = CreateSpineGroupEntry(bundleId, "hero");
        var spineGroupTab = new SpineAssetGroupDocumentTabViewModel(targetGroup.Item, CreateTextSet());
        bundleTab.AddSpineGroupTab(spineGroupTab, activate: true);
        workspace.NotifyAssetDocumentHostStateChanged();
        var refreshCallCount = 0;
        var preserveCurrentDocumentTab = false;
        var captureCallCount = 0;
        var locator = new BundleExplorerActiveDocumentLocator(
            workspace,
            pane,
            preserve =>
            {
                refreshCallCount++;
                preserveCurrentDocumentTab = preserve;
                pane.ReplaceSpineGroups("ui.ab", [targetGroup], "no spine");
                return Task.CompletedTask;
            },
            () => captureCallCount++);

        var located = await locator.LocateActiveDocumentAsync();

        Assert.True(located);
        Assert.Equal(AssetPaneViewMode.SpineGroups, pane.ViewMode);
        Assert.Same(targetGroup, pane.SelectedSpineGroup);
        Assert.Equal(1, refreshCallCount);
        Assert.True(preserveCurrentDocumentTab);
        Assert.Equal(1, captureCallCount);
    }

    private static BundleExplorerDocumentWorkspaceViewModel CreateWorkspace(
        BundleId bundleId,
        out BundleDocumentTabViewModel bundleTab)
    {
        var workspace = new BundleExplorerDocumentWorkspaceViewModel();
        bundleTab = workspace.OpenBundle(
            new BundleNodeViewModel(
                bundleId,
                "ui.ab",
                "ui.ab",
                @"C:\Tests\ui.ab",
                assetCount: 2),
            openAsPinnedTab: true);
        workspace.SetActiveBundleTab(bundleTab);
        return workspace;
    }

    private static AssetListEntryViewModel CreateAssetEntry(BundleId bundleId, string assetId)
    {
        return new AssetListEntryViewModel(
            new AssetListItemDto(
                new AssetId(assetId),
                bundleId,
                $"{assetId}.png",
                $"Assets/{assetId}.png",
                AssetType.Texture2D,
                SizeInBytes: 128,
                PreferredDisplaySizeInBytes: null,
                HasPreview: true,
                IsModified: false),
            "Texture2D",
            "128 B",
            "Original");
    }

    private static SpineAssetGroupEntryViewModel CreateSpineGroupEntry(BundleId bundleId, string groupId)
    {
        return new SpineAssetGroupEntryViewModel(
            new SpineAssetGroupDto(
                groupId,
                bundleId,
                groupId,
                $"characters/{groupId}",
                SkeletonAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.Skeleton,
                        bundleId,
                        new AssetId($"{groupId}-json"),
                        "ui.ab",
                        $"{groupId}.json",
                        $"Assets/{groupId}.json",
                        AssetType.TextAsset,
                        "TextAsset")
                ],
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            "Skeleton 1 / Atlas 0",
            "0 pages",
            "Ready");
    }

    private static InspectorInformationTextSet CreateTextSet() =>
        InspectorInformationTextSetFactory.Create(new TestLocalizationService());
}
