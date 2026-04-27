using System.Globalization;

namespace AssestBundleTools.Presentation.WinUI.Localization;

/// <summary>
/// 统一承载展示层常用的数值格式化逻辑，避免多个 ViewModel 各自拼接单位文本。
/// </summary>
/// <remarks>
/// 这里专门负责“把稳定数值语义转成当前语言下的展示文本”，
/// 这样可把用户可见单位集中资源化，同时避免在业务或基础设施层散落 UI 文案。
/// </remarks>
internal sealed class LocalizedValueFormatter
{
    private readonly ILocalizationService _localizationService;

    public LocalizedValueFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string FormatCompactByteSize(long sizeInBytes)
    {
        if (sizeInBytes >= 1024L * 1024L)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.CommonByteSizeCompactMegabytesFormat,
                (sizeInBytes / 1024d / 1024d).ToString("0.0", CultureInfo.CurrentCulture));
        }

        if (sizeInBytes >= 1024L)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.CommonByteSizeCompactKilobytesFormat,
                (sizeInBytes / 1024d).ToString("0.0", CultureInfo.CurrentCulture));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.CommonByteSizeCompactBytesFormat,
            sizeInBytes.ToString("N0", CultureInfo.CurrentCulture));
    }

    public string FormatDetailedByteSize(long sizeInBytes)
    {
        if (sizeInBytes >= 1024L * 1024L)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.CommonByteSizeDetailedMegabytesWithBytesFormat,
                (sizeInBytes / 1024d / 1024d).ToString("0.0", CultureInfo.CurrentCulture),
                sizeInBytes.ToString("N0", CultureInfo.CurrentCulture));
        }

        if (sizeInBytes >= 1024L)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.CommonByteSizeDetailedKilobytesWithBytesFormat,
                (sizeInBytes / 1024d).ToString("0.0", CultureInfo.CurrentCulture),
                sizeInBytes.ToString("N0", CultureInfo.CurrentCulture));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.CommonByteSizeDetailedBytesFormat,
            sizeInBytes.ToString("N0", CultureInfo.CurrentCulture));
    }
}
