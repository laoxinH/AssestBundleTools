namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 描述工作区数据目录和已存储工作区之间的匹配情况。
/// </summary>
public enum WorkspaceDataRootMatchKind
{
    None,
    MatchedByDataRootKey,
    SingleWorkspaceDetected,
    AmbiguousMultipleWorkspaces
}
