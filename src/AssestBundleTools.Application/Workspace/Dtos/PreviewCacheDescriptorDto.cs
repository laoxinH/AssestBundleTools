using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个预览缓存项的描述信息。
/// </summary>
/// <remarks>
/// 预览内容本体通常以二进制文件形式单独保存，
/// 该描述对象只负责说明内容来源、类型和落盘位置。
/// </remarks>
public sealed record PreviewCacheDescriptorDto(
    string WorkspaceKey,
    string PreviewCacheKey,
    string BundleCacheKey,
    string? AssetId,
    string PreviewKind,
    string ContentType,
    string RelativePayloadPath,
    WorkspaceFileFingerprintDto SourceFingerprint,
    DateTimeOffset GeneratedAtUtc,
    string GeneratorVersion,
    IReadOnlyList<AssetPreviewInfoItemDto>? PreviewInfoItems = null,
    AssetPreviewMessageDto? PreviewMessage = null,
    long? PreferredDisplaySizeInBytes = null,
    string? AudioTranscoderId = null,
    string? AudioTranscoderVersion = null,
    AssetPreviewImageOverlayDto? ImageOverlay = null,
    AssetPreviewImageFocusDto? ImageFocus = null);
