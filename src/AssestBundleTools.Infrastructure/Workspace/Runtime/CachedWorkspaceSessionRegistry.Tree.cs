using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(IReadOnlyList<CachedBundleState> bundleStates)
    {
        return bundleStates
            .GroupBy(bundle => ResolveBundleGroupKey(bundle.LogicalPath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var childBundles = group
                    .OrderBy(bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(bundle => new BundleTreeNodeDto(
                        BundleId: bundle.BundleId,
                        DisplayName: bundle.DisplayName,
                        LogicalPath: bundle.LogicalPath,
                        SourcePath: bundle.SourcePath,
                        IsVirtualFolder: false,
                        AssetCount: bundle.Assets.Count(static asset => asset.IncludeInAssetList),
                        Children: Array.Empty<BundleTreeNodeDto>()))
                    .ToArray();

                return new BundleTreeNodeDto(
                    BundleId: new BundleId($"folder.{group.Key}"),
                    DisplayName: group.Key,
                    LogicalPath: group.Key,
                    SourcePath: string.Empty,
                    IsVirtualFolder: true,
                    AssetCount: childBundles.Sum(static node => node.AssetCount),
                    Children: childBundles);
            })
            .ToArray();
    }

    private static string ResolveBundleGroupKey(string logicalPath)
    {
        var normalizedLogicalPath = logicalPath.Trim().Replace('\\', '/');
        var firstSlashIndex = normalizedLogicalPath.IndexOf('/');
        if (firstSlashIndex <= 0)
        {
            return "bundles";
        }

        return normalizedLogicalPath[..firstSlashIndex];
    }
}
