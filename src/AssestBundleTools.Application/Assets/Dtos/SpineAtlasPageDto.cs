namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine atlas 中声明的一张 texture page 及其 region。
/// </summary>
public sealed record SpineAtlasPageDto(
    string Name,
    int? Width,
    int? Height,
    IReadOnlyList<SpineAtlasRegionDto> Regions,
    SpineAssetGroupPartDto? TexturePageAsset = null);
