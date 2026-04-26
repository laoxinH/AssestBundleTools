using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示 Bundle 浏览树中的一个节点。
/// </summary>
public sealed record BundleTreeNodeDto(
    BundleId BundleId,
    string DisplayName,
    string LogicalPath,
    string SourcePath,
    bool IsVirtualFolder,
    int AssetCount,
    IReadOnlyList<BundleTreeNodeDto> Children)
{
    public bool CanOpenAssets => !IsVirtualFolder;
}
