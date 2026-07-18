namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示一条需要在展示层本地化的结构化预览消息。
/// </summary>
public sealed record AssetPreviewMessageDto(
    AssetPreviewMessageKey Key,
    IReadOnlyList<string>? Arguments = null,
    string? RequiredComponentId = null);
