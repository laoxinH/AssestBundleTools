using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
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
public sealed partial class SettingsDiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IAppThemeService _appThemeService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly ILocalizationService _localizationService;
    private string _activeWorkspaceDisplayName = string.Empty;
    private string _activeWorkspacePrimaryBadgeText = string.Empty;
    private string _activeWorkspaceSecondaryBadgeText = string.Empty;
    private string _activeWorkspaceSourceSummary = string.Empty;
    private string _activeWorkspaceDataRootSummary = string.Empty;
    private string _activeWorkspaceSessionSummary = string.Empty;
    private string _activeWorkspaceAvailabilitySummary = string.Empty;
    private string _workspaceCatalogSummaryText = string.Empty;
    public SettingsDiagnosticsViewModel(
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        IAppPreferencesService appPreferencesService,
        IAppThemeService appThemeService,
        IWorkspaceContextService workspaceContextService,
        ILocalizationService localizationService,
        IWorkspaceCacheMaintenanceGateway workspaceCacheMaintenanceGateway,
        IBackgroundWorkScheduler backgroundWorkScheduler)
    {
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
        _appThemeService = appThemeService ?? throw new ArgumentNullException(nameof(appThemeService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        BundleArtifactCache = new SettingsWorkspaceCacheViewModel(
            workspaceCacheMaintenanceGateway,
            _workspaceContextService,
            _appPreferencesService,
            _localizationService,
            _appNotificationService,
            _appLogService,
            backgroundWorkScheduler);

        LogPanel = _appLogService.LogPanel;
        _restoreLastWorkspaceOnStartup = _appPreferencesService.RestoreLastWorkspaceOnStartup;
        _elevatedDragDropCompatibilityEnabled =
            _appPreferencesService.ElevatedDragDropCompatibilityEnabled;
        _useContentAwareSpineSkeletonFileExtension =
            _appPreferencesService.UseContentAwareSpineSkeletonFileExtension;
        _recentWorkspaceOpenBehaviorSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertRecentWorkspaceOpenBehaviorToSelectedIndex(
                _appPreferencesService.RecentWorkspaceOpenBehavior);
        _assetEditorCloseBehaviorSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertAssetEditorCloseBehaviorToSelectedIndex(
                _appPreferencesService.AssetEditorCloseBehavior);
        _displayLanguageSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertDisplayLanguageToSelectedIndex(
                _appPreferencesService.DisplayLanguage);
        _themeSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertThemeToSelectedIndex(
                _appPreferencesService.Theme);
        _workspaceImportWorkerCountSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertWorkspaceImportWorkerCountToSelectedIndex(
                _appPreferencesService.WorkspaceImportWorkerCount);
        _previewWorkerCountSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertPreviewWorkerCountToSelectedIndex(
                _appPreferencesService.PreviewWorkerCount);
        _operationRecordLimitSelectedIndex =
            SettingsDiagnosticsPreferenceOptionMapper.ConvertOperationRecordLimitToSelectedIndex(
                _appPreferencesService.OperationRecordLimit);
        _workspaceContextService.CurrentWorkspaceChanged += OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged += OnWorkspaceContextChanged;

        RefreshWorkspaceContext();
    }

    public LogPanelViewModel LogPanel { get; }

    public ObservableCollection<SettingsWorkspaceEntryItemViewModel> WorkspaceEntries { get; } = new();

    public SettingsWorkspaceCacheViewModel BundleArtifactCache { get; }

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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await BundleArtifactCache.InitializeAsync(cancellationToken);
    }

    public void Dispose()
    {
        _workspaceContextService.CurrentWorkspaceChanged -= OnWorkspaceContextChanged;
        _workspaceContextService.WorkspaceEntriesChanged -= OnWorkspaceContextChanged;
        BundleArtifactCache.Dispose();
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

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(HasActiveWorkspace));
        OnPropertyChanged(nameof(HasWorkspaceEntries));
        OnPropertyChanged(nameof(ActiveWorkspaceVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceEmptyVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceSecondaryBadgeVisibility));
        OnPropertyChanged(nameof(ActiveWorkspaceAvailabilityVisibility));
        OnPropertyChanged(nameof(WorkspaceEntryListVisibility));
        OnPropertyChanged(nameof(WorkspaceEntryEmptyVisibility));
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

