using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class SpineAnimationAlphaTexturePageResolver
{
    private static readonly string[] TexturePageSuffixes =
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

    private static readonly string[] AlphaTextureSuffixMarkers =
    [
        "_alpha",
        "-alpha",
        ".alpha",
        " alpha",
        "@alpha",
        "_a",
        "-a",
        ".a",
        " a",
        "@a"
    ];

    public static SpineAssetGroupPartDto? Resolve(
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageDto atlasPage)
    {
        if (atlasPage.TexturePageAsset is null)
        {
            return null;
        }

        var alphaKeys = EnumerateAlphaTextureKeys(atlasPage)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (alphaKeys.Count == 0)
        {
            return null;
        }

        foreach (var candidate in details.Group.TexturePageAssets)
        {
            if (IsSamePart(candidate, atlasPage.TexturePageAsset))
            {
                continue;
            }

            if (EnumerateTextureKeys(candidate).Any(alphaKeys.Contains))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAlphaTextureKeys(SpineAtlasPageDto atlasPage)
    {
        foreach (var key in EnumerateTextureKeys(atlasPage.Name))
        {
            foreach (var marker in AlphaTextureSuffixMarkers)
            {
                yield return string.Concat(key, marker);
            }
        }

        if (atlasPage.TexturePageAsset is null)
        {
            yield break;
        }

        foreach (var key in EnumerateTextureKeys(atlasPage.TexturePageAsset))
        {
            foreach (var marker in AlphaTextureSuffixMarkers)
            {
                yield return string.Concat(key, marker);
            }
        }
    }

    private static IEnumerable<string> EnumerateTextureKeys(SpineAssetGroupPartDto part)
    {
        foreach (var key in EnumerateTextureKeys(part.AssetPath))
        {
            yield return key;
        }

        foreach (var key in EnumerateTextureKeys(part.Name))
        {
            yield return key;
        }
    }

    private static IEnumerable<string> EnumerateTextureKeys(string? value)
    {
        var normalizedValue = NormalizeTexturePath(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            yield break;
        }

        var trimmedValue = TrimTexturePageSuffix(normalizedValue);
        yield return trimmedValue;

        var fileName = GetFileName(trimmedValue);
        if (!string.IsNullOrWhiteSpace(fileName) &&
            !string.Equals(fileName, trimmedValue, StringComparison.OrdinalIgnoreCase))
        {
            yield return fileName;
        }
    }

    private static string TrimTexturePageSuffix(string value)
    {
        foreach (var suffix in TexturePageSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                value.Length > suffix.Length)
            {
                return value[..^suffix.Length];
            }
        }

        return value;
    }

    private static string NormalizeTexturePath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()
                .Replace('\\', '/')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .ToLowerInvariant();

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

    private static bool IsSamePart(SpineAssetGroupPartDto left, SpineAssetGroupPartDto right) =>
        left.BundleId.Equals(right.BundleId) &&
        left.AssetId.Equals(right.AssetId);
}
