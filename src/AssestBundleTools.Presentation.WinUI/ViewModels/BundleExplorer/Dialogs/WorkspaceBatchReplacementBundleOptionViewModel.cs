using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementBundleOptionViewModel : ObservableObject
{
    private bool _isSelected = true;

    public WorkspaceBatchReplacementBundleOptionViewModel(
        BundleId bundleId,
        string displayName,
        string logicalPath,
        int candidateCount,
        int issueCount)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        LogicalPath = logicalPath;
        CandidateCount = candidateCount;
        IssueCount = issueCount;
    }

    public event EventHandler? SelectionChanged;

    public BundleId BundleId { get; }

    public string DisplayName { get; }

    public string LogicalPath { get; }

    public int CandidateCount { get; }

    public int IssueCount { get; }

    public string CountText => IssueCount > 0
        ? $"{CandidateCount} / {IssueCount}"
        : CandidateCount.ToString();

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
