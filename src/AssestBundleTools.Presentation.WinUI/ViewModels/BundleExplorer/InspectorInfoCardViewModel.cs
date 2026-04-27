using System.Collections.Generic;
using System.Linq;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示检查器里的单个可折叠信息卡。
/// </summary>
public sealed class InspectorInfoCardViewModel : ObservableObject
{
    private bool _isExpanded;

    public InspectorInfoCardViewModel(
        string key,
        string title,
        string copyToolTipText,
        string summaryText,
        bool isExpanded,
        IReadOnlyList<InspectorInfoRowViewModel> rows)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Card key is required.", nameof(key))
            : key.Trim();
        Title = string.IsNullOrWhiteSpace(title)
            ? throw new ArgumentException("Card title is required.", nameof(title))
            : title.Trim();
        CopyToolTipText = string.IsNullOrWhiteSpace(copyToolTipText)
            ? string.Empty
            : copyToolTipText.Trim();
        SummaryText = string.IsNullOrWhiteSpace(summaryText)
            ? string.Empty
            : summaryText.Trim();
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        _isExpanded = isExpanded;
    }

    public string Key { get; }

    public string Title { get; }

    public string CopyToolTipText { get; }

    public string SummaryText { get; }

    public IReadOnlyList<InspectorInfoRowViewModel> Rows { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
                OnPropertyChanged(nameof(ContentVisibility));
                OnPropertyChanged(nameof(SummaryVisibility));
            }
        }
    }

    public string ExpandGlyph => IsExpanded ? "\uE70D" : "\uE76C";

    public Visibility ContentVisibility =>
        IsExpanded && Rows.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SummaryVisibility =>
        !IsExpanded && !string.IsNullOrWhiteSpace(SummaryText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CopyPayloadText => string.Join(Environment.NewLine, Rows.Select(static row => row.CopyText));

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
