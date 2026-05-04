using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器和预览中“已解析资源”可跳转的稳定目标。
/// PathId 用于用户可理解的 Unity 对象定位，实际打开仍依赖 BundleId + AssetId 避免跨包碰撞。
/// </summary>
public sealed record ResolvedAssetNavigationTarget(
    AssetId AssetId,
    BundleId BundleId,
    long PathId,
    string DisplayName,
    string AssetTypeText);
