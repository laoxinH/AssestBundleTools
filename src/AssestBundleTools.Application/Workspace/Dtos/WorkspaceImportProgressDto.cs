namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入处理过程中的可展示进度快照。
/// </summary>
public sealed record WorkspaceImportProgressDto(
    WorkspaceImportProgressStage Stage,
    int CompletedBundleCount,
    int TotalBundleCount,
    string? CurrentBundleLogicalPath);
