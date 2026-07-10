namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 应用界面的显示语言偏好。
/// </summary>
public enum AppDisplayLanguage
{
    SystemDefault = 0,
    SimplifiedChinese = 1,
    English = 2
}

internal static class AppDisplayLanguageCodec
{
    public const string SystemDefaultPreferenceKey = "system";
    public const string SimplifiedChineseLanguageTag = "zh-CN";
    public const string EnglishLanguageTag = "en-US";

    public static AppDisplayLanguage Deserialize(string? preferenceKey)
    {
        if (string.Equals(
                preferenceKey,
                SimplifiedChineseLanguageTag,
                StringComparison.OrdinalIgnoreCase))
        {
            return AppDisplayLanguage.SimplifiedChinese;
        }

        if (string.Equals(
                preferenceKey,
                EnglishLanguageTag,
                StringComparison.OrdinalIgnoreCase))
        {
            return AppDisplayLanguage.English;
        }

        return AppDisplayLanguage.SystemDefault;
    }

    public static string Serialize(AppDisplayLanguage language)
    {
        return language switch
        {
            AppDisplayLanguage.SimplifiedChinese => SimplifiedChineseLanguageTag,
            AppDisplayLanguage.English => EnglishLanguageTag,
            _ => SystemDefaultPreferenceKey
        };
    }

    public static string ToLanguageTag(AppDisplayLanguage language)
    {
        return language switch
        {
            AppDisplayLanguage.SimplifiedChinese => SimplifiedChineseLanguageTag,
            AppDisplayLanguage.English => EnglishLanguageTag,
            _ => string.Empty
        };
    }
}
