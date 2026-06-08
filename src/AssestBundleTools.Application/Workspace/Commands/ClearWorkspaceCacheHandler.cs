using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 显式清理当前工作区下可重建的标准化资源包与预览缓存。
/// </summary>
public sealed class ClearWorkspaceCacheHandler
    : ICommandHandler<ClearWorkspaceCacheCommand, Result<WorkspaceCacheClearResultDto>>
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;

    public ClearWorkspaceCacheHandler(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory
                                     ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator
                                            ?? NoOpBundleArtifactSessionInvalidator.Instance;
    }

    public Task<Result<WorkspaceCacheClearResultDto>> HandleAsync(
        ClearWorkspaceCacheCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceCacheClearResultDto>.Cancelled());
        }

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(command.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
        var spineGroupCatalogCacheStore = _workspaceDataStoreFactory.CreateSpineGroupCatalogCacheStore(command.WorkspaceDataRootPath);
        var bundleArtifactResult = bundleArtifactStore.Prune(new BundleArtifactPruneRequestDto(
            command.WorkspaceKey,
            MaximumSizeInBytes: 0,
            MaximumArtifactCount: 0));
        var previewCacheResult = previewCacheStore.Prune(new PreviewCachePruneRequestDto(
            command.WorkspaceKey,
            MaximumSizeInBytes: 0,
            MaximumEntryCount: 0));
        spineGroupCatalogCacheStore.DeleteWorkspace(command.WorkspaceKey);

        foreach (var prunedEntry in bundleArtifactResult.PrunedEntries)
        {
            _bundleArtifactSessionInvalidator.InvalidateBundleArtifact(
                prunedEntry.WorkspaceKey,
                prunedEntry.BundleCacheKey);
        }

        var result = new WorkspaceCacheClearResultDto(
            SizeBeforeInBytes: bundleArtifactResult.SizeBeforeInBytes + previewCacheResult.SizeBeforeInBytes,
            SizeAfterInBytes: bundleArtifactResult.SizeAfterInBytes + previewCacheResult.SizeAfterInBytes,
            EntryCountBefore: bundleArtifactResult.ArtifactCountBefore + previewCacheResult.EntryCountBefore,
            EntryCountAfter: bundleArtifactResult.ArtifactCountAfter + previewCacheResult.EntryCountAfter,
            BundleArtifactCacheResult: bundleArtifactResult,
            PreviewCacheResult: previewCacheResult);

        return Task.FromResult(Result<WorkspaceCacheClearResultDto>.Success(result));
    }
}
