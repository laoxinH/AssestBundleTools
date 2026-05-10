namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个 Spine 资源组文档标签的轻量恢复状态。
/// </summary>
/// <remarks>
/// 该状态只记录页面级组文档身份与显示信息，
/// 不保存 atlas 解析结果、Texture2D 预览载荷或其他工作区事实数据。
/// </remarks>
public sealed record WorkspaceMetadataSpineGroupTabDto(
    string GroupId,
    string DisplayName,
    string GroupKey,
    WorkspaceSpinePreviewStateDto? PreviewState = null,
    WorkspaceInspectorScrollStateDto? InspectorScrollState = null);
