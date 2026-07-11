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

        if (!manifest.IsMaterialized)
        {
            return null;
        }

        if (!IsInsideArtifactDirectory(manifest, resolvedPath))
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
        var bundlePath = CopyMaterializedBundle(request.WorkspaceKey, request.BundleCacheKey, sourcePath);
        var bundleFingerprint = File.Exists(bundlePath)
            ? AssetBundleFileFingerprintFactory.Create(bundlePath)
            : request.ResolvedBundleFingerprint;

        var manifest = new BundleArtifactManifestDto(
            WorkspaceKey: request.WorkspaceKey,
            BundleCacheKey: request.BundleCacheKey,
            ResolvedBundlePath: bundlePath,
            IsMaterialized: true,
            SourceFingerprint: request.SourceFingerprint,
            BundleFingerprint: bundleFingerprint,
            DecryptorId: request.DecryptorId,
            DecryptorVersion: request.DecryptorVersion,
            NormalizerVersion: request.NormalizerVersion,
            CreatedAtUtc: request.CreatedAtUtc,
            LastAccessedAtUtc: request.CreatedAtUtc,
            CodecParameters: request.CodecParameters);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetBundleArtifactManifestFilePath(request.WorkspaceKey, request.BundleCacheKey),
            manifest,
            JsonWorkspaceSerialization.SerializerOptions);
        return manifest;
    }

    public BundleArtifactManifestDto UpdateLastAccessed(
        BundleArtifactManifestDto manifest,
        DateTimeOffset lastAccessedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var updatedManifest = manifest with
        {
            LastAccessedAtUtc = lastAccessedAtUtc,
        };
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetBundleArtifactManifestFilePath(manifest.WorkspaceKey, manifest.BundleCacheKey),
            updatedManifest,
            JsonWorkspaceSerialization.SerializerOptions);
        return updatedManifest;
    }

    public BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null)
    {
        var entries = EnumerateCacheEntries(workspaceKey)
            .OrderBy(static entry => entry.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BundleArtifactCacheUsageDto(
            TotalSizeInBytes: entries.Sum(static entry => entry.SizeInBytes),
            ArtifactCount: entries.Length,
            Entries: entries);
    }

    public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumSizeInBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumSizeInBytes));
        }

        if (request.MaximumArtifactCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumArtifactCount));
        }

        var usageBefore = GetUsage(request.WorkspaceKey);
        var targetSizeInBytes = request.MaximumSizeInBytes ?? long.MaxValue;
        var targetArtifactCount = request.MaximumArtifactCount ?? int.MaxValue;
        var protectedBundleCacheKeys = (request.ProtectedBundleCacheKeys ?? Array.Empty<string>())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sizeAfter = usageBefore.TotalSizeInBytes;
        var countAfter = usageBefore.ArtifactCount;
        var prunedEntries = new List<BundleArtifactCacheEntryDto>();

        foreach (var entry in usageBefore.Entries
                     .OrderBy(static entry => entry.LastAccessedAtUtc ?? entry.CreatedAtUtc)
                     .ThenBy(static entry => entry.CreatedAtUtc)
                     .ThenBy(static entry => entry.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase))
        {
            if (sizeAfter <= targetSizeInBytes && countAfter <= targetArtifactCount)
            {
                break;
            }

            if (protectedBundleCacheKeys.Contains(entry.BundleCacheKey))
            {
                continue;
            }

            Delete(entry.WorkspaceKey, entry.BundleCacheKey);
            prunedEntries.Add(entry);
            sizeAfter -= entry.SizeInBytes;
            countAfter--;
        }

        return new BundleArtifactPruneResultDto(
            SizeBeforeInBytes: usageBefore.TotalSizeInBytes,
            SizeAfterInBytes: Math.Max(0, sizeAfter),
            ArtifactCountBefore: usageBefore.ArtifactCount,
            ArtifactCountAfter: Math.Max(0, countAfter),
            PrunedEntries: prunedEntries);
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

    private IEnumerable<BundleArtifactCacheEntryDto> EnumerateCacheEntries(string? workspaceKey)
    {
        foreach (var artifactDirectoryPath in EnumerateArtifactDirectories(workspaceKey))
        {
            var manifestFilePath = Path.Combine(artifactDirectoryPath, "manifest.json");
            var manifest = JsonWorkspaceFileStorageHelper.TryReadJson<BundleArtifactManifestDto>(
                manifestFilePath,
                JsonWorkspaceSerialization.SerializerOptions);
            if (manifest is null)
            {
                continue;
            }

            yield return new BundleArtifactCacheEntryDto(
                WorkspaceKey: manifest.WorkspaceKey,
                BundleCacheKey: manifest.BundleCacheKey,
                SizeInBytes: CalculateDirectorySize(artifactDirectoryPath),
                CreatedAtUtc: manifest.CreatedAtUtc,
                LastAccessedAtUtc: manifest.LastAccessedAtUtc,
                IsMaterialized: manifest.IsMaterialized);
        }
    }

    private IEnumerable<string> EnumerateArtifactDirectories(string? workspaceKey)
    {
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var workspaceDirectoryPath = _storagePaths.GetBundleArtifactWorkspaceDirectoryPath(workspaceKey);
            return Directory.Exists(workspaceDirectoryPath)
                ? Directory.EnumerateDirectories(workspaceDirectoryPath)
                : [];
        }

        if (!Directory.Exists(_storagePaths.BundleArtifactCacheDirectoryPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(_storagePaths.BundleArtifactCacheDirectoryPath)
            .SelectMany(static workspaceDirectoryPath => Directory.EnumerateDirectories(workspaceDirectoryPath));
    }

    private static long CalculateDirectorySize(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Sum(static filePath =>
                {
                    try
                    {
                        return new FileInfo(filePath).Length;
                    }
                    catch (IOException)
                    {
                        return 0;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return 0;
                    }
                });
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
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
