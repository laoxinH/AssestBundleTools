using System.Globalization;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceBatchReplacementDialogViewModel
{
    private string BuildSubtitle(WorkspaceBatchReplacementCandidateDto candidate, string? typeNameOverride = null)
    {
        var typeName = !string.IsNullOrWhiteSpace(typeNameOverride)
            ? typeNameOverride
            : string.IsNullOrWhiteSpace(candidate.AssetTypeDisplayName)
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
            WorkspaceBatchReplacementMatchStatus.HighConfidence => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusHighConfidence),
            WorkspaceBatchReplacementMatchStatus.MediumConfidence => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusMediumConfidence),
            WorkspaceBatchReplacementMatchStatus.LowConfidence => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusLowConfidence),
            WorkspaceBatchReplacementMatchStatus.Ambiguous => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusAmbiguous),
            WorkspaceBatchReplacementMatchStatus.Rejected => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusRejected),
            _ => GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusIssue)
        };

    private string ResolveSourceModeText(WorkspaceBatchReplacementMatchingMode matchingMode) =>
        matchingMode switch
        {
            WorkspaceBatchReplacementMatchingMode.Manifest => GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceModeManifest),
            WorkspaceBatchReplacementMatchingMode.Heuristic => GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceModeHeuristic),
            _ => GetString(ResourceKeys.WorkspaceBatchReplacementDialogSourceModeAuto)
        };

    private string ResolveSourceSummaryText(WorkspaceBatchReplacementPlanDto plan)
    {
        if (plan.MatchingMode == WorkspaceBatchReplacementMatchingMode.Manifest)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchReplacementDialogSourceManifestSummaryFormat,
                plan.TotalManifestItemCount.ToString(CultureInfo.CurrentCulture),
                plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
                plan.IssueCount.ToString(CultureInfo.CurrentCulture));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchReplacementDialogSourceHeuristicCountsFormat,
            plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
            CountConfirmationCandidates(plan).ToString(CultureInfo.CurrentCulture),
            plan.IssueCount.ToString(CultureInfo.CurrentCulture));
    }

    private string ResolvePlanSummaryText(WorkspaceBatchReplacementPlanDto plan)
    {
        if (plan.MatchingMode == WorkspaceBatchReplacementMatchingMode.Heuristic)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchReplacementDialogHeuristicPreviewSummaryFormat,
                plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
                CountConfirmationCandidates(plan).ToString(CultureInfo.CurrentCulture),
                plan.IssueCount.ToString(CultureInfo.CurrentCulture));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchReplacementDialogPreviewSummaryFormat,
            plan.ChangedCount.ToString(CultureInfo.CurrentCulture),
            plan.UnchangedCount.ToString(CultureInfo.CurrentCulture),
            plan.IssueCount.ToString(CultureInfo.CurrentCulture));
    }

    private static int CountConfirmationCandidates(WorkspaceBatchReplacementPlanDto plan) =>
        plan.Candidates.Count(static candidate => candidate.MatchStatus is
            WorkspaceBatchReplacementMatchStatus.MediumConfidence or
            WorkspaceBatchReplacementMatchStatus.LowConfidence or
            WorkspaceBatchReplacementMatchStatus.Ambiguous);

    private static bool CanSelectCandidate(WorkspaceBatchReplacementMatchStatus status) =>
        status is
            WorkspaceBatchReplacementMatchStatus.Changed or
            WorkspaceBatchReplacementMatchStatus.HighConfidence or
            WorkspaceBatchReplacementMatchStatus.MediumConfidence or
            WorkspaceBatchReplacementMatchStatus.LowConfidence;

    private static bool IsExecutableMatchStatus(WorkspaceBatchReplacementMatchStatus status) =>
        status is WorkspaceBatchReplacementMatchStatus.Changed or WorkspaceBatchReplacementMatchStatus.HighConfidence;

    private static bool IsIssueMatchStatus(WorkspaceBatchReplacementMatchStatus status) =>
        status is not WorkspaceBatchReplacementMatchStatus.Changed and
            not WorkspaceBatchReplacementMatchStatus.Unchanged and
            not WorkspaceBatchReplacementMatchStatus.HighConfidence;
}
