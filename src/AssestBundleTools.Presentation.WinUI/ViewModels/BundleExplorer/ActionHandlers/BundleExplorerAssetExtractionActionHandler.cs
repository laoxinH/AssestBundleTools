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
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IFilePickerService _filePickerService;

    public BundleExplorerAssetExtractionActionHandler(
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
            string? selectedOutputPath;
            try
            {
                selectedOutputPath = await _filePickerService.PickAssetExtractionOutputFileAsync(
                    details.AssetType,
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
                        entry.Item.AssetId,
                        workspaceSummary.WorkspaceKey,
                        workspaceSummary.WorkspaceDataRootPath));

                if (!TryGetValue(result, out var extractionResult, appendResultFailure))
                {
                    continue;
                }

                extractedResults.Add(extractionResult);
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogAssetExtracted,
                    extractionResult.AssetName,
                    ResolveExtractionOutputPath(extractionResult)));
            }

            if (extractedResults.Count == 0)
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchAssetsExtracted,
                extractedResults.Count,
                ResolveExtractionOutputPath(extractedResults[0])));

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

    public async Task ExtractSpineAssetGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Func<Task> refreshPrimaryPaneAsync,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(refreshPrimaryPaneAsync);
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

            await refreshPrimaryPaneAsync();
        }
        finally
        {
            setBusyState(false);
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
