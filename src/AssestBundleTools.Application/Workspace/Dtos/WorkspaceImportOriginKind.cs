namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入来源是通过哪种入口登记进来的。
/// </summary>
public enum WorkspaceImportOriginKind
{
    BundleFiles = 0,
    BundleDirectory = 1,
    DragDrop = 2
}
