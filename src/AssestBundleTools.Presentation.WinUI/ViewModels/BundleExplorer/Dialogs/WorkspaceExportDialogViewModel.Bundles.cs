using System;
using System.Collections.Generic;
using System.Linq;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceExportDialogViewModel
{
    private string _bundleSearchText = string.Empty;
    private WorkspaceExportBundleSortOptionViewModel? _selectedBundleSortOption;

    public string BundleSearchText
    {
        get => _bundleSearchText;
        set
        {
            if (SetProperty(ref _bundleSearchText, value ?? string.Empty))
            {
                RefreshVisibleBundles();
            }
        }
    }

    public string SelectedBundleCountText =>
        _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogSelectedBundleCountFormat,
            Bundles.Count(static bundle => bundle.IsSelected));

    public Visibility BundleEmptyStateVisibility =>
        VisibleBundles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public WorkspaceExportBundleSortOptionViewModel? SelectedBundleSortOption
    {
        get => _selectedBundleSortOption;
        set
        {
            if (SetProperty(ref _selectedBundleSortOption, value))
            {
                RefreshVisibleBundles();
            }
        }
    }

    public bool HasExportTargets => Bundles.Any(static bundle => bundle.IsSelected);

    public bool CanStartExport => HasExportTargets && !IsExecuting;

    public void SelectAllBundles()
    {
        foreach (var bundle in VisibleBundles.Select(static item => item.Bundle).OfType<WorkspaceExportBundleOptionViewModel>())
        {
            bundle.IsSelected = true;
        }
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in VisibleBundles.Select(static item => item.Bundle).OfType<WorkspaceExportBundleOptionViewModel>())
        {
            bundle.IsSelected = false;
        }
    }

    private void OnBundleSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasExportTargets));
        OnPropertyChanged(nameof(CanStartExport));
        OnPropertyChanged(nameof(SelectedBundleCountText));
        RefreshPlanSummary();
    }

    private void RefreshPlanSummary()
    {
        var selectedBundles = Bundles
            .Where(static bundle => bundle.IsSelected)
            .ToArray();
        PlanSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogPlanSummaryFormat,
            selectedBundles.Length,
            selectedBundles.Sum(static bundle => bundle.ChangeCount),
            selectedBundles.Sum(static bundle => bundle.ModifiedAssetCount),
            selectedBundles.Sum(static bundle => bundle.WorkspaceReplacementCount + bundle.FileReplacementCount),
            selectedBundles.Sum(static bundle => bundle.ExtractedAssetCount));
    }

    private void RefreshVisibleBundles()
    {
        var query = BundleSearchText.Trim();
        var candidates = Bundles.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            candidates = candidates.Where(bundle =>
                bundle.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                bundle.OutputPath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        candidates = SelectedBundleSortOption?.Id == "name"
            ? candidates
                .OrderBy(static bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static bundle => bundle.LastChangedAtUtc ?? DateTimeOffset.MinValue)
            : candidates
                .OrderByDescending(static bundle => bundle.LastChangedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(static bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase);

        var orderedBundles = candidates.ToArray();
        VisibleBundles.Clear();
        if (SelectedBundleSortOption?.Id == "time" && string.IsNullOrWhiteSpace(query))
        {
            AppendTimeGroupedVisibleBundles(orderedBundles);
        }
        else
        {
            foreach (var bundle in orderedBundles)
            {
                VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
            }
        }

        OnPropertyChanged(nameof(BundleEmptyStateVisibility));
    }

    private void AppendTimeGroupedVisibleBundles(IReadOnlyList<WorkspaceExportBundleOptionViewModel> orderedBundles)
    {
        if (orderedBundles.Count == 0)
        {
            return;
        }

        var latestChangedAt = orderedBundles
            .Select(static bundle => bundle.LastChangedAtUtc)
            .Where(static value => value is not null)
            .Max();
        var recentCutoff = latestChangedAt?.AddHours(-24);
        var recentBundles = recentCutoff is null
            ? Array.Empty<WorkspaceExportBundleOptionViewModel>()
            : orderedBundles
                .Where(bundle => bundle.LastChangedAtUtc >= recentCutoff)
                .ToArray();
        var previousBundles = recentCutoff is null
            ? orderedBundles
            : orderedBundles
                .Where(bundle => bundle.LastChangedAtUtc < recentCutoff || bundle.LastChangedAtUtc is null)
                .ToArray();

        if (recentBundles.Length > 0)
        {
            VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(
                bundle: null,
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleGroupRecent)));
            foreach (var bundle in recentBundles)
            {
                VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
            }
        }

        if (previousBundles.Count > 0)
        {
            VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(
                bundle: null,
                _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogBundleGroupPrevious)));
            foreach (var bundle in previousBundles)
            {
                VisibleBundles.Add(new WorkspaceExportBundleListItemViewModel(bundle));
            }
        }
    }
}
