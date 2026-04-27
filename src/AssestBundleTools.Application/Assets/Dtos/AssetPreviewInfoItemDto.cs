namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示单条结构化预览补充信息。
/// </summary>
/// <remarks>
/// 预览生成器只负责给出稳定语义和值，
/// 至于标签文本、单位和本地化格式由 Presentation 层决定。
/// </remarks>
public sealed record AssetPreviewInfoItemDto(
    AssetPreviewInfoKey Key,
    AssetPreviewInfoValueKind ValueKind,
    string? TextValue = null,
    long? Int64Value = null,
    double? NumberValue = null,
    bool? BooleanValue = null);
