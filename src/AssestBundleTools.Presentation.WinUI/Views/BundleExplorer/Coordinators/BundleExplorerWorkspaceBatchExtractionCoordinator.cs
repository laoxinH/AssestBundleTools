using System.IO;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerWorkspaceBatchExtractionCoordinator
    {
        private static readonly TimeSpan PreviewRefreshDebounceDelay = TimeSpan.FromMilliseconds(180);

        private readonly BundleExplorerPage _owner;
        private readonly IWorkspaceBatchOperationsGateway _workspaceBatchOperationsGateway;
        private readonly IWorkspaceContextService _workspaceContextService;
        private readonly IFilePickerService _filePickerService;
        private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
        private readonly IAppLogService _appLogService;
        private readonly IAppNotificationService _appNotificationService;
        private readonly ILocalizationService _localizationService;
        private readonly WorkspaceBatchExtractionProgressFormatter _batchExtractionProgressFormatter;
        private WorkspaceBatchExtractionWindow? _activeBatchExtractionWindow;
        private CancellationTokenSource? _previewRefreshCancellationSource;
        private int _previewRequestVersion;

        public BundleExplorerWorkspaceBatchExtractionCoordinator(
            BundleExplorerPage owner,
            IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
            IWorkspaceContextService workspaceContextService,
            IFilePickerService filePickerService,
            IBackgroundWorkScheduler backgroundWorkScheduler,
            IAppLogService appLogService,
            IAppNotificationService appNotificationService,
            ILocalizationService localizationService)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
            _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
            _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
            _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _batchExtractionProgressFormatter = new WorkspaceBatchExtractionProgressFormatter(_localizationService);
        }

        public async Task ShowAsync()
        {
            if (_owner.XamlRoot is null ||
                !TryResolveWorkspaceScope(out var workspaceSummary, out var workspaceKey, out var workspaceDataRootPath))
            {
                return;
            }

            var bundleNodes = _owner.ViewModel.BundlePane.BundleGroups
                .SelectMany(static group => group.Bundles)
                .ToArray();
            if (bundleNodes.Length == 0)
            {
                ShowNotification(
                    GetString(ResourceKeys.AppNotificationInfoTitle),
                    GetString(ResourceKeys.WorkspaceBatchExtractionDialogNoBundlesMessage),
                    AppNotificationSeverity.Informational);
                return;
            }

            var viewModel = new WorkspaceBatchExtractionDialogViewModel(
                _localizationService,
                bundleNodes,
                _owner.ViewModel.DocumentWorkspace.ActiveBundleTab?.BundleId);

            if (_activeBatchExtractionWindow is not null)
            {
                _activeBatchExtractionWindow.BringToFront();
                return;
            }

            var windowNotificationService = new AppNotificationService();
            var window = new WorkspaceBatchExtractionWindow(viewModel, windowNotificationService);
            App.CurrentApp.RegisterAuxiliaryWindow(
                window,
                _owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null);
            _activeBatchExtractionWindow = window;
            window.Closed += OnBatchExtractionWindowClosed;

            void OnViewModelStateChanged(object? sender, EventArgs args)
            {
                SchedulePreviewRefresh(viewModel, workspaceSummary.WorkspaceId, workspaceDataRootPath);
            }

            viewModel.StateChanged += OnViewModelStateChanged;
            var submissionTask = window.ShowForSubmissionAsync();
            SchedulePreviewRefreshAfterWindowShown(viewModel, workspaceSummary.WorkspaceId, workspaceDataRootPath);
            var submission = await submissionTask;
            viewModel.StateChanged -= OnViewModelStateChanged;
            CancelPendingPreviewRefresh();
            window.Closed -= OnBatchExtractionWindowClosed;
            if (ReferenceEquals(_activeBatchExtractionWindow, window))
            {
                _activeBatchExtractionWindow = null;
            }

            if (submission is null)
            {
                return;
            }

            await ExecuteSubmittedExtractionAsync(
                workspaceSummary,
                workspaceKey,
                workspaceDataRootPath,
                submission);

            void OnBatchExtractionWindowClosed(object sender, WindowEventArgs args)
            {
                if (ReferenceEquals(_activeBatchExtractionWindow, window))
                {
                    _activeBatchExtractionWindow = null;
                }

                CancelPendingPreviewRefresh();
            }
        }

        private void SchedulePreviewRefreshAfterWindowShown(
            WorkspaceBatchExtractionDialogViewModel viewModel,
            Domain.Workspace.WorkspaceId workspaceId,
            string? workspaceDataRootPath)
        {
            if (_owner.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Low,
                    () => SchedulePreviewRefresh(viewModel, workspaceId, workspaceDataRootPath)))
            {
                return;
            }

            SchedulePreviewRefresh(viewModel, workspaceId, workspaceDataRootPath);
        }

        private void SchedulePreviewRefresh(
            WorkspaceBatchExtractionDialogViewModel viewModel,
            Domain.Workspace.WorkspaceId workspaceId,
            string? workspaceDataRootPath)
        {
            var version = Interlocked.Increment(ref _previewRequestVersion);
            var cancellationSource = new CancellationTokenSource();
            var previousCancellationSource = Interlocked.Exchange(
                ref _previewRefreshCancellationSource,
                cancellationSource);
            previousCancellationSource?.Cancel();
            _ = RefreshPreviewAsync(viewModel, workspaceId, workspaceDataRootPath, version, cancellationSource);
        }

        private async Task RefreshPreviewAsync(
            WorkspaceBatchExtractionDialogViewModel viewModel,
            Domain.Workspace.WorkspaceId workspaceId,
            string? workspaceDataRootPath,
            int version,
            CancellationTokenSource cancellationSource)
        {
            try
            {
                var cancellationToken = cancellationSource.Token;
                await Task.Delay(PreviewRefreshDebounceDelay, cancellationToken);

                var selectedBundleIds = viewModel.SelectedBundleIds;
                if (selectedBundleIds.Count == 0)
                {
                    viewModel.ApplyPreviewUnavailable();
                    return;
                }

                viewModel.SetPreviewLoading();
                var selectedCategories = viewModel.SelectedCategories.Count == 0
                    ? new[] { WorkspaceBatchExtractionCategoryDto.All }
                    : viewModel.SelectedCategories;
                Result<WorkspaceBatchExtractionPreviewDto> result;
                try
                {
                    result = await _backgroundWorkScheduler.RunAsync(
                        new BackgroundWorkRequest<Result<WorkspaceBatchExtractionPreviewDto>>(
                            BackgroundWorkProfile.TransientRead,
                            context => _workspaceBatchOperationsGateway.GetWorkspaceBatchExtractionPreviewAsync(
                                new GetWorkspaceBatchExtractionPreviewQuery(
                                    workspaceId,
                                    WorkspaceDataRootPath: workspaceDataRootPath,
                                    SelectedBundleIds: selectedBundleIds,
                                    SelectedCategories: selectedCategories),
                                context.CancellationToken))
                        {
                            SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                            Priority = BackgroundWorkPriority.Background,
                            WorkspaceId = workspaceId.Value,
                            ShowInStatusBar = false,
                            CanCancel = true,
                            ReleaseProfileGateOnCallerCancellation = true
                        },
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    if (version == _previewRequestVersion)
                    {
                        viewModel.ApplyPreviewUnavailable();
                    }

                    return;
                }

                if (version != _previewRequestVersion)
                {
                    return;
                }

                if (result.IsSuccess && result.Value is not null)
                {
                    viewModel.ApplyPreview(result.Value);
                    return;
                }

                if (!result.IsCancelled)
                {
                    viewModel.ApplyPreviewUnavailable();
                }
            }
            finally
            {
                Interlocked.CompareExchange(
                    ref _previewRefreshCancellationSource,
                    null,
                    cancellationSource);
                cancellationSource.Dispose();
            }
        }

        private void CancelPendingPreviewRefresh()
        {
            Interlocked.Increment(ref _previewRequestVersion);
            var cancellationSource = Interlocked.Exchange(ref _previewRefreshCancellationSource, null);
            cancellationSource?.Cancel();
        }

        private async Task ExecuteSubmittedExtractionAsync(
            WorkspaceSummaryDto workspaceSummary,
            string? workspaceKey,
            string? workspaceDataRootPath,
            WorkspaceBatchExtractionDialogSubmission submission)
        {
            var outputDirectory = await ResolveSubmittedOutputDirectoryAsync(
                workspaceSummary,
                workspaceDataRootPath,
                submission);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            var commandTemplate = new ExtractWorkspaceAssetsCommand(
                workspaceSummary.WorkspaceId,
                OutputDirectory: outputDirectory,
                WorkspaceKey: workspaceKey,
                WorkspaceDataRootPath: workspaceDataRootPath,
                SelectedBundleIds: submission.SelectedBundleIds,
                SelectedCategories: submission.SelectedCategories);
            Result<WorkspaceBatchExtractionResultDto> result;
            try
            {
                result = await _backgroundWorkScheduler.RunAsync(
                    new BackgroundWorkRequest<Result<WorkspaceBatchExtractionResultDto>>(
                        BackgroundWorkProfile.BatchExtraction,
                        context =>
                        {
                            var command = commandTemplate with
                            {
                                Progress = new WorkspaceBatchExtractionProgressBridge(
                                    context.Progress,
                                    _batchExtractionProgressFormatter)
                            };
                            return _workspaceBatchOperationsGateway.ExtractWorkspaceAssetsAsync(
                                command,
                                context.CancellationToken);
                        })
                    {
                        SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                        Priority = BackgroundWorkPriority.Visible,
                        WorkspaceId = commandTemplate.WorkspaceId.Value,
                        WorkspaceKey = commandTemplate.WorkspaceKey,
                        StatusTitle = GetString(ResourceKeys.WorkspaceBatchExtractionDialogStatusTitle),
                        CanCancel = true
                    });
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                ShowNotification(
                    GetString(ResourceKeys.AppNotificationErrorTitle),
                    GetString(ResourceKeys.CommonUnexpectedFailure),
                    AppNotificationSeverity.Error);
                return;
            }

            if (result.IsCancelled)
            {
                return;
            }

            if (!TryGetValue(result, out var extractionResult))
            {
                return;
            }

            var message = _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchExtractionDialogCompletedMessageFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount,
                extractionResult.ExtractedSpineGroupCount,
                extractionResult.SuggestedOutputDirectory);
            _appLogService.AppendInfo(message);
            ShowNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                message,
                AppNotificationSeverity.Success);
        }

        private async Task<string?> ResolveSubmittedOutputDirectoryAsync(
            WorkspaceSummaryDto workspaceSummary,
            string? workspaceDataRootPath,
            WorkspaceBatchExtractionDialogSubmission submission)
        {
            if (!submission.UseWorkspaceExtractionDirectory)
            {
                return await _filePickerService.PickAssetBatchExtractionOutputDirectoryAsync();
            }

            var dataRootPath = string.IsNullOrWhiteSpace(workspaceDataRootPath)
                ? workspaceSummary.WorkspaceDataRootPath
                : workspaceDataRootPath;
            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                ShowNotification(
                    GetString(ResourceKeys.AppNotificationErrorTitle),
                    GetString(ResourceKeys.WorkspaceBatchExtractionDialogWorkspaceExtractionDirectoryUnavailable),
                    AppNotificationSeverity.Error);
                return null;
            }

            return Path.Combine(dataRootPath.Trim(), "extractions");
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
            if (_workspaceContextService.TryGetCurrentWorkspaceEntry(out var entry) &&
                entry is not null)
            {
                workspaceKey = entry.WorkspaceKey;
                workspaceDataRootPath = entry.WorkspaceDataRootPath;
                return true;
            }

            workspaceKey = workspaceSummary.WorkspaceKey;
            workspaceDataRootPath = workspaceSummary.WorkspaceDataRootPath;
            return true;
        }

        private bool TryGetValue<T>(Result<T> result, out T value)
        {
            if (result.IsSuccess && result.Value is not null)
            {
                value = result.Value;
                return true;
            }

            value = default!;
            if (!result.IsCancelled)
            {
                AppendResultFailure(result.Errors);
            }

            return false;
        }

        private void AppendResultFailure(IReadOnlyList<AppError> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            var message = FormatError(errors[0]);
            _appLogService.AppendError(message);
            ShowNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                message,
                AppNotificationSeverity.Error);
        }

        private void ShowNotification(string title, string message, AppNotificationSeverity severity)
        {
            var notificationService = _activeBatchExtractionWindow?.NotificationService ?? _appNotificationService;
            notificationService.Show(title, message, severity);
        }

        private string FormatError(AppError error) =>
            error.Parameters.Count > 0
                ? _localizationService.GetFormattedString(
                    error.MessageToken,
                    error.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
                : GetString(error.MessageToken);

        private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

        private sealed class WorkspaceBatchExtractionProgressBridge : IProgress<WorkspaceBatchExtractionProgressDto>
        {
            private readonly IBackgroundWorkProgress _progress;
            private readonly WorkspaceBatchExtractionProgressFormatter _formatter;

            public WorkspaceBatchExtractionProgressBridge(
                IBackgroundWorkProgress progress,
                WorkspaceBatchExtractionProgressFormatter formatter)
            {
                _progress = progress ?? throw new ArgumentNullException(nameof(progress));
                _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            }

            public void Report(WorkspaceBatchExtractionProgressDto value)
            {
                ArgumentNullException.ThrowIfNull(value);

                var totalCount = Math.Max(1, value.TotalItemCount);
                var completedCount = Math.Clamp(value.CompletedItemCount, 0, totalCount);
                _progress.ReportProgress(
                    completedCount,
                    totalCount,
                    _formatter.Format(value));
            }
        }
    }
}
