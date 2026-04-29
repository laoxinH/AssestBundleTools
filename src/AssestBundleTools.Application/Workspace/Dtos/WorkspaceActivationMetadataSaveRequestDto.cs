namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区在“创建 / 打开 / 再次激活”阶段需要补齐的基础元数据。
/// </summary>
public sealed record WorkspaceActivationMetadataSaveRequestDto(
    string WorkspaceKey,
    string? WorkspaceDataRootPath,
    string DisplayName,
    string SourcePath,
    DateTimeOffset OpenedAtUtc,
    string? AssetBundleDecryptorId);
