namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示从完整文本预览载荷中读取的一段文本。
/// </summary>
public sealed record AssetPreviewTextChunkDto(
    AssetPreviewFullContentReferenceDto Reference,
    string Text,
    int Offset,
    int Length,
    int TotalLength,
    bool HasMore);
