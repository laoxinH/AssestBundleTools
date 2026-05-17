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
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;

    public PruneBundleArtifactCacheHandler(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory
                                     ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator
                                            ?? NoOpBundleArtifactSessionInvalidator.Instance;
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
        foreach (var prunedEntry in result.PrunedEntries)
        {
            _bundleArtifactSessionInvalidator.InvalidateBundleArtifact(
                prunedEntry.WorkspaceKey,
                prunedEntry.BundleCacheKey);
        }

        return Task.FromResult(Result<BundleArtifactPruneResultDto>.Success(result));
    }
}
