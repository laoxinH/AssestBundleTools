using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchExtractionCategoryOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public WorkspaceBatchExtractionCategoryOptionViewModel(
        WorkspaceBatchExtractionCategoryDto category,
        string displayName,
        string candidateCountText,
        int candidateCount,
        bool isSelected)
    {
        Category = category;
        DisplayName = displayName;
        CandidateCountText = candidateCountText;
        CandidateCount = candidateCount;
        _isSelected = isSelected;
    }

    public event EventHandler? SelectionChanged;

    public WorkspaceBatchExtractionCategoryDto Category { get; }

    public string DisplayName { get; }

    public string CandidateCountText { get; }

    public int CandidateCount { get; }

    public bool IsAllCategory => Category.Kind == WorkspaceBatchExtractionCategoryKind.All;

    public Visibility AllCategoryDividerVisibility =>
        IsAllCategory ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
