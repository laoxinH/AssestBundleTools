using System.ComponentModel;
using System.Globalization;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed partial class WorkspaceBatchReplacementDialogViewModel
{
    public IReadOnlyList<WorkspaceBatchReplacementCandidateDto> GetSelectedCandidates() =>
        Candidates
            .Where(static candidate => candidate.IsSelected)
            .SelectMany(static candidate => candidate.ReplacementCandidates)
            .ToArray();

    public void SetExecuting()
    {
        IsExecuting = true;
        ValidationText = string.Empty;
    }

    public void ApplyExecutionResult(WorkspaceBatchReplacementResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        IsExecuting = false;
        PlanSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceBatchReplacementDialogExecutionSummaryFormat,
            result.ReplacedAssetCount.ToString(CultureInfo.CurrentCulture),
            result.SkippedAssetCount.ToString(CultureInfo.CurrentCulture),
            result.FailedAssetCount.ToString(CultureInfo.CurrentCulture));
        ValidationText = result.FailedAssetCount > 0
            ? GetString(ResourceKeys.WorkspaceBatchReplacementDialogExecutionPartialFailure)
            : string.Empty;
        OnPropertyChanged(nameof(CanSubmit));
    }

    public void ApplyExecutionUnavailable()
    {
        IsExecuting = false;
        ValidationText = GetString(ResourceKeys.CommonUnexpectedFailure);
        OnPropertyChanged(nameof(CanSubmit));
    }

    private void SubscribeCandidateSelectionChanged()
    {
        foreach (var candidate in Candidates)
        {
            candidate.PropertyChanged += OnCandidateSelectionChanged;
        }
    }

    private void UnsubscribeCandidateSelectionChanged()
    {
        foreach (var candidate in Candidates)
        {
            candidate.PropertyChanged -= OnCandidateSelectionChanged;
        }
    }

    private void OnCandidateSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkspaceBatchReplacementCandidateViewModel.IsSelected), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanSubmit));
        }
    }
}
