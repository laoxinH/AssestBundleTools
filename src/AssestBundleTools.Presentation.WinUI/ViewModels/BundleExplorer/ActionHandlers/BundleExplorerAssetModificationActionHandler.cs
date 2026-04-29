using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 聚合资产级提取、替换与撤销动作，避免主工作流继续堆积动作编排细节。
/// </summary>
internal sealed class BundleExplorerAssetModificationActionHandler
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IFilePickerService _filePickerService;

    public BundleExplorerAssetModificationActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
    }

    public async Task ExtractSelectedAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Func<Task> refreshPrimaryPaneAsync,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(refreshPrimaryPaneAsync);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.ExtractAssetAsync(
                new ExtractAssetCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var extractionResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetExtracted,
                extractionResult.AssetName,
                extractionResult.SuggestedOutputPath));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationAssetExtracted,
                    extractionResult.AssetName),
                AppNotificationSeverity.Success);

            await refreshPrimaryPaneAsync();
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task ExtractSelectedAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetListEntryViewModel> selectedEntries,
        Action<bool> setBusyState,
        Func<Task> refreshPrimaryPaneAsync,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(selectedEntries);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(refreshPrimaryPaneAsync);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (selectedEntries.Count == 0)
        {
            return;
        }

        setBusyState(true);
        try
        {
            var extractedResults = new List<AssetExtractionResultDto>(selectedEntries.Count);

            foreach (var entry in selectedEntries)
            {
                var result = await _workspaceAssetOperationGateway.ExtractAssetAsync(
                    new ExtractAssetCommand(
                        workspaceSummary.WorkspaceId,
                        entry.Item.BundleId,
                        entry.Item.AssetId));

                if (!TryGetValue(result, out var extractionResult, appendResultFailure))
                {
                    continue;
                }

                extractedResults.Add(extractionResult);
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogAssetExtracted,
                    extractionResult.AssetName,
                    extractionResult.SuggestedOutputPath));
            }

            if (extractedResults.Count == 0)
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchAssetsExtracted,
                extractedResults.Count,
                extractedResults[0].SuggestedOutputPath));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBatchAssetsExtracted,
                    extractedResults.Count),
                AppNotificationSeverity.Success);

            await refreshPrimaryPaneAsync();
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task SelectWorkspaceReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(reapplyAssetDocumentState);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.GetWorkspaceReplacementCandidatesAsync(
                new GetWorkspaceReplacementCandidatesQuery(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var candidates, appendResultFailure))
            {
                return;
            }

            var selectedCandidate = candidates[0];
            assetTab.UpdatePendingWorkspaceReplacementCandidate(selectedCandidate);
            reapplyAssetDocumentState(assetTab, details);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementSourceSelected,
                selectedCandidate.SourceBundleDisplayName,
                selectedCandidate.SourceAssetName,
                details.Name));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementSourceSelected,
                    selectedCandidate.SourceAssetName),
                AppNotificationSeverity.Informational);
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task SelectFileReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(reapplyAssetDocumentState);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            string? selectedFilePath;
            try
            {
                selectedFilePath = await _filePickerService.PickReplacementSourceFileAsync(details.AssetType);
            }
            catch (Exception ex)
            {
                _appLogService.AppendError(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFilePickerOpenFailedFormat,
                    ex.GetType().Name));
                ShowOperationNotification(
                    GetString(ResourceKeys.AppNotificationErrorTitle),
                    GetString(ResourceKeys.BundleExplorerNotificationFilePickerOpenFailed),
                    AppNotificationSeverity.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFileReplacementSelectionCancelled,
                    details.Name));
                return;
            }

            var result = await _workspaceAssetOperationGateway.GetFileReplacementCandidateAsync(
                new GetFileReplacementCandidateQuery(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    selectedFilePath));

            if (!TryGetValue(result, out var candidate, appendResultFailure))
            {
                return;
            }

            assetTab.UpdatePendingFileReplacementCandidate(candidate);
            reapplyAssetDocumentState(assetTab, details);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFileReplacementSourceSelected,
                candidate.SourceFileName,
                candidate.SourceFilePath,
                details.Name));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationFileReplacementSourceSelected,
                    candidate.SourceFileName),
                AppNotificationSeverity.Informational);
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task ApplyPendingReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Func<Task> refreshPrimaryPaneAsync,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(refreshPrimaryPaneAsync);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            if (assetTab.PendingFileReplacementCandidate is not null)
            {
                var fileResult = await _workspaceAssetOperationGateway.ReplaceAssetFromFileAsync(
                    new ReplaceAssetFromFileCommand(
                        workspaceSummary.WorkspaceId,
                        details.BundleId,
                        details.AssetId,
                        assetTab.PendingFileReplacementCandidate.SourceFilePath,
                        assetTab.PendingFileReplacementCandidate.SourceAssetType));

                if (!TryGetValue(fileResult, out var fileReplacementResult, appendResultFailure))
                {
                    return;
                }

                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFileReplacementApplied,
                    fileReplacementResult.TargetAssetName,
                    fileReplacementResult.SourceFileName,
                    fileReplacementResult.SourceFilePath));

                ShowOperationNotification(
                    GetString(ResourceKeys.AppNotificationSuccessTitle),
                    _localizationService.GetFormattedString(
                        ResourceKeys.BundleExplorerNotificationFileReplacementApplied,
                        fileReplacementResult.TargetAssetName),
                    AppNotificationSeverity.Success);

                assetTab.ClearPendingReplacementCandidates();
                await refreshPrimaryPaneAsync();
                return;
            }

            if (assetTab.PendingWorkspaceReplacementCandidate is null)
            {
                return;
            }

            var workspaceResult = await _workspaceAssetOperationGateway.ReplaceAssetFromWorkspaceAsync(
                new ReplaceAssetFromWorkspaceCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceBundleId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceAssetId));

            if (!TryGetValue(workspaceResult, out var replacementResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementApplied,
                replacementResult.TargetAssetName,
                replacementResult.SourceBundleDisplayName,
                replacementResult.SourceAssetName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementApplied,
                    replacementResult.TargetAssetName),
                AppNotificationSeverity.Success);

            assetTab.ClearPendingReplacementCandidates();
            await refreshPrimaryPaneAsync();
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task UndoAppliedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Func<Task> refreshPrimaryPaneAsync,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(refreshPrimaryPaneAsync);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.UndoAssetReplacementAsync(
                new UndoAssetReplacementCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var undoResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementUndone,
                undoResult.TargetAssetName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementUndone,
                    undoResult.TargetAssetName),
                AppNotificationSeverity.Informational);

            assetTab.ClearPendingReplacementCandidates();
            await refreshPrimaryPaneAsync();
        }
        finally
        {
            setBusyState(false);
        }
    }

    private bool TryGetValue<T>(
        Result<T> result,
        [NotNullWhen(true)] out T? value,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;

        if (result.IsCancelled)
        {
            return false;
        }

        appendResultFailure(result.Errors);
        return false;
    }

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
