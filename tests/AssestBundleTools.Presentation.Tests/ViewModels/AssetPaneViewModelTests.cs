using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetPaneViewModelTests
{
    [Fact]
    public void ReplaceAssetTypeOptions_ShouldNotMutateCollection_WhenOptionsAreEquivalent()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);
        viewModel.SelectedAssetTypeOption = viewModel.AssetTypeOptions[1];
        var collectionChangedCount = 0;
        viewModel.AssetTypeOptions.CollectionChanged += (_, _) => collectionChangedCount++;

        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);

        Assert.Equal(0, collectionChangedCount);
        Assert.Same(viewModel.AssetTypeOptions[1], viewModel.SelectedAssetTypeOption);
    }

    [Fact]
    public void ReplaceAssetTypeOptions_ShouldRebindSelectedItem_ToCurrentCollectionInstance()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);
        viewModel.SelectedAssetTypeOption = viewModel.AssetTypeOptions[0];

        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 4),
            new AssetTypeOptionViewModel("Material", AssetType.Material, Count: 1)
        ]);

        Assert.Same(viewModel.AssetTypeOptions[0], viewModel.SelectedAssetTypeOption);
        Assert.Equal("All", viewModel.SelectedAssetTypeOption?.DisplayName);
    }

    [Fact]
    public void ReplaceAssetTypeOptions_ShouldExposeSynchronizationFlag_WhileCollectionChanges()
    {
        var viewModel = new AssetPaneViewModel();
        var sawSynchronizationFlag = false;
        viewModel.AssetTypeOptions.CollectionChanged += (_, _) =>
        {
            sawSynchronizationFlag |= viewModel.IsReplacingAssetTypeOptions;
        };

        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Shader", AssetType.Unknown, "Shader", Count: 1)
        ]);

        Assert.True(sawSynchronizationFlag);
        Assert.False(viewModel.IsReplacingAssetTypeOptions);
    }

    [Fact]
    public void SelectedAssetTypeOption_ShouldNotNotify_WhenSameOptionIsWrittenBack()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);
        var option = viewModel.AssetTypeOptions[1];
        viewModel.SelectedAssetTypeOption = option;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        viewModel.SelectedAssetTypeOption = option;

        Assert.Empty(changedProperties);
    }

    [Fact]
    public void RestoreViewState_ShouldKeepDynamicFilterKey_BeforeDynamicOptionsAreLoaded()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);

        viewModel.RestoreViewState(
            searchKeyword: string.Empty,
            selectedAssetTypeFilterKey: "Unknown:Shader",
            showModifiedOnly: false,
            showPreviewableOnly: false);

        Assert.Equal("Unknown:Shader", viewModel.SelectedAssetTypeFilterKey);
        Assert.Equal(AssetType.Unknown, viewModel.SelectedAssetType);
        Assert.Equal("Shader", viewModel.SelectedAssetTypeDisplayNameFilter);
        Assert.Contains(viewModel.AssetTypeOptions, option => option.FilterKey == "Unknown:Shader");
    }

    [Fact]
    public void ReplaceAssetTypeOptions_ShouldPromoteRestoredDynamicFilter_ToLoadedFacetOption()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2)
        ]);
        viewModel.RestoreViewState(
            searchKeyword: string.Empty,
            selectedAssetTypeFilterKey: "Unknown:Shader",
            showModifiedOnly: false,
            showPreviewableOnly: false);

        viewModel.ReplaceAssetTypeOptions(
        [
            new AssetTypeOptionViewModel("All", null),
            new AssetTypeOptionViewModel("Texture2D", AssetType.Texture2D, Count: 2),
            new AssetTypeOptionViewModel("Shader", AssetType.Unknown, "Shader", Count: 3)
        ]);

        Assert.Equal("Unknown:Shader", viewModel.SelectedAssetTypeFilterKey);
        Assert.Equal("Shader (3)", viewModel.SelectedAssetTypeOption?.DisplayText);
        Assert.Equal(3, viewModel.SelectedAssetTypeOption?.Count);
    }

    [Fact]
    public void ViewMode_ShouldExposeSpineGroupListAndHideAssetOnlyControls()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true
        };
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");
        viewModel.UpdateSelection([firstAsset, secondAsset], firstAsset);
        viewModel.ReplaceSpineGroups("ui.ab", [CreateSpineGroupEntry("hero")], "no spine");

        viewModel.ViewMode = AssetPaneViewMode.SpineGroups;

        Assert.True(viewModel.IsSpineGroupViewMode);
        Assert.Equal(Visibility.Collapsed, viewModel.AssetFilterVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.AssetListVisibility);
        Assert.Equal(Visibility.Visible, viewModel.SpineGroupListVisibility);
        Assert.False(viewModel.CanBatchExtractSelectedAssets);
        Assert.Equal(Visibility.Collapsed, viewModel.MultiSelectionQuickActionsVisibility);
    }

    [Fact]
    public void SelectedAsset_ShouldCollapseToSingleSelectionState_WhenSetProgrammatically()
    {
        var viewModel = new AssetPaneViewModel();
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");
        viewModel.UpdateSelection([firstAsset, secondAsset], firstAsset);

        viewModel.SelectedAsset = secondAsset;

        Assert.Same(secondAsset, viewModel.SelectedAsset);
        Assert.Collection(viewModel.SelectedAssets, selected => Assert.Same(secondAsset, selected));
        Assert.False(firstAsset.IsSelected);
        Assert.True(secondAsset.IsSelected);
        Assert.Equal(1, viewModel.SelectedAssetCount);
        Assert.False(viewModel.HasMultipleSelection);
    }

    [Fact]
    public void RestoreViewState_ShouldRestoreSpineGroupViewMode()
    {
        var viewModel = new AssetPaneViewModel();

        viewModel.RestoreViewState(
            searchKeyword: "hero",
            selectedAssetTypeFilterKey: null,
            showModifiedOnly: false,
            showPreviewableOnly: false,
            viewMode: AssetPaneViewMode.SpineGroups);

        Assert.Equal(AssetPaneViewMode.SpineGroups, viewModel.ViewMode);
        Assert.True(viewModel.IsSpineGroupViewMode);
        Assert.Equal(Visibility.Collapsed, viewModel.AssetFilterVisibility);
    }

    private static AssetListEntryViewModel CreateAssetEntry(string assetId)
    {
        return new AssetListEntryViewModel(
            new AssetListItemDto(
                new AssetId(assetId),
                new BundleId("bundle-ui"),
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

    private static SpineAssetGroupEntryViewModel CreateSpineGroupEntry(string groupId)
    {
        return new SpineAssetGroupEntryViewModel(
            new SpineAssetGroupDto(
                groupId,
                new BundleId("bundle-ui"),
                "hero",
                "characters/hero",
                SkeletonAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.Skeleton,
                        new BundleId("bundle-ui"),
                        new AssetId("hero-json"),
                        "ui.ab",
                        "hero.json",
                        "Assets/hero.json",
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
}
