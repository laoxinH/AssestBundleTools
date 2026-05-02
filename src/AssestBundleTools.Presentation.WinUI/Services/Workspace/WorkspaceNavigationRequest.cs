namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示一次工作区打开请求。
/// </summary>
public sealed record WorkspaceNavigationRequest(
    string SourcePath,
    string WorkspaceDisplayName,
    string WorkspaceDataRootPath,
    WorkspaceOpenTarget OpenTarget,
    string? AssetBundleDecryptorId = null,
    bool ForceNewWorkspaceSession = false);
