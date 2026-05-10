namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示 Spine 预览器的轻量页面级浏览状态。
/// </summary>
/// <remarks>
/// 该状态只保存用户在预览器中的选择与观察位置，
/// 不保存 skeleton、atlas、Texture2D 载荷、运行时帧或 GPU 资源。
/// </remarks>
public sealed record WorkspaceSpinePreviewStateDto(
    string? SelectedAnimationName = null,
    string? SelectedSkinName = null,
    double? PlaybackSpeed = null,
    double? PlaybackTime = null,
    bool? IsPlaying = null,
    bool? IsShowingSetupPose = null,
    string? BackgroundMode = null,
    double? ViewportZoom = null,
    double? ViewportPanX = null,
    double? ViewportPanY = null,
    string? SelectedAtlasPageName = null,
    int? SelectedAtlasPageIndex = null);
