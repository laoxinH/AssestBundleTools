namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责在新窗口中打开指定工作区。
/// </summary>
public interface IWorkspaceWindowService
{
    void OpenWorkspaceInNewWindow(WorkspaceNavigationRequest navigationRequest);
}
