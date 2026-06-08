using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class SpineAssetGroupPreviewInputSignature
{
    public static string Create(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        var parts = EnumerateParts(groupTab.Item)
            .Select(part => string.Join(
                "\u001f",
                part.Role,
                part.BundleId.Value,
                part.AssetId.Value,
                ResolveEffectiveSourcePath(groupTab, part),
                ResolveEffectiveSourceFileName(groupTab, part),
                ResolveEffectiveSourceAssetType(groupTab, part)))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        return string.Join(
            "\u001e",
            groupTab.BundleId.Value,
            groupTab.GroupId,
            string.Join("\u001d", parts),
            string.Join("\u001d", EnumerateDiagnostics(groupTab.Item)));
    }

    public static bool MatchesLoadedPreviewInput(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        if (groupTab.Details is null ||
            string.IsNullOrWhiteSpace(groupTab.LoadedPreviewInputSignature))
        {
            return false;
        }

        return string.Equals(
            groupTab.LoadedPreviewInputSignature,
            Create(groupTab),
            StringComparison.Ordinal);
    }

    private static string ResolveEffectiveSourcePath(
        SpineAssetGroupDocumentTabViewModel groupTab,
        SpineAssetGroupPartDto part)
    {
        var draftSourcePath = groupTab.SpineEditDraft?.Parts
            .FirstOrDefault(draftPart =>
                draftPart.BundleId.Equals(part.BundleId) &&
                string.Equals(draftPart.AssetId, part.AssetId.Value, StringComparison.Ordinal))
            ?.SelectedSourceFilePath;
        return NormalizeSourcePath(draftSourcePath ?? part.FileReplacementSourcePath);
    }

    private static string ResolveEffectiveSourceFileName(
        SpineAssetGroupDocumentTabViewModel groupTab,
        SpineAssetGroupPartDto part)
    {
        var effectiveSourcePath = ResolveEffectiveSourcePath(groupTab, part);
        if (!string.IsNullOrWhiteSpace(effectiveSourcePath))
        {
            return Path.GetFileName(effectiveSourcePath);
        }

        return part.FileReplacementSourceFileName ?? string.Empty;
    }

    private static string ResolveEffectiveSourceAssetType(
        SpineAssetGroupDocumentTabViewModel groupTab,
        SpineAssetGroupPartDto part)
    {
        var effectiveSourcePath = ResolveEffectiveSourcePath(groupTab, part);
        if (string.IsNullOrWhiteSpace(effectiveSourcePath))
        {
            return string.Empty;
        }

        return (part.FileReplacementSourceAssetType ?? part.AssetType).ToString();
    }

    private static string NormalizeSourcePath(string? sourcePath) =>
        string.IsNullOrWhiteSpace(sourcePath)
            ? string.Empty
            : sourcePath.Trim();

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
