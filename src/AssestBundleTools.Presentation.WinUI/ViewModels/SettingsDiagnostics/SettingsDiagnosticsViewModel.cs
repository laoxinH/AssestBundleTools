using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

/// <summary>
/// 表示“设置与诊断”页面当前阶段的展示状态。
/// </summary>
/// <remarks>
/// 当前先聚焦承接应用级日志中心，后续再继续向这里补语言切换、
/// 环境诊断、日志导出和调试开关等设置项。
/// </remarks>
public sealed class SettingsDiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceImportCoordinatorService _workspaceImportCoordinatorService;
    private readonly IAssetBundleDecryptorCatalogService _assetBundleDecryptorCatalogService;
    private readonly SettingsDecryptorCatalogEntryPresenter _decryptorEntryPresenter;
    private readonly Dictionary<string, AssetBundleDecryptorCatalogEntryDto> _decryptorEntriesById = new(StringComparer.OrdinalIgnoreCase);
    private string _activeWorkspaceDisplayName = string.Empty;
    private string _activeWorkspacePrimaryBadgeText = string.Empty;
    private string _activeWorkspaceSecondaryBadgeText = string.Empty;
    private string _activeWorkspaceSourceSummary = string.Empty;
    private string _activeWorkspaceDataRootSummary = string.Empty;
    private string _activeWorkspaceSessionSummary = string.Empty;
    private string _activeWorkspaceAvailabilitySummary = string.Empty;
    private string _workspaceCatalogSummaryText = string.Empty;
    private bool _restoreLastWorkspaceOnStartup;
    private int _recentWorkspaceOpenBehaviorSelectedIndex;
    private int _workspaceImportWorkerCountSelectedIndex;

    public SettingsDiagnosticsViewModel(
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        IAppPreferencesService appPreferencesService,
        IWorkspaceContextService workspaceContextService,
        ILocalizationService localizationService,
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService,
        IAssetBundleDecryptorCatalogService assetBundleDecryptorCatalogService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _workspaceImportCoordinatorService = workspaceImportCoordinatorService ?? throw new ArgumentNullException(nameof(workspaceImportCoordinatorService));
        _assetBundleDecryptorCatalogService = assetBundleDecryptorCatalogService ?? throw new ArgumentNullException(nameof(assetBundleDecryptorCatalogService));
        _decryptorEntryPresenter = new SettingsDecryptorCatalogEntryPresenter(
            _localizationService,
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));

        LogPanel = _appLogService.LogPanel;
        ImportBundleFilesCommand = new AsyncRelayCommand(
            executeAsync: RequestBundleFileImportAsync,
            canExecute: () => HasActiveWorkspace);
        ImportBundleDirectoryCommand = new AsyncRelayCommand(
            executeAsync: RequestBundleDirectoryImportAsync,
            canExecute: () => HasActiveWorkspace);
        _restoreLastWorkspaceOnStartup = _appPreferencesService.RestoreLastWorkspaceOnStartup;
        _recentWorkspaceOpenBehaviorSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertRecentWorkspaceOpenBehaviorToSelectedIndex(
                _appPreferencesService.RecentWorkspaceOpenBehavior);
        _workspaceImportWorkerCountSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertWorkspaceImportWorkerCountToSelectedIndex(
                _appPreferencesService.WorkspaceImportWorkerCount);
        _workspaceContextService.CurrentWorkspaceChanged += OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged += OnWorkspaceContextChanged;

        RefreshWorkspaceContext();
    }

    public LogPanelViewModel LogPanel { get; }

    public ObservableCollection<SettingsWorkspaceEntryItemViewModel> WorkspaceEntries { get; } = new();

    public ObservableCollection<SettingsDecryptorCatalogEntryItemViewModel> DecryptorEntries { get; } = new();

    public AsyncRelayCommand ImportBundleFilesCommand { get; }

    public AsyncRelayCommand ImportBundleDirectoryCommand { get; }

    public string ActiveWorkspaceDisplayName
    {
        get => _activeWorkspaceDisplayName;
        private set => SetProperty(ref _activeWorkspaceDisplayName, value);
    }

    public string ActiveWorkspacePrimaryBadgeText
    {
        get => _activeWorkspacePrimaryBadgeText;
        private set => SetProperty(ref _activeWorkspacePrimaryBadgeText, value);
    }

    public string ActiveWorkspaceSecondaryBadgeText
    {
        get => _activeWorkspaceSecondaryBadgeText;
        private set => SetProperty(ref _activeWorkspaceSecondaryBadgeText, value);
    }

    public string ActiveWorkspaceSourceSummary
    {
        get => _activeWorkspaceSourceSummary;
        private set => SetProperty(ref _activeWorkspaceSourceSummary, value);
    }

    public string ActiveWorkspaceDataRootSummary
    {
        get => _activeWorkspaceDataRootSummary;
        private set => SetProperty(ref _activeWorkspaceDataRootSummary, value);
    }

    public string ActiveWorkspaceSessionSummary
    {
        get => _activeWorkspaceSessionSummary;
        private set => SetProperty(ref _activeWorkspaceSessionSummary, value);
    }

    public string ActiveWorkspaceAvailabilitySummary
    {
        get => _activeWorkspaceAvailabilitySummary;
        private set => SetProperty(ref _activeWorkspaceAvailabilitySummary, value);
    }

    public string WorkspaceCatalogSummaryText
    {
        get => _workspaceCatalogSummaryText;
        private set => SetProperty(ref _workspaceCatalogSummaryText, value);
    }

    /// <summary>
    /// 控制应用启动时是否自动恢复上次活动工作区。
    /// 这个偏好属于窗口 / 启动体验层面的设置，不下沉到业务用例。
    /// </summary>
    public bool RestoreLastWorkspaceOnStartup
    {
        get => _restoreLastWorkspaceOnStartup;
        set
        {
            if (_restoreLastWorkspaceOnStartup == value)
            {
                return;
            }

            if (_appPreferencesService.TrySetRestoreLastWorkspaceOnStartup(value))
            {
                SetProperty(ref _restoreLastWorkspaceOnStartup, value);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsStartupBehaviorSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsStartupBehaviorSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 控制台中“最近工作区”的默认打开策略。
    /// 当前窗口、新建窗口或每次询问。
    /// </summary>
    public int RecentWorkspaceOpenBehaviorSelectedIndex
    {
        get => _recentWorkspaceOpenBehaviorSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.CoerceRecentWorkspaceOpenBehaviorSelectedIndex(value);
            if (_recentWorkspaceOpenBehaviorSelectedIndex == selectedIndex)
            {
                return;
            }

            var openBehavior = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToRecentWorkspaceOpenBehavior(selectedIndex);
            if (_appPreferencesService.TrySetRecentWorkspaceOpenBehavior(openBehavior))
            {
                SetProperty(ref _recentWorkspaceOpenBehaviorSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int WorkspaceImportWorkerCountSelectedIndex
    {
        get => _workspaceImportWorkerCountSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.ConvertWorkspaceImportWorkerCountToSelectedIndex(value);
            if (_workspaceImportWorkerCountSelectedIndex == selectedIndex)
            {
                return;
            }

            var workerCount = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToWorkspaceImportWorkerCount(selectedIndex);
            if (_appPreferencesService.TrySetWorkspaceImportWorkerCount(workerCount))
            {
                SetProperty(ref _workspaceImportWorkerCountSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public bool HasActiveWorkspace => _workspaceContextService.CurrentWorkspaceSummary is not null;

    public bool HasWorkspaceEntries => WorkspaceEntries.Count > 0;

    public Visibility ActiveWorkspaceVisibility => HasActiveWorkspace ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ActiveWorkspaceEmptyVisibility => HasActiveWorkspace ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ActiveWorkspaceSecondaryBadgeVisibility =>
        string.IsNullOrWhiteSpace(ActiveWorkspaceSecondaryBadgeText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ActiveWorkspaceAvailabilityVisibility =>
        string.IsNullOrWhiteSpace(ActiveWorkspaceAvailabilitySummary) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WorkspaceEntryListVisibility => HasWorkspaceEntries ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkspaceEntryEmptyVisibility => HasWorkspaceEntries ? Visibility.Collapsed : Visibility.Visible;

    public bool HasDecryptorEntries => DecryptorEntries.Count > 0;

    public Visibility DecryptorEntryListVisibility => HasDecryptorEntries ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DecryptorEntryEmptyVisibility => HasDecryptorEntries ? Visibility.Collapsed : Visibility.Visible;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDecryptorCatalogAsync(cancellationToken);
    }

    public bool TryGetDecryptorEntry(string decryptorId, out AssetBundleDecryptorCatalogEntryDto? entry)
    {
        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            entry = null;
            return false;
        }

        return _decryptorEntriesById.TryGetValue(decryptorId.Trim(), out entry);
    }

    public async Task RefreshDecryptorCatalogAsync(CancellationToken cancellationToken = default)
    {
        var result = await _assetBundleDecryptorCatalogService.GetEntriesAsync(cancellationToken);
        if (!TryGetValue(result, out var entries, ResourceKeys.SettingsDiagnosticsDecryptorLoadFailedMessage))
        {
            return;
        }

        ReplaceDecryptorEntries(entries ?? Array.Empty<AssetBundleDecryptorCatalogEntryDto>());
    }

    public async Task<bool> SaveDecryptorAsync(
        UpsertAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _assetBundleDecryptorCatalogService.UpsertAsync(command, cancellationToken);
        if (!TryGetValue(result, out var savedEntry, ResourceKeys.SettingsDiagnosticsDecryptorSaveFailedMessage))
        {
            return false;
        }

        if (savedEntry is null)
        {
            return false;
        }

        await RefreshDecryptorCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsDecryptorSaveSucceededMessageFormat,
                _decryptorEntryPresenter.GetDisplayName(savedEntry.Definition.Id)),
            AppNotificationSeverity.Success);
        return true;
    }

    public async Task<bool> DeleteDecryptorAsync(
        string decryptorId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetDecryptorEntry(decryptorId, out var entry) ||
            entry is null)
        {
            return false;
        }

        var targetEntry = entry;

        var result = await _assetBundleDecryptorCatalogService.DeleteAsync(
            new DeleteAssetBundleDecryptorCommand(decryptorId),
            cancellationToken);
        if (!TryGetValue(result, ResourceKeys.SettingsDiagnosticsDecryptorDeleteFailedMessage))
        {
            return false;
        }

        await RefreshDecryptorCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsDecryptorDeleteSucceededMessageFormat,
                _decryptorEntryPresenter.GetDisplayName(targetEntry.Definition.Id)),
            AppNotificationSeverity.Success);
        return true;
    }

    public void Dispose()
    {
        _workspaceContextService.CurrentWorkspaceChanged -= OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged -= OnWorkspaceContextChanged;
    }

    private void ShowErrorNotification(string messageResourceKey)
    {
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            GetString(messageResourceKey),
            AppNotificationSeverity.Error);
    }

    /// <summary>
    /// 统一从应用级工作区上下文服务刷新页面摘要。
    /// 这样设置页不会自行拼装状态来源，只消费 Presentation 层共享上下文。
    /// </summary>
    private void RefreshWorkspaceContext()
    {
        ApplyActiveWorkspaceState(_workspaceContextService.CurrentWorkspaceSummary);
        ReplaceWorkspaceEntries(_workspaceContextService.WorkspaceEntries);
        WorkspaceCatalogSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.SettingsDiagnosticsWorkspaceCatalogSummaryFormat,
            _workspaceContextService.WorkspaceEntryCount,
            _workspaceContextService.WorkspaceEntries.Count(static entry => entry.HasLiveSession));

        NotifyWorkspaceState();
    }

    private void ApplyActiveWorkspaceState(WorkspaceSummaryDto? activeWorkspace)
    {
        if (activeWorkspace is null)
        {
            ActiveWorkspaceDisplayName = string.Empty;
            ActiveWorkspacePrimaryBadgeText = string.Empty;
            ActiveWorkspaceSecondaryBadgeText = string.Empty;
            ActiveWorkspaceSourceSummary = string.Empty;
            ActiveWorkspaceDataRootSummary = string.Empty;
            ActiveWorkspaceSessionSummary = string.Empty;
            ActiveWorkspaceAvailabilitySummary = string.Empty;
            return;
        }

        var workspaceEntry = _workspaceContextService.WorkspaceEntries.FirstOrDefault(entry =>
            string.Equals(
                entry.WorkspaceSummary?.WorkspaceId.Value,
                activeWorkspace.WorkspaceId.Value,
                StringComparison.Ordinal));
        var presentationDescriptor = workspaceEntry is null
            ? null
            : WorkspaceEntryPresentationBuilder.Build(
                workspaceEntry,
                activeWorkspace.WorkspaceId.Value,
                _localizationService);

        ActiveWorkspaceDisplayName = activeWorkspace.DisplayName;
        ActiveWorkspacePrimaryBadgeText = presentationDescriptor?.PrimaryBadgeText
            ?? GetString(ResourceKeys.HomePageRecentWorkspaceCurrentBadge);
        ActiveWorkspaceSecondaryBadgeText = presentationDescriptor?.SecondaryBadgeText ?? string.Empty;
        ActiveWorkspaceSourceSummary = BuildSourceSummary(activeWorkspace.SourcePath);
        ActiveWorkspaceDataRootSummary = BuildDataRootSummary(workspaceEntry?.WorkspaceDataRootPath);
        ActiveWorkspaceSessionSummary = _localizationService.GetFormattedString(
            ResourceKeys.SettingsDiagnosticsActiveWorkspaceSessionSummaryFormat,
            activeWorkspace.BundleCount,
            activeWorkspace.AssetCount,
            FormatTimestamp(activeWorkspace.OpenedAtUtc));
        ActiveWorkspaceAvailabilitySummary = presentationDescriptor?.AvailabilitySummary ?? string.Empty;
    }

    /// <summary>
    /// 构建工作区目录摘要列表。
    /// 当前先强调“当前活动 / 已加载会话 / 已登记目录项”三类状态，
    /// 为后续固定工作区、不可用状态表达预留稳定承载面。
    /// </summary>
    private void ReplaceWorkspaceEntries(IReadOnlyList<WorkspaceContextEntryDto> entries)
    {
        var currentWorkspaceId = _workspaceContextService.CurrentWorkspaceSummary?.WorkspaceId.Value;

        WorkspaceEntries.Clear();
        foreach (var entry in entries)
        {
            var presentationDescriptor = WorkspaceEntryPresentationBuilder.Build(
                entry,
                currentWorkspaceId,
                _localizationService);
            WorkspaceEntries.Add(new SettingsWorkspaceEntryItemViewModel(
                DisplayName: entry.DisplayName,
                PrimaryBadgeText: presentationDescriptor.PrimaryBadgeText,
                SecondaryBadgeText: presentationDescriptor.SecondaryBadgeText,
                SourceSummary: BuildSourceSummary(entry.SourcePath),
                LastActivatedSummary: _localizationService.GetFormattedString(
                    ResourceKeys.HomePageRecentWorkspaceLastActivatedFormat,
                    FormatTimestamp(entry.LastActivatedAtUtc)),
                AvailabilitySummary: presentationDescriptor.AvailabilitySummary ?? string.Empty));
        }
    }

    private void ReplaceDecryptorEntries(IReadOnlyList<AssetBundleDecryptorCatalogEntryDto> entries)
    {
        _decryptorEntriesById.Clear();
        DecryptorEntries.Clear();

        foreach (var entry in entries)
        {
            _decryptorEntriesById[entry.Definition.Id] = entry;
            DecryptorEntries.Add(_decryptorEntryPresenter.Build(entry));
        }

        NotifyWorkspaceState();
    }

    private string BuildSourceSummary(string? sourcePath)
    {
        var sourceText = string.IsNullOrWhiteSpace(sourcePath)
            ? GetString(ResourceKeys.WorkspaceSourceNotImported)
            : sourcePath;

        return _localizationService.GetFormattedString(
            ResourceKeys.HomePageRecentWorkspaceSourceFormat,
            sourceText);
    }

    private string BuildDataRootSummary(string? workspaceDataRootPath)
    {
        var dataRootText = string.IsNullOrWhiteSpace(workspaceDataRootPath)
            ? GetString(ResourceKeys.HomePageRecentWorkspaceDataRootNotSpecified)
            : workspaceDataRootPath;

        return _localizationService.GetFormattedString(
            ResourceKeys.HomePageRecentWorkspaceDataRootFormat,
            dataRootText);
    }

    private void OnWorkspaceContextChanged(object? sender, EventArgs e)
    {
        RefreshWorkspaceContext();
    }

    private async Task RequestBundleFileImportAsync()
    {
        await _workspaceImportCoordinatorService.RequestBundleFileImportAsync();
    }

    private async Task RequestBundleDirectoryImportAsync()
    {
        await _workspaceImportCoordinatorService.RequestBundleDirectoryImportAsync();
    }

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(HasActiveWorkspace));
        OnPropertyChanged(nameof(HasWorkspaceEntries));
        OnPropertyChanged(nameof(HasDecryptorEntries));
        OnPropertyChanged(nameof(ActiveWorkspaceVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceEmptyVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceSecondaryBadgeVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceAvailabilityVisibility));
        OnPropertyChanged(nameof(WorkspaceEntryListVisibility));
        OnPropertyChanged(nameof(WorkspaceEntryEmptyVisibility));
        OnPropertyChanged(nameof(DecryptorEntryListVisibility));
        OnPropertyChanged(nameof(DecryptorEntryEmptyVisibility));
        ImportBundleFilesCommand.NotifyCanExecuteChanged();
        ImportBundleDirectoryCommand.NotifyCanExecuteChanged();
    }

    private string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private bool TryGetValue<T>(
        AssestBundleTools.Application.Common.Results.Result<T> result,
        out T? value,
        string fallbackMessageResourceKey)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;
        if (result.IsCancelled)
        {
            return false;
        }

        ReportErrors(result.Errors, fallbackMessageResourceKey);
        return false;
    }

    private bool TryGetValue(
        AssestBundleTools.Application.Common.Results.Result result,
        string fallbackMessageResourceKey)
    {
        if (result.IsSuccess)
        {
            return true;
        }

        if (result.IsCancelled)
        {
            return false;
        }

        ReportErrors(result.Errors, fallbackMessageResourceKey);
        return false;
    }

    private void ReportErrors(IReadOnlyList<AppError> errors, string fallbackMessageResourceKey)
    {
        var message = ResolveErrorMessage(errors, fallbackMessageResourceKey);
        _appLogService.AppendError(message);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            message,
            AppNotificationSeverity.Error);
    }

    private string ResolveErrorMessage(IReadOnlyList<AppError> errors, string fallbackMessageResourceKey)
    {
        if (errors.Count == 0)
        {
            return GetString(fallbackMessageResourceKey);
        }

        var firstError = errors[0];
        return firstError.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                firstError.MessageToken,
                firstError.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : _localizationService.GetRequiredString(firstError.MessageToken);
    }

}

