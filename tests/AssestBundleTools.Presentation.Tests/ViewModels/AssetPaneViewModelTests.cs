using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
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
    public void LoadingState_ShouldUseProgressEmptyStateUntilItemsArrive()
    {
        var viewModel = new AssetPaneViewModel();

        viewModel.BeginAssetListLoading("ui.ab", "Loading assets", "loading");

        Assert.True(viewModel.IsListLoading);
        Assert.Equal("Loading assets", viewModel.EmptyStateTitleText);
        Assert.Equal(Visibility.Visible, viewModel.EmptyStateVisibility);
        Assert.Equal(Visibility.Visible, viewModel.EmptyStateLoadingVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.EmptyStateIconVisibility);

        viewModel.ReplaceItems(
            "ui.ab",
            [CreateAssetEntry("asset-1")],
            "No assets",
            "empty",
            preferredSelectedAssetId: null);

        Assert.False(viewModel.IsListLoading);
        Assert.Equal("No assets", viewModel.EmptyStateTitleText);
        Assert.Equal(Visibility.Collapsed, viewModel.EmptyStateVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.EmptyStateLoadingVisibility);

        viewModel.ReplaceItems(
            "ui.ab",
            [],
            "No assets",
            "empty",
            preferredSelectedAssetId: null);

        Assert.False(viewModel.IsListLoading);
        Assert.Equal("No assets", viewModel.EmptyStateTitleText);
        Assert.Equal(Visibility.Visible, viewModel.EmptyStateVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.EmptyStateLoadingVisibility);
        Assert.Equal(Visibility.Visible, viewModel.EmptyStateIconVisibility);
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
        Assert.False(viewModel.IsMultiSelectionMode);
    }

    [Fact]
    public void EnterMultiSelectionMode_ShouldShowQuickActions_ForSingleAssetSelection()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true
        };
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");
        viewModel.UpdateSelection([firstAsset], firstAsset);

        viewModel.EnterMultiSelectionMode();

        Assert.True(viewModel.IsMultiSelectionMode);
        Assert.Equal(1, viewModel.SelectedItemCount);
        Assert.True(viewModel.CanBatchExtractSelectedAssets);
        Assert.Equal(Visibility.Visible, viewModel.MultiSelectionQuickActionsVisibility);
    }

    [Fact]
    public void SelectAllVisibleItems_ShouldEnterAssetMultiSelectionMode()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true
        };
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");

        viewModel.SelectAllVisibleItems();

        Assert.True(viewModel.IsMultiSelectionMode);
        Assert.Equal(2, viewModel.SelectedAssetCount);
        Assert.Collection(
            viewModel.SelectedAssets,
            selected => Assert.Same(firstAsset, selected),
            selected => Assert.Same(secondAsset, selected));
        Assert.True(firstAsset.IsSelected);
        Assert.True(secondAsset.IsSelected);
    }

    [Fact]
    public void InvertVisibleSelection_ShouldToggleAssetSelection_WithoutLeavingMultiSelectionMode()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true
        };
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");
        viewModel.UpdateSelection([firstAsset], firstAsset);
        viewModel.EnterMultiSelectionMode();

        viewModel.InvertVisibleSelection();

        Assert.True(viewModel.IsMultiSelectionMode);
        Assert.Collection(viewModel.SelectedAssets, selected => Assert.Same(secondAsset, selected));
        Assert.False(firstAsset.IsSelected);
        Assert.True(secondAsset.IsSelected);
    }

    [Fact]
    public void ExitMultiSelectionMode_ShouldCollapseAssetSelectionToPrimary()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true
        };
        var firstAsset = CreateAssetEntry("asset-1");
        var secondAsset = CreateAssetEntry("asset-2");
        viewModel.ReplaceItems("ui.ab", [firstAsset, secondAsset], "empty");
        viewModel.UpdateSelection([firstAsset, secondAsset], secondAsset);

        viewModel.ExitMultiSelectionMode();

        Assert.False(viewModel.IsMultiSelectionMode);
        Assert.Same(secondAsset, viewModel.SelectedAsset);
        Assert.Collection(viewModel.SelectedAssets, selected => Assert.Same(secondAsset, selected));
        Assert.False(firstAsset.IsSelected);
        Assert.True(secondAsset.IsSelected);
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

    [Fact]
    public void TrySelectSpineGroup_ShouldSelectMatchingGroup()
    {
        var viewModel = new AssetPaneViewModel();
        var firstGroup = CreateSpineGroupEntry("hero");
        var secondGroup = CreateSpineGroupEntry("enemy");
        viewModel.ReplaceSpineGroups("ui.ab", [firstGroup, secondGroup], "no spine");

        var selected = viewModel.TrySelectSpineGroup("enemy");

        Assert.True(selected);
        Assert.Same(secondGroup, viewModel.SelectedSpineGroup);
    }

    [Fact]
    public void ApplyAssetOperationState_ShouldUpdateExistingAssetRowWithoutReplacingList()
    {
        var viewModel = new AssetPaneViewModel();
        var assetEntry = CreateAssetEntry("asset-1");
        viewModel.ReplaceItems("ui.ab", [assetEntry], "empty");
        viewModel.SelectedAsset = assetEntry;
        var extractedAtUtc = new DateTimeOffset(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

        var updated = viewModel.ApplyAssetOperationState(
            CreateAssetDetails(assetEntry.Item.BundleId, assetEntry.Item.AssetId, isModified: true, extractedAtUtc),
            "Modified");

        Assert.True(updated);
        Assert.Same(assetEntry, Assert.Single(viewModel.Items));
        Assert.Same(assetEntry, viewModel.SelectedAsset);
        Assert.True(assetEntry.Item.IsModified);
        Assert.Equal(extractedAtUtc, assetEntry.Item.LastExtractedAtUtc);
        Assert.Equal("Modified", assetEntry.StateText);
    }

    [Fact]
    public void ApplyAssetOperationState_ShouldNotRebuildFilteredList_WhenVisibilityIsUnchanged()
    {
        var viewModel = new AssetPaneViewModel
        {
            ShowModifiedOnly = true
        };
        var assetEntry = CreateAssetEntry("asset-1", isModified: true);
        viewModel.ReplaceItems("ui.ab", [assetEntry], "empty");
        viewModel.SelectedAsset = assetEntry;
        var collectionChanged = false;
        viewModel.Items.CollectionChanged += (_, _) => collectionChanged = true;
        var extractedAtUtc = new DateTimeOffset(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

        var updated = viewModel.ApplyAssetOperationState(
            CreateAssetDetails(assetEntry.Item.BundleId, assetEntry.Item.AssetId, isModified: true, extractedAtUtc),
            "Modified");

        Assert.True(updated);
        Assert.False(collectionChanged);
        Assert.Same(assetEntry, Assert.Single(viewModel.Items));
        Assert.Same(assetEntry, viewModel.SelectedAsset);
        Assert.Equal(extractedAtUtc, assetEntry.Item.LastExtractedAtUtc);
    }

    [Fact]
    public void UpdateSpineGroupSelection_ShouldPreserveMultiSelection()
    {
        var viewModel = new AssetPaneViewModel
        {
            ViewMode = AssetPaneViewMode.SpineGroups
        };
        var firstGroup = CreateSpineGroupEntry("hero");
        var secondGroup = CreateSpineGroupEntry("enemy");
        viewModel.ReplaceSpineGroups("ui.ab", [firstGroup, secondGroup], "no spine");

        viewModel.UpdateSpineGroupSelection([firstGroup, secondGroup], secondGroup);

        Assert.Same(secondGroup, viewModel.SelectedSpineGroup);
        Assert.Collection(
            viewModel.SelectedSpineGroups,
            selected => Assert.Same(firstGroup, selected),
            selected => Assert.Same(secondGroup, selected));
        Assert.True(firstGroup.IsSelected);
        Assert.True(secondGroup.IsSelected);
        Assert.Equal(2, viewModel.SelectedSpineGroupCount);
        Assert.True(viewModel.IsMultiSelectionMode);
    }

    [Fact]
    public void SpineGroupMultiSelection_ShouldUseSpineCount_AndEnableBatchExtraction()
    {
        var viewModel = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true,
            ViewMode = AssetPaneViewMode.SpineGroups
        };
        var firstGroup = CreateSpineGroupEntry("hero");
        var secondGroup = CreateSpineGroupEntry("enemy");
        viewModel.ReplaceSpineGroups("ui.ab", [firstGroup, secondGroup], "no spine");

        viewModel.SelectAllVisibleItems();

        Assert.True(viewModel.IsMultiSelectionMode);
        Assert.Equal(2, viewModel.SelectedItemCount);
        Assert.Equal(Visibility.Visible, viewModel.MultiSelectionQuickActionsVisibility);
        Assert.True(viewModel.CanBatchExtractSelectedAssets);
    }

    [Fact]
    public void UpdateSpineGroupSelection_ShouldKeepPrimarySelection_WhenSelectedItemsAreEmpty()
    {
        var viewModel = new AssetPaneViewModel();
        var group = CreateSpineGroupEntry("hero");
        viewModel.ReplaceSpineGroups("ui.ab", [group], "no spine");

        viewModel.UpdateSpineGroupSelection([], group);

        Assert.Same(group, viewModel.SelectedSpineGroup);
        Assert.Collection(viewModel.SelectedSpineGroups, selected => Assert.Same(group, selected));
        Assert.True(group.IsSelected);
        Assert.Equal(1, viewModel.SelectedSpineGroupCount);
    }

    [Fact]
    public void TrySelectSpineGroup_ShouldReturnFalse_WhenGroupIsMissing()
    {
        var viewModel = new AssetPaneViewModel();
        viewModel.ReplaceSpineGroups("ui.ab", [CreateSpineGroupEntry("hero")], "no spine");

        var selected = viewModel.TrySelectSpineGroup("missing");

        Assert.False(selected);
        Assert.Null(viewModel.SelectedSpineGroup);
    }

    private static AssetListEntryViewModel CreateAssetEntry(string assetId, bool isModified = false)
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
                IsModified: isModified),
            "Texture2D",
            "128 B",
            isModified ? "Modified" : "Original");
    }

    private static AssetDetailsDto CreateAssetDetails(
        BundleId bundleId,
        AssetId assetId,
        bool isModified,
        DateTimeOffset? lastExtractedAtUtc)
    {
        return new AssetDetailsDto(
            WorkspaceId: new WorkspaceId("workspace-ui"),
            BundleId: bundleId,
            AssetId: assetId,
            Name: $"{assetId.Value}.png",
            AssetPath: $"Assets/{assetId.Value}.png",
            ContainerPath: $"Assets/{assetId.Value}.png",
            AssetType: AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: null,
            PathId: 1,
            IsModified: isModified,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: lastExtractedAtUtc,
            Dependencies: Array.Empty<AssetDependencyDto>());
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
