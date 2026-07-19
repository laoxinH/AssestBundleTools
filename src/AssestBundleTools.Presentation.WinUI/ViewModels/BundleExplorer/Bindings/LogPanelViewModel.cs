using System.Collections.ObjectModel;
using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示详细日志面板的展示状态。
/// </summary>
public sealed class LogPanelViewModel : ObservableObject
{
    private string _emptyStateText = string.Empty;
    private string _emptyStateTitleText = string.Empty;
    private string _filterEmptyStateText = string.Empty;
    private string _filterEmptyStateTitleText = string.Empty;
    private LogPanelFilter _selectedFilter;

    public string EmptyStateText
    {
        get => _emptyStateText;
        set
        {
            if (SetProperty(ref _emptyStateText, value))
            {
                OnPropertyChanged(nameof(DisplayedEmptyStateText));
            }
        }
    }

    public string EmptyStateTitleText
    {
        get => _emptyStateTitleText;
        set
        {
            if (SetProperty(ref _emptyStateTitleText, value))
            {
                OnPropertyChanged(nameof(DisplayedEmptyStateTitleText));
            }
        }
    }

    public string FilterEmptyStateText
    {
        get => _filterEmptyStateText;
        set
        {
            if (SetProperty(ref _filterEmptyStateText, value))
            {
                OnPropertyChanged(nameof(DisplayedEmptyStateText));
            }
        }
    }

    public string FilterEmptyStateTitleText
    {
        get => _filterEmptyStateTitleText;
        set
        {
            if (SetProperty(ref _filterEmptyStateTitleText, value))
            {
                OnPropertyChanged(nameof(DisplayedEmptyStateTitleText));
            }
        }
    }

    public ObservableCollection<LogEntryViewModel> Entries { get; } = new();

    public ObservableCollection<LogEntryViewModel> FilteredEntries { get; } = new();

    public LogPanelFilter SelectedFilter => _selectedFilter;

    public bool IsAllFilterSelected => SelectedFilter == LogPanelFilter.All;

    public bool IsInformationFilterSelected => SelectedFilter == LogPanelFilter.Information;

    public bool IsWarningFilterSelected => SelectedFilter == LogPanelFilter.Warning;

    public bool IsErrorFilterSelected => SelectedFilter == LogPanelFilter.Error;

    public string DisplayedEmptyStateText =>
        Entries.Count == 0 ? EmptyStateText : FilterEmptyStateText;

    public string DisplayedEmptyStateTitleText =>
        Entries.Count == 0 ? EmptyStateTitleText : FilterEmptyStateTitleText;

    public bool CanCopyAll => Entries.Count > 0;

    public bool CanClear => Entries.Count > 0;

    public Visibility EntriesVisibility =>
        FilteredEntries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility =>
        FilteredEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public void Clear()
    {
        Entries.Clear();
        FilteredEntries.Clear();
        NotifyEntriesChanged();
    }

    public void Append(
        string message,
        WorkspaceDiagnosticLogLevel level = WorkspaceDiagnosticLogLevel.Information)
    {
        var entry = new LogEntryViewModel(
            TimestampText: DateTime.Now.ToString("HH:mm:ss"),
            Message: message,
            Level: level);
        Entries.Insert(0, entry);
        if (MatchesFilter(entry, SelectedFilter))
        {
            FilteredEntries.Insert(0, entry);
        }

        NotifyEntriesChanged();
    }

    public void SetFilter(LogPanelFilter filter)
    {
        if (_selectedFilter == filter)
        {
            return;
        }

        _selectedFilter = filter;
        OnPropertyChanged(nameof(SelectedFilter));
        OnPropertyChanged(nameof(IsAllFilterSelected));
        OnPropertyChanged(nameof(IsInformationFilterSelected));
        OnPropertyChanged(nameof(IsWarningFilterSelected));
        OnPropertyChanged(nameof(IsErrorFilterSelected));
        RebuildFilteredEntries();
    }

    public string BuildCopyAllText() =>
        string.Join(Environment.NewLine, Entries.Select(static entry => entry.CopyText));

    public void NotifyEntriesChanged()
    {
        OnPropertyChanged(nameof(EntriesVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(DisplayedEmptyStateText));
        OnPropertyChanged(nameof(DisplayedEmptyStateTitleText));
        OnPropertyChanged(nameof(CanCopyAll));
        OnPropertyChanged(nameof(CanClear));
    }

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();
        foreach (var entry in Entries.Where(entry => MatchesFilter(entry, SelectedFilter)))
        {
            FilteredEntries.Add(entry);
        }

        NotifyEntriesChanged();
    }

    private static bool MatchesFilter(LogEntryViewModel entry, LogPanelFilter filter) =>
        filter switch
        {
            LogPanelFilter.All => true,
            LogPanelFilter.Information => entry.Level == WorkspaceDiagnosticLogLevel.Information,
            LogPanelFilter.Warning => entry.Level == WorkspaceDiagnosticLogLevel.Warning,
            LogPanelFilter.Error => entry.Level == WorkspaceDiagnosticLogLevel.Error,
            _ => true
        };
}
