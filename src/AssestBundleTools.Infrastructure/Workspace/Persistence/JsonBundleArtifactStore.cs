using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Infrastructure.Bundles.Decryption;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON manifest 和标准化 bundle 文件的资源包物理缓存存储。
/// </summary>
public sealed class JsonBundleArtifactStore : IBundleArtifactStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonBundleArtifactStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            return null;
        }

        return JsonWorkspaceFileStorageHelper.TryReadJson<BundleArtifactManifestDto>(
            _storagePaths.GetBundleArtifactManifestFilePath(workspaceKey, bundleCacheKey),
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public string? ResolveBundlePath(BundleArtifactManifestDto manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.ResolvedBundlePath))
        {
            return null;
        }

        var resolvedPath = Path.GetFullPath(manifest.ResolvedBundlePath);
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        if (manifest.IsMaterialized && !IsInsideArtifactDirectory(manifest, resolvedPath))
        {
            return null;
        }

        return resolvedPath;
    }

    public BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BundleCacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResolvedBundlePath);

        var sourcePath = Path.GetFullPath(request.ResolvedBundlePath);
        var isMaterialized = request.ProducedDerivedFile;
        var bundlePath = isMaterialized
            ? CopyMaterializedBundle(request.WorkspaceKey, request.BundleCacheKey, sourcePath)
            : sourcePath;
        var bundleFingerprint = File.Exists(bundlePath)
            ? AssetBundleFileFingerprintFactory.Create(bundlePath)
            : request.ResolvedBundleFingerprint;

        var manifest = new BundleArtifactManifestDto(
            WorkspaceKey: request.WorkspaceKey,
            BundleCacheKey: request.BundleCacheKey,
            ResolvedBundlePath: bundlePath,
            IsMaterialized: isMaterialized,
            SourceFingerprint: request.SourceFingerprint,
            BundleFingerprint: bundleFingerprint,
            DecryptorId: request.DecryptorId,
            DecryptorVersion: request.DecryptorVersion,
            NormalizerVersion: request.NormalizerVersion,
            CreatedAtUtc: request.CreatedAtUtc);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetBundleArtifactManifestFilePath(request.WorkspaceKey, request.BundleCacheKey),
            manifest,
            JsonWorkspaceSerialization.SerializerOptions);
        return manifest;
    }

    public void Delete(string workspaceKey, string bundleCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetBundleArtifactDirectoryPath(workspaceKey, bundleCacheKey));
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetBundleArtifactWorkspaceDirectoryPath(workspaceKey));
    }

    private string CopyMaterializedBundle(string workspaceKey, string bundleCacheKey, string sourcePath)
    {
        var targetPath = _storagePaths.GetBundleArtifactNormalizedFilePath(workspaceKey, bundleCacheKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return targetPath;
    }

    private bool IsInsideArtifactDirectory(BundleArtifactManifestDto manifest, string resolvedPath)
    {
        var artifactDirectoryPath = Path.GetFullPath(
            _storagePaths.GetBundleArtifactDirectoryPath(manifest.WorkspaceKey, manifest.BundleCacheKey));
        var normalizedArtifactDirectoryPath = artifactDirectoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return resolvedPath.StartsWith(normalizedArtifactDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }
}
