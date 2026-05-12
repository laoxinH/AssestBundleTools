using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 编排资产提取动作，包括保存位置选择、应用层提取命令和页面反馈。
/// </summary>
internal sealed class BundleExplorerAssetExtractionActionHandler
{
    private const int MaxBatchFailureDetails = 3;

    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IFilePickerService _filePickerService;
    private readonly BundleExplorerBatchExtractionBackgroundExecutor _batchExtractionExecutor;
    private bool _isBatchExtractionRunning;

    public BundleExplorerAssetExtractionActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IBackgroundTaskRunner? backgroundTaskRunner = null,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier = null)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _batchExtractionExecutor = new BundleExplorerBatchExtractionBackgroundExecutor(
            _workspaceAssetOperationGateway,
            _localizationService,
            backgroundTaskRunner,
            backgroundTaskStatusService,
            operationChangeNotifier);
    }

    public async Task ExtractSelectedAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            string? selectedOutputPath;
            try
            {
                selectedOutputPath = await _filePickerService.PickAssetExtractionOutputFileAsync(
                    details.AssetType,
                    details.AssetTypeDisplayName,
                    details.Name,
                    details.AssetPath,
                    details.ContainerPath);
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

            if (string.IsNullOrWhiteSpace(selectedOutputPath))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogAssetExtractionSelectionCancelled,
                    details.Name));
                return;
            }

            var result = await _workspaceAssetOperationGateway.ExtractAssetAsync(
                new ExtractAssetCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath,
                    selectedOutputPath,
                    PreserveSuggestedFileName: true));

            if (!TryGetValue(result, out var extractionResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetExtracted,
                extractionResult.AssetName,
                ResolveExtractionOutputPath(extractionResult)));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationAssetExtracted,
                    extractionResult.AssetName),
                AppNotificationSeverity.Success);

        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task ExtractSelectedAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetListEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(selectedEntries);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (selectedEntries.Count == 0)
        {
            return;
        }

        if (_isBatchExtractionRunning)
        {
            return;
        }

        string? selectedOutputDirectory;
        setBusyState(true);
        try
        {
            try
            {
                selectedOutputDirectory = await _filePickerService.PickAssetBatchExtractionOutputDirectoryAsync();
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

            if (string.IsNullOrWhiteSpace(selectedOutputDirectory))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogBatchAssetExtractionSelectionCancelled,
                    selectedEntries.Count));
                return;
            }
        }
        finally
        {
            setBusyState(false);
        }

        _isBatchExtractionRunning = true;
        try
        {
            var executionResult = await _batchExtractionExecutor.ExtractAssetsAsync(
                workspaceSummary,
                selectedEntries,
                defaultBundleDisplayName,
                selectedOutputDirectory);
            if (executionResult.IsCancelled)
            {
                return;
            }

            AppendFailures(executionResult.Failures, appendResultFailure);

            if (executionResult.ExtractedResults.Count == 0)
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchAssetsExtracted,
                executionResult.ExtractedResults.Count,
                ResolveExtractionOutputPath(executionResult.ExtractedResults[0])));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBatchAssetsExtracted,
                    executionResult.ExtractedResults.Count),
                AppNotificationSeverity.Success);

        }
        finally
        {
            _isBatchExtractionRunning = false;
        }
    }

    public async Task ExtractSpineAssetGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            string? selectedOutputDirectory;
            try
            {
                selectedOutputDirectory = await _filePickerService.PickSpineAssetGroupExtractionDirectoryAsync();
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

            if (string.IsNullOrWhiteSpace(selectedOutputDirectory))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogSpineGroupExtractionSelectionCancelled,
                    spineGroupTab.DisplayName));
                return;
            }

            var result = await _workspaceAssetOperationGateway.ExtractSpineAssetGroupAsync(
                new ExtractSpineAssetGroupCommand(
                    workspaceSummary.WorkspaceId,
                    workspaceSummary.WorkspaceKey ?? string.Empty,
                    workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                    spineGroupTab.BundleId,
                    spineGroupTab.GroupId,
                    selectedOutputDirectory));

            if (!TryGetValue(result, out var extractionResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogSpineGroupExtracted,
                extractionResult.GroupDisplayName,
                extractionResult.ExtractedAssetCount,
                extractionResult.FailedAssetCount,
                extractionResult.SuggestedOutputDirectory));

            ShowOperationNotification(
                GetString(extractionResult.FailedAssetCount == 0
                    ? ResourceKeys.AppNotificationSuccessTitle
                    : ResourceKeys.AppNotificationWarningTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationSpineGroupExtracted,
                    extractionResult.GroupDisplayName,
                    extractionResult.ExtractedAssetCount,
                    extractionResult.FailedAssetCount),
                extractionResult.FailedAssetCount == 0
                    ? AppNotificationSeverity.Success
                    : AppNotificationSeverity.Warning);
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task ExtractSelectedSpineAssetGroupsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<SpineAssetGroupEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(selectedEntries);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (selectedEntries.Count == 0)
        {
            return;
        }

        if (_isBatchExtractionRunning)
        {
            return;
        }

        string? selectedOutputDirectory;
        setBusyState(true);
        try
        {
            try
            {
                selectedOutputDirectory = await _filePickerService.PickAssetBatchExtractionOutputDirectoryAsync();
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

            if (string.IsNullOrWhiteSpace(selectedOutputDirectory))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogBatchSpineGroupExtractionSelectionCancelled,
                    selectedEntries.Count));
                return;
            }
        }
        finally
        {
            setBusyState(false);
        }

        _isBatchExtractionRunning = true;
        try
        {
            var executionResult = await _batchExtractionExecutor.ExtractSpineGroupsAsync(
                workspaceSummary,
                selectedEntries,
                defaultBundleDisplayName,
                selectedOutputDirectory);
            if (executionResult.IsCancelled)
            {
                return;
            }

            AppendFailures(executionResult.Failures, appendResultFailure);

            if (executionResult.ExtractionResults.Count == 0)
            {
                return;
            }

            var extractedAssetCount = executionResult.ExtractionResults.Sum(static result => result.ExtractedAssetCount);
            var failedAssetCount = executionResult.ExtractionResults.Sum(static result => result.FailedAssetCount);
            var failedGroupCount = selectedEntries.Count - executionResult.ExtractionResults.Count;
            var totalFailureCount = failedAssetCount + failedGroupCount;

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchSpineGroupsExtracted,
                executionResult.ExtractionResults.Count,
                selectedEntries.Count,
                extractedAssetCount,
                totalFailureCount,
                selectedOutputDirectory));

            ShowOperationNotification(
                GetString(totalFailureCount == 0
                    ? ResourceKeys.AppNotificationSuccessTitle
                    : ResourceKeys.AppNotificationWarningTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBatchSpineGroupsExtracted,
                    executionResult.ExtractionResults.Count,
                    selectedEntries.Count,
                    extractedAssetCount,
                    totalFailureCount),
                totalFailureCount == 0
                    ? AppNotificationSeverity.Success
                    : AppNotificationSeverity.Warning);

        }
        finally
        {
            _isBatchExtractionRunning = false;
        }
    }

    private static bool TryGetValue<T>(
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

    private void AppendFailures(
        IReadOnlyList<IReadOnlyList<AppError>> failures,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        var appendedFailureCount = 0;
        foreach (var failure in failures)
        {
            if (failure.Count > 0)
            {
                appendResultFailure(failure);
                appendedFailureCount++;
                if (appendedFailureCount >= MaxBatchFailureDetails)
                {
                    break;
                }
            }
        }

        var omittedFailureCount = failures.Count(static failure => failure.Count > 0) - appendedFailureCount;
        if (omittedFailureCount > 0)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchExtractionFailuresSuppressed,
                omittedFailureCount));
        }
    }

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private static string ResolveExtractionOutputPath(AssetExtractionResultDto result) =>
        string.IsNullOrWhiteSpace(result.OutputPath)
            ? result.SuggestedOutputPath
            : result.OutputPath;

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
