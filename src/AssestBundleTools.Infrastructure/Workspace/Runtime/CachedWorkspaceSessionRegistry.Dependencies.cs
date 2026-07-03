using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

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
            .ToDictionary(static node => CreateAssetKey(node.BundleId.Value, node.AssetState.AssetId.Value), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> BuildReverseDependenciesByAssetKey(
        IReadOnlyDictionary<string, AssetGraphNodeState> assetGraphNodesByKey)
    {
        var reverseDependencies = new Dictionary<string, List<AssetDependencyDto>>(StringComparer.Ordinal);

        foreach (var assetNode in assetGraphNodesByKey.Values)
        {
            foreach (var dependency in assetNode.AssetState.Dependencies)
            {
                var dependencyAssetKey = CreateAssetKey(dependency.BundleId.Value, dependency.AssetId.Value);
                if (!reverseDependencies.TryGetValue(dependencyAssetKey, out var reverseDependencyList))
                {
                    reverseDependencyList = new List<AssetDependencyDto>();
                    reverseDependencies[dependencyAssetKey] = reverseDependencyList;
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
        IReadOnlyDictionary<string, AssetGraphNodeState> assetGraphNodesByKey)
    {
        if (sourceAssetState.Dependencies.Count == 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        var closure = new List<AssetDependencyDto>();
        var visitedAssetKeys = new HashSet<string>(StringComparer.Ordinal);
        var pendingDependencies = new Queue<(AssetDependencyDto Dependency, int Depth)>();
        foreach (var dependency in sourceAssetState.Dependencies)
        {
            pendingDependencies.Enqueue((dependency, 1));
        }

        while (pendingDependencies.Count > 0)
        {
            var (dependency, depth) = pendingDependencies.Dequeue();
            var dependencyAssetKey = CreateAssetKey(dependency.BundleId.Value, dependency.AssetId.Value);
            if (!visitedAssetKeys.Add(dependencyAssetKey))
            {
                continue;
            }

            closure.Add(dependency with
            {
                DependencyDepth = depth
            });

            if (!assetGraphNodesByKey.TryGetValue(dependencyAssetKey, out var dependencyNode))
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

    private static IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> BuildTextureAssociationsByAssetKey(
        IReadOnlyDictionary<string, AssetGraphNodeState> assetGraphNodesByKey,
        IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> reverseDependenciesByAssetKey)
    {
        var textureAssociations = new Dictionary<string, IReadOnlyList<AssetDependencyDto>>(StringComparer.Ordinal);
        foreach (var assetNode in assetGraphNodesByKey.Values.Where(static node => node.AssetState.AssetType == AssetType.Mesh))
        {
            var associations = BuildTextureAssociationsForMesh(
                assetNode,
                assetGraphNodesByKey,
                reverseDependenciesByAssetKey);
            if (associations.Count > 0)
            {
                textureAssociations[CreateAssetKey(assetNode.BundleId.Value, assetNode.AssetState.AssetId.Value)] = associations;
            }
        }

        return textureAssociations;
    }

    private static IReadOnlyList<WorkspaceAssociatedAssetImpactDto> BuildAssociatedAssetImpacts(
        WorkspaceSessionState workspaceState,
        BundleId changedBundleId,
        AssetId changedAssetId)
    {
        if (string.IsNullOrWhiteSpace(changedAssetId.Value))
        {
            return Array.Empty<WorkspaceAssociatedAssetImpactDto>();
        }

        var changedAssetKey = CreateAssetKey(changedBundleId.Value, changedAssetId.Value);
        var impactsByAssetKey = new Dictionary<string, WorkspaceAssociatedAssetImpactDto>(StringComparer.Ordinal);
        if (workspaceState.TryGetReverseDependencies(changedBundleId, changedAssetId, out var directDependents))
        {
            foreach (var dependent in directDependents)
            {
                AddAssociatedAssetImpact(impactsByAssetKey, dependent);
            }
        }

        // 直接反向引用覆盖 Texture2D / TextAsset / Material / Sprite 等通用关联资源；
        // Mesh 的贴图化预览还需要额外保留从 Renderer/Material 链路推导出的间接贴图影响。
        foreach (var assetNode in workspaceState.AssetGraphNodesByKey.Values)
        {
            if (!assetNode.AssetState.IncludeInAssetList)
            {
                continue;
            }

            if (!workspaceState.TryGetTextureAssociations(assetNode.BundleId, assetNode.AssetState.AssetId, out var textureAssociations) ||
                !textureAssociations.Any(textureAssociation =>
                    string.Equals(
                        CreateAssetKey(textureAssociation.BundleId.Value, textureAssociation.AssetId.Value),
                        changedAssetKey,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            AddAssociatedAssetImpact(impactsByAssetKey, CreateDependencyDescriptor(assetNode));
        }

        impactsByAssetKey.Remove(changedAssetKey);
        return impactsByAssetKey.Values
            .OrderBy(static impact => impact.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static impact => impact.BundleId.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static impact => impact.PathId)
            .ToArray();
    }

    private static void AddAssociatedAssetImpact(
        IDictionary<string, WorkspaceAssociatedAssetImpactDto> impactsByAssetKey,
        AssetDependencyDto dependency)
    {
        var assetKey = CreateAssetKey(dependency.BundleId.Value, dependency.AssetId.Value);
        if (string.IsNullOrWhiteSpace(dependency.AssetId.Value) ||
            string.IsNullOrWhiteSpace(dependency.BundleId.Value) ||
            impactsByAssetKey.ContainsKey(assetKey))
        {
            return;
        }

        impactsByAssetKey[assetKey] = new WorkspaceAssociatedAssetImpactDto(
            dependency.BundleId,
            dependency.AssetId,
            dependency.Name,
            dependency.AssetType,
            dependency.PathId,
            dependency.AssetTypeDisplayName);
    }

    private static IReadOnlyList<AssetDependencyDto> BuildTextureAssociationsForMesh(
        AssetGraphNodeState meshNode,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetGraphNodesByKey,
        IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> reverseDependenciesByAssetKey)
    {
        var meshAsset = meshNode.AssetState;
        var meshAssetKey = CreateAssetKey(meshNode.BundleId.Value, meshAsset.AssetId.Value);
        var associations = new List<AssetDependencyDto>();
        var seenAssetKeys = new HashSet<string>(StringComparer.Ordinal);

        if (reverseDependenciesByAssetKey.TryGetValue(meshAssetKey, out var reverseDependencies))
        {
            foreach (var referrer in reverseDependencies.Where(IsMeshTextureAssociationReferrer))
            {
                AddReachableTextureAssociations(
                    referrer,
                    meshAssetKey,
                    assetGraphNodesByKey,
                    associations,
                    seenAssetKeys,
                    maxDepth: 4);
            }
        }

        if (associations.Count == 0)
        {
            // 主路径无法从 MeshFilter/Renderer 链路抵达贴图时，再兼容少数 Mesh 自身直接挂 Texture2D/Sprite 的结构。
            foreach (var directDependency in meshAsset.Dependencies.Where(static dependency => IsTextureAssociationType(dependency.AssetType)))
            {
                AddTextureAssociation(directDependency, associations, seenAssetKeys);
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
        string sourceMeshAssetKey,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetGraphNodesByKey,
        ICollection<AssetDependencyDto> associations,
        ISet<string> seenAssetKeys,
        int maxDepth)
    {
        var visitedAssetKeys = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(sourceMeshAssetKey))
        {
            visitedAssetKeys.Add(sourceMeshAssetKey);
        }

        var pending = new Queue<(AssetDependencyDto Dependency, int Depth)>();
        pending.Enqueue((rootDependency, 0));

        while (pending.Count > 0)
        {
            var (dependency, depth) = pending.Dequeue();
            var dependencyAssetKey = CreateAssetKey(dependency.BundleId.Value, dependency.AssetId.Value);
            if (!visitedAssetKeys.Add(dependencyAssetKey) || depth > maxDepth)
            {
                continue;
            }

            if (IsTextureAssociationType(dependency.AssetType))
            {
                AddTextureAssociation(dependency, associations, seenAssetKeys);
                continue;
            }

            if (!assetGraphNodesByKey.TryGetValue(dependencyAssetKey, out var dependencyNode))
            {
                continue;
            }

            if (IsTextureAssociationType(dependencyNode.AssetState.AssetType))
            {
                AddTextureAssociation(CreateDependencyDescriptor(dependencyNode), associations, seenAssetKeys);
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
        ISet<string> seenAssetKeys)
    {
        if (seenAssetKeys.Add(CreateAssetKey(textureDependency.BundleId.Value, textureDependency.AssetId.Value)))
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
