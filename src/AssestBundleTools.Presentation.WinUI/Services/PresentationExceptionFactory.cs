using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 统一构造 Presentation 层会抛出的稳定异常消息。
/// </summary>
/// <remarks>
/// 这些异常消息主要用于 WinUI 壳层和启动链路，
/// 因此统一走资源化文本，避免少量基础服务继续散落英文硬编码。
/// </remarks>
internal static class PresentationExceptionFactory
{
    public static InvalidOperationException CreateOwnerWindowUnavailableException(
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        return new InvalidOperationException(
            localizationService.GetRequiredString(ResourceKeys.ErrorPresentationOwnerWindowUnavailable));
    }

    public static NotSupportedException CreateUnsupportedLaunchNavigationParameterException(
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        return new NotSupportedException(
            localizationService.GetRequiredString(ResourceKeys.ErrorPresentationUnsupportedLaunchNavigationParameter));
    }

    public static InvalidOperationException CreateStartWorkspaceProcessFailedException(
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        return new InvalidOperationException(
            localizationService.GetRequiredString(ResourceKeys.ErrorPresentationStartWorkspaceProcessFailed));
    }

    public static InvalidOperationException CreateCurrentProcessPathUnavailableException(
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        return new InvalidOperationException(
            localizationService.GetRequiredString(ResourceKeys.ErrorPresentationCurrentProcessPathUnavailable));
    }

    public static InvalidOperationException CreateFallbackDecryptorRequiredException(
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        return new InvalidOperationException(
            localizationService.GetRequiredString(ResourceKeys.ErrorPresentationFallbackDecryptorRequired));
    }
}
