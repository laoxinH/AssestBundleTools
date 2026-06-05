using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class SpineAssetGroupPreviewInputSignature
{
    public static string Create(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        var parts = EnumerateParts(groupTab.Item)
            .Select(static part => string.Join(
                "\u001f",
                part.Role,
                part.BundleId.Value,
                part.AssetId.Value,
                part.FileReplacementSourcePath ?? string.Empty,
                part.FileReplacementSourceFileName ?? string.Empty,
                part.FileReplacementSourceAssetType?.ToString() ?? string.Empty,
                string.Join(",", (part.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>())
                    .Select(static record => string.Join(
                        ":",
                        record.OperationId,
                        record.Kind,
                        record.IsEffectiveReplacement,
                        record.IsEffectiveEdit,
                        record.AppliedAtUtc?.ToUnixTimeMilliseconds().ToString() ?? string.Empty,
                        record.SourceFilePath ?? string.Empty)))))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        var draftParts = groupTab.SpineEditDraft?.Parts
            .Select(static part => string.Join(
                "\u001f",
                part.BundleId.Value,
                part.AssetId,
                part.SelectedSourceFilePath ?? string.Empty))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        return string.Join(
            "\u001e",
            groupTab.BundleId.Value,
            groupTab.GroupId,
            string.Join("\u001d", parts),
            string.Join("\u001d", EnumerateDiagnostics(groupTab.Item)),
            string.Join("\u001d", draftParts));
    }

    private static IEnumerable<SpineAssetGroupPartDto> EnumerateParts(SpineAssetGroupDto group)
    {
        foreach (var part in group.SkeletonAssets)
        {
            yield return part;
        }

        foreach (var part in group.AtlasAssets)
        {
            yield return part;
        }

        foreach (var part in group.TexturePageAssets)
        {
            yield return part;
        }
    }

    private static IEnumerable<string> EnumerateDiagnostics(SpineAssetGroupDto group)
    {
        return group.Diagnostics
            .Select(static diagnostic => string.Join(
                "\u001f",
                diagnostic.Kind,
                diagnostic.GroupKey,
                diagnostic.AtlasPageName ?? string.Empty,
                diagnostic.AtlasPageWidth?.ToString() ?? string.Empty,
                diagnostic.AtlasPageHeight?.ToString() ?? string.Empty,
                diagnostic.TextureWidth?.ToString() ?? string.Empty,
                diagnostic.TextureHeight?.ToString() ?? string.Empty,
                diagnostic.AnimationUnsupportedFeature?.ToString() ?? string.Empty,
                diagnostic.SpineVersion ?? string.Empty,
                diagnostic.RuntimeAdapterKey ?? string.Empty,
                string.Join(",", diagnostic.RelatedParts
                    .Select(static part => string.Join(
                        ":",
                        part.Role,
                        part.BundleId.Value,
                        part.AssetId.Value)))))
            .OrderBy(static value => value, StringComparer.Ordinal);
    }
}
