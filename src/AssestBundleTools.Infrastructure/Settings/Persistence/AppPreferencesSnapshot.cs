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
    string RecentWorkspaceOpenBehavior)
{
    public static AppPreferencesSnapshot Default { get; } = new(
        RestoreLastWorkspaceOnStartup: true,
        RecentWorkspaceOpenBehavior: "currentWindow");
}
