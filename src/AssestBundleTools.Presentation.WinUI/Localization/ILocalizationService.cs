namespace AssestBundleTools.Presentation.WinUI.Localization;

/// <summary>
/// UI 层本地化服务接口，统一负责读取用户可见文本资源。
/// </summary>
public interface ILocalizationService
{
    string GetRequiredString(string resourceKey);

    string GetFormattedString(string resourceKey, params object[] arguments);
}
