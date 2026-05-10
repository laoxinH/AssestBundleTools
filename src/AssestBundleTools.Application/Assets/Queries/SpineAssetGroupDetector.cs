using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

internal static partial class SpineAssetGroupDetector
{
    private static readonly string[] AtlasSuffixes =
    [
        ".atlas.txt",
        ".atlas"
    ];

    private static readonly string[] SkeletonSuffixes =
    [
        ".skel.bytes",
        ".skel",
        ".json.txt",
        ".json"
    ];

    private static readonly string[] TextureSuffixes =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".psd",
        ".ktx",
        ".pvr",
        ".astc",
        ".webp"
    ];

    public static SpineAssetGroupListResultDto CreateResult(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        string targetBundleDisplayName,
        string? searchKeyword,
        IReadOnlyList<AssetListResultDto> bundleAssetLists,
        IReadOnlyDictionary<string, AssetDetailsDto>? assetDetailsByAssetKey = null)
    {
        ArgumentNullException.ThrowIfNull(bundleAssetLists);

        var normalizedSearchKeyword = NormalizeSearchKeyword(searchKeyword);
        var candidates = bundleAssetLists
            .SelectMany(bundle => bundle.Items.Select(item => new AssetCandidate(bundle, item)))
            .ToArray();

        var textParts = new List<ClassifiedPart>();
        var anchorGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!TryCreateTextPart(candidate, out var classifiedPart))
            {
                continue;
            }

            textParts.Add(classifiedPart);
            if (candidate.Bundle.BundleId.Equals(targetBundleId))
            {
                anchorGroupKeys.Add(classifiedPart.GroupKey);
            }
        }

        var textureCandidates = candidates
            .Select(TryCreateTexturePart)
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .ToArray();
        var linkedGroups = CreateLinkedGroups(
            targetBundleId,
            candidates,
            textParts,
            textureCandidates,
            assetDetailsByAssetKey);
        var linkedGroupKeys = linkedGroups
            .Select(static group => group.GroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allGroups = anchorGroupKeys
            .Where(groupKey => !linkedGroupKeys.Contains(groupKey))
            .Select(groupKey => CreateGroup(targetBundleId, groupKey, textParts, textureCandidates))
            .Concat(linkedGroups)
            .OrderBy(static group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.GroupId, StringComparer.Ordinal)
            .ToArray();

        var filteredGroups = allGroups
            .Where(group => MatchesSearch(group, normalizedSearchKeyword))
            .ToArray();

        return new SpineAssetGroupListResultDto(
            WorkspaceId: workspaceId,
            BundleId: targetBundleId,
            BundleDisplayName: targetBundleDisplayName,
            SearchKeyword: normalizedSearchKeyword,
            TotalCount: allGroups.Length,
            FilteredCount: filteredGroups.Length,
            Items: filteredGroups);
    }

    private static SpineAssetGroupDto CreateGroup(
        BundleId targetBundleId,
        string groupKey,
        IReadOnlyList<ClassifiedPart> textParts,
        IReadOnlyList<TextureCandidate> textureCandidates)
    {
        var skeletons = textParts
            .Where(part => part.Role == SpineAssetGroupPartRole.Skeleton)
            .Where(part => string.Equals(part.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
            .Select(static part => part.Part)
            .OrderBy(PartSortKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var atlases = textParts
            .Where(part => part.Role == SpineAssetGroupPartRole.Atlas)
            .Where(part => string.Equals(part.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
            .Select(static part => part.Part)
            .OrderBy(PartSortKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var texturePages = textureCandidates
            .Where(texture => texture.Keys.Any(key => MatchesTextureGroup(key, groupKey)))
            .Select(static texture => texture.Part)
            .DistinctBy(static part => $"{part.BundleId.Value}\n{part.AssetId.Value}")
            .OrderBy(PartSortKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var diagnostics = CreateDiagnostics(groupKey, skeletons, atlases, texturePages);

        return new SpineAssetGroupDto(
            GroupId: CreateGroupId(targetBundleId, groupKey),
            BundleId: targetBundleId,
            DisplayName: ResolveDisplayName(groupKey),
            GroupKey: groupKey,
            SkeletonAssets: skeletons,
            AtlasAssets: atlases,
            TexturePageAssets: texturePages,
            Diagnostics: diagnostics);
    }

    private static IReadOnlyList<SpineAssetGroupDiagnosticDto> CreateDiagnostics(
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletons,
        IReadOnlyList<SpineAssetGroupPartDto> atlases,
        IReadOnlyList<SpineAssetGroupPartDto> texturePages)
    {
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>();
        if (skeletons.Count == 0)
        {
            diagnostics.Add(new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.MissingSkeleton,
                groupKey,
                Array.Empty<SpineAssetGroupPartDto>()));
        }

        if (atlases.Count == 0)
        {
            diagnostics.Add(new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.MissingAtlas,
                groupKey,
                Array.Empty<SpineAssetGroupPartDto>()));
        }

        if (texturePages.Count == 0)
        {
            diagnostics.Add(new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.MissingTexturePage,
                groupKey,
                Array.Empty<SpineAssetGroupPartDto>()));
        }

        if (skeletons.Count > 1)
        {
            diagnostics.Add(new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.MultipleSkeletons,
                groupKey,
                skeletons));
        }

        if (atlases.Count > 1)
        {
            diagnostics.Add(new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.MultipleAtlases,
                groupKey,
                atlases));
        }

        return diagnostics;
    }

    private static bool TryCreateTextPart(AssetCandidate candidate, out ClassifiedPart classifiedPart)
    {
        if (candidate.Item.AssetType != AssetType.TextAsset)
        {
            classifiedPart = default;
            return false;
        }

        foreach (var path in EnumerateCandidatePaths(candidate.Item))
        {
            if (TryTrimSuffix(path, AtlasSuffixes, out var atlasGroupKey))
            {
                classifiedPart = new ClassifiedPart(
                    NormalizeGroupKey(atlasGroupKey),
                    SpineAssetGroupPartRole.Atlas,
                    CreatePart(candidate, SpineAssetGroupPartRole.Atlas));
                return !string.IsNullOrWhiteSpace(classifiedPart.GroupKey);
            }

            if (TryTrimSuffix(path, SkeletonSuffixes, out var skeletonGroupKey))
            {
                classifiedPart = new ClassifiedPart(
                    NormalizeGroupKey(skeletonGroupKey),
                    SpineAssetGroupPartRole.Skeleton,
                    CreatePart(candidate, SpineAssetGroupPartRole.Skeleton));
                return !string.IsNullOrWhiteSpace(classifiedPart.GroupKey);
            }
        }

        classifiedPart = default;
        return false;
    }

    private static TextureCandidate? TryCreateTexturePart(AssetCandidate candidate)
    {
        if (candidate.Item.AssetType != AssetType.Texture2D)
        {
            return null;
        }

        var keys = EnumerateCandidatePaths(candidate.Item)
            .Select(CreateTextureKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keys.Length == 0
            ? null
            : new TextureCandidate(keys, CreatePart(candidate, SpineAssetGroupPartRole.TexturePage));
    }

    private static SpineAssetGroupPartDto CreatePart(
        AssetCandidate candidate,
        SpineAssetGroupPartRole role)
    {
        return new SpineAssetGroupPartDto(
            Role: role,
            BundleId: candidate.Bundle.BundleId,
            AssetId: candidate.Item.AssetId,
            BundleDisplayName: candidate.Bundle.BundleDisplayName,
            Name: candidate.Item.Name,
            AssetPath: candidate.Item.AssetPath,
            AssetType: candidate.Item.AssetType,
            AssetTypeDisplayName: candidate.Item.AssetTypeDisplayName);
    }

    private static IEnumerable<string> EnumerateCandidatePaths(AssetListItemDto item)
    {
        var assetPath = NormalizePath(item.AssetPath);
        var name = NormalizePath(item.Name);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            yield return assetPath;
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, assetPath, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var assetDirectory = GetDirectoryName(assetPath);
        if (!string.IsNullOrWhiteSpace(assetDirectory) &&
            !name.Contains('/'))
        {
            yield return string.Concat(assetDirectory, "/", name);
        }

        yield return name;
    }

    private static bool TryTrimSuffix(string value, IReadOnlyList<string> suffixes, out string trimmedValue)
    {
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                value.Length > suffix.Length)
            {
                trimmedValue = value[..^suffix.Length];
                return true;
            }
        }

        trimmedValue = string.Empty;
        return false;
    }

    private static string CreateTextureKey(string value)
    {
        foreach (var suffix in TextureSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                value.Length > suffix.Length)
            {
                return NormalizeGroupKey(value[..^suffix.Length]);
            }
        }

        return NormalizeGroupKey(value);
    }

    private static bool MatchesTextureGroup(string textureKey, string groupKey)
    {
        if (string.Equals(textureKey, groupKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var textureDirectory = GetDirectoryName(textureKey);
        var groupDirectory = GetDirectoryName(groupKey);
        if (!string.Equals(textureDirectory, groupDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var textureName = GetFileName(textureKey);
        var groupName = GetFileName(groupKey);
        if (string.IsNullOrWhiteSpace(textureName) ||
            string.IsNullOrWhiteSpace(groupName) ||
            !textureName.StartsWith(groupName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (textureName.Length == groupName.Length)
        {
            return true;
        }

        var suffixMarker = textureName[groupName.Length];
        return suffixMarker is '_' or '-' or '.' or ' ' or '@' ||
               char.IsDigit(suffixMarker);
    }

    private static bool MatchesSearch(SpineAssetGroupDto group, string? normalizedSearchKeyword)
    {
        if (normalizedSearchKeyword is null)
        {
            return true;
        }

        return Contains(group.DisplayName, normalizedSearchKeyword) ||
               Contains(group.GroupKey, normalizedSearchKeyword) ||
               group.SkeletonAssets.Any(part => MatchesPart(part, normalizedSearchKeyword)) ||
               group.AtlasAssets.Any(part => MatchesPart(part, normalizedSearchKeyword)) ||
               group.TexturePageAssets.Any(part => MatchesPart(part, normalizedSearchKeyword));
    }

    private static bool MatchesPart(SpineAssetGroupPartDto part, string normalizedSearchKeyword)
    {
        return Contains(part.Name, normalizedSearchKeyword) ||
               Contains(part.AssetPath, normalizedSearchKeyword) ||
               Contains(part.BundleDisplayName, normalizedSearchKeyword);
    }

    private static bool Contains(string value, string normalizedSearchKeyword) =>
        value.Contains(normalizedSearchKeyword, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeGroupKey(string value) =>
        NormalizePath(value).ToLowerInvariant();

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace('\\', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private static string ResolveDisplayName(string groupKey)
    {
        var fileName = GetFileName(groupKey);
        return string.IsNullOrWhiteSpace(fileName)
            ? groupKey
            : fileName;
    }

    private static string CreateGroupId(BundleId bundleId, string groupKey) =>
        string.Concat("spine-group:", bundleId.Value, ":", groupKey);

    private static string PartSortKey(SpineAssetGroupPartDto part) =>
        string.Concat(part.BundleDisplayName, "\n", part.AssetPath, "\n", part.Name, "\n", part.AssetId.Value);

    private static string GetDirectoryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var slashIndex = value.LastIndexOf('/');
        return slashIndex <= 0
            ? string.Empty
            : value[..slashIndex];
    }

    private static string GetFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var slashIndex = value.LastIndexOf('/');
        return slashIndex >= 0 && slashIndex < value.Length - 1
            ? value[(slashIndex + 1)..]
            : value;
    }

    private static string? NormalizeSearchKeyword(string? searchKeyword) =>
        string.IsNullOrWhiteSpace(searchKeyword) ? null : searchKeyword.Trim();

    private readonly record struct AssetCandidate(
        AssetListResultDto Bundle,
        AssetListItemDto Item);

    private readonly record struct ClassifiedPart(
        string GroupKey,
        SpineAssetGroupPartRole Role,
        SpineAssetGroupPartDto Part);

    private sealed record TextureCandidate(
        IReadOnlyList<string> Keys,
        SpineAssetGroupPartDto Part);
}
