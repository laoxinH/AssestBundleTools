using System.IO;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 根据已选中的外部文件路径，为文件替换流程生成可应用的候选来源。
/// </summary>
public sealed class GetFileReplacementCandidateHandler : IQueryHandler<GetFileReplacementCandidateQuery, Result<FileReplacementCandidateDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public GetFileReplacementCandidateHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<FileReplacementCandidateDto>> HandleAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.TargetBundleId.Value))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.TargetAssetId.Value))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.SourceFilePath))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.SourceFilePathRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.TargetBundleId))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.BundleNotFound(query.TargetBundleId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(query.WorkspaceId, query.TargetBundleId, query.TargetAssetId))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.AssetNotFound(query.TargetAssetId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                query.TargetBundleId,
                query.TargetAssetId,
                out var targetDetails))
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(ApplicationErrors.AssetNotFound(query.TargetAssetId)));
        }

        var normalizedSourcePath = query.SourceFilePath.Trim();
        var resolvedSourceAssetType = ReplacementFileTypeCatalog.ResolveCandidateAssetType(
            targetDetails!.AssetType,
            normalizedSourcePath);

        if (resolvedSourceAssetType != targetDetails.AssetType)
        {
            return Task.FromResult(Result<FileReplacementCandidateDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(resolvedSourceAssetType, targetDetails.AssetType)));
        }

        var sourceFileName = Path.GetFileName(normalizedSourcePath);
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            sourceFileName = normalizedSourcePath;
        }

        return Task.FromResult(Result<FileReplacementCandidateDto>.Success(
            new FileReplacementCandidateDto(
                SourceFilePath: normalizedSourcePath,
                SourceFileName: sourceFileName,
                SourceAssetType: resolvedSourceAssetType)));
    }
}
