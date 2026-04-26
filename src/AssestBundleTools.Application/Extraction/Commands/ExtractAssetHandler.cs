using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 处理单资源提取请求，并把结果记录到工作区会话中。
/// </summary>
public sealed class ExtractAssetHandler : ICommandHandler<ExtractAssetCommand, Result<AssetExtractionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public ExtractAssetHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<AssetExtractionResultDto>> HandleAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(command.WorkspaceId, command.BundleId))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.BundleNotFound(command.BundleId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(command.WorkspaceId, command.BundleId, command.AssetId))
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        _workspaceSessionRegistry.TryExtractAsset(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            out var extractionResult);

        return Task.FromResult(Result<AssetExtractionResultDto>.Success(extractionResult!));
    }
}
