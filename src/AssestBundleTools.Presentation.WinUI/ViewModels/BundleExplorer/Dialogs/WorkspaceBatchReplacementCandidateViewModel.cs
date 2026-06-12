using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementCandidateViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isVisible = true;
    private readonly string? _displayName;

    public WorkspaceBatchReplacementCandidateViewModel(
        WorkspaceBatchReplacementCandidateDto candidate,
        string statusText,
        string subtitleText,
        bool canSelect,
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto>? replacementCandidates = null,
        string? typeFilterKey = null,
        string? typeFilterText = null,
        string? displayName = null,
        string? relativePathText = null)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        ReplacementCandidates = replacementCandidates is { Count: > 0 }
            ? replacementCandidates
            : [candidate];
        StatusText = statusText;
        SubtitleText = subtitleText;
        CanSelect = canSelect;
        TypeFilterKey = string.IsNullOrWhiteSpace(typeFilterKey) ? ResolveDefaultTypeFilterText(candidate) : typeFilterKey;
        TypeFilterText = string.IsNullOrWhiteSpace(typeFilterText) ? ResolveDefaultTypeFilterText(candidate) : typeFilterText;
        _displayName = displayName;
        RelativePathText = string.IsNullOrWhiteSpace(relativePathText) ? candidate.RelativePath : relativePathText;
        _isSelected = ReplacementCandidates.Any(static item => item.IsDefaultSelected) && canSelect;
    }

    public WorkspaceBatchReplacementCandidateDto Candidate { get; }

    public IReadOnlyList<WorkspaceBatchReplacementCandidateDto> ReplacementCandidates { get; }

    public BundleId? BundleId => Candidate.BundleId;

    public string BundleDisplayNameForOrdering => Candidate.BundleDisplayName;

    public string AssetName => !string.IsNullOrWhiteSpace(_displayName)
        ? _displayName!
        : string.IsNullOrWhiteSpace(Candidate.AssetName)
            ? Candidate.RelativePath
            : Candidate.AssetName;

    public string StatusText { get; }

    public string SubtitleText { get; }

    public string TypeFilterKey { get; }

    public string TypeFilterText { get; }

    public string RelativePath => RelativePathText;

    public string RelativePathText { get; }

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

    private static string ResolveDefaultTypeFilterText(WorkspaceBatchReplacementCandidateDto candidate) =>
        string.IsNullOrWhiteSpace(candidate.AssetTypeDisplayName)
            ? candidate.AssetType.ToString()
            : candidate.AssetTypeDisplayName;
}
