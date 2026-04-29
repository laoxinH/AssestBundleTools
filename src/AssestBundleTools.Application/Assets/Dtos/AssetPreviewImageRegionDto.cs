namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示图片预览上的单个高亮区域。
/// </summary>
/// <remarks>
/// 坐标统一使用预览图片左上角为原点的像素空间，
/// 方便展示层直接叠加可视化标注，而不必再反推图片坐标系。
/// </remarks>
public sealed record AssetPreviewImageRegionDto(
    double X,
    double Y,
    double Width,
    double Height);
