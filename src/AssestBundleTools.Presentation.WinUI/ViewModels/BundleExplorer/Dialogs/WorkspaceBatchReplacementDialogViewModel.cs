using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchReplacementDialogViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly Dictionary<string, BundleNodeViewModel> _bundleNodesById;
    private string _sourceDirectory = string.Empty;
    private string _planSummaryText;
    private string _sourceSummaryText;
    private string _validationText = string.Empty;
    private bool _isPlanLoading;

    public WorkspaceBatchReplacementDialogViewModel(
        ILocalizationService localizationService,
        IEnumerable<BundleNodeViewModel> bundleNodes)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _bundleNodesById = bundleNodes.ToDictionary(static node => node.BundleId.Value, StringComparer.Ordinal);
        _planSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewWaiting);
        _sourceSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceNotSelected);
    }

    public ObservableCollection<WorkspaceBatchReplacementBundleOptionViewModel> Bundles { get; } = [];

    public ObservableCollection<WorkspaceBatchReplacementCandidateViewModel> Candidates { get; } = [];

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

    public Visibility PlanLoadingVisibility => IsPlanLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ValidationVisibility =>
        string.IsNullOrWhiteSpace(ValidationText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SourceEmptyVisibility => HasSourceDirectory ? Visibility.Collapsed : Visibility.Visible;

    public bool CanSubmit => false;

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
        ValidationText = string.Empty;
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewLoading);
    }

    public void ApplyPlan(WorkspaceBatchReplacementPlanDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        IsPlanLoading = false;
        Candidates.Clear();
        Bundles.Clear();

        foreach (var candidate in plan.Candidates)
        {
            Candidates.Add(new WorkspaceBatchReplacementCandidateViewModel(
                candidate,
                ResolveStatusText(candidate.MatchStatus),
                BuildSubtitle(candidate),
                candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.Changed));
        }

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
                group.Count(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.Changed),
                group.Count(static candidate => candidate.MatchStatus is not WorkspaceBatchReplacementMatchStatus.Changed and not WorkspaceBatchReplacementMatchStatus.Unchanged)));
        }

        SourceSummaryText = plan.MatchingMode == WorkspaceBatchReplacementMatchingMode.Manifest
            ? _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchReplacementDialogSourceManifestSummaryFormat,
                plan.TotalManifestItemCount.ToString(CultureInfo.CurrentCulture),
                plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
                plan.IssueCount.ToString(CultureInfo.CurrentCulture))
            : GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceHeuristicSummary);
        PlanSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchReplacementDialogPreviewSummaryFormat,
            plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
            plan.UnchangedCount.ToString(CultureInfo.CurrentCulture),
            plan.IssueCount.ToString(CultureInfo.CurrentCulture));
        ValidationText = plan.Candidates.Any(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.HeuristicNotImplemented)
            ? GetString(ResourceKeys.WorkspaceBatchReplacementDialogHeuristicPending)
            : string.Empty;
        OnPropertyChanged(nameof(CanSubmit));
    }

    public void ApplyPlanUnavailable()
    {
        IsPlanLoading = false;
        PlanSummaryText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewUnavailable);
        ValidationText = GetString(ResourceKeys.WorkspaceBatchReplacementDialogPreviewUnavailable);
    }

    public void SelectAllBundles()
    {
        foreach (var bundle in Bundles)
        {
            bundle.IsSelected = true;
        }
    }

    public void ClearBundleSelection()
    {
        foreach (var bundle in Bundles)
        {
            bundle.IsSelected = false;
        }
    }

    private string BuildSubtitle(WorkspaceBatchReplacementCandidateDto candidate)
    {
        var typeName = string.IsNullOrWhiteSpace(candidate.AssetTypeDisplayName)
            ? candidate.AssetType.ToString()
            : candidate.AssetTypeDisplayName;
        return $"{candidate.BundleDisplayName} · {typeName}";
    }

    private string ResolveStatusText(WorkspaceBatchReplacementMatchStatus status) =>
        status switch
        {
            WorkspaceBatchReplacementMatchStatus.Changed => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusChanged),
            WorkspaceBatchReplacementMatchStatus.Unchanged => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusUnchanged),
            WorkspaceBatchReplacementMatchStatus.Missing => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusMissing),
            WorkspaceBatchReplacementMatchStatus.TargetMissing => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusTargetMissing),
            WorkspaceBatchReplacementMatchStatus.TypeMismatch => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusTypeMismatch),
            WorkspaceBatchReplacementMatchStatus.HeuristicNotImplemented => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusHeuristicPending),
            _ => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusIssue)
        };

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
