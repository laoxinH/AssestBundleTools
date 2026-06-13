using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceBatchReplacementDialogViewModelTests
{
    [Fact]
    public void CandidateFilters_ShouldApplyLocallyWithoutDiscardingPlanCandidates()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlan());

        Assert.Equal(3, viewModel.Candidates.Count);
        Assert.Equal(
            new[] { "char_452_bstalk_unveiling#4", "hero_mesh" },
            viewModel.FilteredCandidates.Select(static candidate => candidate.AssetName).ToArray());
        Assert.Equal(
            WorkspaceBatchReplacementMatchStatus.Changed.ToString(),
            viewModel.SelectedStatusFilter?.Key);

        viewModel.CandidateSearchText = "unveiling";

        var searched = Assert.Single(viewModel.FilteredCandidates);
        Assert.Equal("char_452_bstalk_unveiling#4", searched.AssetName);
        Assert.Equal(3, viewModel.Candidates.Count);

        viewModel.CandidateSearchText = string.Empty;

        Assert.Equal(
            new[] { "char_452_bstalk_unveiling#4", "hero_mesh" },
            viewModel.FilteredCandidates.Select(static candidate => candidate.AssetName).ToArray());

        viewModel.TypeFilters.Single(filter =>
            string.Equals(filter.Key, AssetType.Texture2D.ToString(), StringComparison.Ordinal)).IsSelected = true;

        searched = Assert.Single(viewModel.FilteredCandidates);
        Assert.Equal("char_452_bstalk_unveiling#4", searched.AssetName);

        viewModel.Bundles.Single(bundle => string.Equals(bundle.BundleId.Value, "bundle-spine", StringComparison.Ordinal)).IsSelected = false;

        Assert.Empty(viewModel.FilteredCandidates);
        Assert.Equal(3, viewModel.Candidates.Count);
    }

    [Fact]
    public void ChangedBundlesOnly_ShouldFilterBundleListWithoutChangingSelection()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlanWithUnchangedBundle());

        Assert.Equal(2, viewModel.FilteredBundles.Count);
        Assert.True(viewModel.Bundles.All(static bundle => bundle.IsSelected));

        viewModel.ShowChangedBundlesOnly = true;

        var visibleBundle = Assert.Single(viewModel.FilteredBundles);
        Assert.Equal("bundle-spine", visibleBundle.BundleId.Value);
        Assert.True(viewModel.Bundles.All(static bundle => bundle.IsSelected));
    }

    [Fact]
    public void TypeFilters_ShouldTreatEmptySelectionAsAllTypes()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlan());
        viewModel.SelectedStatusFilter = viewModel.StatusFilters.Single(static filter => filter.IsAll);

        Assert.Equal(3, viewModel.FilteredCandidates.Count);

        var textureFilter = viewModel.TypeFilters.Single(filter =>
            string.Equals(filter.Key, AssetType.Texture2D.ToString(), StringComparison.Ordinal));
        var meshFilter = viewModel.TypeFilters.Single(filter =>
            string.Equals(filter.Key, AssetType.Mesh.ToString(), StringComparison.Ordinal));
        textureFilter.IsSelected = true;
        meshFilter.IsSelected = true;

        Assert.Equal(
            new[] { "char_452_bstalk_unveiling#4", "hero_mesh" },
            viewModel.FilteredCandidates.Select(static candidate => candidate.AssetName).ToArray());

        textureFilter.IsSelected = false;
        meshFilter.IsSelected = false;

        Assert.Equal(3, viewModel.FilteredCandidates.Count);
    }

    [Fact]
    public void InitialState_ShouldDisableReplacementAndBundleFiltersWithoutResources()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasBundles);
        Assert.False(viewModel.HasCandidates);
        Assert.False(viewModel.CanSubmit);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogPrimaryButton, viewModel.PrimaryButtonText);
    }

    [Fact]
    public void ExecutionState_ShouldShowRunningSummaryAndRetryAfterCompletion()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlan());

        Assert.True(viewModel.CanSubmit);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogPrimaryButton, viewModel.PrimaryButtonText);

        viewModel.SetExecuting();

        Assert.False(viewModel.CanSubmit);
        Assert.True(viewModel.IsSummaryProgressActive);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogExecutionRunning, viewModel.PlanSummaryText);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogPrimaryButton, viewModel.PrimaryButtonText);

        viewModel.ApplyExecutionResult(new WorkspaceBatchReplacementResultDto(
            new WorkspaceId("workspace-ui"),
            @"C:\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            RequestedCandidateCount: 2,
            ReplacedAssetCount: 2,
            SkippedAssetCount: 0,
            FailedAssetCount: 0,
            ProcessedBundleCount: 2,
            ProcessedSpineGroupCount: 0,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ItemResults: []));

        Assert.True(viewModel.CanSubmit);
        Assert.False(viewModel.IsSummaryProgressActive);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogRetryPrimaryButton, viewModel.PrimaryButtonText);
        Assert.StartsWith(ResourceKeys.WorkspaceBatchReplacementDialogExecutionSummaryFormat, viewModel.PlanSummaryText, StringComparison.Ordinal);

        viewModel.SetPlanLoading();

        Assert.False(viewModel.CanSubmit);
        Assert.True(viewModel.IsSummaryProgressActive);
        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogRetryPrimaryButton, viewModel.PrimaryButtonText);
    }

    [Fact]
    public void SourceDirectoryChange_ShouldRestorePrimaryButtonText()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlan());
        viewModel.ApplyExecutionResult(new WorkspaceBatchReplacementResultDto(
            new WorkspaceId("workspace-ui"),
            @"C:\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            RequestedCandidateCount: 1,
            ReplacedAssetCount: 1,
            SkippedAssetCount: 0,
            FailedAssetCount: 0,
            ProcessedBundleCount: 1,
            ProcessedSpineGroupCount: 0,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ItemResults: []));

        viewModel.SetSourceDirectory(@"C:\Exports2");

        Assert.Equal(ResourceKeys.WorkspaceBatchReplacementDialogPrimaryButton, viewModel.PrimaryButtonText);
    }

    [Fact]
    public void ApplyPlan_ShouldGroupSpinePartCandidates_AndExposeSpineTypeFilter()
    {
        var viewModel = CreateViewModel();
        viewModel.ApplyPlan(CreatePlanWithSpineGroupParts());
        viewModel.SelectedStatusFilter = viewModel.StatusFilters.Single(static filter => filter.IsAll);

        Assert.Equal(2, viewModel.Candidates.Count);
        var spineCandidate = Assert.Single(viewModel.Candidates, static candidate =>
            candidate.TypeFilterKey == "__spine");
        Assert.Equal("char_452_bstalk_snow#4 [1]", spineCandidate.AssetName);
        Assert.Equal(2, spineCandidate.ReplacementCandidates.Count);
        Assert.Contains(viewModel.TypeFilters, filter =>
            string.Equals(filter.Key, "__spine", StringComparison.Ordinal) &&
            string.Equals(filter.DisplayText, ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterSpine, StringComparison.Ordinal));

        foreach (var filter in viewModel.TypeFilters)
        {
            filter.IsSelected = string.Equals(filter.Key, "__spine", StringComparison.Ordinal);
        }

        var visibleSpineCandidate = Assert.Single(viewModel.FilteredCandidates);
        Assert.Equal("char_452_bstalk_snow#4 [1]", visibleSpineCandidate.AssetName);

        viewModel.CandidateSearchText = ".atlas";
        visibleSpineCandidate = Assert.Single(viewModel.FilteredCandidates);
        Assert.Equal("char_452_bstalk_snow#4 [1]", visibleSpineCandidate.AssetName);
        viewModel.CandidateSearchText = string.Empty;

        foreach (var candidate in viewModel.Candidates)
        {
            candidate.IsSelected = ReferenceEquals(candidate, spineCandidate);
        }

        Assert.Equal(
            new[] { "spine-texture", "spine-atlas" },
            viewModel.GetSelectedCandidates().Select(static candidate => candidate.CandidateId).ToArray());
    }

    private static WorkspaceBatchReplacementDialogViewModel CreateViewModel() =>
        new(
            new TestLocalizationService(),
            new[]
            {
                new BundleNodeViewModel(
                    new BundleId("bundle-spine"),
                    "char_452_bstalk.ab",
                    "ab/char_452_bstalk.ab",
                    @"C:\Bundles\char_452_bstalk.ab",
                    assetCount: 2),
                new BundleNodeViewModel(
                    new BundleId("bundle-mesh"),
                    "hero_mesh.ab",
                    "ab/hero_mesh.ab",
                    @"C:\Bundles\hero_mesh.ab",
                    assetCount: 1)
            });

    private static WorkspaceBatchReplacementPlanDto CreatePlan() =>
        new(
            new WorkspaceId("workspace-ui"),
            @"C:\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            @"C:\Exports\_abt-export-manifest.json",
            "workspace-key",
            TotalManifestItemCount: 3,
            ChangedCount: 2,
            UnchangedCount: 1,
            MissingCount: 0,
            IssueCount: 0,
            Candidates:
            [
                CreateCandidate(
                    "c1",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.Texture2D,
                    "char_452_bstalk_unveiling#4",
                    "Texture2D/char_452_bstalk_unveiling#4.png",
                    WorkspaceBatchReplacementMatchStatus.Changed),
                CreateCandidate(
                    "c2",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.AudioClip,
                    "CN_001",
                    "AudioClip/CN_001.wav",
                    WorkspaceBatchReplacementMatchStatus.Unchanged),
                CreateCandidate(
                    "c3",
                    "bundle-mesh",
                    "hero_mesh.ab",
                    AssetType.Mesh,
                    "hero_mesh",
                    "Mesh/hero_mesh.mesh",
                    WorkspaceBatchReplacementMatchStatus.Changed)
            ]);

    private static WorkspaceBatchReplacementPlanDto CreatePlanWithUnchangedBundle() =>
        new(
            new WorkspaceId("workspace-ui"),
            @"C:\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            @"C:\Exports\_abt-export-manifest.json",
            "workspace-key",
            TotalManifestItemCount: 2,
            ChangedCount: 1,
            UnchangedCount: 1,
            MissingCount: 0,
            IssueCount: 0,
            Candidates:
            [
                CreateCandidate(
                    "c1",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.Texture2D,
                    "char_452_bstalk_unveiling#4",
                    "Texture2D/char_452_bstalk_unveiling#4.png",
                    WorkspaceBatchReplacementMatchStatus.Changed),
                CreateCandidate(
                    "c2",
                    "bundle-mesh",
                    "hero_mesh.ab",
                    AssetType.Mesh,
                    "hero_mesh",
                    "Mesh/hero_mesh.mesh",
                    WorkspaceBatchReplacementMatchStatus.Unchanged)
            ]);

    private static WorkspaceBatchReplacementPlanDto CreatePlanWithSpineGroupParts() =>
        new(
            new WorkspaceId("workspace-ui"),
            @"C:\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            @"C:\Exports\_abt-export-manifest.json",
            "workspace-key",
            TotalManifestItemCount: 3,
            ChangedCount: 3,
            UnchangedCount: 0,
            MissingCount: 0,
            IssueCount: 0,
            Candidates:
            [
                CreateCandidate(
                    "spine-texture",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.Texture2D,
                    "char_452_bstalk_snow#4",
                    "char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.png",
                    WorkspaceBatchReplacementMatchStatus.Changed,
                    spineGroupId: "spine-group-1",
                    spineGroupDisplayName: "char_452_bstalk_snow#4 [1]",
                    outputRole: "TexturePage"),
                CreateCandidate(
                    "spine-atlas",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.TextAsset,
                    "char_452_bstalk_snow#4",
                    "char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.atlas",
                    WorkspaceBatchReplacementMatchStatus.Changed,
                    spineGroupId: "spine-group-1",
                    spineGroupDisplayName: "char_452_bstalk_snow#4 [1]",
                    outputRole: "Atlas"),
                CreateCandidate(
                    "ordinary-texture",
                    "bundle-spine",
                    "char_452_bstalk.ab",
                    AssetType.Texture2D,
                    "build_char_452_bstalk_snow#4",
                    "char_452_bstalk.ab/Texture2D/build_char_452_bstalk_snow#4.png",
                    WorkspaceBatchReplacementMatchStatus.Changed)
            ]);

    private static WorkspaceBatchReplacementCandidateDto CreateCandidate(
        string candidateId,
        string bundleId,
        string bundleDisplayName,
        AssetType assetType,
        string assetName,
        string relativePath,
        WorkspaceBatchReplacementMatchStatus status,
        string? spineGroupId = null,
        string? spineGroupDisplayName = null,
        string outputRole = "") =>
        new(
            candidateId,
            new BundleId(bundleId),
            bundleDisplayName,
            new AssetId($"{candidateId}-asset"),
            PathId: 1,
            assetType,
            assetType.ToString(),
            assetName,
            ContainerPath: null,
            relativePath,
            $@"C:\Exports\{relativePath}",
            CurrentFileSizeInBytes: 10,
            CurrentFileHashAlgorithm: "MD5",
            CurrentFileHash: "changed",
            ExportedFileSizeInBytes: 10,
            ExportedFileHashAlgorithm: "MD5",
            ExportedFileHash: "original",
            SpineGroupId: spineGroupId,
            SpineGroupDisplayName: spineGroupDisplayName,
            OutputRole: outputRole,
            status,
            WorkspaceBatchReplacementMatchingMode.Manifest,
            IsDefaultSelected: status == WorkspaceBatchReplacementMatchStatus.Changed,
            Diagnostics: []);
}
