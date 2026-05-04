using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

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
}
