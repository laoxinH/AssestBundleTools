using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

/// <summary>
/// 承载设置页中的当前工作区缓存治理状态。
/// </summary>
public sealed class SettingsWorkspaceCacheViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceCacheMaintenanceGateway _workspaceCacheMaintenanceGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IAppLogService _appLogService;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly LocalizedValueFormatter _valueFormatter;
    private WorkspaceCacheUsageDto? _usage;
    private string _targetText = string.Empty;
    private string _summaryText = string.Empty;
    private string _lastResultText = string.Empty;
    private bool _autoPruneEnabled;
    private int _maximumSizeSelectedIndex;
    private bool _isBusy;

    public SettingsWorkspaceCacheViewModel(
        IWorkspaceCacheMaintenanceGateway workspaceCacheMaintenanceGateway,
        IWorkspaceContextService workspaceContextService,
        IAppPreferencesService appPreferencesService,
        ILocalizationService localizationService,
        IAppNotificationService appNotificationService,
        IAppLogService appLogService,
        IBackgroundWorkScheduler backgroundWorkScheduler)
    {
        _workspaceCacheMaintenanceGateway = workspaceCacheMaintenanceGateway
                                            ?? throw new ArgumentNullException(nameof(workspaceCacheMaintenanceGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
        _valueFormatter = new LocalizedValueFormatter(_localizationService);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ClearCommand = new AsyncRelayCommand(ClearAsync, CanClear);
        _autoPruneEnabled = _appPreferencesService.BundleArtifactCacheAutoPruneEnabled;
        _maximumSizeSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertBundleArtifactCacheMaximumSizeToSelectedIndex(
                _appPreferencesService.BundleArtifactCacheMaximumSizeInBytes);
        _workspaceContextService.CurrentWorkspaceChanged += OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged += OnWorkspaceContextChanged;
        ApplyNoWorkspaceState();
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ClearCommand { get; }

    public string TargetText
    {
        get => _targetText;
        private set => SetProperty(ref _targetText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string LastResultText
    {
        get => _lastResultText;
        private set
        {
            if (SetProperty(ref _lastResultText, value))
            {
                OnPropertyChanged(nameof(LastResultVisibility));
            }
        }
    }

    public Visibility LastResultVisibility =>
        string.IsNullOrWhiteSpace(LastResultText) ? Visibility.Collapsed : Visibility.Visible;

    public bool AutoPruneEnabled
    {
        get => _autoPruneEnabled;
        set
        {
            if (_autoPruneEnabled == value)
            {
                return;
            }

            if (_appPreferencesService.TrySetBundleArtifactCacheAutoPruneEnabled(value))
            {
                SetProperty(ref _autoPruneEnabled, value);
                return;
            }

            ShowFailure(ResourceKeys.SettingsDiagnosticsBundleArtifactCachePreferenceSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int MaximumSizeSelectedIndex
    {
        get => _maximumSizeSelectedIndex;
        set
        {
            var maximumSizeInBytes =
                SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToBundleArtifactCacheMaximumSize(value);
            var selectedIndex =
                SettingsDiagnosticsPreferenceOptionMapper.ConvertBundleArtifactCacheMaximumSizeToSelectedIndex(
                    maximumSizeInBytes);
            if (_maximumSizeSelectedIndex == selectedIndex)
            {
                return;
            }

            if (_appPreferencesService.TrySetBundleArtifactCacheMaximumSizeInBytes(maximumSizeInBytes))
            {
                if (SetProperty(ref _maximumSizeSelectedIndex, selectedIndex))
                {
                    OnPropertyChanged(nameof(MaximumSizeSliderValue));
                    OnPropertyChanged(nameof(MaximumSizeSelectedText));
                }

                return;
            }

            ShowFailure(ResourceKeys.SettingsDiagnosticsBundleArtifactCachePreferenceSaveFailedMessage);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaximumSizeSliderValue));
            OnPropertyChanged(nameof(MaximumSizeSelectedText));
        }
    }

    public double MaximumSizeSliderValue
    {
        get => MaximumSizeSelectedIndex;
        set => MaximumSizeSelectedIndex = (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    public string MaximumSizeSelectedText => GetString(GetMaximumSizeOptionKey(MaximumSizeSelectedIndex));

    public bool HasActiveWorkspace => TryResolveCurrentWorkspaceScope(out _, out _, out _);

    public bool HasCacheEntries => (_usage?.TotalEntryCount ?? 0) > 0;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public void Dispose()
    {
        _workspaceContextService.CurrentWorkspaceChanged -= OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged -= OnWorkspaceContextChanged;
    }

    private async Task RefreshAsync()
    {
        await RefreshAsync(CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentWorkspaceScope(out var workspaceKey, out var workspaceDataRootPath, out var displayName))
        {
            ApplyNoWorkspaceState();
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            TargetText = _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsBundleArtifactCacheTargetFormat,
                displayName);
            var usageResult = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceCacheUsageDto>>(
                    BackgroundWorkProfile.Maintenance,
                    async context =>
                    {
                        context.Progress.ReportIndeterminate(GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshStatusTitle));
                        return await _workspaceCacheMaintenanceGateway.GetWorkspaceCacheUsageAsync(
                            new GetWorkspaceCacheUsageQuery(workspaceDataRootPath, workspaceKey),
                            context.CancellationToken);
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                    Priority = BackgroundWorkPriority.Maintenance,
                    WorkspaceKey = workspaceKey,
                    StatusTitle = GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshStatusTitle),
                    ShowInStatusBar = true
                },
                cancellationToken);

            if (!TryApplyUsageResult(usageResult, ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshFailedMessage))
            {
                return;
            }

            LastResultText = string.Empty;
        });
    }

    private async Task ClearAsync()
    {
        if (!TryResolveCurrentWorkspaceScope(out var workspaceKey, out var workspaceDataRootPath, out _))
        {
            ApplyNoWorkspaceState();
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var beforeSize = _usage?.TotalSizeInBytes ?? 0;
            var clearResult = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceCacheClearResultDto>>(
                    BackgroundWorkProfile.Maintenance,
                    async context =>
                    {
                        context.Progress.ReportIndeterminate(GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearStatusTitle));
                        return await _workspaceCacheMaintenanceGateway.ClearWorkspaceCacheAsync(
                            new ClearWorkspaceCacheCommand(
                                workspaceDataRootPath,
                                workspaceKey),
                            context.CancellationToken);
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.WorkspaceMutation,
                    Priority = BackgroundWorkPriority.Maintenance,
                    WorkspaceKey = workspaceKey,
                    StatusTitle = GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearStatusTitle),
                    ShowInStatusBar = true
                });

            if (!clearResult.IsSuccess || clearResult.Value is null)
            {
                ShowFailure(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearFailedMessage);
                return;
            }

            var releasedBytes = Math.Max(0, beforeSize - clearResult.Value.SizeAfterInBytes);
            LastResultText = _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearSucceededFormat,
                clearResult.Value.EntryCountBefore - clearResult.Value.EntryCountAfter,
                _valueFormatter.FormatDetailedByteSize(releasedBytes));
            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                LastResultText,
                AppNotificationSeverity.Success);
            _usage = new WorkspaceCacheUsageDto(
                clearResult.Value.SizeAfterInBytes,
                clearResult.Value.EntryCountAfter,
                clearResult.Value.BundleArtifactCacheResult.ToUsage(),
                clearResult.Value.PreviewCacheResult.ToUsage());
            SummaryText = _usage.TotalEntryCount == 0
                ? GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheEmpty)
                : _localizationService.GetFormattedString(
                    ResourceKeys.SettingsDiagnosticsBundleArtifactCacheSummaryFormat,
                    _usage.TotalEntryCount,
                    _valueFormatter.FormatDetailedByteSize(_usage.TotalSizeInBytes),
                    _usage.BundleArtifactCache.ArtifactCount,
                    _usage.PreviewCache.EntryCount);
            NotifyCacheState();
        });
    }

    private async Task ExecuteBusyAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        NotifyCommandState();
        try
        {
            await action();
        }
        finally
        {
            _isBusy = false;
            NotifyCommandState();
        }
    }

    private bool TryApplyUsageResult(Result<WorkspaceCacheUsageDto> result, string failureResourceKey)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            ShowFailure(failureResourceKey);
            return false;
        }

        _usage = result.Value;
        SummaryText = _usage.TotalEntryCount == 0
            ? GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheEmpty)
            : _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsBundleArtifactCacheSummaryFormat,
                _usage.TotalEntryCount,
                _valueFormatter.FormatDetailedByteSize(_usage.TotalSizeInBytes),
                _usage.BundleArtifactCache.ArtifactCount,
                _usage.PreviewCache.EntryCount);
        NotifyCacheState();
        return true;
    }

    private bool TryResolveCurrentWorkspaceScope(
        out string workspaceKey,
        out string? workspaceDataRootPath,
        out string displayName)
    {
        workspaceKey = string.Empty;
        workspaceDataRootPath = null;
        displayName = string.Empty;
        if (!_workspaceContextService.TryGetCurrentWorkspaceEntry(out var entry) ||
            entry is null ||
            string.IsNullOrWhiteSpace(entry.WorkspaceKey))
        {
            return false;
        }

        workspaceKey = entry.WorkspaceKey;
        workspaceDataRootPath = entry.WorkspaceDataRootPath;
        displayName = entry.DisplayName;
        return true;
    }

    private void ApplyNoWorkspaceState()
    {
        _usage = null;
        TargetText = GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheNoWorkspace);
        SummaryText = GetString(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheNoWorkspaceHint);
        LastResultText = string.Empty;
        NotifyCacheState();
    }

    private void ShowFailure(string messageResourceKey)
    {
        var message = GetString(messageResourceKey);
        _appLogService.AppendError(message);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            message,
            AppNotificationSeverity.Error);
    }

    private void OnWorkspaceContextChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync(CancellationToken.None);
    }

    private bool CanRefresh() => !_isBusy && HasActiveWorkspace;

    private bool CanClear() => !_isBusy && HasActiveWorkspace && HasCacheEntries;

    private static string GetMaximumSizeOptionKey(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionOneGb,
            1 => ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionTwoGb,
            3 => ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionEightGb,
            4 => ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionSixteenGb,
            _ => ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionFourGb
        };
    }

    private void NotifyCacheState()
    {
        OnPropertyChanged(nameof(HasActiveWorkspace));
        OnPropertyChanged(nameof(HasCacheEntries));
        RefreshCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommandState()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}

file static class WorkspaceCacheSettingsDtoMapper
{
    public static BundleArtifactCacheUsageDto ToUsage(this BundleArtifactPruneResultDto result)
    {
        return new BundleArtifactCacheUsageDto(
            result.SizeAfterInBytes,
            result.ArtifactCountAfter,
            Array.Empty<BundleArtifactCacheEntryDto>());
    }

    public static PreviewCacheUsageDto ToUsage(this PreviewCachePruneResultDto result)
    {
        return new PreviewCacheUsageDto(
            result.SizeAfterInBytes,
            result.EntryCountAfter,
            Array.Empty<PreviewCacheEntryDto>());
    }
}
