using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class ElevationRestartPromptCoordinator
{
    private const int XamlRootWaitAttemptCount = 50;
    private static readonly TimeSpan XamlRootWaitInterval = TimeSpan.FromMilliseconds(100);
    private readonly ILocalizationService _localizationService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IElevatedDragDropCompatibilityService _dragDropCompatibilityService;
    private readonly IElevatedDragExportBrokerService _dragExportBrokerService;
    private readonly IUnelevatedProcessRestartService _restartService;

    public ElevationRestartPromptCoordinator(
        ILocalizationService localizationService,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        IAppPreferencesService appPreferencesService,
        IElevatedDragDropCompatibilityService dragDropCompatibilityService,
        IElevatedDragExportBrokerService dragExportBrokerService,
        IUnelevatedProcessRestartService restartService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
        _dragDropCompatibilityService = dragDropCompatibilityService ?? throw new ArgumentNullException(nameof(dragDropCompatibilityService));
        _dragExportBrokerService = dragExportBrokerService ?? throw new ArgumentNullException(nameof(dragExportBrokerService));
        _restartService = restartService ?? throw new ArgumentNullException(nameof(restartService));
    }

    public bool IsUnelevatedRestartLaunch =>
        UnelevatedRestartLaunchArguments.IsCurrentProcessRestartLaunch();

    public async Task<bool> PromptAndRestartIfRequestedAsync(
        Window ownerWindow,
        ShellLaunchRequest? launchRequest)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);

        var elevation = ProcessElevationDiagnostics.Capture();
        StartupDiagnostics.Log($"Process elevation state resolved: {elevation.DiagnosticValue}.");
        if (elevation.IsElevated != true)
        {
            return false;
        }

        if (_appPreferencesService.ElevatedDragDropCompatibilityEnabled)
        {
            var inboundResult = _dragDropCompatibilityService.TryEnable(ownerWindow);
            var brokerResult = await _dragExportBrokerService.EnsureStartedAsync();
            ShowElevatedDragDropCompatibilityNotification(
                inboundResult,
                brokerResult);
            return false;
        }

        if (TryReportRecursiveRestart())
        {
            return false;
        }

        return await PromptForRestartAsync(
            ownerWindow,
            launchRequest,
            ResourceKeys.AppElevationWarningTitle,
            ResourceKeys.AppElevationWarningMessage);
    }

    public async Task<bool> PromptForDragExportRestartAsync(
        Window ownerWindow,
        ShellLaunchRequest? launchRequest)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);

        if (ProcessElevationDiagnostics.Capture().IsElevated != true || TryReportRecursiveRestart())
        {
            return false;
        }

        return await PromptForRestartAsync(
            ownerWindow,
            launchRequest,
            ResourceKeys.AppElevationDragExportWarningTitle,
            ResourceKeys.AppElevationDragExportWarningMessage);
    }

    private async Task<bool> PromptForRestartAsync(
        Window ownerWindow,
        ShellLaunchRequest? launchRequest,
        string titleResourceKey,
        string messageResourceKey)
    {
        var title = _localizationService.GetRequiredString(titleResourceKey);
        var message = _localizationService.GetRequiredString(messageResourceKey);
        _appLogService.AppendWarning(message);

        var xamlRoot = await WaitForXamlRootAsync(ownerWindow.Content);
        if (xamlRoot is null)
        {
            ShowWarningNotification(title, message);
            StartupDiagnostics.Log(
                $"Elevation restart prompt was skipped because XamlRoot remained unavailable after " +
                $"{XamlRootWaitAttemptCount * XamlRootWaitInterval.TotalMilliseconds:0} ms.");
            return false;
        }

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = message,
                PrimaryButtonText = _localizationService.GetRequiredString(
                    ResourceKeys.AppElevationWarningRestartButton),
                CloseButtonText = _localizationService.GetRequiredString(
                    ResourceKeys.AppElevationWarningContinueButton),
                DefaultButton = ContentDialogButton.Primary
            };

            var selection = await dialog.ShowAsync();
            if (selection != ContentDialogResult.Primary)
            {
                StartupDiagnostics.Log("User chose to continue in the elevated process.");
                return false;
            }

            var arguments = UnelevatedRestartLaunchArguments.Create(
                launchRequest,
                _localizationService);
            var result = await _restartService.TryRestartAsync(arguments);
            if (result.Succeeded)
            {
                StartupDiagnostics.Log(
                    $"Unelevated restart process started. ProcessId={result.ProcessId}.");
                return true;
            }

            var diagnostic = string.Format(
                _localizationService.GetRequiredString(ResourceKeys.AppElevationRestartFailedLogFormat),
                result.NativeErrorCode?.ToString() ?? "n/a",
                result.DiagnosticDetail);
            _appLogService.AppendError(diagnostic);
            StartupDiagnostics.Log(diagnostic);
            _appNotificationService.Show(
                _localizationService.GetRequiredString(ResourceKeys.AppNotificationErrorTitle),
                _localizationService.GetRequiredString(ResourceKeys.AppElevationRestartFailedMessage),
                AppNotificationSeverity.Error,
                TimeSpan.FromSeconds(12));
            return false;
        }
        catch (Exception ex)
        {
            var diagnostic = string.Format(
                _localizationService.GetRequiredString(ResourceKeys.AppElevationRestartFailedLogFormat),
                "n/a",
                ex.Message);
            _appLogService.AppendError(diagnostic);
            StartupDiagnostics.Log($"Elevation restart prompt failed: {ex}");
            ShowWarningNotification(title, message);
            return false;
        }
    }

    private bool TryReportRecursiveRestart()
    {
        if (!IsUnelevatedRestartLaunch)
        {
            return false;
        }

        var restartStillElevatedMessage = _localizationService.GetRequiredString(
            ResourceKeys.AppElevationRestartStillElevatedMessage);
        _appLogService.AppendError(restartStillElevatedMessage);
        StartupDiagnostics.Log("Unelevated restart process remained elevated; recursive prompt suppressed.");
        _appNotificationService.Show(
            _localizationService.GetRequiredString(ResourceKeys.AppNotificationErrorTitle),
            restartStillElevatedMessage,
            AppNotificationSeverity.Error,
            TimeSpan.FromSeconds(15));
        return true;
    }

    private void ShowElevatedDragDropCompatibilityNotification(
        ElevatedDragDropCompatibilityResult inboundResult,
        ElevatedDragExportBrokerResult outboundResult)
    {
        var succeeded = inboundResult.Succeeded && outboundResult.Succeeded;
        var diagnostic = _localizationService.GetFormattedString(
            ResourceKeys.AppElevatedDragDropCompatibilityLogFormat,
            succeeded,
            $"Inbound={inboundResult.DiagnosticDetail}; Outbound={outboundResult.DiagnosticDetail}");
        if (succeeded)
        {
            _appLogService.AppendWarning(diagnostic);
        }
        else
        {
            _appLogService.AppendError(diagnostic);
        }

        StartupDiagnostics.Log(diagnostic);
        _appNotificationService.Show(
            _localizationService.GetRequiredString(ResourceKeys.AppElevatedDragDropCompatibilityTitle),
            _localizationService.GetRequiredString(succeeded
                ? ResourceKeys.AppElevatedDragDropCompatibilityEnabledMessage
                : ResourceKeys.AppElevatedDragDropCompatibilityFailedMessage),
            succeeded ? AppNotificationSeverity.Warning : AppNotificationSeverity.Error,
            TimeSpan.FromSeconds(15));
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

    private void ShowWarningNotification(string title, string message)
    {
        _appNotificationService.Show(
            title,
            message,
            AppNotificationSeverity.Warning,
            TimeSpan.FromSeconds(15));
    }
}
