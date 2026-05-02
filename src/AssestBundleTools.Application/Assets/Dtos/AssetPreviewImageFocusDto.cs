namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示从主图片预览中派生出的局部焦点图像。
/// </summary>
/// <remarks>
/// 该对象用于 Sprite 这类“源贴图 + 裁切区域”预览，
/// 主 payload 仍保留源贴图，焦点图像则展示实际裁切结果。
/// </remarks>
public sealed record AssetPreviewImageFocusDto(
    string ContentType,
    byte[]? BinaryContent,
    string? RelativePayloadPath,
    int PixelWidth,
    int PixelHeight);
