namespace AssestBundleTools.Presentation.WinUI.Services;

public enum AppThemePreference
{
    SystemDefault,
    Light,
    Dark
}

internal static class AppThemePreferenceCodec
{
    public const string SystemDefaultPreferenceKey = "system";
    private const string LightPreferenceKey = "light";
    private const string DarkPreferenceKey = "dark";

    public static AppThemePreference Deserialize(string? value) => value switch
    {
        LightPreferenceKey => AppThemePreference.Light,
        DarkPreferenceKey => AppThemePreference.Dark,
        _ => AppThemePreference.SystemDefault
    };

    public static string Serialize(AppThemePreference value) => value switch
    {
        AppThemePreference.Light => LightPreferenceKey,
        AppThemePreference.Dark => DarkPreferenceKey,
        _ => SystemDefaultPreferenceKey
    };
}
