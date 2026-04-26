namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示对某个工作区数据目录的检查结果。
/// </summary>
/// <remarks>
/// 该结果用于帮助上层判断：
/// 选中的目录是否已经承载了某个工作区，
/// 以及当前是否可以安全地直接打开或重置。
/// </remarks>
public sealed record WorkspaceDataRootInspectionDto(
    string RootDirectoryPath,
    WorkspaceDataRootMatchKind MatchKind,
    WorkspaceDataRootWorkspaceDto? Workspace,
    int WorkspaceCount)
{
    public bool HasMatchedWorkspace => Workspace is not null;
}
