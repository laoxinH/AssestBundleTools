using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.IO;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed class BundleExplorerWorkspaceExportCoordinator
{
    private readonly BundleExplorerPage _owner;
    private readonly IWorkspaceBatchOperationsGateway _workspaceBatchOperationsGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IResourceProcessorCatalogService _resourceProcessorCatalogService;
    private readonly IFilePickerService _filePickerService;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly WorkspaceExportFailureDisplayFormatter _failureDisplayFormatter;
    private WorkspaceExportWindow? _activeWindow;
    private BackgroundWorkHandle<Result<WorkspaceSaveExecutionResultDto>>? _activeExportHandle;

    public BundleExplorerWorkspaceExportCoordinator(
        BundleExplorerPage owner,
        IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
        IWorkspaceContextService workspaceContextService,
        IResourceProcessorCatalogService resourceProcessorCatalogService,
        IFilePickerService filePickerService,
        IBackgroundWorkScheduler backgroundWorkScheduler,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _resourceProcessorCatalogService = resourceProcessorCatalogService ?? throw new ArgumentNullException(nameof(resourceProcessorCatalogService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _failureDisplayFormatter = new WorkspaceExportFailureDisplayFormatter(_localizationService);
    }

    public async Task ShowAsync()
    {
        if (_owner.XamlRoot is null || _activeWindow is not null)
        {
            _activeWindow?.BringToFront();
            return;
        }

        if (!TryResolveWorkspaceScope(out var workspaceSummary, out var workspaceKey, out var workspaceDataRootPath))
        {
            return;
        }

        var workspaceExportDirectory = ResolveWorkspaceExportDirectory(workspaceSummary, workspaceDataRootPath);
        var browserState = _workspaceContextService.GetCurrentWorkspaceBrowserState();
        var exportSettings = browserState?.ExportSettings;
        var previewConflictPolicy = exportSettings?.OverwriteExistingFiles == true
            ? WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting
            : WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists;

        var changeSummaryResult = await _workspaceBatchOperationsGateway.GetWorkspaceChangeSummaryAsync(
            new GetWorkspaceChangeSummaryQuery(workspaceSummary.WorkspaceId));
        if (!TryGetValue(changeSummaryResult, out WorkspaceChangeSummaryDto? changeSummary))
        {
            return;
        }

        var savePreviewResult = await _workspaceBatchOperationsGateway.GetWorkspaceSavePreviewAsync(
            new GetWorkspaceSavePreviewQuery(
                workspaceSummary.WorkspaceId,
                OutputDirectory: workspaceExportDirectory,
                WorkspaceKey: workspaceKey,
                WorkspaceDataRootPath: workspaceDataRootPath,
                OutputConflictPolicy: previewConflictPolicy));
        if (!TryGetValue(savePreviewResult, out var savePreview))
        {
            return;
        }

        var processorCatalogResult = await _resourceProcessorCatalogService.GetEntriesAsync();
        if (!TryGetValue(processorCatalogResult, out var processorCatalogEntries))
        {
            return;
        }

        var viewModel = new WorkspaceExportDialogViewModel(
            _localizationService,
            changeSummary,
            savePreview,
            processorCatalogEntries,
            exportSettings);
        var windowNotificationService = new AppNotificationService();
        var window = new WorkspaceExportWindow(viewModel, windowNotificationService);
        App.CurrentApp.RegisterAuxiliaryWindow(window, _owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null);
        _activeWindow = window;
        viewModel.ExportSettingsChanged += OnExportSettingsChanged;
        window.StartExportRequested += OnStartExportRequested;
        window.CancelExportRequested += OnCancelExportRequested;
        window.Closed += OnWindowClosed;
        window.Activate();

        async void OnStartExportRequested(object? sender, EventArgs args)
        {
            await ExecuteExportAsync(workspaceSummary, workspaceKey, workspaceDataRootPath, viewModel);
        }

        void OnCancelExportRequested(object? sender, EventArgs args)
        {
            if (_activeExportHandle?.Cancel() == true)
            {
                viewModel.ApplyExecutionCancellationRequested();
            }
        }

        void OnExportSettingsChanged(object? sender, EventArgs args)
        {
            PersistWorkspaceExportSettings(viewModel.CaptureExportSettings());
        }

        void OnWindowClosed(object sender, WindowEventArgs args)
        {
            viewModel.ExportSettingsChanged -= OnExportSettingsChanged;
            window.StartExportRequested -= OnStartExportRequested;
            window.CancelExportRequested -= OnCancelExportRequested;
            window.Closed -= OnWindowClosed;
            if (ReferenceEquals(_activeWindow, window))
            {
                _activeWindow = null;
            }
        }
    }

    private async Task ExecuteExportAsync(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceKey,
        string? workspaceDataRootPath,
        WorkspaceExportDialogViewModel viewModel)
    {
        if (viewModel.IsExecuting || !viewModel.HasExportTargets)
        {
            return;
        }

        var submission = viewModel.CreateSubmission();
        var outputDirectory = submission.ExportToWorkspaceDirectory
            ? viewModel.OutputDirectoryText
            : await _filePickerService.PickWorkspaceExportOutputDirectoryAsync();
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            viewModel.ApplyExecutionCancelled();
            return;
        }

        viewModel.ApplySelectedOutputDirectory(outputDirectory);
        viewModel.SetExecuting(submission.SelectedBundleIds.Count);
        var commandTemplate = new SaveWorkspaceAsNewBundleCommand(
            workspaceSummary.WorkspaceId,
            OutputDirectory: outputDirectory,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            SelectedBundleIds: submission.SelectedBundleIds,
            ProcessorSelections: submission.ProcessorSelections,
            CompressionMode: submission.CompressionMode,
            OutputConflictPolicy: submission.OutputConflictPolicy,
            AudioWriteMode: submission.AudioWriteMode);

        Result<WorkspaceSaveExecutionResultDto> result;
        BackgroundWorkHandle<Result<WorkspaceSaveExecutionResultDto>>? exportHandle = null;
        try
        {
            exportHandle = _backgroundWorkScheduler.StartAsync(
                new BackgroundWorkRequest<Result<WorkspaceSaveExecutionResultDto>>(
                    BackgroundWorkProfile.SaveRebuild,
                    context =>
                    {
                        var command = commandTemplate with
                        {
                            Progress = new WorkspaceSaveExecutionProgressBridge(
                                context.Progress,
                                viewModel,
                                _owner.DispatcherQueue,
                                _localizationService),
                            Diagnostics = new WorkspaceSaveExecutionDiagnosticLogBridge(
                                _appLogService,
                                _owner.DispatcherQueue,
                                _localizationService)
                        };
                        return _workspaceBatchOperationsGateway.SaveWorkspaceAsNewBundleAsync(
                            command,
                            context.CancellationToken);
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = workspaceSummary.WorkspaceId.Value,
                    WorkspaceKey = workspaceKey,
                    StatusTitle = GetString(ResourceKeys.WorkspaceExportDialogStatusTitle),
                    TotalCount = submission.SelectedBundleIds.Count,
                    CanCancel = true
                });
            _activeExportHandle = exportHandle;
            result = await exportHandle.ResultTask;
        }
        catch (OperationCanceledException)
        {
            viewModel.ApplyExecutionCancelled();
            return;
        }
        catch
        {
            viewModel.ApplyExecutionUnavailable();
            ShowNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                GetString(ResourceKeys.CommonUnexpectedFailure),
                AppNotificationSeverity.Error);
            return;
        }
        finally
        {
            if (exportHandle is not null && ReferenceEquals(_activeExportHandle, exportHandle))
            {
                _activeExportHandle = null;
            }
        }

        if (result.IsCancelled)
        {
            viewModel.ApplyExecutionCancelled();
            return;
        }

        if (!TryGetValue(result, out var executionResult))
        {
            viewModel.ApplyExecutionUnavailable();
            return;
        }

        viewModel.ApplyExecutionResult(executionResult);
        var message = BuildExecutionNotificationMessage(executionResult);
        var severity = executionResult.FailedBundleCount > 0
            ? AppNotificationSeverity.Warning
            : AppNotificationSeverity.Success;
        var title = executionResult.FailedBundleCount > 0
            ? GetString(ResourceKeys.AppNotificationWarningTitle)
            : GetString(ResourceKeys.AppNotificationSuccessTitle);
        ShowNotification(
            title,
            message,
            severity);
    }

    private void PersistWorkspaceExportSettings(WorkspaceExportSettingsSnapshotDto exportSettings)
    {
        var browserState = _workspaceContextService.GetCurrentWorkspaceBrowserState()
                           ?? CreateEmptyBrowserState(exportSettings);
        _workspaceContextService.UpdateCurrentWorkspaceBrowserState(browserState with
        {
            ExportSettings = exportSettings
        });
    }

    private static WorkspaceBrowserStateSnapshotDto CreateEmptyBrowserState(
        WorkspaceExportSettingsSnapshotDto exportSettings)
    {
        return new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null,
            LastBundleId: null,
            LastAssetId: null,
            OpenAssetEditorWindows: null,
            ExportSettings: exportSettings);
    }

    private string BuildExecutionNotificationMessage(WorkspaceSaveExecutionResultDto executionResult)
    {
        var message = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogCompletedMessageFormat,
            executionResult.WrittenBundleCount,
            executionResult.FailedBundleCount,
            executionResult.ExportManifestPath ?? string.Empty);
        var failureDetail = BuildFailureDetail(executionResult);
        return string.IsNullOrWhiteSpace(failureDetail)
            ? message
            : string.Concat(message, Environment.NewLine, failureDetail);
    }

    private bool TryGetValue<T>(Result<T> result, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;
        if (!result.IsCancelled)
        {
            AppendResultFailure(result.Errors);
        }

        return false;
    }

    private bool TryResolveWorkspaceScope(
        out WorkspaceSummaryDto workspaceSummary,
        out string? workspaceKey,
        out string? workspaceDataRootPath)
    {
        var resolvedWorkspaceSummary = _workspaceContextService.CurrentWorkspaceSummary
                                       ?? _owner.ViewModel.WorkspaceShell.WorkspaceSummary;
        if (resolvedWorkspaceSummary is null)
        {
            workspaceSummary = null!;
            workspaceKey = null;
            workspaceDataRootPath = null;
            return false;
        }

        workspaceSummary = resolvedWorkspaceSummary;
        if (_workspaceContextService.TryGetCurrentWorkspaceEntry(out var entry) && entry is not null)
        {
            workspaceKey = entry.WorkspaceKey;
            workspaceDataRootPath = entry.WorkspaceDataRootPath;
            return true;
        }

        workspaceKey = workspaceSummary.WorkspaceKey;
        workspaceDataRootPath = workspaceSummary.WorkspaceDataRootPath;
        return true;
    }

    private static string? ResolveWorkspaceExportDirectory(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath)
    {
        var dataRootPath = string.IsNullOrWhiteSpace(workspaceDataRootPath)
            ? workspaceSummary.WorkspaceDataRootPath
            : workspaceDataRootPath;
        return string.IsNullOrWhiteSpace(dataRootPath)
            ? null
            : Path.Combine(dataRootPath.Trim(), "exports");
    }

    private void AppendResultFailure(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        ShowNotification(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            FormatError(errors[0]),
            AppNotificationSeverity.Error);
    }

    private void ShowNotification(string title, string message, AppNotificationSeverity severity)
    {
        var notificationService = _activeWindow?.NotificationService ?? _appNotificationService;
        notificationService.Show(title, message, severity);
    }

    private string FormatError(AppError error)
    {
        if (_failureDisplayFormatter.TryFormat(error, out var exportFailureMessage))
        {
            return exportFailureMessage;
        }

        return error.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                error.MessageToken,
                error.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : GetString(error.MessageToken);
    }

    private string? BuildFailureDetail(WorkspaceSaveExecutionResultDto executionResult)
    {
        var failedBundle = executionResult.BundleResults.FirstOrDefault(static bundle =>
            bundle.Status == WorkspaceSaveBundleExecutionStatusDto.Failed);
        if (failedBundle is null)
        {
            return null;
        }

        return _failureDisplayFormatter.Format(failedBundle);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private sealed class WorkspaceSaveExecutionProgressBridge : IProgress<WorkspaceSaveExecutionProgressDto>
    {
        private readonly IBackgroundWorkProgress _progress;
        private readonly WorkspaceExportDialogViewModel _viewModel;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ILocalizationService _localizationService;

        public WorkspaceSaveExecutionProgressBridge(
            IBackgroundWorkProgress progress,
            WorkspaceExportDialogViewModel viewModel,
            DispatcherQueue dispatcherQueue,
            ILocalizationService localizationService)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public void Report(WorkspaceSaveExecutionProgressDto value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var totalCount = Math.Max(1, value.TotalBundleCount);
            var completedCount = Math.Clamp(value.CompletedBundleCount, 0, totalCount);
            var detail = string.IsNullOrWhiteSpace(value.CurrentBundleDisplayName)
                ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogStatusPreparing)
                : _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceExportDialogStatusBundleFormat,
                    value.CurrentBundleDisplayName,
                    value.SucceededBundleCount,
                    value.FailedBundleCount);
            _progress.ReportProgress(completedCount, totalCount, detail);
            _dispatcherQueue.TryEnqueue(() =>
                _viewModel.ApplyExecutionProgress(completedCount, totalCount, detail));
        }
    }
}
