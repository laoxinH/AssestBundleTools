using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 表示从关联资源变更卡片投影出来的真实操作目标。
/// </summary>
internal sealed record ProjectedChangeRecordTarget(
    BundleId BundleId,
    AssetId AssetId,
    string AssetName);
