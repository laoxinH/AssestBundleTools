namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示 UI 偏好设置持久化所需的最小快照。
/// </summary>
/// <remarks>
/// 该快照只服务于 Presentation 自己的偏好设置语义，
/// 避免普通 UI 服务直接感知基础设施层的 JSON 存储类型。
/// </remarks>
internal sealed record AppPreferencesPersistenceSnapshot(
    bool RestoreLastWorkspaceOnStartup,
    string RecentWorkspaceOpenBehavior,
    int WorkspaceImportWorkerCount,
    int PreviewWorkerCount,
    bool BundleArtifactCacheAutoPruneEnabled = true,
    long BundleArtifactCacheMaximumSizeInBytes = AppPreferencesService.DefaultBundleArtifactCacheMaximumSizeInBytes);
