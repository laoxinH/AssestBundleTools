using System.Collections.Generic;
using System.Linq;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器里的单个可折叠信息卡。
/// </summary>
public sealed class InspectorInfoCardViewModel : ObservableObject
{
    private bool _isExpanded;
    private readonly string? _copyPayloadText;

    public InspectorInfoCardViewModel(
        string key,
        string title,
        string copyToolTipText,
        string summaryText,
        bool isExpanded,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        InspectorInfoCardVisualViewModel? visual = null,
        string? copyPayloadText = null)
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
        Visual = visual;
        _copyPayloadText = string.IsNullOrWhiteSpace(copyPayloadText)
            ? null
            : copyPayloadText;
        _isExpanded = isExpanded;
    }

    public string Key { get; }

    public string Title { get; }

    public string CopyToolTipText { get; }

    public string SummaryText { get; }

    public IReadOnlyList<InspectorInfoRowViewModel> Rows { get; }

    public InspectorInfoCardVisualViewModel? Visual { get; }

    public MaterialPreviewAppearanceViewModel? VisualMaterialAppearance => Visual?.MaterialAppearance;

    public ImageSource? VisualImageSource => Visual?.ImageSource;

    public Brush? VisualImageTintBrush => Visual?.ImageTintBrush;

    public Visibility MaterialAppearanceVisualVisibility =>
        Visual?.MaterialAppearanceVisibility ?? Visibility.Collapsed;

    public Visibility ImagePreviewVisualVisibility =>
        Visual?.ImagePreviewVisibility ?? Visibility.Collapsed;

    public Visibility ImagePreviewTintVisibility =>
        Visual?.ImageTintVisibility ?? Visibility.Collapsed;

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

    public string ExpandGlyph => IsExpanded ? "\uE488" : "\uE48E";

    public Visibility ContentVisibility =>
        IsExpanded && (Rows.Count > 0 || Visual is { HasVisual: true })
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SummaryVisibility =>
        !IsExpanded && !string.IsNullOrWhiteSpace(SummaryText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CopyPayloadText => _copyPayloadText ?? string.Join(Environment.NewLine, Rows.Select(static row => row.CopyText));

    public Visibility CopyButtonVisibility =>
        string.IsNullOrWhiteSpace(CopyPayloadText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
