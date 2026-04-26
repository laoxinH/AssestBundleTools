namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示预览生成器产出的原始内容。
/// </summary>
/// <remarks>
/// 该对象只描述“生成出的内容是什么”，
/// 缓存写入、命中判定和最终对 UI 的映射由应用层用例负责。
/// </remarks>
public sealed record AssetPreviewContentDto(
    AssetPreviewKind Kind,
    string ContentType,
    byte[] Content,
    string? PreviewInfoText = null,
    long? PreferredDisplaySizeInBytes = null);
