namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 资源组识别阶段产生的结构化诊断。
/// </summary>
public sealed record SpineAssetGroupDiagnosticDto(
    SpineAssetGroupDiagnosticKind Kind,
    string GroupKey,
    IReadOnlyList<SpineAssetGroupPartDto> RelatedParts,
    string? AtlasPageName = null,
    int? AtlasPageWidth = null,
    int? AtlasPageHeight = null,
    int? TextureWidth = null,
    int? TextureHeight = null);
