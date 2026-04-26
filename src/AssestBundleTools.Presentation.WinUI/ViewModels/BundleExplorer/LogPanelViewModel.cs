using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示详细日志面板的展示状态。
/// </summary>
public sealed class LogPanelViewModel : ObservableObject
{
    private string _emptyStateText = string.Empty;
    private string _summaryText = string.Empty;

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public ObservableCollection<LogEntryViewModel> Entries { get; } = new();

    public Visibility EntriesVisibility => Entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public void Clear()
    {
        Entries.Clear();
        SummaryText = string.Empty;
        NotifyEntriesChanged();
    }

    public void Append(string message)
    {
        Entries.Insert(0, new LogEntryViewModel(
            TimestampText: DateTime.Now.ToString("HH:mm:ss"),
            Message: message));

        SummaryText = message;
        NotifyEntriesChanged();
    }

    public void NotifyEntriesChanged()
    {
        OnPropertyChanged(nameof(EntriesVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }
}
