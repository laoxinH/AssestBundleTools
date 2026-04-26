namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示工作区目录项当前的可用性状态。
/// </summary>
public enum WorkspaceAvailabilityStatus
{
    Available = 0,
    MissingSourceDirectory = 1,
    MissingWorkspaceDataRootDirectory = 2
}
