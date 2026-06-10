using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementCandidateViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isVisible = true;

    public WorkspaceBatchReplacementCandidateViewModel(
        WorkspaceBatchReplacementCandidateDto candidate,
        string statusText,
        string subtitleText,
        bool canSelect)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        StatusText = statusText;
        SubtitleText = subtitleText;
        CanSelect = canSelect;
        _isSelected = candidate.IsDefaultSelected && canSelect;
    }

    public WorkspaceBatchReplacementCandidateDto Candidate { get; }

    public BundleId? BundleId => Candidate.BundleId;

    public string AssetName => string.IsNullOrWhiteSpace(Candidate.AssetName)
        ? Candidate.RelativePath
        : Candidate.AssetName;

    public string StatusText { get; }

    public string SubtitleText { get; }

    public string RelativePath => Candidate.RelativePath;

    public bool CanSelect { get; }

    public bool IsIssue =>
        Candidate.MatchStatus is not WorkspaceBatchReplacementMatchStatus.Changed and
            not WorkspaceBatchReplacementMatchStatus.Unchanged;

    public Visibility IssueVisibility => IsIssue ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, CanSelect && value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
