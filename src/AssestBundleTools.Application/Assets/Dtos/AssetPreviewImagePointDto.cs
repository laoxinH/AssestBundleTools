namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示图片预览坐标系中的单个像素点。
/// </summary>
/// <remarks>
/// 坐标统一使用预览图片左上角为原点的像素空间。
/// </remarks>
public sealed record AssetPreviewImagePointDto(
    double X,
    double Y);
