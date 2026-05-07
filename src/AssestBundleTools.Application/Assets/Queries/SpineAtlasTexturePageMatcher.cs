using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 负责把 atlas page 名称解析回工作区中的 Texture2D 组成资产。
/// </summary>
internal static class SpineAtlasTexturePageMatcher
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

    public static IReadOnlyList<SpineAtlasPageDto> AttachTexturePageAssets(
        IReadOnlyList<SpineAtlasPageDto> pages,
        IReadOnlyList<SpineAssetGroupPartDto> texturePageAssets)
    {
        if (pages.Count == 0 || texturePageAssets.Count == 0)
        {
            return pages;
        }

        var textureAssetsByKey = new Dictionary<string, SpineAssetGroupPartDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var texturePageAsset in texturePageAssets)
        {
            foreach (var key in EnumerateTexturePageKeys(texturePageAsset.AssetPath)
                         .Concat(EnumerateTexturePageKeys(texturePageAsset.Name)))
            {
                textureAssetsByKey.TryAdd(key, texturePageAsset);
            }
        }

        return pages
            .Select(page => page with
            {
                TexturePageAsset = ResolveTexturePageAsset(page, textureAssetsByKey)
            })
            .ToArray();
    }

    private static SpineAssetGroupPartDto? ResolveTexturePageAsset(
        SpineAtlasPageDto page,
        IReadOnlyDictionary<string, SpineAssetGroupPartDto> textureAssetsByKey)
    {
        foreach (var key in EnumerateTexturePageKeys(page.Name))
        {
            if (textureAssetsByKey.TryGetValue(key, out var texturePageAsset))
            {
                return texturePageAsset;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateTexturePageKeys(string? value)
    {
        var normalizedValue = NormalizePath(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            yield break;
        }

        yield return normalizedValue;

        var normalizedFileName = GetFileName(normalizedValue);
        if (!string.IsNullOrWhiteSpace(normalizedFileName) &&
            !string.Equals(normalizedFileName, normalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalizedFileName;
        }

        var trimmedValue = TrimTexturePageSuffix(normalizedValue);
        if (!string.Equals(trimmedValue, normalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            yield return trimmedValue;
        }

        var trimmedFileName = TrimTexturePageSuffix(normalizedFileName);
        if (!string.IsNullOrWhiteSpace(trimmedFileName) &&
            !string.Equals(trimmedFileName, normalizedFileName, StringComparison.OrdinalIgnoreCase))
        {
            yield return trimmedFileName;
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

    private static string NormalizePath(string? value) =>
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
}
