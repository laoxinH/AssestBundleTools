using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Home;

/// <summary>
/// 负责控制台页中的最近工作区和最近活动摘要展示。
/// </summary>
public sealed class HomeViewModel : ObservableObject
{
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;

    public HomeViewModel(
        IWorkspaceContextService workspaceContextService,
        IAppLogService appLogService,
        ILocalizationService localizationService)
    {
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        _workspaceContextService.WorkspaceEntriesChanged += OnWorkspaceEntriesChanged;
        _workspaceContextService.CurrentWorkspaceChanged += OnWorkspaceEntriesChanged;
        _appLogService.LogPanel.Entries.CollectionChanged += OnLogEntriesCollectionChanged;

        RefreshRecentWorkspaceItems();
        RefreshRecentActivityItems();
    }

    public ObservableCollection<HomeRecentWorkspaceItemViewModel> RecentWorkspaceItems { get; } = new();

    public ObservableCollection<HomeRecentActivityItemViewModel> RecentActivityItems { get; } = new();

    public bool HasRecentWorkspaces => RecentWorkspaceItems.Count > 0;

    public bool HasRecentActivities => RecentActivityItems.Count > 0;

    public HomeRecentWorkspaceItemViewModel? LatestRecentWorkspaceItem =>
        RecentWorkspaceItems.FirstOrDefault(static item => item.CanOpen);

    public bool CanOpenLatestRecentWorkspace => LatestRecentWorkspaceItem is not null;

    public Visibility RecentWorkspaceListVisibility => HasRecentWorkspaces ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecentWorkspaceEmptyStateVisibility => HasRecentWorkspaces ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RecentActivityListVisibility => HasRecentActivities ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecentActivityEmptyStateVisibility => HasRecentActivities ? Visibility.Collapsed : Visibility.Visible;

    public void Dispose()
    {
        _workspaceContextService.WorkspaceEntriesChanged -= OnWorkspaceEntriesChanged;
        _workspaceContextService.CurrentWorkspaceChanged -= OnWorkspaceEntriesChanged;
        _appLogService.LogPanel.Entries.CollectionChanged -= OnLogEntriesCollectionChanged;
    }

    private void OnWorkspaceEntriesChanged(object? sender, EventArgs e)
    {
        RefreshRecentWorkspaceItems();
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentActivityItems();
    }

    private void RefreshRecentWorkspaceItems()
    {
        var currentWorkspaceId = _workspaceContextService.CurrentWorkspaceSummary?.WorkspaceId.Value;

        RecentWorkspaceItems.Clear();
        foreach (var entry in _workspaceContextService.WorkspaceEntries)
        {
            var isCurrentWorkspace = !string.IsNullOrWhiteSpace(currentWorkspaceId) &&
                                     string.Equals(entry.WorkspaceSummary?.WorkspaceId.Value, currentWorkspaceId, StringComparison.Ordinal);
            var presentationDescriptor = WorkspaceEntryPresentationBuilder.Build(
                entry,
                currentWorkspaceId,
                _localizationService);
            var workspaceDataRootPath = string.IsNullOrWhiteSpace(entry.WorkspaceDataRootPath)
                ? _localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspaceDataRootNotSpecified)
                : entry.WorkspaceDataRootPath;
            var sourceText = string.IsNullOrWhiteSpace(entry.SourcePath)
                ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceSourceNotImported)
                : entry.SourcePath;

            RecentWorkspaceItems.Add(new HomeRecentWorkspaceItemViewModel(
                workspaceKey: entry.WorkspaceKey,
                displayName: entry.DisplayName,
                sourcePath: entry.SourcePath,
                workspaceDataRootPath: entry.WorkspaceDataRootPath,
                sourceSummary: _localizationService.GetFormattedString(
                    ResourceKeys.HomePageRecentWorkspaceSourceFormat,
                    sourceText),
                workspaceDataRootSummary: _localizationService.GetFormattedString(
                    ResourceKeys.HomePageRecentWorkspaceDataRootFormat,
                    workspaceDataRootPath),
                lastActivatedSummary: _localizationService.GetFormattedString(
                    ResourceKeys.HomePageRecentWorkspaceLastActivatedFormat,
                    entry.LastActivatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
                primaryBadgeText: presentationDescriptor.PrimaryBadgeText,
                secondaryBadgeText: presentationDescriptor.SecondaryBadgeText,
                availabilitySummary: presentationDescriptor.AvailabilitySummary,
                isCurrentWorkspace: isCurrentWorkspace,
                isPinned: entry.IsPinned,
                canOpen: presentationDescriptor.CanOpen,
                pinButtonToolTipText: entry.IsPinned
                    ? _localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspaceUnpinButtonToolTip)
                    : _localizationService.GetRequiredString(ResourceKeys.HomePageRecentWorkspacePinButtonToolTip)));
        }

        OnPropertyChanged(nameof(HasRecentWorkspaces));
        OnPropertyChanged(nameof(LatestRecentWorkspaceItem));
        OnPropertyChanged(nameof(CanOpenLatestRecentWorkspace));
        OnPropertyChanged(nameof(RecentWorkspaceListVisibility));
        OnPropertyChanged(nameof(RecentWorkspaceEmptyStateVisibility));
    }

    private void RefreshRecentActivityItems()
    {
        RecentActivityItems.Clear();

        foreach (var entry in _appLogService.LogPanel.Entries.Take(3))
        {
            RecentActivityItems.Add(new HomeRecentActivityItemViewModel(
                entry.TimestampText,
                entry.Message,
                detail: string.Empty));
        }

        OnPropertyChanged(nameof(HasRecentActivities));
        OnPropertyChanged(nameof(RecentActivityListVisibility));
        OnPropertyChanged(nameof(RecentActivityEmptyStateVisibility));
    }
}
