using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    private const string NormalizedBundleArtifactFileName = "normalized.bundle";

    private static CachedBundleState MapBundleState(
        BundleIndexCacheEntryDto cacheEntry,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup,
        WorkspaceAssetDependencyTargetLookup assetDependencyTargetLookup)
    {
        var bundleId = ResolveBundleId(cacheEntry);
        var displayName = ResolveBundleDisplayName(cacheEntry);

        var assets = cacheEntry.Assets
            .Select(asset => MapAssetState(bundleId, asset, assetDependencyTargetLookup))
            .ToArray();

        return new CachedBundleState(
            BundleId: bundleId,
            DisplayName: displayName,
            LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, displayName),
            SourcePath: cacheEntry.SourceFingerprint.SourcePath ?? string.Empty,
            BundleExternalDependencies: MapBundleExternalDependencies(cacheEntry.Dependencies, serializedFileBundleLookup),
            Assets: assets);
    }

    private static BundleId ResolveBundleId(BundleIndexCacheEntryDto cacheEntry)
    {
        return BundleId.TryParse(cacheEntry.BundleId, out var parsedBundleId)
            ? parsedBundleId
            : new BundleId($"bundle.{cacheEntry.BundleCacheKey}");
    }

    private static CachedAssetState MapAssetState(
        BundleId bundleId,
        BundleIndexAssetCacheEntryDto asset,
        WorkspaceAssetDependencyTargetLookup assetDependencyTargetLookup)
    {
        var assetId = ResolveCachedAssetId(asset);
        var assetType = ParseAssetType(asset.AssetType);

        return new CachedAssetState(
            assetId: assetId,
            name: asset.AssetName,
            assetPath: string.IsNullOrWhiteSpace(asset.AssetPath) ? asset.AssetName : asset.AssetPath,
            containerPath: asset.ContainerPath ?? string.Empty,
            serializedFileName: asset.SerializedFileName,
            assetType: assetType,
            assetTypeDisplayName: ResolveAssetTypeDisplayName(asset.AssetTypeDisplayName, asset.AssetType),
            sizeInBytes: asset.SizeInBytes,
            preferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
            pathId: asset.PathId,
            hasPreview: asset.HasPreview,
            canExtract: asset.CanExtract,
            canReplace: asset.CanReplace,
            dependencies: MapDependencies(assetId, bundleId, asset.Dependencies, assetDependencyTargetLookup))
        {
            IncludeInAssetList = asset.IncludeInAssetList
        };
    }

    private static IReadOnlyList<AssetDependencyDto> MapDependencies(
        AssetId sourceAssetId,
        BundleId bundleId,
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies,
        WorkspaceAssetDependencyTargetLookup assetDependencyTargetLookup)
    {
        if (dependencies is not { Count: > 0 })
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return dependencies
            .OrderBy(static dependency => dependency.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => MapDependency(sourceAssetId, bundleId, dependency, assetDependencyTargetLookup))
            .ToArray();
    }

    private static AssetDependencyDto MapDependency(
        AssetId sourceAssetId,
        BundleId sourceBundleId,
        BundleIndexAssetDependencyCacheEntryDto dependency,
        WorkspaceAssetDependencyTargetLookup assetDependencyTargetLookup)
    {
        if (assetDependencyTargetLookup.TryResolve(dependency, out var resolvedTarget) &&
            (!string.Equals(resolvedTarget.AssetId.Value, sourceAssetId.Value, StringComparison.Ordinal) ||
             !string.Equals(resolvedTarget.BundleId.Value, sourceBundleId.Value, StringComparison.Ordinal)))
        {
            return new AssetDependencyDto(
                resolvedTarget.AssetId,
                resolvedTarget.BundleId,
                resolvedTarget.Name,
                resolvedTarget.AssetType,
                PathId: resolvedTarget.PathId,
                AssetTypeDisplayName: resolvedTarget.AssetTypeDisplayName);
        }

        return new AssetDependencyDto(
            ResolveCachedDependencyAssetId(dependency),
            sourceBundleId,
            dependency.AssetName,
            ParseAssetType(dependency.AssetType),
            PathId: dependency.PathId,
            AssetTypeDisplayName: ResolveAssetTypeDisplayName(dependency.AssetTypeDisplayName, dependency.AssetType));
    }

    private static AssetId ResolveCachedAssetId(BundleIndexAssetCacheEntryDto asset)
    {
        if (AssetId.TryParse(asset.AssetId, out var parsedAssetId))
        {
            return parsedAssetId;
        }

        if (!string.IsNullOrWhiteSpace(asset.SerializedFileName) && asset.PathId != 0)
        {
            return new AssetId(AssetsToolsAssetIdFactory.CreateAssetId(asset.SerializedFileName, asset.PathId));
        }

        return new AssetId($"asset.{Guid.NewGuid():N}");
    }

    private static AssetId ResolveCachedDependencyAssetId(BundleIndexAssetDependencyCacheEntryDto dependency)
    {
        if (AssetId.TryParse(dependency.AssetId, out var parsedAssetId))
        {
            return parsedAssetId;
        }

        if (!string.IsNullOrWhiteSpace(dependency.SerializedFileName) && dependency.PathId != 0)
        {
            return new AssetId(AssetsToolsAssetIdFactory.CreateAssetId(dependency.SerializedFileName, dependency.PathId));
        }

        return new AssetId($"asset.dependency.{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Bundle externals 是资源包级读模型，
    /// 这里统一归一化路径、去重，并在当前工作区上下文内尽量把 `CAB-*`
    /// 反查回具体资源包文件名，避免把关联解析职责推回 Presentation。
    /// </summary>
    private static IReadOnlyList<BundleExternalDependencyDto> MapBundleExternalDependencies(
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        if (dependencies is not { Count: > 0 })
        {
            return Array.Empty<BundleExternalDependencyDto>();
        }

        return dependencies
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency))
            .Select(static dependency => NormalizeLogicalPath(dependency!, dependency!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static dependency => dependency, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => ResolveBundleExternalDependency(dependency, serializedFileBundleLookup))
            .ToArray();
    }

    private static BundleExternalDependencyDto ResolveBundleExternalDependency(
        string dependency,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        var resolvedDependency = AssetsToolsBundleExternalDependencyDisplayResolver.Resolve(dependency);
        if (!TryResolveWorkspaceBundleTarget(resolvedDependency.ReferencePath, serializedFileBundleLookup, out var bundleTarget))
        {
            return resolvedDependency;
        }

        return resolvedDependency with
        {
            DisplayName = bundleTarget.DisplayName,
            ResolvedBundleDisplayName = bundleTarget.DisplayName
        };
    }

    private static bool TryResolveWorkspaceBundleTarget(
        string referencePath,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup,
        out WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        var matchedTargets = AssetsToolsBundleExternalDependencyDisplayResolver
            .EnumerateLookupCandidates(referencePath)
            .SelectMany(candidate =>
                serializedFileBundleLookup.TryGetValue(candidate, out var candidates)
                    ? candidates
                    : Array.Empty<WorkspaceSerializedFileBundleTarget>())
            .Distinct()
            .ToArray();

        if (matchedTargets.Length == 1)
        {
            bundleTarget = matchedTargets[0];
            return true;
        }

        bundleTarget = default!;
        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> BuildSerializedFileBundleLookup(
        IReadOnlyList<BundleIndexCacheEntryDto> cacheEntries)
    {
        var lookup = new Dictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cacheEntry in cacheEntries)
        {
            var displayName = ResolveBundleDisplayName(cacheEntry);
            var bundleTarget = new WorkspaceSerializedFileBundleTarget(
                BundleId: ResolveBundleId(cacheEntry),
                DisplayName: displayName,
                LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, displayName));

            var serializedFileNames = (cacheEntry.SerializedFileNames is { Count: > 0 }
                    ? cacheEntry.SerializedFileNames
                    : cacheEntry.Assets.Select(static asset => asset.SerializedFileName).ToArray())
                .Where(static serializedFileName => !string.IsNullOrWhiteSpace(serializedFileName))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var serializedFileName in serializedFileNames)
            {
                RegisterSerializedFileBundleTarget(lookup, serializedFileName, bundleTarget);
            }
        }

        return lookup.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<WorkspaceSerializedFileBundleTarget>)pair.Value
                .OrderBy(static target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static target => target.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void RegisterSerializedFileBundleTarget(
        IDictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>> lookup,
        string serializedFileName,
        WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        var normalizedSerializedFileName = NormalizeExternalLookupKey(serializedFileName);
        RegisterSerializedFileBundleTargetCore(lookup, normalizedSerializedFileName, bundleTarget);

        var fileName = Path.GetFileName(normalizedSerializedFileName);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            RegisterSerializedFileBundleTargetCore(lookup, fileName, bundleTarget);
        }
    }

    private static void RegisterSerializedFileBundleTargetCore(
        IDictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>> lookup,
        string key,
        WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!lookup.TryGetValue(key, out var targets))
        {
            targets = new HashSet<WorkspaceSerializedFileBundleTarget>();
            lookup[key] = targets;
        }

        targets.Add(bundleTarget);
    }

    private static string NormalizeExternalLookupKey(string value)
    {
        return value.Trim().Replace('\\', '/');
    }

    private static WorkspaceAssetDependencyTargetLookup BuildAssetDependencyTargetLookup(
        IReadOnlyList<BundleIndexCacheEntryDto> cacheEntries)
    {
        var targetsByAssetId = new Dictionary<string, WorkspaceAssetDependencyTarget>(StringComparer.Ordinal);
        var ambiguousAssetIds = new HashSet<string>(StringComparer.Ordinal);
        var targetsByObjectKey = new Dictionary<WorkspaceAssetObjectKey, WorkspaceAssetDependencyTarget>();
        var ambiguousObjectKeys = new HashSet<WorkspaceAssetObjectKey>();

        foreach (var cacheEntry in cacheEntries)
        {
            var bundleId = ResolveBundleId(cacheEntry);
            foreach (var asset in cacheEntry.Assets)
            {
                var assetId = ResolveCachedAssetId(asset);
                var target = new WorkspaceAssetDependencyTarget(
                    AssetId: assetId,
                    BundleId: bundleId,
                    Name: asset.AssetName,
                    AssetType: ParseAssetType(asset.AssetType),
                    AssetTypeDisplayName: ResolveAssetTypeDisplayName(asset.AssetTypeDisplayName, asset.AssetType),
                    PathId: asset.PathId);

                RegisterUniqueTarget(targetsByAssetId, ambiguousAssetIds, assetId.Value, target);
                foreach (var objectKey in CreateObjectKeys(asset.SerializedFileName, asset.PathId))
                {
                    RegisterUniqueTarget(targetsByObjectKey, ambiguousObjectKeys, objectKey, target);
                }
            }
        }

        foreach (var ambiguousAssetId in ambiguousAssetIds)
        {
            targetsByAssetId.Remove(ambiguousAssetId);
        }

        foreach (var ambiguousObjectKey in ambiguousObjectKeys)
        {
            targetsByObjectKey.Remove(ambiguousObjectKey);
        }

        return new WorkspaceAssetDependencyTargetLookup(targetsByAssetId, targetsByObjectKey);
    }

    private static void RegisterUniqueTarget<TKey>(
        IDictionary<TKey, WorkspaceAssetDependencyTarget> targets,
        ISet<TKey> ambiguousKeys,
        TKey key,
        WorkspaceAssetDependencyTarget target)
        where TKey : notnull
    {
        if (targets.TryGetValue(key, out var existingTarget))
        {
            if (!string.Equals(existingTarget.AssetId.Value, target.AssetId.Value, StringComparison.Ordinal) ||
                !string.Equals(existingTarget.BundleId.Value, target.BundleId.Value, StringComparison.Ordinal))
            {
                ambiguousKeys.Add(key);
            }

            return;
        }

        if (!ambiguousKeys.Contains(key))
        {
            targets[key] = target;
        }
    }

    private static IReadOnlyList<WorkspaceAssetObjectKey> CreateObjectKeys(string? serializedFileName, long pathId)
    {
        if (string.IsNullOrWhiteSpace(serializedFileName) || pathId == 0)
        {
            return Array.Empty<WorkspaceAssetObjectKey>();
        }

        return ExpandSerializedFileLookupCandidates(serializedFileName)
            .Select(candidate => new WorkspaceAssetObjectKey(candidate, pathId))
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceAssetObjectKey> CreateDependencyObjectKeys(
        BundleIndexAssetDependencyCacheEntryDto dependency)
    {
        if (dependency.PathId == 0)
        {
            return Array.Empty<WorkspaceAssetObjectKey>();
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { dependency.SerializedFileName, dependency.ExternalReferencePath })
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            foreach (var candidate in ExpandSerializedFileLookupCandidates(source))
            {
                candidates.Add(candidate);
            }
        }

        return candidates
            .Select(candidate => new WorkspaceAssetObjectKey(candidate, dependency.PathId))
            .ToArray();
    }

    private static IReadOnlyList<string> ExpandSerializedFileLookupCandidates(string value)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in AssetsToolsBundleExternalDependencyDisplayResolver.EnumerateLookupCandidates(value))
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(NormalizeAssetObjectLookupKey(candidate));
            }
        }

        return candidates.ToArray();
    }

    private static string NormalizeAssetObjectLookupKey(string value)
    {
        return NormalizeExternalLookupKey(value).ToLowerInvariant();
    }

    private static string ResolveBundleDisplayName(BundleIndexCacheEntryDto cacheEntry)
    {
        if (!ShouldRepairBundleDisplayName(cacheEntry.DisplayName))
        {
            return cacheEntry.DisplayName.Trim();
        }

        // 兼容早期索引缓存把标准化 artifact 文件名写入 DisplayName 的情况。
        return TryGetLeafName(cacheEntry.SourceFingerprint.SourcePath) ??
               TryGetLeafName(cacheEntry.LogicalPath) ??
               cacheEntry.DisplayName.Trim();
    }

    private static bool ShouldRepairBundleDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ||
               string.Equals(
                   displayName.Trim(),
                   NormalizedBundleArtifactFileName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetLeafName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var leafName = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(leafName) ? null : leafName;
    }
}
