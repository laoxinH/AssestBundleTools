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
    private static CachedBundleState MapBundleState(
        BundleIndexCacheEntryDto cacheEntry,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        var bundleId = ResolveBundleId(cacheEntry);

        var assets = cacheEntry.Assets
            .Select(asset => MapAssetState(bundleId, asset))
            .ToArray();

        return new CachedBundleState(
            BundleId: bundleId,
            DisplayName: cacheEntry.DisplayName,
            LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, cacheEntry.DisplayName),
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

    private static CachedAssetState MapAssetState(BundleId bundleId, BundleIndexAssetCacheEntryDto asset)
    {
        var assetId = AssetId.TryParse(asset.AssetId, out var parsedAssetId)
            ? parsedAssetId
            : new AssetId($"asset.{Guid.NewGuid():N}");
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
            dependencies: MapDependencies(bundleId, asset.Dependencies))
        {
            IncludeInAssetList = asset.IncludeInAssetList
        };
    }

    private static IReadOnlyList<AssetDependencyDto> MapDependencies(
        BundleId bundleId,
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies)
    {
        if (dependencies is not { Count: > 0 })
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return dependencies
            .OrderBy(static dependency => dependency.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => new AssetDependencyDto(
                AssetId.TryParse(dependency.AssetId, out var parsedAssetId)
                    ? parsedAssetId
                    : new AssetId(dependency.AssetId),
                bundleId,
                dependency.AssetName,
                ParseAssetType(dependency.AssetType),
                PathId: dependency.PathId,
                AssetTypeDisplayName: ResolveAssetTypeDisplayName(dependency.AssetTypeDisplayName, dependency.AssetType)))
            .ToArray();
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
            var bundleTarget = new WorkspaceSerializedFileBundleTarget(
                BundleId: ResolveBundleId(cacheEntry),
                DisplayName: cacheEntry.DisplayName,
                LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, cacheEntry.DisplayName));

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
}
