namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示工作区向导表单收集到的原始用户输入。
/// </summary>
/// <remarks>
/// 该对象只描述用户在界面上填写了什么，
/// 最终是否新建、直接打开已有工作区，或先重置再创建，
/// 由后续的对话框服务统一判断。
/// </remarks>
public sealed record WorkspaceSetupSubmission(
    WorkspaceDialogMode DialogMode,
    string WorkspaceDisplayName,
    string WorkspaceDataRootPath,
    WorkspaceOpenTarget OpenTarget,
    string? AssetBundleDecryptorId);
