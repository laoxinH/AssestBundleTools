using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 判断详情读模型刷新后是否需要丢弃旧预览并重新加载有效内容。
/// </summary>
internal static class AssetPreviewInvalidationPolicy
{
    public static bool HasEffectivePreviewProjectionChanged(
        AssetDetailsDto? previousDetails,
        AssetDetailsDto currentDetails)
    {
        ArgumentNullException.ThrowIfNull(currentDetails);
        if (previousDetails is null)
        {
            return false;
        }

        return !string.Equals(
                   previousDetails.FileReplacementSourcePath,
                   currentDetails.FileReplacementSourcePath,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   previousDetails.FileReplacementSourceFileName,
                   currentDetails.FileReplacementSourceFileName,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   previousDetails.WorkspaceReplacementSourceBundleId?.Value,
                   currentDetails.WorkspaceReplacementSourceBundleId?.Value,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   previousDetails.WorkspaceReplacementSourceAssetId?.Value,
                   currentDetails.WorkspaceReplacementSourceAssetId?.Value,
                   StringComparison.Ordinal) ||
               GetLatestReplacementRecordTimestamp(previousDetails) !=
               GetLatestReplacementRecordTimestamp(currentDetails) ||
               !Equals(previousDetails.SpriteEditGeometry, currentDetails.SpriteEditGeometry) ||
               previousDetails.SpriteEditAppliedAtUtc != currentDetails.SpriteEditAppliedAtUtc ||
               GetLatestSpriteEditRecordTimestamp(previousDetails) !=
               GetLatestSpriteEditRecordTimestamp(currentDetails);
    }

    private static DateTimeOffset? GetLatestReplacementRecordTimestamp(AssetDetailsDto details)
    {
        return details.ChangeRecords?
            .Where(static record => record.Kind is
                AssetChangeRecordKind.FileReplacement or
                AssetChangeRecordKind.WorkspaceReplacement or
                AssetChangeRecordKind.AssociatedFileReplacement)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => (DateTimeOffset?)record.DisplayTimestampUtc)
            .FirstOrDefault();
    }

    private static DateTimeOffset? GetLatestSpriteEditRecordTimestamp(AssetDetailsDto details)
    {
        return details.ChangeRecords?
            .Where(static record => record.Kind == AssetChangeRecordKind.SpriteEdit)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => (DateTimeOffset?)record.DisplayTimestampUtc)
            .FirstOrDefault();
    }
}
