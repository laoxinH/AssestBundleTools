namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入处理当前所处的阶段。
/// </summary>
public enum WorkspaceImportProgressStage
{
    Preparing = 0,
    Planning = 1,
    ProcessingBundles = 2,
    Committing = 3,
    Completed = 4
}
