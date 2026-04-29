namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示从工作区目录恢复“上次活动工作区”时需要的最小导航语义。
/// </summary>
public sealed record WorkspaceContextRestoreRequestDto(
    string SourcePath,
    string WorkspaceDisplayName,
    string WorkspaceDataRootPath,
    string? AssetBundleDecryptorId);
