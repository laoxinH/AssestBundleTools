namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示图片预览区域内的九宫格边距。
/// </summary>
/// <remarks>
/// 数值使用像素单位，基于高亮区域自身的左、上、右、下边缘。
/// </remarks>
public sealed record AssetPreviewImageBorderDto(
    double Left,
    double Top,
    double Right,
    double Bottom);
