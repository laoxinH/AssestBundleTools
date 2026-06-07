using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    /// <summary>
    /// 运行时资产图索引用于支撑“反向引用”和“依赖闭包”这类二次视图，
    /// 避免把这类遍历逻辑回推到 Presentation。
    /// </summary>
    private static IReadOnlyDictionary<string, AssetGraphNodeState> BuildAssetGraphNodes(
        IReadOnlyList<CachedBundleState> bundleStates)
    {
        return bundleStates
            .SelectMany(bundleState => bundleState.Assets.Select(assetState => new AssetGraphNodeState(
                BundleId: bundleState.BundleId,
                AssetState: assetState)))
            .ToDictionary(static node => node.AssetState.AssetId.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> BuildReverseDependenciesByAssetId(
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
    {
        var reverseDependencies = new Dictionary<string, List<AssetDependencyDto>>(StringComparer.Ordinal);

        foreach (var assetNode in assetsById.Values)
        {
            foreach (var dependency in assetNode.AssetState.Dependencies)
            {
                if (!reverseDependencies.TryGetValue(dependency.AssetId.Value, out var reverseDependencyList))
                {
                    reverseDependencyList = new List<AssetDependencyDto>();
                    reverseDependencies[dependency.AssetId.Value] = reverseDependencyList;
                }

                reverseDependencyList.Add(CreateDependencyDescriptor(assetNode));
            }
        }

        return reverseDependencies.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<AssetDependencyDto>)pair.Value
                .OrderBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static dependency => dependency.BundleId.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static dependency => dependency.PathId)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<AssetDependencyDto> BuildDependencyClosure(
        CachedAssetState sourceAssetState,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
    {
        if (sourceAssetState.Dependencies.Count == 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        var closure = new List<AssetDependencyDto>();
        var visitedAssetIds = new HashSet<string>(StringComparer.Ordinal);
        var pendingDependencies = new Queue<(AssetDependencyDto Dependency, int Depth)>();
        foreach (var dependency in sourceAssetState.Dependencies)
        {
            pendingDependencies.Enqueue((dependency, 1));
        }

        while (pendingDependencies.Count > 0)
        {
            var (dependency, depth) = pendingDependencies.Dequeue();
            if (!visitedAssetIds.Add(dependency.AssetId.Value))
            {
                continue;
            }

            closure.Add(dependency with
            {
                DependencyDepth = depth
            });

            if (!assetsById.TryGetValue(dependency.AssetId.Value, out var dependencyNode))
            {
                continue;
            }

            foreach (var nestedDependency in dependencyNode.AssetState.Dependencies)
            {
                pendingDependencies.Enqueue((nestedDependency, depth + 1));
            }
        }

        return closure
            .OrderBy(static dependency => dependency.DependencyDepth ?? int.MaxValue)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static dependency => dependency.BundleId.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static dependency => dependency.PathId)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> BuildTextureAssociationsByAssetId(
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById,
        IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> reverseDependenciesByAssetId)
    {
        var textureAssociations = new Dictionary<string, IReadOnlyList<AssetDependencyDto>>(StringComparer.Ordinal);
        foreach (var assetNode in assetsById.Values.Where(static node => node.AssetState.AssetType == AssetType.Mesh))
        {
            var associations = BuildTextureAssociationsForMesh(
                assetNode.AssetState,
                assetsById,
                reverseDependenciesByAssetId);
            if (associations.Count > 0)
            {
                textureAssociations[assetNode.AssetState.AssetId.Value] = associations;
            }
        }

        return textureAssociations;
    }

    private static IReadOnlyList<WorkspaceAssociatedAssetImpactDto> BuildAssociatedAssetImpacts(
        WorkspaceSessionState workspaceState,
        AssetId changedAssetId)
    {
        if (string.IsNullOrWhiteSpace(changedAssetId.Value))
        {
            return Array.Empty<WorkspaceAssociatedAssetImpactDto>();
        }

        var impactsByAssetId = new Dictionary<string, WorkspaceAssociatedAssetImpactDto>(StringComparer.Ordinal);
        if (workspaceState.TryGetReverseDependencies(changedAssetId, out var directDependents))
        {
            foreach (var dependent in directDependents)
            {
                AddAssociatedAssetImpact(impactsByAssetId, dependent);
            }
        }

        // 直接反向引用覆盖 Texture2D / TextAsset / Material / Sprite 等通用关联资源；
        // Mesh 的贴图化预览还需要额外保留从 Renderer/Material 链路推导出的间接贴图影响。
        foreach (var assetNode in workspaceState.AssetsById.Values)
        {
            if (!assetNode.AssetState.IncludeInAssetList)
            {
                continue;
            }

            if (!workspaceState.TryGetTextureAssociations(assetNode.AssetState.AssetId, out var textureAssociations) ||
                !textureAssociations.Any(textureAssociation =>
                    string.Equals(textureAssociation.AssetId.Value, changedAssetId.Value, StringComparison.Ordinal)))
            {
                continue;
            }

            AddAssociatedAssetImpact(impactsByAssetId, CreateDependencyDescriptor(assetNode));
        }

        impactsByAssetId.Remove(changedAssetId.Value);
        return impactsByAssetId.Values
            .OrderBy(static impact => impact.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static impact => impact.BundleId.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static impact => impact.PathId)
            .ToArray();
    }

    private static void AddAssociatedAssetImpact(
        IDictionary<string, WorkspaceAssociatedAssetImpactDto> impactsByAssetId,
        AssetDependencyDto dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency.AssetId.Value) ||
            impactsByAssetId.ContainsKey(dependency.AssetId.Value))
        {
            return;
        }

        impactsByAssetId[dependency.AssetId.Value] = new WorkspaceAssociatedAssetImpactDto(
            dependency.BundleId,
            dependency.AssetId,
            dependency.Name,
            dependency.AssetType,
            dependency.PathId,
            dependency.AssetTypeDisplayName);
    }

    private static IReadOnlyList<AssetDependencyDto> BuildTextureAssociationsForMesh(
        CachedAssetState meshAsset,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById,
        IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> reverseDependenciesByAssetId)
    {
        var associations = new List<AssetDependencyDto>();
        var seenAssetIds = new HashSet<string>(StringComparer.Ordinal);

        if (reverseDependenciesByAssetId.TryGetValue(meshAsset.AssetId.Value, out var reverseDependencies))
        {
            foreach (var referrer in reverseDependencies.Where(IsMeshTextureAssociationReferrer))
            {
                AddReachableTextureAssociations(
                    referrer,
                    meshAsset.AssetId.Value,
                    assetsById,
                    associations,
                    seenAssetIds,
                    maxDepth: 4);
            }
        }

        if (associations.Count == 0)
        {
            // 主路径无法从 MeshFilter/Renderer 链路抵达贴图时，再兼容少数 Mesh 自身直接挂 Texture2D/Sprite 的结构。
            foreach (var directDependency in meshAsset.Dependencies.Where(static dependency => IsTextureAssociationType(dependency.AssetType)))
            {
                AddTextureAssociation(directDependency, associations, seenAssetIds);
            }
        }

        return associations
            .OrderBy(static dependency => dependency.AssetType == AssetType.Texture2D ? 0 : 1)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static dependency => dependency.PathId)
            .ToArray();
    }

    private static void AddReachableTextureAssociations(
        AssetDependencyDto rootDependency,
        string sourceMeshAssetId,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById,
        ICollection<AssetDependencyDto> associations,
        ISet<string> seenAssetIds,
        int maxDepth)
    {
        var visitedAssetIds = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(sourceMeshAssetId))
        {
            visitedAssetIds.Add(sourceMeshAssetId);
        }

        var pending = new Queue<(AssetDependencyDto Dependency, int Depth)>();
        pending.Enqueue((rootDependency, 0));

        while (pending.Count > 0)
        {
            var (dependency, depth) = pending.Dequeue();
            if (!visitedAssetIds.Add(dependency.AssetId.Value) || depth > maxDepth)
            {
                continue;
            }

            if (IsTextureAssociationType(dependency.AssetType))
            {
                AddTextureAssociation(dependency, associations, seenAssetIds);
                continue;
            }

            if (!assetsById.TryGetValue(dependency.AssetId.Value, out var dependencyNode))
            {
                continue;
            }

            if (IsTextureAssociationType(dependencyNode.AssetState.AssetType))
            {
                AddTextureAssociation(CreateDependencyDescriptor(dependencyNode), associations, seenAssetIds);
                continue;
            }

            foreach (var nestedDependency in dependencyNode.AssetState.Dependencies)
            {
                pending.Enqueue((nestedDependency, depth + 1));
            }
        }
    }

    private static void AddTextureAssociation(
        AssetDependencyDto textureDependency,
        ICollection<AssetDependencyDto> associations,
        ISet<string> seenAssetIds)
    {
        if (seenAssetIds.Add(textureDependency.AssetId.Value))
        {
            associations.Add(textureDependency);
        }
    }

    private static bool IsMeshTextureAssociationReferrer(AssetDependencyDto dependency)
    {
        var typeName = string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName)
            ? dependency.AssetType.ToString()
            : dependency.AssetTypeDisplayName;
        return typeName.Contains("Renderer", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("MeshFilter", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("ParticleSystem", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("GameObject", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextureAssociationType(AssetType assetType) =>
        assetType is AssetType.Texture2D or AssetType.Sprite;

    private static AssetDependencyDto CreateDependencyDescriptor(AssetGraphNodeState assetNode)
    {
        return new AssetDependencyDto(
            AssetId: assetNode.AssetState.AssetId,
            BundleId: assetNode.BundleId,
            Name: assetNode.AssetState.Name,
            AssetType: assetNode.AssetState.AssetType,
            PathId: assetNode.AssetState.PathId,
            AssetTypeDisplayName: assetNode.AssetState.AssetTypeDisplayName);
    }
}
