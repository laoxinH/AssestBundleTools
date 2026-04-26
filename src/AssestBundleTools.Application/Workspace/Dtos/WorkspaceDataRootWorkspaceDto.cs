namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示从工作区数据目录中识别出的工作区摘要。
/// </summary>
public sealed record WorkspaceDataRootWorkspaceDto(
    string WorkspaceKey,
    string DisplayName,
    string SourcePath,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastOpenedAtUtc,
    string? AssetBundleDecryptorId);
