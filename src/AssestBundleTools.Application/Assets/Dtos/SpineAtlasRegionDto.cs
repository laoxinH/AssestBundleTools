namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine atlas 中一个 region 的像素区域。
/// </summary>
public sealed record SpineAtlasRegionDto(
    string Name,
    string PageName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsRotated);
