namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示图片预览需要叠加的高亮覆盖层。
/// </summary>
/// <remarks>
/// 该对象只提供稳定的几何语义，
/// 具体画框样式、标签文本和动画效果由 Presentation 层决定。
/// </remarks>
public sealed record AssetPreviewImageOverlayDto(
    int CanvasPixelWidth,
    int CanvasPixelHeight,
    AssetPreviewImageRegionDto HighlightRegion,
    AssetPreviewImagePointDto? PivotPoint = null,
    AssetPreviewImageBorderDto? NineSliceBorder = null);
