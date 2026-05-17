using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 执行标准化资源包 artifact 缓存清理。
/// </summary>
public sealed class PruneBundleArtifactCacheHandler
    : ICommandHandler<PruneBundleArtifactCacheCommand, Result<BundleArtifactPruneResultDto>>
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public PruneBundleArtifactCacheHandler(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory
                                     ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public Task<Result<BundleArtifactPruneResultDto>> HandleAsync(
        PruneBundleArtifactCacheCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<BundleArtifactPruneResultDto>.Cancelled());
        }

        if (command.MaximumSizeInBytes is < 0 || command.MaximumArtifactCount is < 0)
        {
            return Task.FromResult(Result<BundleArtifactPruneResultDto>.Failure(
                ApplicationErrors.BundleArtifactCachePruneLimitInvalid()));
        }

        var store = _workspaceDataStoreFactory.CreateBundleArtifactStore(command.WorkspaceDataRootPath);
        var result = store.Prune(new BundleArtifactPruneRequestDto(
            command.WorkspaceKey,
            command.MaximumSizeInBytes,
            command.MaximumArtifactCount));
        return Task.FromResult(Result<BundleArtifactPruneResultDto>.Success(result));
    }
}
