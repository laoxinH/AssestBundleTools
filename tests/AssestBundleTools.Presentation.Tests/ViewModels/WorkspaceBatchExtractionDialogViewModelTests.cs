using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceBatchExtractionDialogViewModelTests
{
    [Fact]
    public void Constructor_ShouldSelectActiveBundle_AndAllCategoryByDefault()
    {
        var activeBundleId = new BundleId("bundle-ui");
        var viewModel = CreateViewModel(activeBundleId);

        Assert.Equal(2, viewModel.VisibleBundles.Count);
        Assert.True(viewModel.VisibleBundles.Single(bundle => bundle.BundleId.Equals(activeBundleId)).IsSelected);
        Assert.True(viewModel.CategoryOptions.Single(option => option.IsAllCategory).IsSelected);
        Assert.Equal(string.Empty, viewModel.ValidationText);
        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void ManualCategorySelection_ShouldClearAllCategory()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));
        viewModel.ApplyPreview(CreatePreview());
        var allOption = viewModel.CategoryOptions.Single(option => option.IsAllCategory);
        var textureOption = viewModel.CategoryOptions.Single(
            option => option.Category.AssetType == AssetType.Texture2D);

        textureOption.IsSelected = true;

        Assert.False(allOption.IsSelected);
        Assert.True(textureOption.IsSelected);
        Assert.Equal(
            new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D) },
            viewModel.SelectedCategories);
    }

    [Fact]
    public void AllCategorySelection_ShouldClearSpecificCategories()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));
        viewModel.ApplyPreview(CreatePreview());
        var allOption = viewModel.CategoryOptions.Single(option => option.IsAllCategory);
        var textureOption = viewModel.CategoryOptions.Single(
            option => option.Category.AssetType == AssetType.Texture2D);
        var spineOption = viewModel.CategoryOptions.Single(
            option => option.Category.Kind == WorkspaceBatchExtractionCategoryKind.SpineGroup);
        allOption.IsSelected = false;
        textureOption.IsSelected = true;
        spineOption.IsSelected = true;

        allOption.IsSelected = true;

        Assert.True(allOption.IsSelected);
        Assert.False(textureOption.IsSelected);
        Assert.False(spineOption.IsSelected);
        Assert.Equal(new[] { WorkspaceBatchExtractionCategoryDto.All }, viewModel.SelectedCategories);
    }

    [Fact]
    public void TryCreateSubmission_ShouldRequireBundleAndCategory()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));

        Assert.True(viewModel.TryCreateSubmission(out var submission));
        Assert.NotNull(submission);
        Assert.False(submission!.UseWorkspaceExtractionDirectory);
        Assert.Equal(new[] { new BundleId("bundle-ui") }, submission.SelectedBundleIds);
        Assert.Equal(new[] { WorkspaceBatchExtractionCategoryDto.All }, submission.SelectedCategories);
        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void TryCreateSubmission_ShouldCarryWorkspaceExtractionDirectoryChoice()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));
        viewModel.UseWorkspaceExtractionDirectory = true;

        Assert.True(viewModel.TryCreateSubmission(out var submission));
        Assert.NotNull(submission);
        Assert.True(submission!.UseWorkspaceExtractionDirectory);
    }

    [Fact]
    public void ApplyPreview_ShouldRebuildCategoryOptionsWithCandidateCounts()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));

        viewModel.ApplyPreview(CreatePreview());

        Assert.Equal(3, viewModel.CategoryOptions.Count);
        var textureOption = viewModel.CategoryOptions.Single(
            option => option.Category.AssetType == AssetType.Texture2D);
        Assert.Equal(2, textureOption.CandidateCount);
        Assert.Equal(
            $"{ResourceKeys.WorkspaceBatchExtractionDialogCategoryCandidateCountFormat}:2",
            textureOption.CandidateCountText);
    }

    [Fact]
    public void ApplyPreview_ShouldDisplayWorkspaceProvidedUnknownTypeName()
    {
        var viewModel = CreateViewModel(new BundleId("bundle-ui"));
        var preview = CreatePreview() with
        {
            CategoryPlans = new[]
            {
                new WorkspaceBatchExtractionCategoryPlanDto(
                    WorkspaceBatchExtractionCategoryDto.All,
                    CandidateCount: 1,
                    TotalSizeInBytes: 0),
                new WorkspaceBatchExtractionCategoryPlanDto(
                    WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader"),
                    CandidateCount: 1,
                    TotalSizeInBytes: 0)
            }
        };

        viewModel.ApplyPreview(preview);

        var shaderOption = viewModel.CategoryOptions.Single(
            option => option.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader"));
        Assert.Equal("Shader", shaderOption.DisplayName);
        Assert.DoesNotContain(
            viewModel.CategoryOptions,
            option => option.DisplayName == ResourceKeys.AssetTypeDisplayUnknown &&
                      option.Category.AssetType == AssetType.Unknown);
    }

    [Fact]
    public void BundleSearch_ShouldFilterVisibleBundlesWithoutChangingSelection()
    {
        var activeBundleId = new BundleId("bundle-ui");
        var viewModel = CreateViewModel(activeBundleId);

        viewModel.BundleSearchKeyword = "spine";

        var visibleBundle = Assert.Single(viewModel.VisibleBundles);
        Assert.Equal(new BundleId("bundle-spine"), visibleBundle.BundleId);
        Assert.Equal(new[] { activeBundleId }, viewModel.SelectedBundleIds);
    }

    private static WorkspaceBatchExtractionDialogViewModel CreateViewModel(BundleId? activeBundleId)
    {
        return new WorkspaceBatchExtractionDialogViewModel(
            new TestLocalizationService(),
            new[]
            {
                new BundleNodeViewModel(
                    new BundleId("bundle-ui"),
                    "ui.ab",
                    "characters/ui.ab",
                    @"C:\Bundles\ui.ab",
                    assetCount: 3),
                new BundleNodeViewModel(
                    new BundleId("bundle-spine"),
                    "spine.ab",
                    "characters/spine.ab",
                    @"C:\Bundles\spine.ab",
                    assetCount: 5)
            },
            activeBundleId);
    }

    private static WorkspaceBatchExtractionPreviewDto CreatePreview()
    {
        return new WorkspaceBatchExtractionPreviewDto(
            new WorkspaceId("workspace-ui"),
            "Workspace UI",
            AssetTypeFilter: null,
            SuggestedOutputDirectory: string.Empty,
            CandidateAssetCount: 3,
            CandidateBundleCount: 1,
            TotalSizeInBytes: 0,
            BundlePlans: Array.Empty<WorkspaceBatchExtractionBundlePlanDto>(),
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.All },
            CandidateSpineGroupCount: 1,
            CategoryPlans: new[]
            {
                new WorkspaceBatchExtractionCategoryPlanDto(
                    WorkspaceBatchExtractionCategoryDto.All,
                    CandidateCount: 4,
                    TotalSizeInBytes: 0),
                new WorkspaceBatchExtractionCategoryPlanDto(
                    WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D),
                    CandidateCount: 2,
                    TotalSizeInBytes: 0),
                new WorkspaceBatchExtractionCategoryPlanDto(
                    WorkspaceBatchExtractionCategoryDto.SpineGroup,
                    CandidateCount: 1,
                    TotalSizeInBytes: 0)
            });
    }
}
