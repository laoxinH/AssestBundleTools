namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 资源组识别阶段的结构化诊断类别。
/// </summary>
public enum SpineAssetGroupDiagnosticKind
{
    MissingSkeleton = 0,
    MissingAtlas = 1,
    MissingTexturePage = 2,
    MultipleSkeletons = 3,
    MultipleAtlases = 4,
    AtlasPreviewUnavailable = 5,
    AtlasParseFailed = 6,
    AtlasPageTextureUnmatched = 7,
    TexturePreviewUnavailable = 8,
    AtlasPageTextureSizeMismatch = 9,
    SkeletonPreviewUnavailable = 10,
    SkeletonJsonParseFailed = 11
}
