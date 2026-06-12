using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class WorkspaceBatchOperationChangeNotificationTests
{
    [Fact]
    public async Task ExecuteWorkspaceBatchReplacementAsync_ShouldNotifyAssociatedAssetRefresh_WhenImpactRegistryFindsDependents()
    {
        var workspaceId = new WorkspaceId("workspace-batch-associated-impact-notify");
        var textureBundleId = new BundleId("bundle-1");
        var textureAssetId = new AssetId("texture-1");
        var spinePartAssetId = new AssetId("spine-part-1");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceBatchOperationsGateway(
            new SuccessfulWorkspaceBatchOperationsGateway(
                new WorkspaceBatchReplacementResultDto(
                    workspaceId,
                    @"C:\Exports",
                    WorkspaceBatchReplacementMatchingMode.Heuristic,
                    RequestedCandidateCount: 1,
                    ReplacedAssetCount: 1,
                    SkippedAssetCount: 0,
                    FailedAssetCount: 0,
                    ProcessedBundleCount: 1,
                    ProcessedSpineGroupCount: 0,
                    CompletedAtUtc: DateTimeOffset.UtcNow,
                    ItemResults:
                    [
                        new WorkspaceBatchReplacementItemResultDto(
                            CandidateId: "heuristic:texture",
                            BundleId: textureBundleId,
                            AssetId: textureAssetId,
                            AssetType: AssetType.Texture2D,
                            AssetName: "HeroTexture",
                            SourceFilePath: @"C:\Exports\Texture2D\HeroTexture.png",
                            SpineGroupId: null,
                            SpineGroupDisplayName: null,
                            Status: WorkspaceBatchReplacementItemStatus.Replaced)
                    ])),
            notifier,
            new TestAssociatedAssetImpactRegistry(
                new WorkspaceAssociatedAssetImpactDto(
                    textureBundleId,
                    spinePartAssetId,
                    "HeroSpine",
                    AssetType.TextAsset,
                    PathId: 12,
                    AssetTypeDisplayName: "TextAsset")));

        var result = await gateway.ExecuteWorkspaceBatchReplacementAsync(
            new ExecuteWorkspaceBatchReplacementCommand(
                workspaceId,
                WorkspaceKey: "workspace-key",
                WorkspaceDataRootPath: @"D:\WorkspaceData",
                SourceRootDirectory: @"C:\Exports",
                WorkspaceBatchReplacementMatchingMode.Auto,
                Candidates: Array.Empty<WorkspaceBatchReplacementCandidateDto>()));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(WorkspaceOperationChangeKind.Replacement, notification.ChangeKind);
        Assert.Equal(WorkspaceOperationChangeReason.BatchReplacementApplied, notification.Reason);
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(textureAssetId) &&
            affected.Role == WorkspaceOperationAffectedAssetRole.PrimaryTarget);
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(spinePartAssetId) &&
            affected.Role == WorkspaceOperationAffectedAssetRole.ImplicitAffectedTarget);
    }

    private sealed class SuccessfulWorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
    {
        private readonly WorkspaceBatchReplacementResultDto _replacementResult;

        public SuccessfulWorkspaceBatchOperationsGateway(WorkspaceBatchReplacementResultDto replacementResult)
        {
            _replacementResult = replacementResult;
        }

        public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
            GetWorkspaceChangeSummaryQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
            GetWorkspaceSavePreviewQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
            SaveWorkspaceAsNewBundleCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
            GetWorkspaceBatchExtractionPreviewQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
            ExtractWorkspaceAssetsCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchReplacementPlanDto>> GetWorkspaceBatchReplacementPlanAsync(
            GetWorkspaceBatchReplacementPlanQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
            ExecuteWorkspaceBatchReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Success(_replacementResult));
    }

    private sealed class TestAssociatedAssetImpactRegistry : IWorkspaceAssociatedAssetImpactRegistry
    {
        private readonly IReadOnlyList<WorkspaceAssociatedAssetImpactDto> _impacts;

        public TestAssociatedAssetImpactRegistry(params WorkspaceAssociatedAssetImpactDto[] impacts)
        {
            _impacts = impacts;
        }

        public bool TryGetAssociatedAssetImpacts(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out IReadOnlyList<WorkspaceAssociatedAssetImpactDto> impacts)
        {
            impacts = _impacts;
            return true;
        }
    }
}
