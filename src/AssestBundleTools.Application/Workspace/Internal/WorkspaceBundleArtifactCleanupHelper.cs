using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 负责清理指定资源包关联的索引缓存与预览派生缓存。
/// </summary>
internal static class WorkspaceBundleArtifactCleanupHelper
{
    public static void DeleteBundleArtifacts(
        string workspaceKey,
        string bundleCacheKey,
        IBundleIndexCacheStore bundleIndexCacheStore,
        IPreviewCacheStore previewCacheStore,
        IReadOnlyList<string>? previewCacheKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleCacheKey);
        ArgumentNullException.ThrowIfNull(bundleIndexCacheStore);
        ArgumentNullException.ThrowIfNull(previewCacheStore);

        bundleIndexCacheStore.Delete(workspaceKey, bundleCacheKey);

        if (previewCacheKeys is not null)
        {
            foreach (var previewCacheKey in previewCacheKeys.Where(static key => !string.IsNullOrWhiteSpace(key)))
            {
                previewCacheStore.Delete(workspaceKey, previewCacheKey);
            }

            return;
        }

        var previewDescriptors = previewCacheStore.LoadDescriptors(workspaceKey);
        foreach (var previewDescriptor in previewDescriptors.Where(descriptor =>
                     string.Equals(descriptor.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(descriptor.PreviewCacheKey)))
        {
            previewCacheStore.Delete(workspaceKey, previewDescriptor.PreviewCacheKey!);
        }
    }
}
