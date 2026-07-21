namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示按当前显示器 DPI 归一化后的窗口有效尺寸。
/// </summary>
public sealed record WorkspaceWindowSizeDto(
    double Width,
    double Height);

/// <summary>
/// 表示某个工作区最近使用的辅助窗口尺寸。
/// </summary>
public sealed record WorkspaceWindowSizePreferencesDto(
    WorkspaceWindowSizeDto? BatchExtraction = null,
    WorkspaceWindowSizeDto? BatchReplacement = null,
    WorkspaceWindowSizeDto? Export = null,
    WorkspaceWindowSizeDto? AssetEditor = null);
