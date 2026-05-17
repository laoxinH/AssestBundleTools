namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 统一描述工作区目录与元数据文件的落盘路径。
/// </summary>
/// <remarks>
/// 当前实现区分两类路径：
/// 全局目录索引默认落到本机应用数据目录，
/// 单工作区元数据则允许落到用户显式选择的数据根目录中，
/// 以便后续在不同磁盘或独立工作目录下管理工作区数据。
/// </remarks>
internal sealed class JsonWorkspaceStoragePaths
{
    public JsonWorkspaceStoragePaths(string rootDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectoryPath);

        RootDirectoryPath = rootDirectoryPath.Trim();
        CatalogFilePath = Path.Combine(RootDirectoryPath, "workspace-catalog.json");
        WorkspacesDirectoryPath = Path.Combine(RootDirectoryPath, "workspaces");
        CacheDirectoryPath = Path.Combine(RootDirectoryPath, "cache");
        BundleArtifactCacheDirectoryPath = Path.Combine(CacheDirectoryPath, "bundles");
        BundleIndexCacheDirectoryPath = Path.Combine(CacheDirectoryPath, "bundle-index");
        PreviewCacheRootDirectoryPath = Path.Combine(CacheDirectoryPath, "previews");
        StagingDirectoryPath = Path.Combine(RootDirectoryPath, "staging");
        WorkspaceChangesDirectoryPath = Path.Combine(StagingDirectoryPath, "changes");
    }

    public string RootDirectoryPath { get; }

    public string CatalogFilePath { get; }

    public string WorkspacesDirectoryPath { get; }

    public string CacheDirectoryPath { get; }

    public string BundleArtifactCacheDirectoryPath { get; }

    public string BundleIndexCacheDirectoryPath { get; }

    public string PreviewCacheRootDirectoryPath { get; }

    public string StagingDirectoryPath { get; }

    public string WorkspaceChangesDirectoryPath { get; }

    public string GetMetadataFilePath(string workspaceKey)
    {
        return Path.Combine(WorkspacesDirectoryPath, $"{NormalizeFileSegment(workspaceKey, nameof(workspaceKey))}.json");
    }

    public string GetBundleIndexCacheDirectoryPath(string workspaceKey)
    {
        return Path.Combine(BundleIndexCacheDirectoryPath, NormalizeFileSegment(workspaceKey, nameof(workspaceKey)));
    }

    public string GetBundleArtifactWorkspaceDirectoryPath(string workspaceKey)
    {
        return Path.Combine(BundleArtifactCacheDirectoryPath, NormalizeFileSegment(workspaceKey, nameof(workspaceKey)));
    }

    public string GetBundleArtifactDirectoryPath(string workspaceKey, string bundleCacheKey)
    {
        return Path.Combine(
            GetBundleArtifactWorkspaceDirectoryPath(workspaceKey),
            NormalizeFileSegment(bundleCacheKey, nameof(bundleCacheKey)));
    }

    public string GetBundleArtifactManifestFilePath(string workspaceKey, string bundleCacheKey)
    {
        return Path.Combine(GetBundleArtifactDirectoryPath(workspaceKey, bundleCacheKey), "manifest.json");
    }

    public string GetBundleArtifactNormalizedFilePath(string workspaceKey, string bundleCacheKey)
    {
        return Path.Combine(GetBundleArtifactDirectoryPath(workspaceKey, bundleCacheKey), "normalized.bundle");
    }

    public string GetBundleIndexCacheFilePath(string workspaceKey, string bundleCacheKey)
    {
        return Path.Combine(
            GetBundleIndexCacheDirectoryPath(workspaceKey),
            $"{NormalizeFileSegment(bundleCacheKey, nameof(bundleCacheKey))}.json");
    }

    public string GetPreviewCacheDirectoryPath(string workspaceKey)
    {
        return Path.Combine(PreviewCacheRootDirectoryPath, NormalizeFileSegment(workspaceKey, nameof(workspaceKey)));
    }

    public string GetPreviewDescriptorFilePath(string workspaceKey, string previewCacheKey)
    {
        return Path.Combine(
            GetPreviewCacheDirectoryPath(workspaceKey),
            $"{NormalizeFileSegment(previewCacheKey, nameof(previewCacheKey))}.json");
    }

    public string GetPreviewPayloadFilePath(string workspaceKey, string previewCacheKey, string relativePayloadPath)
    {
        NormalizeFileSegment(previewCacheKey, nameof(previewCacheKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePayloadPath);

        if (Path.IsPathRooted(relativePayloadPath))
        {
            throw new ArgumentException("Preview cache payload path must be relative to the workspace preview cache directory.", nameof(relativePayloadPath));
        }

        var normalizedRelativePath = relativePayloadPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var combinedPath = Path.GetFullPath(Path.Combine(GetPreviewCacheDirectoryPath(workspaceKey), normalizedRelativePath));
        var previewCacheDirectoryPath = Path.GetFullPath(GetPreviewCacheDirectoryPath(workspaceKey));

        if (!combinedPath.StartsWith(previewCacheDirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Preview cache payload path must stay inside the workspace preview cache directory.", nameof(relativePayloadPath));
        }

        return combinedPath;
    }

    public string GetWorkspaceChangeFilePath(string workspaceKey)
    {
        return Path.Combine(WorkspaceChangesDirectoryPath, $"{NormalizeFileSegment(workspaceKey, nameof(workspaceKey))}.json");
    }

    private static string NormalizeFileSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalizedValue = value.Trim();
        if (normalizedValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalizedValue.Contains(Path.DirectorySeparatorChar) ||
            normalizedValue.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Workspace storage keys must be valid single file path segments.", parameterName);
        }

        return normalizedValue;
    }

    public static JsonWorkspaceStoragePaths CreateDefault()
    {
        var localApplicationDataPath =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new JsonWorkspaceStoragePaths(
            Path.Combine(localApplicationDataPath, "AssestBundleTools"));
    }
}
