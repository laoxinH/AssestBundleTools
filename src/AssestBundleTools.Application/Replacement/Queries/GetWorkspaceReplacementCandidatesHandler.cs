using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 为替换流程提供工作区内兼容候选资源列表。
/// </summary>
public sealed class GetWorkspaceReplacementCandidatesHandler : IQueryHandler<GetWorkspaceReplacementCandidatesQuery, Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public GetWorkspaceReplacementCandidatesHandler(IWorkspaceSessionRegistry workspaceSessionRegistry)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
    }

    public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> HandleAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.TargetBundleId.Value))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.TargetAssetId.Value))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.TargetBundleId))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.BundleNotFound(query.TargetBundleId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(query.WorkspaceId, query.TargetBundleId, query.TargetAssetId))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(ApplicationErrors.AssetNotFound(query.TargetAssetId)));
        }

        _workspaceSessionRegistry.TryGetWorkspaceReplacementCandidates(
            query.WorkspaceId,
            query.TargetBundleId,
            query.TargetAssetId,
            out var candidates);

        if (candidates is null || candidates.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Failure(
                ApplicationErrors.NoWorkspaceReplacementCandidates(query.TargetAssetId)));
        }

        return Task.FromResult(Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>.Success(candidates));
    }
}
