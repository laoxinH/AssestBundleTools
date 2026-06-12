using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

public sealed partial class BatchTasksViewModel
{
    private async Task RunBatchExtractAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var command = new ExtractWorkspaceAssetsCommand(
                WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                AssetTypeFilter: SelectedBatchExtractAssetTypeOption?.AssetType,
                WorkspaceKey: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceKey,
                WorkspaceDataRootPath: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceDataRootPath);
            var result = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceBatchExtractionResultDto>>(
                    BackgroundWorkProfile.BatchExtraction,
                    context => _workspaceBatchOperationsGateway.ExtractWorkspaceAssetsAsync(
                        command,
                        context.CancellationToken))
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = command.WorkspaceId.Value,
                    WorkspaceKey = command.WorkspaceKey,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BatchTasksBatchExtractStatusText)
                });

            if (!TryGetValue(result, out WorkspaceBatchExtractionResultDto? extractionResult))
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
            var command = new SaveWorkspaceAsNewBundleCommand(
                WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                WorkspaceKey: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceKey,
                WorkspaceDataRootPath: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceDataRootPath);
            var result = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceSaveExecutionResultDto>>(
                    BackgroundWorkProfile.SaveRebuild,
                    context => _workspaceBatchOperationsGateway.SaveWorkspaceAsNewBundleAsync(
                        command,
                        context.CancellationToken))
                {
                    SemanticKind = BackgroundWorkSemanticKind.WorkspaceMutation,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = command.WorkspaceId.Value,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BatchTasksSaveExecutionStatusText)
                });

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
