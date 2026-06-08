using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class SpineAssetGroupSnapshotComparer
{
    public static bool HasSameSnapshot(
        SpineAssetGroupDto left,
        SpineAssetGroupDto right)
    {
        return string.Equals(left.GroupId, right.GroupId, StringComparison.Ordinal) &&
               left.BundleId.Equals(right.BundleId) &&
               string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
               string.Equals(left.GroupKey, right.GroupKey, StringComparison.Ordinal) &&
               HasSameParts(left.SkeletonAssets, right.SkeletonAssets) &&
               HasSameParts(left.AtlasAssets, right.AtlasAssets) &&
               HasSameParts(left.TexturePageAssets, right.TexturePageAssets) &&
               HasSameDiagnostics(left.Diagnostics, right.Diagnostics);
    }

    public static bool HasSameStructureSnapshot(
        SpineAssetGroupDto left,
        SpineAssetGroupDto right)
    {
        return string.Equals(left.GroupId, right.GroupId, StringComparison.Ordinal) &&
               left.BundleId.Equals(right.BundleId) &&
               string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
               string.Equals(left.GroupKey, right.GroupKey, StringComparison.Ordinal) &&
               HasSameStructuralParts(left.SkeletonAssets, right.SkeletonAssets) &&
               HasSameStructuralParts(left.AtlasAssets, right.AtlasAssets) &&
               HasSameStructuralParts(left.TexturePageAssets, right.TexturePageAssets) &&
               HasSameStructuralDiagnostics(left.Diagnostics, right.Diagnostics);
    }

    private static bool HasSameParts(
        IReadOnlyList<SpineAssetGroupPartDto> left,
        IReadOnlyList<SpineAssetGroupPartDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!HasSamePart(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSamePart(
        SpineAssetGroupPartDto left,
        SpineAssetGroupPartDto right)
    {
        return left.Role == right.Role &&
               left.BundleId.Equals(right.BundleId) &&
               left.AssetId.Equals(right.AssetId) &&
               left.AssetType == right.AssetType &&
               string.Equals(left.BundleDisplayName, right.BundleDisplayName, StringComparison.Ordinal) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
               string.Equals(left.AssetPath, right.AssetPath, StringComparison.Ordinal) &&
               string.Equals(left.AssetTypeDisplayName, right.AssetTypeDisplayName, StringComparison.Ordinal) &&
               Nullable.Equals(left.LastExtractedAtUtc, right.LastExtractedAtUtc) &&
               string.Equals(left.FileReplacementSourcePath, right.FileReplacementSourcePath, StringComparison.Ordinal) &&
               string.Equals(left.FileReplacementSourceFileName, right.FileReplacementSourceFileName, StringComparison.Ordinal) &&
               Nullable.Equals(left.FileReplacementSourceAssetType, right.FileReplacementSourceAssetType) &&
               HasSameChangeRecordIds(left.ChangeRecords, right.ChangeRecords);
    }

    private static bool HasSameStructuralParts(
        IReadOnlyList<SpineAssetGroupPartDto> left,
        IReadOnlyList<SpineAssetGroupPartDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!HasSameStructuralPart(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameStructuralPart(
        SpineAssetGroupPartDto left,
        SpineAssetGroupPartDto right)
    {
        return left.Role == right.Role &&
               left.BundleId.Equals(right.BundleId) &&
               left.AssetId.Equals(right.AssetId) &&
               left.AssetType == right.AssetType &&
               string.Equals(left.BundleDisplayName, right.BundleDisplayName, StringComparison.Ordinal) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
               string.Equals(left.AssetPath, right.AssetPath, StringComparison.Ordinal) &&
               string.Equals(left.AssetTypeDisplayName, right.AssetTypeDisplayName, StringComparison.Ordinal);
    }

    private static bool HasSameChangeRecordIds(
        IReadOnlyList<AssetChangeRecordDto>? left,
        IReadOnlyList<AssetChangeRecordDto>? right)
    {
        left ??= Array.Empty<AssetChangeRecordDto>();
        right ??= Array.Empty<AssetChangeRecordDto>();
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].OperationId, right[index].OperationId, StringComparison.Ordinal) ||
                left[index].Kind != right[index].Kind ||
                left[index].IsEffectiveReplacement != right[index].IsEffectiveReplacement ||
                left[index].IsEffectiveEdit != right[index].IsEffectiveEdit ||
                !left[index].AppliedAtUtc.Equals(right[index].AppliedAtUtc))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> left,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Kind != right[index].Kind ||
                !string.Equals(left[index].GroupKey, right[index].GroupKey, StringComparison.Ordinal) ||
                !string.Equals(left[index].AtlasPageName, right[index].AtlasPageName, StringComparison.Ordinal) ||
                left[index].AtlasPageWidth != right[index].AtlasPageWidth ||
                left[index].AtlasPageHeight != right[index].AtlasPageHeight ||
                left[index].TextureWidth != right[index].TextureWidth ||
                left[index].TextureHeight != right[index].TextureHeight ||
                left[index].AnimationUnsupportedFeature != right[index].AnimationUnsupportedFeature ||
                !string.Equals(left[index].SpineVersion, right[index].SpineVersion, StringComparison.Ordinal) ||
                !string.Equals(left[index].RuntimeAdapterKey, right[index].RuntimeAdapterKey, StringComparison.Ordinal) ||
                !HasSameParts(left[index].RelatedParts, right[index].RelatedParts))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameStructuralDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> left,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Kind != right[index].Kind ||
                !string.Equals(left[index].GroupKey, right[index].GroupKey, StringComparison.Ordinal) ||
                !string.Equals(left[index].AtlasPageName, right[index].AtlasPageName, StringComparison.Ordinal) ||
                left[index].AtlasPageWidth != right[index].AtlasPageWidth ||
                left[index].AtlasPageHeight != right[index].AtlasPageHeight ||
                left[index].TextureWidth != right[index].TextureWidth ||
                left[index].TextureHeight != right[index].TextureHeight ||
                left[index].AnimationUnsupportedFeature != right[index].AnimationUnsupportedFeature ||
                !string.Equals(left[index].SpineVersion, right[index].SpineVersion, StringComparison.Ordinal) ||
                !string.Equals(left[index].RuntimeAdapterKey, right[index].RuntimeAdapterKey, StringComparison.Ordinal) ||
                !HasSameStructuralParts(left[index].RelatedParts, right[index].RelatedParts))
            {
                return false;
            }
        }

        return true;
    }
}
