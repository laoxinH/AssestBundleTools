namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 暴露当前应用会话可见的偏好设置。
/// </summary>
/// <remarks>
/// 当前只承接 Presentation 层直接依赖的启动偏好。
/// 后续若继续补语言、主题或窗口行为，也统一沿用这里扩展，
/// 避免页面直接感知底层 JSON 文件结构。
/// </remarks>
public interface IAppPreferencesService
{
    bool RestoreLastWorkspaceOnStartup { get; }

    RecentWorkspaceOpenBehavior RecentWorkspaceOpenBehavior { get; }

    AssetEditorCloseBehavior AssetEditorCloseBehavior { get; }

    AppDisplayLanguage DisplayLanguage { get; }

    AppThemePreference Theme { get; }

    int WorkspaceImportWorkerCount { get; }

    int PreviewWorkerCount { get; }

    bool BundleArtifactCacheAutoPruneEnabled { get; }

    long BundleArtifactCacheMaximumSizeInBytes { get; }

    int OperationRecordLimit { get; }

    bool ElevatedDragDropCompatibilityEnabled { get; }

    double? MainWindowWidth { get; }

    double? MainWindowHeight { get; }

    bool TrySetRestoreLastWorkspaceOnStartup(bool value);

    bool TrySetRecentWorkspaceOpenBehavior(RecentWorkspaceOpenBehavior value);

    bool TrySetAssetEditorCloseBehavior(AssetEditorCloseBehavior value);

    bool TrySetDisplayLanguage(AppDisplayLanguage value);

    bool TrySetTheme(AppThemePreference value);

    bool TrySetWorkspaceImportWorkerCount(int value);

    bool TrySetPreviewWorkerCount(int value);

    bool TrySetBundleArtifactCacheAutoPruneEnabled(bool value);

    bool TrySetBundleArtifactCacheMaximumSizeInBytes(long value);

    bool TrySetOperationRecordLimit(int value);

    bool TrySetElevatedDragDropCompatibilityEnabled(bool value);

    bool TrySetMainWindowSize(double width, double height);
}
