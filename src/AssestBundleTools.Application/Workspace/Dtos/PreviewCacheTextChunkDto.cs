namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示从预览缓存文本 payload 中读取的规范化文本片段。
/// </summary>
public sealed record PreviewCacheTextChunkDto(
    PreviewCacheDescriptorDto Descriptor,
    string Text,
    int Offset,
    int Length,
    int TotalLength,
    long ContentLength);
