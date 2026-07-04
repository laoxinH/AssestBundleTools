using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Saving.Queries;

/// <summary>
/// 为批处理页和后续保存摘要页提供保存前预览读模型。
/// </summary>
public sealed class GetWorkspaceSavePreviewHandler : IQueryHandler<GetWorkspaceSavePreviewQuery, Result<WorkspaceSavePreviewDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceSaveArtifactInputResolver? _artifactInputResolver;
    private readonly IWorkspaceSaveOutputProbe? _outputProbe;

    public GetWorkspaceSavePreviewHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory = null,
        IWorkspaceSaveOutputProbe? outputProbe = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _artifactInputResolver = workspaceDataStoreFactory is null
            ? null
            : new WorkspaceSaveArtifactInputResolver(workspaceDataStoreFactory);
        _outputProbe = outputProbe;
    }

    public Task<Result<WorkspaceSavePreviewDto>> HandleAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceSavePreviewDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceSavePreviewDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceSavePreviewDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        var plan = WorkspaceSavePlanner.BuildPlan(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            query.OutputDirectory,
            _artifactInputResolver,
            _outputProbe,
            query.WorkspaceKey,
            query.WorkspaceDataRootPath,
            query.OutputConflictPolicy);
        var bundlePlans = plan.BundlePlans
            .Select(bundlePlan => new WorkspaceSaveBundlePlanDto(
                BundleId: bundlePlan.BundleId,
                BundleDisplayName: bundlePlan.BundleDisplayName,
                OutputFilePath: bundlePlan.OutputFilePath,
                ChangeCount: bundlePlan.ChangeCount,
                ModifiedAssetCount: bundlePlan.ModifiedAssetCount,
                ExtractedAssetCount: bundlePlan.ExtractedAssetCount,
                WorkspaceReplacementCount: bundlePlan.WorkspaceReplacementCount,
                FileReplacementCount: bundlePlan.FileReplacementCount,
                BundleCacheKey: bundlePlan.ArtifactInput.BundleCacheKey,
                HasStandardizedArtifact: bundlePlan.ArtifactInput.HasStandardizedArtifact,
                ArtifactBundlePath: bundlePlan.ArtifactInput.ArtifactBundlePath,
                BundleSessionCacheKey: bundlePlan.ArtifactInput.BundleSessionCacheKey,
                ArtifactDiagnosticCode: bundlePlan.ArtifactInput.ArtifactDiagnosticCode,
                OutputReadiness: bundlePlan.OutputPreflight.Readiness,
                IsOutputWriteBlocked: bundlePlan.OutputPreflight.IsWriteBlocked,
                OutputDiagnosticCode: bundlePlan.OutputPreflight.DiagnosticCode))
            .ToArray();

        var preview = new WorkspaceSavePreviewDto(
            WorkspaceId: plan.WorkspaceId,
            WorkspaceDisplayName: plan.WorkspaceDisplayName,
            OutputDirectory: plan.OutputDirectory,
            PlannedBundleCount: bundlePlans.Length,
            TotalChangeCount: plan.TotalChangeCount,
            ModifiedAssetCount: plan.ModifiedAssetCount,
            ExtractedAssetCount: plan.ExtractedAssetCount,
            WorkspaceReplacementCount: plan.WorkspaceReplacementCount,
            FileReplacementCount: plan.FileReplacementCount,
            BundlePlans: bundlePlans,
            ResolvedArtifactInputCount: plan.ResolvedArtifactInputCount,
            UnresolvedArtifactInputCount: plan.UnresolvedArtifactInputCount,
            OutputReadyBundleCount: plan.OutputReadyBundleCount,
            OutputBlockedBundleCount: plan.OutputBlockedBundleCount,
            OutputUnprobedBundleCount: plan.OutputUnprobedBundleCount,
            IsReadyForWrite: plan.IsReadyForWrite);

        return Task.FromResult(Result<WorkspaceSavePreviewDto>.Success(preview));
    }
}
