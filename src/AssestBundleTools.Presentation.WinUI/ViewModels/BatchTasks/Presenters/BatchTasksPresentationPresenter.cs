using System.Globalization;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

public sealed partial class BatchTasksViewModel
{
    private sealed class BatchTasksPresentationPresenter
    {
        private readonly LocalizedValueFormatter _localizedValueFormatter;
        private readonly ILocalizationService _localizationService;
        private readonly BatchTasksViewModel _owner;

        public BatchTasksPresentationPresenter(
            BatchTasksViewModel owner,
            ILocalizationService localizationService)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        }

        public void InitializeStaticState()
        {
            ReplaceBatchExtractAssetTypeOptions(CreateAssetTypeOptions());
            _owner.BatchExtractFileNamePolicyText = GetString(ResourceKeys.BatchTasksBatchExtractFileNamePolicyRetainOriginal);
            _owner.BatchExtractConflictPolicyText = GetString(ResourceKeys.BatchTasksBatchExtractConflictPolicySkipDuplicates);
        }

        public void ApplyNoWorkspaceState()
        {
            _owner._activeWorkspaceId = null;
            _owner.WorkspaceOverviewText = GetString(ResourceKeys.BatchTasksWorkspaceOverviewEmpty);
            _owner.ChangeSummaryText = GetString(ResourceKeys.BatchTasksChangeSummaryEmpty);
            _owner.BatchExtractScopeText = GetString(ResourceKeys.BatchTasksBatchExtractScopeEmpty);
            _owner.BatchExtractOutputDirectoryText = GetString(ResourceKeys.BatchTasksBatchExtractOutputDirectoryEmpty);
            _owner.BatchExtractSummaryText = GetString(ResourceKeys.BatchTasksBatchExtractEmpty);
            _owner.SaveOutputDirectoryText = GetString(ResourceKeys.BatchTasksSavePreviewOutputDirectoryEmpty);
            _owner.SaveFileNamePolicyText = GetString(ResourceKeys.BatchTasksSavePreviewFileNamePolicyRetainOriginal);
            _owner.SavePreviewSummaryText = GetString(ResourceKeys.BatchTasksSavePreviewEmpty);
            _owner.ResetWorkspaceScopedExecutionState(clearTaskResults: true);
            _owner.SummaryCards.Clear();
            _owner.RecentChanges.Clear();
            _owner.BatchExtractionBundles.Clear();
            _owner.SavePreviewBundles.Clear();
            _owner.SaveRiskNotes.Clear();
            _owner.NotifyWorkspaceState();
        }

        public void ApplyWorkspaceSummary(WorkspaceChangeSummaryDto summary)
        {
            var sourceText = string.IsNullOrWhiteSpace(summary.SourcePath)
                ? GetString(ResourceKeys.WorkspaceSourceNotImported)
                : summary.SourcePath;

            _owner.WorkspaceOverviewText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksWorkspaceOverviewFormat,
                summary.WorkspaceDisplayName,
                summary.BundleCount,
                summary.AssetCount,
                sourceText);

