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
               !string.Equals(
                   GetReplacementProjectionSignature(previousDetails),
                   GetReplacementProjectionSignature(currentDetails),
                   StringComparison.Ordinal) ||
               !Equals(previousDetails.SpriteEditGeometry, currentDetails.SpriteEditGeometry) ||
               previousDetails.SpriteEditAppliedAtUtc != currentDetails.SpriteEditAppliedAtUtc ||
               !string.Equals(
                   previousDetails.TextEditDraftId,
                   currentDetails.TextEditDraftId,
                   StringComparison.Ordinal) ||
               !string.Equals(
                   previousDetails.TextEditDraftMode,
                   currentDetails.TextEditDraftMode,
                   StringComparison.Ordinal) ||
               previousDetails.TextEditAppliedAtUtc != currentDetails.TextEditAppliedAtUtc ||
               GetLatestTextEditRecordTimestamp(previousDetails) !=
               GetLatestTextEditRecordTimestamp(currentDetails) ||
               GetLatestSpriteEditRecordTimestamp(previousDetails) !=
               GetLatestSpriteEditRecordTimestamp(currentDetails);
    }

    private static string GetReplacementProjectionSignature(AssetDetailsDto details)
    {
        return string.Join(
            "\u001f",
            details.ChangeRecords?
            .Where(static record => record.Kind is
                AssetChangeRecordKind.FileReplacement or
                AssetChangeRecordKind.WorkspaceReplacement or
                AssetChangeRecordKind.AssociatedFileReplacement)
            .OrderByDescending(static record => record.Sequence)
            .ThenByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => string.Join(
                "\u001e",
                record.Kind.ToString(),
                record.OperationId ?? string.Empty,
                record.IsEffectiveReplacement ? "1" : "0",
                record.TargetBundleId ?? string.Empty,
                record.TargetAssetId ?? string.Empty,
                record.SourceFilePath ?? string.Empty,
                record.SourceFileName ?? string.Empty,
                record.DisplayTimestampUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture))) ??
            Array.Empty<string>());
    }

    private static DateTimeOffset? GetLatestSpriteEditRecordTimestamp(AssetDetailsDto details)
    {
        return details.ChangeRecords?
            .Where(static record => record.Kind == AssetChangeRecordKind.SpriteEdit)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => (DateTimeOffset?)record.DisplayTimestampUtc)
            .FirstOrDefault();
    }

    private static DateTimeOffset? GetLatestTextEditRecordTimestamp(AssetDetailsDto details)
    {
        return details.ChangeRecords?
            .Where(static record => record.Kind == AssetChangeRecordKind.TextEdit)
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .Select(static record => (DateTimeOffset?)record.DisplayTimestampUtc)
            .FirstOrDefault();
    }
}
