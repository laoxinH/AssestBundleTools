using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 从 Spine 组投影变更卡片解析真实组成资产目标。
/// </summary>
internal static class ProjectedChangeRecordTargetResolver
{
    public static bool TryResolve(
        SpineAssetGroupDocumentTabViewModel? spineGroupTab,
        string? operationId,
        [NotNullWhen(true)] out ProjectedChangeRecordTarget? target)
    {
        target = null;
        if (spineGroupTab is null || string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        var normalizedOperationId = operationId.Trim();
        var record = spineGroupTab.Inspector.ChangeRecords.FirstOrDefault(candidate =>
            string.Equals(candidate.OperationId, normalizedOperationId, StringComparison.Ordinal));
        if (record is null ||
            !record.HasTargetAsset ||
            !BundleId.TryParse(record.TargetBundleId, out var targetBundleId) ||
            !AssetId.TryParse(record.TargetAssetId, out var targetAssetId))
        {
            return false;
        }

        target = new ProjectedChangeRecordTarget(
            targetBundleId,
            targetAssetId,
            string.IsNullOrWhiteSpace(record.TargetAssetName)
                ? record.TargetAssetId
                : record.TargetAssetName.Trim());
        return true;
    }
}