            _owner.ChangeSummaryText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeSummaryFormat,
                summary.TotalChangeCount,
                summary.ModifiedAssetCount,
                summary.ExtractedAssetCount,
                summary.WorkspaceReplacementCount,
                summary.FileReplacementCount);

            _owner.SummaryCards.Clear();
            _owner.SummaryCards.Add(new BatchTaskSummaryCardViewModel(
                GetString(ResourceKeys.BatchTasksSummaryTotalChangesTitle),
                summary.TotalChangeCount.ToString(),
                GetString(ResourceKeys.BatchTasksSummaryTotalChangesDescription)));
            _owner.SummaryCards.Add(new BatchTaskSummaryCardViewModel(
                GetString(ResourceKeys.BatchTasksSummaryModifiedTitle),
                summary.ModifiedAssetCount.ToString(),
                GetString(ResourceKeys.BatchTasksSummaryModifiedDescription)));
            _owner.SummaryCards.Add(new BatchTaskSummaryCardViewModel(
                GetString(ResourceKeys.BatchTasksSummaryExtractedTitle),
                summary.ExtractedAssetCount.ToString(),
                GetString(ResourceKeys.BatchTasksSummaryExtractedDescription)));
            _owner.SummaryCards.Add(new BatchTaskSummaryCardViewModel(
                GetString(ResourceKeys.BatchTasksSummaryReplacedTitle),
                summary.WorkspaceReplacementCount.ToString(),
                GetString(ResourceKeys.BatchTasksSummaryReplacedDescription)));
            _owner.SummaryCards.Add(new BatchTaskSummaryCardViewModel(
                GetString(ResourceKeys.BatchTasksSummaryFileReplacedTitle),
                summary.FileReplacementCount.ToString(),
                GetString(ResourceKeys.BatchTasksSummaryFileReplacedDescription)));

            _owner.RecentChanges.Clear();
            foreach (var item in summary.Items)
            {
                _owner.RecentChanges.Add(new BatchTaskChangeItemViewModel(
                    AssetName: item.AssetName,
                    BundleDisplayName: item.BundleDisplayName,
                    AssetTypeText: GetAssetTypeDisplayName(item.AssetType),
                    StateText: BuildStateText(item),
                    DetailText: BuildDetailText(item),
                    TimeText: item.LastChangedAtUtc.HasValue
                        ? FormatTimestamp(item.LastChangedAtUtc.Value)
                        : GetString(ResourceKeys.BatchTasksChangeTimeUnknown)));
            }

            _owner.NotifyWorkspaceState();
        }

        public void ApplyBatchExtractionPreview(WorkspaceBatchExtractionPreviewDto preview)
        {
            _owner.BatchExtractScopeText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractScopeFormat,
                preview.WorkspaceDisplayName);
            _owner.BatchExtractOutputDirectoryText = preview.SuggestedOutputDirectory;
            _owner.BatchExtractSummaryText = preview.CandidateAssetCount > 0
                ? _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksBatchExtractSummaryFormat,
                    preview.CandidateAssetCount,
                    preview.CandidateBundleCount,
                    _localizedValueFormatter.FormatCompactByteSize(preview.TotalSizeInBytes))
                : GetString(ResourceKeys.BatchTasksBatchExtractEmpty);

            _owner.BatchExtractionBundles.Clear();
            foreach (var bundlePlan in preview.BundlePlans)
            {
                _owner.BatchExtractionBundles.Add(new BatchTaskExtractionBundleItemViewModel(
                    BundleDisplayName: bundlePlan.BundleDisplayName,
                    SummaryText: _localizationService.GetFormattedString(
                        ResourceKeys.BatchTasksBatchExtractBundlePlanSummaryFormat,
                        bundlePlan.AssetCount,
                        _localizedValueFormatter.FormatCompactByteSize(bundlePlan.TotalSizeInBytes))));
            }

            _owner.NotifyWorkspaceState();
        }

        public void ApplySavePreview(WorkspaceSavePreviewDto preview)
        {
            _owner.SaveOutputDirectoryText = preview.OutputDirectory;
            _owner.SaveFileNamePolicyText = GetString(ResourceKeys.BatchTasksSavePreviewFileNamePolicyRetainOriginal);
            _owner.SavePreviewSummaryText = preview.PlannedBundleCount > 0
                ? _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksSavePreviewSummaryFormat,
                    preview.PlannedBundleCount,
                    preview.TotalChangeCount,
                    preview.OutputDirectory)
                : GetString(ResourceKeys.BatchTasksSavePreviewEmpty);

            _owner.SavePreviewBundles.Clear();
            foreach (var bundlePlan in preview.BundlePlans)
            {
                _owner.SavePreviewBundles.Add(new BatchTaskSaveBundleItemViewModel(
                    BundleDisplayName: bundlePlan.BundleDisplayName,
                    OutputPath: bundlePlan.OutputFilePath,
                    SummaryText: _localizationService.GetFormattedString(
                        ResourceKeys.BatchTasksSavePreviewBundlePlanSummaryFormat,
                        bundlePlan.ChangeCount,
                        bundlePlan.ModifiedAssetCount,
                        bundlePlan.ExtractedAssetCount,
                        bundlePlan.WorkspaceReplacementCount,
                        bundlePlan.FileReplacementCount)));
            }

            _owner.SaveRiskNotes.Clear();
            _owner.SaveRiskNotes.Add(new BatchTaskTextItemViewModel(GetString(ResourceKeys.BatchTasksSavePreviewRiskNoOverwrite)));
            if (preview.OutputBlockedBundleCount > 0)
            {
                _owner.SaveRiskNotes.Add(new BatchTaskTextItemViewModel(_localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksSavePreviewRiskOutputBlockedFormat,
                    preview.OutputBlockedBundleCount)));
            }
            else if (preview.OutputReadyBundleCount > 0)
            {
                _owner.SaveRiskNotes.Add(new BatchTaskTextItemViewModel(_localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksSavePreviewRiskOutputReadyFormat,
                    preview.OutputReadyBundleCount)));
            }

            if (preview.UnresolvedArtifactInputCount > 0)
            {
                _owner.SaveRiskNotes.Add(new BatchTaskTextItemViewModel(_localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksSavePreviewRiskArtifactUnresolvedFormat,
                    preview.UnresolvedArtifactInputCount)));
            }

            _owner.SaveRiskNotes.Add(new BatchTaskTextItemViewModel(GetString(ResourceKeys.BatchTasksSavePreviewRiskExecutionDeferred)));

            _owner.NotifyWorkspaceState();
        }

        public BatchTaskExecutionResultItemViewModel CreateBatchExtractTaskResult(WorkspaceBatchExtractionResultDto extractionResult) =>
            new(
                Title: GetString(ResourceKeys.BatchTasksTaskResultBatchExtractTitle),
                SummaryText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksTaskResultBatchExtractSummaryFormat,
                    extractionResult.ExtractedAssetCount,
                    extractionResult.ExtractedBundleCount),
                DetailText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksTaskResultBatchExtractDetailFormat,
                    extractionResult.SuggestedOutputDirectory),
                StatusText: GetString(ResourceKeys.BatchTasksTaskResultStatusCompleted),
                TimeText: FormatTimestamp(extractionResult.CompletedAtUtc));

        public BatchTaskExecutionResultItemViewModel CreateSaveTaskResult(WorkspaceSaveExecutionResultDto saveResult) =>
            new(
                Title: GetString(ResourceKeys.BatchTasksTaskResultSaveTitle),
                SummaryText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksTaskResultSaveSummaryFormat,
                    saveResult.PlannedBundleCount,
                    saveResult.TotalChangeCount),
                DetailText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksTaskResultSaveDetailFormat,
                    saveResult.OutputDirectory),
                StatusText: GetString(ResourceKeys.BatchTasksTaskResultStatusCompleted),
                TimeText: FormatTimestamp(saveResult.CompletedAtUtc));

        public string FormatTimestamp(DateTimeOffset timestamp) =>
            timestamp.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

        private string BuildStateText(WorkspaceChangeItemDto item)
        {
            var hasWorkspaceReplacement = !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName);
            var hasFileReplacement = !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName);
            var hasExtraction = item.LastExtractedAtUtc.HasValue;

            return (hasWorkspaceReplacement, hasFileReplacement, hasExtraction, item.IsModified) switch
            {
                (true, false, true, _) => GetString(ResourceKeys.BatchTasksChangeStateWorkspaceReplacementAndExtracted),
                (true, false, false, _) => GetString(ResourceKeys.BatchTasksChangeStateWorkspaceReplacementOnly),
                (false, true, true, _) => GetString(ResourceKeys.BatchTasksChangeStateFileReplacementAndExtracted),
                (false, true, false, _) => GetString(ResourceKeys.BatchTasksChangeStateFileReplacementOnly),
                (false, false, true, true) => GetString(ResourceKeys.BatchTasksChangeStateModifiedAndExtracted),
                (false, false, true, false) => GetString(ResourceKeys.BatchTasksChangeStateExtractedOnly),
                _ => GetString(ResourceKeys.BatchTasksChangeStateModifiedOnly)
            };
        }

        private string BuildDetailText(WorkspaceChangeItemDto item)
        {
            var hasWorkspaceReplacement = !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName) &&
                                          !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceBundleDisplayName);
            var hasFileReplacement = !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName);
            var hasExtraction = item.LastExtractedAtUtc.HasValue;

            if (hasWorkspaceReplacement && hasExtraction)
            {
                return _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksChangeDetailWorkspaceReplacementAndExtractedFormat,
                    item.WorkspaceReplacementSourceBundleDisplayName!,
                    item.WorkspaceReplacementSourceAssetName!,
                    FormatTimestamp(item.LastExtractedAtUtc!.Value));
            }

            if (hasWorkspaceReplacement)
            {
                return _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksChangeDetailWorkspaceReplacementOnlyFormat,
                    item.WorkspaceReplacementSourceBundleDisplayName!,
                    item.WorkspaceReplacementSourceAssetName!);
            }

            if (hasFileReplacement && hasExtraction)
            {
                return _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksChangeDetailFileReplacementAndExtractedFormat,
                    item.FileReplacementSourceFileName!,
                    FormatTimestamp(item.LastExtractedAtUtc!.Value));
            }

            if (hasFileReplacement)
            {
                return _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksChangeDetailFileReplacementOnlyFormat,
                    item.FileReplacementSourceFileName!,
                    item.FileReplacementSourcePath ?? item.FileReplacementSourceFileName!);
            }

            if (hasExtraction)
            {
                return _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksChangeDetailExtractedOnlyFormat,
                    FormatTimestamp(item.LastExtractedAtUtc!.Value));
            }

            return GetString(ResourceKeys.BatchTasksChangeDetailModifiedOnly);
        }

        private string GetAssetTypeDisplayName(AssetType assetType) =>
            assetType switch
            {
                AssetType.Texture2D => GetString(ResourceKeys.AssetTypeDisplayTexture2D),
                AssetType.Sprite => GetString(ResourceKeys.AssetTypeDisplaySprite),
                AssetType.TextAsset => GetString(ResourceKeys.AssetTypeDisplayTextAsset),
                AssetType.AudioClip => GetString(ResourceKeys.AssetTypeDisplayAudioClip),
                AssetType.Mesh => GetString(ResourceKeys.AssetTypeDisplayMesh),
                AssetType.Material => GetString(ResourceKeys.AssetTypeDisplayMaterial),
                _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
            };

        private void ReplaceBatchExtractAssetTypeOptions(IEnumerable<AssetTypeOptionViewModel> options)
        {
            _owner.BatchExtractAssetTypeOptions.Clear();
            foreach (var option in options)
            {
                _owner.BatchExtractAssetTypeOptions.Add(option);
            }

            _owner.SelectedBatchExtractAssetTypeOption = _owner.BatchExtractAssetTypeOptions.FirstOrDefault();
        }

        private IEnumerable<AssetTypeOptionViewModel> CreateAssetTypeOptions() =>
            new[]
            {
                new AssetTypeOptionViewModel(GetString(ResourceKeys.AssetTypeFilterAll), null),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Texture2D), AssetType.Texture2D),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Sprite), AssetType.Sprite),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.TextAsset), AssetType.TextAsset),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.AudioClip), AssetType.AudioClip),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Mesh), AssetType.Mesh),
                new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Material), AssetType.Material)
            };

        private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
    }
}
