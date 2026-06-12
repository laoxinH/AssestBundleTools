using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceBatchReplacementDialogViewModel : ObservableObject
{
    private const string SpineTypeFilterKey = "__spine";
    private readonly ILocalizationService _localizationService;
    private readonly Dictionary<string, BundleNodeViewModel> _bundleNodesById;
    private string _sourceDirectory = string.Empty;
    private string _planSummaryText;
    private string _sourceSummaryText;
    private string _sourceModeText;
    private string _validationText = string.Empty;
    private bool _isPlanLoading;
    private bool _isExecuting;
    private bool _showChangedBundlesOnly;
    private string _candidateSearchText = string.Empty;
    private WorkspaceBatchReplacementFilterOptionViewModel? _selectedStatusFilter;
    private string _typeFilterButtonText = string.Empty;

    public WorkspaceBatchReplacementDialogViewModel(
        ILocalizationService localizationService,
        IEnumerable<BundleNodeViewModel> bundleNodes)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _bundleNodesById = bundleNodes.ToDictionary(static node => node.BundleId.Value, StringComparer.Ordinal);
        _planSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewWaiting);
        _sourceSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceNotSelected);
        _sourceModeText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceModeAuto);
        _typeFilterButtonText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterAll);
    }

    public ObservableCollection<WorkspaceBatchReplacementBundleOptionViewModel> Bundles { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementBundleOptionViewModel> FilteredBundles { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementCandidateViewModel> Candidates { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementCandidateViewModel> FilteredCandidates { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementFilterOptionViewModel> StatusFilters { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementFilterOptionViewModel> TypeFilters { get; } = [];

    public string SourceDirectory
    {
        get => _sourceDirectory;
        private set
        {
            if (SetProperty(ref _sourceDirectory, value))
            {
                OnPropertyChanged(nameof(HasSourceDirectory));
            }
        }
    }

    public bool HasSourceDirectory => !string.IsNullOrWhiteSpace(SourceDirectory);

    public string SourceSummaryText
    {
        get => _sourceSummaryText;
        private set => SetProperty(ref _sourceSummaryText, value);
    }

    public string SourceModeText
    {
        get => _sourceModeText;
        private set => SetProperty(ref _sourceModeText, value);
    }

    public string PlanSummaryText
    {
        get => _planSummaryText;
        private set => SetProperty(ref _planSummaryText, value);
    }

    public string ValidationText
    {
        get => _validationText;
        private set
        {
            if (SetProperty(ref _validationText, value))
            {
                OnPropertyChanged(nameof(ValidationVisibility));
            }
        }
    }

    public bool IsPlanLoading
    {
        get => _isPlanLoading;
        private set
        {
            if (SetProperty(ref _isPlanLoading, value))
            {
                OnPropertyChanged(nameof(PlanLoadingVisibility));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (SetProperty(ref _isExecuting, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public Visibility PlanLoadingVisibility => IsPlanLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ValidationVisibility =>
        string.IsNullOrWhiteSpace(ValidationText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SourceEmptyVisibility => HasSourceDirectory ? Visibility.Collapsed : Visibility.Visible;

    public bool HasCandidates => Candidates.Count > 0;

    public bool CanSubmit => !IsPlanLoading && !IsExecuting && Candidates.Any(static candidate => candidate.IsSelected);

    public bool ShowChangedBundlesOnly
    {
        get => _showChangedBundlesOnly;
        set
        {
            if (SetProperty(ref _showChangedBundlesOnly, value))
            {
                ApplyBundleFilters();
            }
        }
    }

    public string CandidateSearchText
    {
        get => _candidateSearchText;
        set
        {
            if (SetProperty(ref _candidateSearchText, value ?? string.Empty))
            {
                ApplyCandidateFilters();
            }
        }
    }

    public WorkspaceBatchReplacementFilterOptionViewModel? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyCandidateFilters();
            }
        }
    }

    public string TypeFilterButtonText
    {
        get => _typeFilterButtonText;
        private set => SetProperty(ref _typeFilterButtonText, value);
    }

    public void SetSourceDirectory(string sourceDirectory)
    {
        SourceDirectory = sourceDirectory?.Trim() ?? string.Empty;
        SourceSummaryText = HasSourceDirectory
            ? GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewLoading)
            : GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceNotSelected);
        OnPropertyChanged(nameof(SourceEmptyVisibility));
    }

    public void SetPlanLoading()
    {
        IsPlanLoading = true;
        IsExecuting = false;
        ValidationText = string.Empty;
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewLoading);
    }

    public void ApplyPlan(WorkspaceBatchReplacementPlanDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        IsPlanLoading = false;
        IsExecuting = false;
        UnsubscribeCandidateSelectionChanged();
        UnsubscribeBundleSelectionChanged();
        UnsubscribeTypeFilterSelectionChanged();
        Candidates.Clear();
        FilteredCandidates.Clear();
        Bundles.Clear();
        FilteredBundles.Clear();
        StatusFilters.Clear();
        TypeFilters.Clear();

        foreach (var candidate in CreateDisplayCandidates(plan.Candidates))
        {
            Candidates.Add(candidate);
        }
        SubscribeCandidateSelectionChanged();

        var groupedBundles = plan.Candidates
            .Where(static candidate => candidate.BundleId is not null)
            .GroupBy(static candidate => candidate.BundleId!.Value.Value, StringComparer.Ordinal)
            .OrderBy(static group => group.First().BundleDisplayName, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groupedBundles)
        {
            var first = group.First();
            var bundleId = first.BundleId!.Value;
            var logicalPath = _bundleNodesById.TryGetValue(bundleId.Value, out var node)
                ? node.LogicalPath
                : first.BundleDisplayName;
            Bundles.Add(new WorkspaceBatchReplacementBundleOptionViewModel(
                bundleId,
                first.BundleDisplayName,
                logicalPath,
                group.Count(static candidate => IsExecutableMatchStatus(candidate.MatchStatus)),
                group.Count(static candidate => IsIssueMatchStatus(candidate.MatchStatus))));
        }
        SubscribeBundleSelectionChanged();
        RebuildCandidateFilters();
        ApplyBundleFilters();

        SourceModeText = ResolveSourceModeText(plan.MatchingMode);
        SourceSummaryText = ResolveSourceSummaryText(plan);
        PlanSummaryText = ResolvePlanSummaryText(plan);
        ValidationText = plan.MatchingMode == WorkspaceBatchReplacementMatchingMode.Heuristic
            ? GetString(ResourceKeys.WorkspaceBatchReplacementDialogHeuristicWarning)
            : string.Empty;
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public void ApplyPlanUnavailable()
    {
        IsPlanLoading = false;
        IsExecuting = false;
        UnsubscribeCandidateSelectionChanged();
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewUnavailable);
        ValidationText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewUnavailable);
        UnsubscribeBundleSelectionChanged();
        UnsubscribeTypeFilterSelectionChanged();
        Candidates.Clear();
        FilteredCandidates.Clear();
        Bundles.Clear();
        FilteredBundles.Clear();
        StatusFilters.Clear();
        TypeFilters.Clear();
        SelectedStatusFilter = null;
        TypeFilterButtonText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterAll);
        SourceModeText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceModeAuto);
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public void SelectAllBundles()
    {
        foreach (var bundle in Bundles)
        {
            bundle.IsSelected = true;
        }
        ApplyCandidateFilters();
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in Bundles)
        {
            bundle.IsSelected = false;
        }
        ApplyCandidateFilters();
    }

    public void ToggleChangedBundlesOnly()
    {
        ShowChangedBundlesOnly = !ShowChangedBundlesOnly;
    }

    private void RebuildCandidateFilters()
    {
        var previousStatusKey = SelectedStatusFilter?.Key;
        var previousTypeKeys = TypeFilters
            .Where(static filter => !filter.IsAll && filter.IsSelected)
            .Select(static filter => filter.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        StatusFilters.Clear();
        UnsubscribeTypeFilterSelectionChanged();
        TypeFilters.Clear();

        StatusFilters.Add(WorkspaceBatchReplacementFilterOptionViewModel.All(
            GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusFilterAll)));
        foreach (var status in Candidates
            .Select(static candidate => candidate.Candidate.MatchStatus)
            .Distinct()
            .OrderBy(static status => status.ToString(), StringComparer.Ordinal))
        {
            StatusFilters.Add(new WorkspaceBatchReplacementFilterOptionViewModel(
                status.ToString(),
                ResolveStatusText(status)));
        }

        foreach (var type in Candidates
            .Select(static candidate => new { candidate.TypeFilterKey, candidate.TypeFilterText })
            .Where(static value => !string.IsNullOrWhiteSpace(value.TypeFilterKey))
            .GroupBy(static value => value.TypeFilterKey, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static value => value.TypeFilterText, StringComparer.CurrentCultureIgnoreCase))
        {
            TypeFilters.Add(new WorkspaceBatchReplacementFilterOptionViewModel(type.TypeFilterKey, type.TypeFilterText));
        }

        SubscribeTypeFilterSelectionChanged();
        RestoreTypeFilters(previousTypeKeys);
        SelectedStatusFilter = SelectDefaultStatusFilter(previousStatusKey);
        UpdateTypeFilterButtonText();
        ApplyCandidateFilters();
    }

    private void ApplyBundleFilters()
    {
        FilteredBundles.Clear();
        foreach (var bundle in Bundles)
        {
            if (ShowChangedBundlesOnly && bundle.CandidateCount <= 0)
            {
                continue;
            }

            FilteredBundles.Add(bundle);
        }
    }

    private void ApplyCandidateFilters()
    {
        var selectedBundleIds = Bundles
            .Where(static bundle => bundle.IsSelected)
            .Select(static bundle => bundle.BundleId.Value)
            .ToHashSet(StringComparer.Ordinal);
        var searchText = CandidateSearchText.Trim();
        var statusFilter = SelectedStatusFilter?.IsAll == false ? SelectedStatusFilter.Key : null;
        var typeFilters = TypeFilters
            .Where(static filter => !filter.IsAll && filter.IsSelected)
            .Select(static filter => filter.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        FilteredCandidates.Clear();
        foreach (var candidate in Candidates)
        {
            if (candidate.BundleId is { } bundleId &&
                selectedBundleIds.Count > 0 &&
                !selectedBundleIds.Contains(bundleId.Value))
            {
                continue;
            }

            if (candidate.BundleId is not null &&
                selectedBundleIds.Count == 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                !string.Equals(candidate.Candidate.MatchStatus.ToString(), statusFilter, StringComparison.Ordinal))
            {
                continue;
            }

            if (typeFilters.Count > 0 &&
                !typeFilters.Contains(candidate.TypeFilterKey))
            {
                continue;
            }

            if (!MatchesSearch(candidate, searchText))
            {
                continue;
            }

            FilteredCandidates.Add(candidate);
        }
    }

    private WorkspaceBatchReplacementFilterOptionViewModel? SelectDefaultStatusFilter(string? previousKey)
    {
        if (!string.IsNullOrWhiteSpace(previousKey))
        {
            var restored = StatusFilters.FirstOrDefault(filter => string.Equals(filter.Key, previousKey, StringComparison.Ordinal));
            if (restored is not null)
            {
                return restored;
            }
        }

        return StatusFilters.FirstOrDefault(filter =>
            string.Equals(filter.Key, WorkspaceBatchReplacementMatchStatus.Changed.ToString(), StringComparison.Ordinal)) ??
            StatusFilters.FirstOrDefault(filter =>
                string.Equals(filter.Key, WorkspaceBatchReplacementMatchStatus.HighConfidence.ToString(), StringComparison.Ordinal)) ??
            StatusFilters.FirstOrDefault(filter =>
                string.Equals(filter.Key, WorkspaceBatchReplacementMatchStatus.MediumConfidence.ToString(), StringComparison.Ordinal)) ??
            StatusFilters.FirstOrDefault();
    }

    private void RestoreTypeFilters(IReadOnlySet<string> previousTypeKeys)
    {
        if (previousTypeKeys.Count == 0)
        {
            return;
        }

        foreach (var filter in TypeFilters.Where(filter => previousTypeKeys.Contains(filter.Key)))
        {
            filter.IsSelected = true;
        }
    }

    private void UpdateTypeFilterButtonText()
    {
        var selectedFilters = TypeFilters
            .Where(static filter => filter.IsSelected)
            .ToArray();
        TypeFilterButtonText = selectedFilters.Length switch
        {
            0 => GetString(ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterAll),
            1 => selectedFilters[0].DisplayText,
            _ => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterSelectedFormat,
                selectedFilters.Length.ToString(CultureInfo.CurrentCulture))
        };
    }

    private static bool MatchesSearch(WorkspaceBatchReplacementCandidateViewModel candidate, string searchText) =>
        string.IsNullOrWhiteSpace(searchText) ||
        Contains(candidate.AssetName, searchText) ||
        Contains(candidate.SubtitleText, searchText) ||
        Contains(candidate.RelativePath, searchText) ||
        Contains(candidate.Candidate.ContainerPath, searchText) ||
        Contains(candidate.Candidate.FullPath, searchText) ||
        candidate.ReplacementCandidates.Any(item => MatchesSearch(item, searchText));

    private static bool MatchesSearch(WorkspaceBatchReplacementCandidateDto candidate, string searchText) =>
        Contains(candidate.AssetName, searchText) ||
        Contains(candidate.RelativePath, searchText) ||
        Contains(candidate.ContainerPath, searchText) ||
        Contains(candidate.FullPath, searchText) ||
        Contains(candidate.SpineGroupDisplayName, searchText) ||
        Contains(candidate.OutputRole, searchText);

    private static bool Contains(string? value, string searchText) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);

    private void SubscribeBundleSelectionChanged()
    {
        foreach (var bundle in Bundles)
        {
            bundle.SelectionChanged += OnBundleSelectionChanged;
        }
    }

    private void UnsubscribeBundleSelectionChanged()
    {
        foreach (var bundle in Bundles)
        {
            bundle.SelectionChanged -= OnBundleSelectionChanged;
        }
    }

    private void OnBundleSelectionChanged(object? sender, EventArgs e) => ApplyCandidateFilters();

    private void SubscribeTypeFilterSelectionChanged()
    {
        foreach (var filter in TypeFilters)
        {
            filter.SelectionChanged += OnTypeFilterSelectionChanged;
        }
    }

    private void UnsubscribeTypeFilterSelectionChanged()
    {
        foreach (var filter in TypeFilters)
        {
            filter.SelectionChanged -= OnTypeFilterSelectionChanged;
        }
    }

    private void OnTypeFilterSelectionChanged(object? sender, EventArgs e)
    {
        UpdateTypeFilterButtonText();
        ApplyCandidateFilters();
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private IReadOnlyList<WorkspaceBatchReplacementCandidateViewModel> CreateDisplayCandidates(
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto> candidates)
    {
        var displayCandidates = new List<WorkspaceBatchReplacementCandidateViewModel>();
        foreach (var ordinaryCandidate in candidates
                     .Where(static candidate => string.IsNullOrWhiteSpace(candidate.SpineGroupId)))
        {
            displayCandidates.Add(CreateCandidateViewModel(ordinaryCandidate));
        }

        var spineTypeText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogTypeFilterSpine);
        foreach (var group in candidates
                     .Where(static candidate => !string.IsNullOrWhiteSpace(candidate.SpineGroupId))
                     .GroupBy(static candidate => string.Concat(
                         candidate.BundleId?.Value,
                         "\n",
                         candidate.SpineGroupId,
                         "\n",
                         candidate.MatchStatus),
                         StringComparer.Ordinal)
                     .OrderBy(static group => group.First().BundleDisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static group => group.First().SpineGroupDisplayName ?? group.First().SpineGroupId, StringComparer.OrdinalIgnoreCase))
        {
            var groupCandidates = group
                .OrderBy(static candidate => candidate.OutputRole, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static candidate => candidate.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var representative = groupCandidates[0];
            var displayName = string.IsNullOrWhiteSpace(representative.SpineGroupDisplayName)
                ? representative.AssetName
                : representative.SpineGroupDisplayName!;
            displayCandidates.Add(CreateCandidateViewModel(
                representative,
                groupCandidates,
                SpineTypeFilterKey,
                spineTypeText,
                displayName,
                ResolveSpineGroupRelativePath(groupCandidates)));
        }

        return displayCandidates
            .OrderBy(static candidate => candidate.BundleDisplayNameForOrdering, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.AssetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private WorkspaceBatchReplacementCandidateViewModel CreateCandidateViewModel(
        WorkspaceBatchReplacementCandidateDto candidate,
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto>? replacementCandidates = null,
        string? typeFilterKey = null,
        string? typeFilterText = null,
        string? displayName = null,
        string? relativePathText = null) =>
        new(
            candidate,
            ResolveStatusText(candidate.MatchStatus),
            BuildSubtitle(candidate, typeFilterText),
            CanSelectCandidate(candidate.MatchStatus),
            replacementCandidates,
            typeFilterKey,
            typeFilterText,
            displayName,
            relativePathText);

    private static string ResolveSpineGroupRelativePath(IReadOnlyList<WorkspaceBatchReplacementCandidateDto> candidates)
    {
        var first = candidates[0];
        if (candidates.Count == 1)
        {
            return first.RelativePath;
        }

        var groupPath = Path.GetDirectoryName(first.RelativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(groupPath)
            ? first.RelativePath
            : groupPath.Replace(Path.DirectorySeparatorChar, '/');
    }
}
