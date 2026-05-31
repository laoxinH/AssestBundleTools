using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过预览管线补充 atlas page 与 Texture2D 之间的结构化诊断。
/// </summary>
internal sealed class SpineAssetGroupTextureDiagnosticBuilder
{
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;

    public SpineAssetGroupTextureDiagnosticBuilder(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler)
    {
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
    }

    public async Task<TextureDiagnosticResult> BuildAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        SpineAssetGroupPartDto atlasAsset,
        IReadOnlyList<SpineAtlasPageDto> pages,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>();
        var previewResultsByTextureKey = new Dictionary<string, Result<AssetPreviewDto>>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TextureDiagnosticResult.Cancelled;
            }

            if (page.TexturePageAsset is not { } texturePageAsset)
            {
                diagnostics.Add(CreateDiagnostic(
                    SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched,
                    groupKey,
                    [atlasAsset],
                    page));
                continue;
            }

            var texturePreviewResult = await GetTexturePreviewAsync(
                query,
                texturePageAsset,
                previewResultsByTextureKey,
                cancellationToken);
            if (texturePreviewResult.IsCancelled)
            {
                return TextureDiagnosticResult.Cancelled;
            }

            if (!TryReadImagePreview(texturePreviewResult, out var texturePreview))
            {
                diagnostics.Add(CreateDiagnostic(
                    SpineAssetGroupDiagnosticKind.TexturePreviewUnavailable,
                    groupKey,
                    [atlasAsset, texturePageAsset],
                    page));
                continue;
            }

            if (TryCreateSizeMismatchDiagnostic(
                    groupKey,
                    atlasAsset,
                    texturePageAsset,
                    page,
                    texturePreview,
                    out var mismatchDiagnostic))
            {
                diagnostics.Add(mismatchDiagnostic);
            }
        }

        return new TextureDiagnosticResult(false, diagnostics);
    }

    private async Task<Result<AssetPreviewDto>> GetTexturePreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        SpineAssetGroupPartDto texturePageAsset,
        IDictionary<string, Result<AssetPreviewDto>> previewResultsByTextureKey,
        CancellationToken cancellationToken)
    {
        var textureKey = $"{texturePageAsset.BundleId.Value}\n{texturePageAsset.AssetId.Value}";
        if (previewResultsByTextureKey.TryGetValue(textureKey, out var cachedResult))
        {
            return cachedResult;
        }

        var previewResult = await _assetPreviewQueryHandler.HandleAsync(
            SpineAssetGroupPreviewQueryFactory.Create(query, texturePageAsset),
            cancellationToken);
        previewResultsByTextureKey[textureKey] = previewResult;
        return previewResult;
    }

    private static bool TryReadImagePreview(
        Result<AssetPreviewDto> texturePreviewResult,
        out AssetPreviewDto texturePreview)
    {
        if (texturePreviewResult.IsSuccess &&
            texturePreviewResult.Value is { Kind: AssetPreviewKind.Image } preview)
        {
            texturePreview = preview;
            return true;
        }

        texturePreview = null!;
        return false;
    }

    private static bool TryCreateSizeMismatchDiagnostic(
        string groupKey,
        SpineAssetGroupPartDto atlasAsset,
        SpineAssetGroupPartDto texturePageAsset,
        SpineAtlasPageDto page,
        AssetPreviewDto texturePreview,
        out SpineAssetGroupDiagnosticDto diagnostic)
    {
        if (page is not { Width: > 0, Height: > 0 } ||
            !TryResolveTextureDimensions(texturePreview.PreviewInfoItems, out var textureWidth, out var textureHeight) ||
            page.Width == textureWidth && page.Height == textureHeight)
        {
            diagnostic = null!;
            return false;
        }

        diagnostic = CreateDiagnostic(
            SpineAssetGroupDiagnosticKind.AtlasPageTextureSizeMismatch,
            groupKey,
            [atlasAsset, texturePageAsset],
            page,
            textureWidth,
            textureHeight);
        return true;
    }

    private static bool TryResolveTextureDimensions(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        out int width,
        out int height)
    {
        width = ResolveIntegerInfo(items, AssetPreviewInfoKey.TextureWidth);
        height = ResolveIntegerInfo(items, AssetPreviewInfoKey.TextureHeight);
        if (width > 0 && height > 0)
        {
            return true;
        }

        return TryResolveImageDimensions(items, out width, out height);
    }

    private static int ResolveIntegerInfo(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        AssetPreviewInfoKey key)
    {
        var value = items?.FirstOrDefault(item => item.Key == key)?.Int64Value;
        return value is > 0 and <= int.MaxValue
            ? (int)value.Value
            : 0;
    }

    private static bool TryResolveImageDimensions(
        IReadOnlyList<AssetPreviewInfoItemDto>? items,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        var text = items?.FirstOrDefault(item => item.Key == AssetPreviewInfoKey.ImageDimensions)?.TextValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var segments = text.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 &&
               int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
               int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height) &&
               width > 0 &&
               height > 0;
    }

    private static SpineAssetGroupDiagnosticDto CreateDiagnostic(
        SpineAssetGroupDiagnosticKind kind,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> relatedParts,
        SpineAtlasPageDto page,
        int? textureWidth = null,
        int? textureHeight = null) =>
        new(
            kind,
            groupKey,
            relatedParts,
            NormalizePageName(page.Name),
            page.Width,
            page.Height,
            textureWidth,
            textureHeight);

    private static string? NormalizePageName(string pageName) =>
        string.IsNullOrWhiteSpace(pageName)
            ? null
            : pageName.Trim();

    public sealed record TextureDiagnosticResult(
        bool IsCancelled,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
    {
        public static TextureDiagnosticResult Cancelled { get; } =
            new(true, Array.Empty<SpineAssetGroupDiagnosticDto>());
    }
}
