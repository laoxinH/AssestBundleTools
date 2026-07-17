using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Windows.Storage;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

/// <summary>
/// 复用单项提取用例，把真实输出适配为 Windows 文件拖放载荷。
/// </summary>
internal sealed class BundleExplorerAssetDragExportCoordinator
{
    private sealed record PreparedDragExportOutput(
        string OutputPath,
        bool IsDirectory,
        string StagingSessionPath);

    private readonly BundleExplorerBatchExtractionBackgroundExecutor _extractionExecutor;
    private readonly AssetDragExportStagingService _stagingService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;

    public BundleExplorerAssetDragExportCoordinator(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IAppBackgroundTaskStatusService backgroundTaskStatusService,
        IWorkspaceOperationChangeNotifier operationChangeNotifier,
        IBackgroundWorkScheduler backgroundWorkScheduler,
        AssetDragExportStagingService? stagingService = null)
    {
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _stagingService = stagingService ?? new AssetDragExportStagingService();
        _extractionExecutor = new BundleExplorerBatchExtractionBackgroundExecutor(
            workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway)),
            localizationService,
            backgroundTaskRunner,
            backgroundTaskStatusService,
            operationChangeNotifier,
            backgroundWorkScheduler);
    }

    public Task<AssetPaneDragExportPayload?> PrepareAsync(
        WorkspaceSummaryDto workspaceSummary,
        object item) =>
        PrepareCoreAsync(
            workspaceSummary,
            item,
            async output =>
            {
                var storageItem = output.IsDirectory
                    ? await _stagingService.GetFolderAsync(output.OutputPath)
                    : await _stagingService.GetFileAsync(output.OutputPath);
                return new AssetPaneDragExportPayload(
                    [storageItem],
                    output.StagingSessionPath);
            });

    public Task<IReadOnlyList<string>?> PreparePathsAsync(
        WorkspaceSummaryDto workspaceSummary,
        object item) =>
        PrepareCoreAsync<IReadOnlyList<string>>(
            workspaceSummary,
            item,
            output => Task.FromResult<IReadOnlyList<string>>([output.OutputPath]));

    private async Task<TResult?> PrepareCoreAsync<TResult>(
        WorkspaceSummaryDto workspaceSummary,
        object item,
        Func<PreparedDragExportOutput, Task<TResult>> projectResultAsync)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(projectResultAsync);

        AssetDragExportStagingSession? session = null;
        var displayName = ResolveDisplayName(item);
        try
        {
            session = await _stagingService.CreateSessionAsync();
            var output = item switch
            {
                AssetListEntryViewModel { CanDragExport: true } asset =>
                    await ExtractAssetPathAsync(workspaceSummary, asset, session),
                SpineAssetGroupEntryViewModel { CanDragExport: true } spineGroup =>
                    await ExtractSpineGroupPathAsync(workspaceSummary, spineGroup, session),
                _ => null
            };

            if (output is not { } preparedOutput)
            {
                await CleanupQuietlyAsync(session.Path);
                return null;
            }

            var result = await projectResultAsync(new PreparedDragExportOutput(
                preparedOutput.OutputPath,
                preparedOutput.IsDirectory,
                session.Path));
            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogDragExportPreparedFormat,
                displayName,
                Path.GetFileName(preparedOutput.OutputPath)));
            return result;
        }
        catch (Exception ex)
        {
            if (session is not null)
            {
                await CleanupQuietlyAsync(session.Path);
            }

            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogDragExportUnexpectedFailureFormat,
                displayName,
                ex.GetType().Name));
            ShowFailureNotification();
            return null;
        }
    }

    public async Task CleanupAsync(string stagingSessionPath)
    {
        try
        {
            await _stagingService.CleanupSessionAsync(stagingSessionPath);
        }
        catch (Exception ex)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogDragExportCleanupFailedFormat,
                ex.GetType().Name));
        }
    }

    private async Task<(string OutputPath, bool IsDirectory)?> ExtractAssetPathAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetListEntryViewModel asset,
        AssetDragExportStagingSession session)
    {
        var suggestedFileName = string.IsNullOrWhiteSpace(asset.Item.ExportFileName)
            ? WorkspaceAssetExportNameProjection.SanitizeFileName(asset.DisplayName)
            : asset.Item.ExportFileName;
        var result = await _extractionExecutor.ExtractAssetAsync(
            workspaceSummary,
            asset.DisplayName,
            new ExtractAssetCommand(
                workspaceSummary.WorkspaceId,
                asset.Item.BundleId,
                asset.Item.AssetId,
                workspaceSummary.WorkspaceKey,
                workspaceSummary.WorkspaceDataRootPath,
                Path.Combine(session.Path, suggestedFileName)));
        if (!result.IsSuccess || result.Value is null)
        {
            HandleResultFailure(result.IsCancelled, result.Errors);
            return null;
        }

        var outputPath = result.Value.OutputPath;
        if (!result.Value.OutputFileExists || string.IsNullOrWhiteSpace(outputPath))
        {
            HandleMissingOutput(asset.DisplayName);
            return null;
        }

        return (outputPath, false);
    }

    private async Task<(string OutputPath, bool IsDirectory)?> ExtractSpineGroupPathAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupEntryViewModel spineGroup,
        AssetDragExportStagingSession session)
    {
        var result = await _extractionExecutor.ExtractSpineGroupAsync(
            workspaceSummary,
            spineGroup.DisplayName,
            new ExtractSpineAssetGroupCommand(
                workspaceSummary.WorkspaceId,
                workspaceSummary.WorkspaceKey ?? string.Empty,
                workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                spineGroup.Item.BundleId,
                spineGroup.Item.GroupId,
                session.Path));
        if (!result.IsSuccess || result.Value is null)
        {
            HandleResultFailure(result.IsCancelled, result.Errors);
            return null;
        }

        if (result.Value.ExtractedAssetCount == 0 ||
            string.IsNullOrWhiteSpace(result.Value.SuggestedOutputDirectory))
        {
            HandleMissingOutput(spineGroup.DisplayName);
            return null;
        }

        return (result.Value.SuggestedOutputDirectory, true);
    }

    private void HandleResultFailure(bool isCancelled, IReadOnlyList<AppError> errors)
    {
        if (isCancelled)
        {
            return;
        }

        if (errors.Count > 0)
        {
            _appLogService.AppendError(FormatError(errors[0]));
        }

        ShowFailureNotification();
    }

    private void HandleMissingOutput(string displayName)
    {
        _appLogService.AppendError(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogDragExportMissingOutputFormat,
            displayName));
        ShowFailureNotification();
    }

    private async Task CleanupQuietlyAsync(string stagingSessionPath)
    {
        try
        {
            await _stagingService.CleanupSessionAsync(stagingSessionPath);
        }
        catch
        {
        }
    }

    private void ShowFailureNotification()
    {
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            GetString(ResourceKeys.BundleExplorerNotificationDragExportFailed),
            AppNotificationSeverity.Error);
    }

    private string FormatError(AppError error) =>
        error.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                error.MessageToken,
                error.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : GetString(error.MessageToken);

    private static string ResolveDisplayName(object item) => item switch
    {
        AssetListEntryViewModel asset => asset.DisplayName,
        SpineAssetGroupEntryViewModel spineGroup => spineGroup.DisplayName,
        _ => item.GetType().Name
    };

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
