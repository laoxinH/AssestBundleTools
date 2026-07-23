using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 协调首次启动教程提示，并只在用户完成选择后持久化处理状态。
/// </summary>
internal sealed class FirstRunTutorialPromptCoordinator
{
    internal const string TutorialPageUrl = "https://www.modwu.com/10635.html";
    private const int XamlRootWaitAttemptCount = 50;
    private static readonly TimeSpan XamlRootWaitInterval = TimeSpan.FromMilliseconds(100);
    private readonly ILocalizationService _localizationService;
    private readonly IAppPreferencesService _appPreferencesService;

    public FirstRunTutorialPromptCoordinator(
        ILocalizationService localizationService,
        IAppPreferencesService appPreferencesService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
    }

    public async Task PromptIfNeededAsync(Window ownerWindow)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);

        if (_appPreferencesService.FirstRunTutorialPromptHandled)
        {
            return;
        }

        var xamlRoot = await WaitForXamlRootAsync(ownerWindow.Content);
        if (xamlRoot is null)
        {
            StartupDiagnostics.Log("First-run tutorial prompt deferred because XamlRoot is unavailable.");
            return;
        }

        ContentDialogResult selection;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = _localizationService.GetRequiredString(ResourceKeys.FirstRunTutorialPromptTitle),
                Content = _localizationService.GetRequiredString(ResourceKeys.FirstRunTutorialPromptMessage),
                PrimaryButtonText = _localizationService.GetRequiredString(
                    ResourceKeys.FirstRunTutorialPromptOpenButton),
                CloseButtonText = _localizationService.GetRequiredString(
                    ResourceKeys.FirstRunTutorialPromptCloseButton),
                DefaultButton = ContentDialogButton.Primary
            };

            selection = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"First-run tutorial prompt failed and will be retried: {ex}");
            return;
        }

        if (!_appPreferencesService.TryMarkFirstRunTutorialPromptHandled())
        {
            StartupDiagnostics.Log("First-run tutorial prompt state could not be persisted; it will be retried.");
            return;
        }

        if (selection != ContentDialogResult.Primary)
        {
            StartupDiagnostics.Log("First-run tutorial prompt completed without opening the tutorial.");
            return;
        }

        try
        {
            var launched = await Launcher.LaunchUriAsync(new Uri(TutorialPageUrl));
            StartupDiagnostics.Log(launched
                ? "First-run tutorial page opened in the default browser."
                : "The system declined to open the first-run tutorial page.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Opening the first-run tutorial page failed: {ex}");
        }
    }

    private static async Task<XamlRoot?> WaitForXamlRootAsync(UIElement? contentRoot)
    {
        for (var attempt = 0; attempt < XamlRootWaitAttemptCount; attempt++)
        {
            if (contentRoot?.XamlRoot is { } xamlRoot)
            {
                return xamlRoot;
            }

            await Task.Delay(XamlRootWaitInterval);
        }

        return contentRoot?.XamlRoot;
    }
}
