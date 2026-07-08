using System.Globalization;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceBatchReplacementDialogViewModel
{
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

        OnPropertyChanged(nameof(BundleListVisibility));
        OnPropertyChanged(nameof(BundleEmptyVisibility));
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

        OnPropertyChanged(nameof(CandidateListVisibility));
        OnPropertyChanged(nameof(CandidateEmptyVisibility));
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
}
