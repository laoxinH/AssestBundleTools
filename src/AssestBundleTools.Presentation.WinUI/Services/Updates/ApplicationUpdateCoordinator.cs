using System.Diagnostics;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Updates.Models;
using AssestBundleTools.Application.Updates.Services;
using AssestBundleTools.Application.Updates.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Services;

public enum ApplicationUpdateStateKind
{
    Idle,
    Unmanaged,
    Checking,
    UpToDate,
    Available,
    Downloading,
    ReadyToRestart,
    Failed
}

public sealed record ApplicationUpdateViewState(
    ApplicationUpdateStateKind Kind,
    string CurrentVersion,
    string? TargetVersion = null,
    double? ProgressPercentage = null);

/// <summary>
/// 协调启动检查、关于页手动检查、用户确认和更新重启。
/// </summary>
public sealed class ApplicationUpdateCoordinator
{
    private readonly IApplicationUpdateGateway _updateGateway;
    private readonly Func<Window?> _activeWindowAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNotificationService _notificationService;
    private readonly IAppLogService _appLogService;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _downloadCancellationSource;
    private ApplicationUpdateViewState _state;

    public ApplicationUpdateCoordinator(
        IApplicationUpdateGateway updateGateway,
        Func<Window?> activeWindowAccessor,
        ILocalizationService localizationService,
        IAppNotificationService notificationService,
        IAppLogService appLogService,
        IBackgroundWorkScheduler backgroundWorkScheduler)
    {
        _updateGateway = updateGateway ?? throw new ArgumentNullException(nameof(updateGateway));
        _activeWindowAccessor = activeWindowAccessor ?? throw new ArgumentNullException(nameof(activeWindowAccessor));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));

        var runtimeInfo = updateGateway.RuntimeInfo;
        _state = new ApplicationUpdateViewState(
            runtimeInfo.CanSelfUpdate ? ApplicationUpdateStateKind.Idle : ApplicationUpdateStateKind.Unmanaged,
            runtimeInfo.CurrentVersion);
    }

    public event EventHandler? StateChanged;

    public ApplicationUpdateViewState State => _state;

    public async Task CheckAtStartupAsync()
    {
        if (!_updateGateway.RuntimeInfo.CanSelfUpdate)
        {
            return;
        }

        var update = await CheckCoreAsync(showFailureFeedback: false);
        if (update is null)
        {
            return;
        }

        if (await ConfirmUpdateAsync(update.TargetVersion))
        {
            await DownloadAndApplyAsync(requireConfirmation: false);
        }
    }

    public async Task CheckManuallyAsync()
    {
        if (!_updateGateway.RuntimeInfo.CanSelfUpdate)
        {
            SetState(ApplicationUpdateStateKind.Unmanaged);
            _notificationService.Show(
                GetString(ResourceKeys.ApplicationUpdateUnmanagedTitle),
                GetString(ResourceKeys.ApplicationUpdateUnmanagedMessage),
                AppNotificationSeverity.Informational);
            return;
        }

        await CheckCoreAsync(showFailureFeedback: true);
    }

    public async Task DownloadAndApplyAsync(bool requireConfirmation = true)
    {
        var targetVersion = State.TargetVersion;
        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            await CheckManuallyAsync();
            return;
        }

        if (requireConfirmation &&
            State.Kind != ApplicationUpdateStateKind.ReadyToRestart &&
            !await ConfirmUpdateAsync(targetVersion))
        {
            return;
        }

        await _operationGate.WaitAsync();
        try
        {
            if (State.Kind != ApplicationUpdateStateKind.ReadyToRestart)
            {
                SetState(ApplicationUpdateStateKind.Downloading, targetVersion, 0);

                using var downloadCancellationSource = new CancellationTokenSource();
                _downloadCancellationSource = downloadCancellationSource;
                Result downloadResult;
                try
                {
                    downloadResult = await _backgroundWorkScheduler.RunAsync(
                        new BackgroundWorkRequest<Result>(
                            BackgroundWorkProfile.ApplicationUpdateDownload,
                            async context =>
                            {
                                var progress = new Progress<ApplicationUpdateProgress>(snapshot =>
                                {
                                    if (snapshot.Percentage is { } percentage)
                                    {
                                        SetState(ApplicationUpdateStateKind.Downloading, targetVersion, percentage);
                                        context.Progress.ReportProgress(
                                            (int)Math.Round(percentage),
                                            100,
                                            GetString(ResourceKeys.ApplicationUpdateDownloadingStatus));
                                    }
                                    else
                                    {
                                        context.Progress.ReportIndeterminate(
                                            GetString(ResourceKeys.ApplicationUpdateDownloadingStatus));
                                    }
                                });
                                return await _updateGateway.DownloadUpdateAsync(
                                    targetVersion,
                                    progress,
                                    context.CancellationToken);
                            })
                        {
                            SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                            Priority = BackgroundWorkPriority.UserBlocking,
                            StatusTitle = GetString(ResourceKeys.ApplicationUpdateDownloadingStatus),
                            TotalCount = 100,
                            ShowInStatusBar = true,
                            CanCancel = true
                        },
                        downloadCancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    downloadResult = Result.Cancelled();
                }
                catch (Exception ex)
                {
                    downloadResult = Result.Failure(ApplicationUpdateErrors.DownloadFailed(ex.Message));
                }

                _downloadCancellationSource = null;
                if (downloadResult.IsCancelled)
                {
                    SetState(ApplicationUpdateStateKind.Available, targetVersion);
                    _notificationService.Show(
                        GetString(ResourceKeys.ApplicationUpdateCancelledTitle),
                        GetString(ResourceKeys.ApplicationUpdateCancelledMessage),
                        AppNotificationSeverity.Informational);
                    return;
                }

                if (!downloadResult.IsSuccess)
                {
                    HandleFailure(ResourceKeys.ApplicationUpdateDownloadFailed, downloadResult.Errors.FirstOrDefault()?.Code);
                    return;
                }

                SetState(ApplicationUpdateStateKind.ReadyToRestart, targetVersion, 100);
            }

            var restartBlockerCode = ResolveRestartBlockerCode();
            if (restartBlockerCode is not null)
            {
                _appLogService.AppendWarning(_localizationService.GetFormattedString(
                    ResourceKeys.ApplicationUpdateRestartBlockedLogFormat,
                    restartBlockerCode));
                _notificationService.Show(
                    GetString(ResourceKeys.ApplicationUpdateReadyTitle),
                    GetString(ResourceKeys.ApplicationUpdateBusyMessage),
                    AppNotificationSeverity.Warning,
                    TimeSpan.FromSeconds(8));
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.ApplicationUpdateApplyingLogFormat,
                targetVersion));
            var applyResult = _updateGateway.ApplyUpdateAndRestart(targetVersion);
            if (!applyResult.IsSuccess)
            {
                HandleFailure(ResourceKeys.ApplicationUpdateApplyFailed, applyResult.Errors.FirstOrDefault()?.Code);
            }
        }
        finally
        {
            _downloadCancellationSource = null;
            _operationGate.Release();
        }
    }

    public void CancelDownload() => _downloadCancellationSource?.Cancel();

    private async Task<ApplicationUpdateInfo?> CheckCoreAsync(bool showFailureFeedback)
    {
        if (!await _operationGate.WaitAsync(0))
        {
            return null;
        }

        try
        {
            SetState(ApplicationUpdateStateKind.Checking);
            Result<ApplicationUpdateInfo?> result;
            try
            {
                result = await _backgroundWorkScheduler.RunAsync(
                    new BackgroundWorkRequest<Result<ApplicationUpdateInfo?>>(
                        BackgroundWorkProfile.ApplicationUpdateCheck,
                        context => _updateGateway.CheckForUpdatesAsync(context.CancellationToken))
                    {
                        SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                        Priority = BackgroundWorkPriority.Background,
                        ShowInStatusBar = false,
                        Timeout = TimeSpan.FromSeconds(30)
                    });
            }
            catch (Exception ex)
            {
                result = Result<ApplicationUpdateInfo?>.Failure(
                    ApplicationUpdateErrors.CheckFailed(ex.Message));
            }
            if (!result.IsSuccess)
            {
                SetState(ApplicationUpdateStateKind.Failed);
                _appLogService.AppendWarning(_localizationService.GetFormattedString(
                    ResourceKeys.ApplicationUpdateCheckFailedLogFormat,
                    result.Errors.FirstOrDefault()?.Code ?? string.Empty));
                if (showFailureFeedback)
                {
                    _notificationService.Show(
                        GetString(ResourceKeys.ApplicationUpdateCheckFailedTitle),
                        GetString(ResourceKeys.ApplicationUpdateCheckFailed),
                        AppNotificationSeverity.Warning);
                }

                return null;
            }

            if (result.Value is null)
            {
                SetState(ApplicationUpdateStateKind.UpToDate);
                if (showFailureFeedback)
                {
                    _notificationService.Show(
                        GetString(ResourceKeys.ApplicationUpdateUpToDateTitle),
                        GetString(ResourceKeys.ApplicationUpdateUpToDateMessage),
                        AppNotificationSeverity.Success);
                }

                return null;
            }

            SetState(ApplicationUpdateStateKind.Available, result.Value.TargetVersion);
            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.ApplicationUpdateAvailableLogFormat,
                result.Value.TargetVersion));
            return result.Value;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<bool> ConfirmUpdateAsync(string targetVersion)
    {
        if (_activeWindowAccessor()?.Content is not FrameworkElement { XamlRoot: { } xamlRoot })
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = GetString(ResourceKeys.ApplicationUpdatePromptTitle),
            Content = new TextBlock
            {
                Text = _localizationService.GetFormattedString(
                    ResourceKeys.ApplicationUpdatePromptMessageFormat,
                    targetVersion),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            },
            PrimaryButtonText = GetString(ResourceKeys.ApplicationUpdatePromptPrimaryButton),
            CloseButtonText = GetString(ResourceKeys.ApplicationUpdatePromptCloseButton),
            DefaultButton = ContentDialogButton.Primary
        };

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.ApplicationUpdatePromptFailedLogFormat,
                ex.Message));
            return false;
        }
    }

    private string? ResolveRestartBlockerCode()
    {
        if (_backgroundWorkScheduler.CurrentTasks.Count > 0)
        {
            return ApplicationUpdateErrors.ActiveOperations().Code;
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            var hasOtherInstance = Process.GetProcessesByName(currentProcess.ProcessName)
                .Any(process =>
                {
                    using (process)
                    {
                        return process.Id != currentProcess.Id;
                    }
                });
            return hasOtherInstance
                ? ApplicationUpdateErrors.OtherInstancesRunning().Code
                : null;
        }
        catch
        {
            return ApplicationUpdateErrors.OtherInstancesRunning().Code;
        }
    }

    private void HandleFailure(string messageKey, string? diagnosticCode)
    {
        SetState(ApplicationUpdateStateKind.Failed, State.TargetVersion);
        var message = GetString(messageKey);
        _appLogService.AppendError(_localizationService.GetFormattedString(
            ResourceKeys.ApplicationUpdateFailedLogFormat,
            message,
            diagnosticCode ?? string.Empty));
        _notificationService.Show(
            GetString(ResourceKeys.ApplicationUpdateFailedTitle),
            message,
            AppNotificationSeverity.Error);
    }

    private void SetState(
        ApplicationUpdateStateKind kind,
        string? targetVersion = null,
        double? progressPercentage = null)
    {
        _state = new ApplicationUpdateViewState(
            kind,
            _updateGateway.RuntimeInfo.CurrentVersion,
            targetVersion,
            progressPercentage);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private string GetString(string key) => _localizationService.GetRequiredString(key);
}
