namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示带有实际内容的预览缓存项。
/// </summary>
public sealed record PreviewCachePayloadDto(
    PreviewCacheDescriptorDto Descriptor,
    byte[] Content,
    byte[]? ImageFocusContent = null);
