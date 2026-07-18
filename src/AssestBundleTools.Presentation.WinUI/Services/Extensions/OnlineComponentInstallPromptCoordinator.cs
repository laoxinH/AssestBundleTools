using System.Collections.Concurrent;
using AssestBundleTools.Application.Extensions.Dtos;
using AssestBundleTools.Application.Extensions.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 协调在线组件的用户确认、后台安装和应用级反馈。
/// </summary>
public sealed class OnlineComponentInstallPromptCoordinator : IOnlineComponentInstallPromptCoordinator
{
    private readonly IOnlineComponentInstallationService _installationService;
    private readonly Func<Window?> _activeWindowAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IAppBackgroundTaskStatusService _backgroundTaskStatusService;
    private readonly IAppNotificationService _notificationService;
    private readonly IAppLogService _appLogService;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _componentGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _dismissedComponentIds = new(StringComparer.OrdinalIgnoreCase);

    public OnlineComponentInstallPromptCoordinator(
        IOnlineComponentInstallationService installationService,
        Func<Window?> activeWindowAccessor,
        ILocalizationService localizationService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IAppBackgroundTaskStatusService backgroundTaskStatusService,
        IAppNotificationService notificationService,
        IAppLogService appLogService)
    {
        _installationService = installationService ?? throw new ArgumentNullException(nameof(installationService));
        _activeWindowAccessor = activeWindowAccessor ?? throw new ArgumentNullException(nameof(activeWindowAccessor));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _backgroundTaskStatusService = backgroundTaskStatusService ?? throw new ArgumentNullException(nameof(backgroundTaskStatusService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
    }

    public async Task<bool> EnsureInstalledAsync(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return false;
        }

        var normalizedId = componentId.Trim();
        var componentGate = _componentGates.GetOrAdd(normalizedId, static _ => new SemaphoreSlim(1, 1));
        await componentGate.WaitAsync();
        try
        {
            var descriptor = _installationService.ResolveDescriptor(normalizedId);
            if (descriptor is null)
            {
                return false;
            }

            if (descriptor.IsInstalled)
            {
                return true;
            }

            if (_dismissedComponentIds.ContainsKey(normalizedId))
            {
                return false;
            }

            var confirmation = await ConfirmInstallAsync(descriptor);
            if (confirmation is null)
            {
                return false;
            }

            if (!confirmation.Value)
            {
                _dismissedComponentIds.TryAdd(normalizedId, 0);
                return false;
            }

            return await InstallAsync(descriptor);
        }
        finally
        {
            componentGate.Release();
        }
    }

    private async Task<bool?> ConfirmInstallAsync(OnlineComponentDescriptorDto descriptor)
    {
        var ownerWindow = _activeWindowAccessor();
        if (ownerWindow?.Content is not FrameworkElement { XamlRoot: { } xamlRoot })
        {
            _appLogService.AppendWarning(GetString(ResourceKeys.OnlineComponentInstallPromptUnavailableLog));
            return null;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = GetString(ResourceKeys.OnlineComponentInstallPromptTitle),
            Content = new TextBlock
            {
                Text = _localizationService.GetFormattedString(
                    ResourceKeys.OnlineComponentInstallPromptContentFormat,
                    descriptor.DisplayName,
                    descriptor.Publisher,
                    descriptor.SourceDisplayName,
                    descriptor.LicenseName,
                    descriptor.ProjectUri),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            },
            PrimaryButtonText = GetString(ResourceKeys.OnlineComponentInstallPromptPrimaryButton),
            CloseButtonText = GetString(ResourceKeys.OnlineComponentInstallPromptCloseButton),
            DefaultButton = ContentDialogButton.Primary
        };
        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception exception)
        {
            _appLogService.AppendWarning(
                $"{GetString(ResourceKeys.OnlineComponentInstallPromptUnavailableLog)} {exception.Message}");
            return null;
        }
    }

    private async Task<bool> InstallAsync(OnlineComponentDescriptorDto descriptor)
    {
        using var statusHandle = _backgroundTaskStatusService.BeginTask(
            _localizationService.GetFormattedString(
                ResourceKeys.OnlineComponentInstallStatusTitleFormat,
                descriptor.DisplayName));
        statusHandle.ReportIndeterminate(GetStageText(OnlineComponentInstallStage.ResolvingPackage));
        var progress = new Progress<OnlineComponentInstallProgressDto>(snapshot =>
            ReportProgress(statusHandle, snapshot));
        var result = await _backgroundTaskRunner.RunAsync(
            cancellationToken => _installationService.InstallAsync(
                descriptor.Id,
                progress,
                cancellationToken));
        if (!result.IsSuccess || result.Value is null)
        {
            var errorText = result.Errors.Count > 0
                ? AppErrorDisplayFormatter.Format(result.Errors[0], _localizationService)
                : GetString(ResourceKeys.OnlineComponentInstallFailedMessage);
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.OnlineComponentInstallFailedLogFormat,
                descriptor.DisplayName,
                errorText,
                ResolveDiagnosticDetail(result.Errors.FirstOrDefault())));
            _notificationService.Show(
                GetString(ResourceKeys.OnlineComponentInstallFailedTitle),
                errorText,
                AppNotificationSeverity.Error);
            return false;
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.OnlineComponentInstallSucceededLogFormat,
            descriptor.DisplayName,
            result.Value.InstalledVersion ?? string.Empty));
        _notificationService.Show(
            GetString(ResourceKeys.OnlineComponentInstallSucceededTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.OnlineComponentInstallSucceededMessageFormat,
                descriptor.DisplayName),
            AppNotificationSeverity.Success);
        return true;
    }

    private void ReportProgress(
        IAppBackgroundTaskHandle statusHandle,
        OnlineComponentInstallProgressDto progress)
    {
        var detail = GetStageText(progress.Stage);
        if (progress.Stage == OnlineComponentInstallStage.Downloading &&
            progress.TotalBytes is > 0 and <= int.MaxValue &&
            progress.BytesReceived is >= 0 and <= int.MaxValue)
        {
            statusHandle.ReportProgress(
                (int)progress.BytesReceived,
                (int)progress.TotalBytes.Value,
                detail);
            return;
        }

        statusHandle.ReportIndeterminate(detail);
    }

    private string GetStageText(OnlineComponentInstallStage stage) =>
        GetString(stage switch
        {
            OnlineComponentInstallStage.ResolvingPackage => ResourceKeys.OnlineComponentInstallStageResolving,
            OnlineComponentInstallStage.Downloading => ResourceKeys.OnlineComponentInstallStageDownloading,
            OnlineComponentInstallStage.Verifying => ResourceKeys.OnlineComponentInstallStageVerifying,
            OnlineComponentInstallStage.Extracting => ResourceKeys.OnlineComponentInstallStageExtracting,
            OnlineComponentInstallStage.Installing => ResourceKeys.OnlineComponentInstallStageInstalling,
            _ => ResourceKeys.OnlineComponentInstallStageResolving
        });

    private static string ResolveDiagnosticDetail(AppError? error) =>
        error?.Parameters.TryGetValue("Detail", out var detail) == true
            ? detail?.ToString() ?? string.Empty
            : string.Empty;

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}
