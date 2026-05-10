namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示资源详情检查器各内部页签的轻量滚动恢复状态。
/// </summary>
/// <remarks>
/// 这里只记录页面级浏览位置，不包含详情内容或预览载荷。
/// </remarks>
public sealed record WorkspaceInspectorScrollStateDto(
    WorkspaceInspectorSectionScrollPositionDto? DetailsSection = null,
    WorkspaceInspectorSectionScrollPositionDto? DependenciesSection = null,
    WorkspaceInspectorSectionScrollPositionDto? ReplacementSection = null,
    WorkspaceInspectorSectionScrollPositionDto? ChangesSection = null,
    WorkspaceInspectorSectionScrollPositionDto? SectionSelector = null);

/// <summary>
/// 表示单个详情内部页签的滚动位置。
/// </summary>
public sealed record WorkspaceInspectorSectionScrollPositionDto(
    double HorizontalOffset,
    double VerticalOffset);
