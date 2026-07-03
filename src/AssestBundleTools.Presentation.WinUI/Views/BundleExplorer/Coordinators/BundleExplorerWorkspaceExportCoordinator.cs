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
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private WorkspaceExportWindow? _activeWindow;

    public BundleExplorerWorkspaceExportCoordinator(
        BundleExplorerPage owner,
        IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
        IWorkspaceContextService workspaceContextService,
        IResourceProcessorCatalogService resourceProcessorCatalogService,
        IFilePickerService filePickerService,
        IBackgroundWorkScheduler backgroundWorkScheduler,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _resourceProcessorCatalogService = resourceProcessorCatalogService ?? throw new ArgumentNullException(nameof(resourceProcessorCatalogService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
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
                WorkspaceDataRootPath: workspaceDataRootPath));
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
            processorCatalogEntries);
        var windowNotificationService = new AppNotificationService();
        var window = new WorkspaceExportWindow(viewModel, windowNotificationService);
        App.CurrentApp.RegisterAuxiliaryWindow(window, _owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null);
        _activeWindow = window;
        window.StartExportRequested += OnStartExportRequested;
        window.Closed += OnWindowClosed;
        window.Activate();

        async void OnStartExportRequested(object? sender, EventArgs args)
        {
            await ExecuteExportAsync(workspaceSummary, workspaceKey, workspaceDataRootPath, viewModel);
        }

        void OnWindowClosed(object sender, WindowEventArgs args)
        {
            window.StartExportRequested -= OnStartExportRequested;
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
        viewModel.SetExecuting();
        var commandTemplate = new SaveWorkspaceAsNewBundleCommand(
            workspaceSummary.WorkspaceId,
            OutputDirectory: outputDirectory,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            SelectedBundleIds: submission.SelectedBundleIds,
            ProcessorSelections: submission.ProcessorSelections);

        Result<WorkspaceSaveExecutionResultDto> result;
        try
        {
            result = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceSaveExecutionResultDto>>(
                    BackgroundWorkProfile.SaveRebuild,
                    context =>
                    {
                        var command = commandTemplate with
                        {
                            Progress = new WorkspaceSaveExecutionProgressBridge(context.Progress, _localizationService)
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
        var message = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogCompletedMessageFormat,
            executionResult.WrittenBundleCount,
            executionResult.FailedBundleCount,
            executionResult.ExportManifestPath ?? string.Empty);
        ShowNotification(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            message,
            AppNotificationSeverity.Success);
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

    private string FormatError(AppError error) =>
        error.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                error.MessageToken,
                error.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : GetString(error.MessageToken);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private sealed class WorkspaceSaveExecutionProgressBridge : IProgress<WorkspaceSaveExecutionProgressDto>
    {
        private readonly IBackgroundWorkProgress _progress;
        private readonly ILocalizationService _localizationService;

        public WorkspaceSaveExecutionProgressBridge(
            IBackgroundWorkProgress progress,
            ILocalizationService localizationService)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
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
        }
    }
}
