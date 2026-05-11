using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

public sealed partial class BatchTasksViewModel
{
    private async Task RunBatchExtractAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        WorkspaceBatchExtractionResultDto? extractionResult = null;
        IsBusy = true;
        try
        {
            var result = await _workspaceBatchOperationsGateway.ExtractWorkspaceAssetsAsync(
                new ExtractWorkspaceAssetsCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                    AssetTypeFilter: SelectedBatchExtractAssetTypeOption?.AssetType,
                    WorkspaceKey: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceKey,
                    WorkspaceDataRootPath: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceDataRootPath));

            if (!TryGetValue(result, out extractionResult))
            {
                return;
            }

            BatchExtractLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractLastRunFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount,
                _presentationPresenter.FormatTimestamp(extractionResult.CompletedAtUtc));

            AddTaskExecutionResult(_presentationPresenter.CreateBatchExtractTaskResult(extractionResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractLogExecutedFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount,
                extractionResult.SuggestedOutputDirectory));
        }
        finally
        {
            IsBusy = false;
        }

        if (extractionResult is not null)
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// 执行批量替换骨架。
    /// 当前阶段只验证并汇总已经进入替换队列的资源，不重新回放真实替换动作。
    /// </summary>
    private async Task RunBatchReplacementAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBatchOperationsGateway.ExecuteWorkspaceBatchReplacementAsync(
                new ExecuteWorkspaceBatchReplacementCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                    SourceFilter: SelectedBatchReplaceSourceOption?.SourceKind ?? BatchReplacementSourceKind.All));

            if (!TryGetValue(result, out WorkspaceBatchReplacementResultDto? replacementResult))
            {
                return;
            }

            BatchReplaceLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchReplaceLastRunFormat,
                replacementResult.ProcessedAssetCount,
                replacementResult.ProcessedBundleCount,
                replacementResult.WorkspaceReplacementCount,
                replacementResult.FileReplacementCount,
                _presentationPresenter.FormatTimestamp(replacementResult.CompletedAtUtc));

            AddTaskExecutionResult(_presentationPresenter.CreateBatchReplacementTaskResult(replacementResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchReplaceLogExecutedFormat,
                replacementResult.ProcessedAssetCount,
                replacementResult.ProcessedBundleCount,
                _presentationPresenter.GetBatchReplacementSourceDisplayName(replacementResult.SourceFilter)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 执行“另存为新包”骨架。
    /// 当前阶段不写真实文件，只返回一份稳定执行摘要并记录到应用日志。
    /// </summary>
    private async Task RunSaveAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBatchOperationsGateway.SaveWorkspaceAsNewBundleAsync(
                new SaveWorkspaceAsNewBundleCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId));

            if (!TryGetValue(result, out WorkspaceSaveExecutionResultDto? saveResult))
            {
                return;
            }

            SaveLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksSaveExecutionLastRunFormat,
                saveResult.PlannedBundleCount,
                saveResult.TotalChangeCount,
                _presentationPresenter.FormatTimestamp(saveResult.CompletedAtUtc));

            AddTaskExecutionResult(_presentationPresenter.CreateSaveTaskResult(saveResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksSaveExecutionLogExecutedFormat,
                saveResult.PlannedBundleCount,
                saveResult.TotalChangeCount,
                saveResult.OutputDirectory));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 维护最近批处理结果面板，确保页面只保留有限条目，避免把右侧结果区膨胀成日志面板。
    /// </summary>
    private void AddTaskExecutionResult(BatchTaskExecutionResultItemViewModel item)
    {
        RecentTaskResults.Insert(0, item);

        while (RecentTaskResults.Count > MaxRecentTaskResults)
        {
            RecentTaskResults.RemoveAt(RecentTaskResults.Count - 1);
        }

        NotifyWorkspaceState();
    }
}
