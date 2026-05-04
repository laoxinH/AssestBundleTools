using System.IO;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 负责把工作区中的可提取资源整理成批量提取计划。
/// </summary>
/// <remarks>
/// 这里不做真实导出，只负责基于现有工作区读模型把范围、输出目录和候选项聚合出来，
/// 供预览查询和执行骨架复用，避免相同的遍历逻辑在多个 Handler 中重复出现。
/// </remarks>
internal static class WorkspaceBatchExtractionPlanner
{
    public static WorkspaceBatchExtractionPlan BuildPlan(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        AssetType? assetTypeFilter,
        string? outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);

        if (!workspaceSessionRegistry.TryGetBundleTree(workspaceId, out var bundleTree))
        {
            return new WorkspaceBatchExtractionPlan(
                WorkspaceDisplayName: string.Empty,
                SuggestedOutputDirectory: string.Empty,
                AssetTypeFilter: assetTypeFilter,
                Assets: Array.Empty<PlannedAssetExtraction>());
        }

        var bundleNodes = EnumerateBundleNodes(bundleTree).ToArray();
        var plannedAssets = new List<PlannedAssetExtraction>(capacity: 64);

        foreach (var bundleNode in bundleNodes)
        {
            if (!workspaceSessionRegistry.TryGetAssetList(
                    workspaceId,
                    bundleNode.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: assetTypeFilter,
                    assetTypeDisplayNameFilter: null,
                    out var assetListResult))
            {
                continue;
            }

            foreach (var item in assetListResult!.Items)
            {
                plannedAssets.Add(new PlannedAssetExtraction(
                    BundleId: item.BundleId,
                    AssetId: item.AssetId,
                    BundleDisplayName: assetListResult.BundleDisplayName,
                    AssetName: item.Name,
                    AssetType: item.AssetType,
                    SizeInBytes: item.SizeInBytes,
                    SuggestedOutputPath: BuildSuggestedOutputPath(
                        ResolveOutputDirectory(outputDirectory, workspaceId, assetTypeFilter),
                        assetListResult.BundleDisplayName,
                        item.Name,
                        item.AssetType)));
            }
        }

        var workspaceDisplayName = ResolveWorkspaceDisplayName(bundleNodes);
        return new WorkspaceBatchExtractionPlan(
            WorkspaceDisplayName: workspaceDisplayName,
            SuggestedOutputDirectory: ResolveOutputDirectory(outputDirectory, workspaceId, assetTypeFilter),
            AssetTypeFilter: assetTypeFilter,
            Assets: plannedAssets);
    }

    private static IEnumerable<BundleTreeNodeDto> EnumerateBundleNodes(IEnumerable<BundleTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsVirtualFolder)
            {
                foreach (var child in EnumerateBundleNodes(node.Children))
                {
                    yield return child;
                }

                continue;
            }

            yield return node;
        }
    }

    private static string ResolveWorkspaceDisplayName(IEnumerable<BundleTreeNodeDto> bundleNodes)
    {
        // 当前假数据工作区不直接在树节点里携带工作区名，这里保持兜底，避免预览模型出现空引用。
        return bundleNodes.Any() ? "CurrentWorkspace" : string.Empty;
    }

    private static string ResolveOutputDirectory(string? outputDirectory, WorkspaceId workspaceId, AssetType? assetTypeFilter)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory.Trim();
        }

        var scopeFolder = assetTypeFilter?.ToString() ?? "AllAssets";
        return Path.Combine("Exports", workspaceId.Value, scopeFolder);
    }

    private static string BuildSuggestedOutputPath(
        string outputDirectory,
        string bundleDisplayName,
        string assetName,
        AssetType assetType)
    {
        return Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(bundleDisplayName),
            $"{assetName}{GetAssetExtension(assetType)}");
    }

    private static string GetAssetExtension(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => ".png",
            AssetType.Sprite => ".png",
            AssetType.TextAsset => ".txt",
            AssetType.AudioClip => ".wav",
            AssetType.Mesh => ".mesh",
            AssetType.Material => ".mat",
            _ => ".bin"
        };
}

/// <summary>
/// 承载批量提取骨架规划结果的内部模型。
/// </summary>
internal sealed record WorkspaceBatchExtractionPlan(
    string WorkspaceDisplayName,
    string SuggestedOutputDirectory,
    AssetType? AssetTypeFilter,
    IReadOnlyList<PlannedAssetExtraction> Assets);

/// <summary>
/// 表示单个即将参与批量提取的资源。
/// </summary>
internal sealed record PlannedAssetExtraction(
    BundleId BundleId,
    AssetId AssetId,
    string BundleDisplayName,
    string AssetName,
    AssetType AssetType,
    long SizeInBytes,
    string SuggestedOutputPath);
