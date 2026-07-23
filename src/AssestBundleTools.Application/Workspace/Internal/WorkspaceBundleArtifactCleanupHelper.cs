using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 负责清理指定资源包关联的标准化物理缓存、索引缓存与预览派生缓存。
/// </summary>
internal static class WorkspaceBundleArtifactCleanupHelper
{
    public static void DeleteBundleArtifacts(
        string workspaceKey,
        string bundleCacheKey,
        IBundleArtifactStore? bundleArtifactStore,
        IBundleIndexCacheStore bundleIndexCacheStore,
        IPreviewCacheStore previewCacheStore,
        IReadOnlyList<string>? previewCacheKeys = null,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null,
        Action<WorkspaceBundleArtifactCleanupStage, string?, Exception>? cleanupFailureObserver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleCacheKey);
        ArgumentNullException.ThrowIfNull(bundleIndexCacheStore);
        ArgumentNullException.ThrowIfNull(previewCacheStore);

        if (bundleArtifactStore is not null)
        {
            ExecuteCleanupStep(
                WorkspaceBundleArtifactCleanupStage.LoadedSession,
                bundleCacheKey,
                () => (bundleArtifactSessionInvalidator ?? NoOpBundleArtifactSessionInvalidator.Instance)
                    .InvalidateBundleArtifact(workspaceKey, bundleCacheKey),
                cleanupFailureObserver);
            ExecuteCleanupStep(
                WorkspaceBundleArtifactCleanupStage.BundleArtifact,
                bundleCacheKey,
                () => bundleArtifactStore.Delete(workspaceKey, bundleCacheKey),
                cleanupFailureObserver);
        }

        ExecuteCleanupStep(
            WorkspaceBundleArtifactCleanupStage.BundleIndex,
            bundleCacheKey,
            () => bundleIndexCacheStore.Delete(workspaceKey, bundleCacheKey),
            cleanupFailureObserver);

        if (previewCacheKeys is not null)
        {
            foreach (var previewCacheKey in previewCacheKeys.Where(static key => !string.IsNullOrWhiteSpace(key)))
            {
                ExecuteCleanupStep(
                    WorkspaceBundleArtifactCleanupStage.PreviewEntry,
                    previewCacheKey,
                    () => previewCacheStore.Delete(workspaceKey, previewCacheKey),
                    cleanupFailureObserver);
            }

            return;
        }

        IReadOnlyList<PreviewCacheDescriptorDto> previewDescriptors;
        try
        {
            previewDescriptors = previewCacheStore.LoadDescriptors(workspaceKey);
        }
        catch (Exception exception) when (
            cleanupFailureObserver is not null &&
            IsNonFatalCleanupException(exception))
        {
            cleanupFailureObserver(
                WorkspaceBundleArtifactCleanupStage.PreviewCatalog,
                workspaceKey,
                exception);
            return;
        }

        foreach (var previewDescriptor in previewDescriptors.Where(descriptor =>
                     string.Equals(descriptor.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(descriptor.PreviewCacheKey)))
        {
            ExecuteCleanupStep(
                WorkspaceBundleArtifactCleanupStage.PreviewEntry,
                previewDescriptor.PreviewCacheKey,
                () => previewCacheStore.Delete(workspaceKey, previewDescriptor.PreviewCacheKey!),
                cleanupFailureObserver);
        }
    }

    private static void ExecuteCleanupStep(
        WorkspaceBundleArtifactCleanupStage stage,
        string? target,
        Action cleanup,
        Action<WorkspaceBundleArtifactCleanupStage, string?, Exception>? cleanupFailureObserver)
    {
        if (cleanupFailureObserver is null)
        {
            cleanup();
            return;
        }

        try
        {
            cleanup();
        }
        catch (Exception exception) when (IsNonFatalCleanupException(exception))
        {
            cleanupFailureObserver(stage, target, exception);
        }
    }

    private static bool IsNonFatalCleanupException(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;
}

internal enum WorkspaceBundleArtifactCleanupStage
{
    LoadedSession,
    BundleArtifact,
    BundleIndex,
    PreviewCatalog,
    PreviewEntry
}
