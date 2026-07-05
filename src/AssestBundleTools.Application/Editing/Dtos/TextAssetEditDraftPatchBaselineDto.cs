namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示 range patch 生成时编辑器看到的草稿正文基线。
/// </summary>
public sealed record TextAssetEditDraftPatchBaselineDto(
    int ContentLength,
    string ContentSha256);
