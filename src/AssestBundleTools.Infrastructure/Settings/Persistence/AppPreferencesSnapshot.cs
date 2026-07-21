namespace AssestBundleTools.Infrastructure.Settings.Persistence;

/// <summary>
/// 表示应用级偏好设置的轻量快照。
/// </summary>
/// <remarks>
/// 当前先承接少量启动偏好，后续若需要补语言、主题或调试开关，
/// 继续在这一份快照上按字段演进即可，避免把简单设置散落到多个零散文件里。
/// </remarks>
public sealed record AppPreferencesSnapshot(
    bool RestoreLastWorkspaceOnStartup,
    string RecentWorkspaceOpenBehavior,
    int WorkspaceImportWorkerCount,
    int PreviewWorkerCount = 0,
    bool BundleArtifactCacheAutoPruneEnabled = true,
    long BundleArtifactCacheMaximumSizeInBytes = AppPreferencesSnapshot.DefaultBundleArtifactCacheMaximumSizeInBytes,
    int OperationRecordLimit = AppPreferencesSnapshot.DefaultOperationRecordLimit,
    string AssetEditorCloseBehavior = AppPreferencesSnapshot.DefaultAssetEditorCloseBehavior,
    string DisplayLanguage = AppPreferencesSnapshot.DefaultDisplayLanguage,
    bool ElevatedDragDropCompatibilityEnabled = false,
    double? MainWindowWidth = null,
    double? MainWindowHeight = null)
{
    public const long DefaultBundleArtifactCacheMaximumSizeInBytes = 4L * 1024L * 1024L * 1024L;
    public const int DefaultOperationRecordLimit = 250;
    public const string DefaultAssetEditorCloseBehavior = "returnToMainWindow";
    public const string DefaultDisplayLanguage = "system";

    public static AppPreferencesSnapshot Default { get; } = new(
        RestoreLastWorkspaceOnStartup: true,
        RecentWorkspaceOpenBehavior: "currentWindow",
        AssetEditorCloseBehavior: DefaultAssetEditorCloseBehavior,
        WorkspaceImportWorkerCount: 0,
        PreviewWorkerCount: 0,
        BundleArtifactCacheAutoPruneEnabled: true,
        BundleArtifactCacheMaximumSizeInBytes: DefaultBundleArtifactCacheMaximumSizeInBytes,
        OperationRecordLimit: DefaultOperationRecordLimit,
        DisplayLanguage: DefaultDisplayLanguage,
        ElevatedDragDropCompatibilityEnabled: false,
        MainWindowWidth: null,
        MainWindowHeight: null);
}
