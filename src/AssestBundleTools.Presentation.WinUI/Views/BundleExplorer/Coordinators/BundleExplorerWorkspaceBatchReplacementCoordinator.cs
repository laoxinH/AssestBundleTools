using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerWorkspaceBatchReplacementCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private readonly IWorkspaceBatchOperationsGateway _workspaceBatchOperationsGateway;
        private readonly IWorkspaceContextService _workspaceContextService;
        private readonly IFilePickerService _filePickerService;
        private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
        private readonly IAppNotificationService _appNotificationService;
        private readonly ILocalizationService _localizationService;
        private WorkspaceBatchReplacementWindow? _activeWindow;
        private CancellationTokenSource? _planCancellationSource;
        private int _planRequestVersion;

        public BundleExplorerWorkspaceBatchReplacementCoordinator(
            BundleExplorerPage owner,
            IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
            IWorkspaceContextService workspaceContextService,
            IFilePickerService filePickerService,
            IBackgroundWorkScheduler backgroundWorkScheduler,
            IAppNotificationService appNotificationService,
            ILocalizationService localizationService)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
            _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
            _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public async Task ShowAsync()
        {
            if (_owner.XamlRoot is null ||
                TryActivateExistingWindow() ||
                !TryResolveWorkspaceScope(out var workspaceSummary, out var workspaceKey, out var workspaceDataRootPath))
            {
                return;
            }

            var bundleNodes = _owner.ViewModel.BundlePane.BundleGroups
                .SelectMany(static group => group.Bundles)
                .ToArray();
            var viewModel = new WorkspaceBatchReplacementDialogViewModel(_localizationService, bundleNodes);
            var windowNotificationService = new AppNotificationService();
            var window = new WorkspaceBatchReplacementWindow(viewModel, windowNotificationService);
            App.CurrentApp.RegisterAuxiliaryWindow(
                window,
                _owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null);
            _activeWindow = window;
            window.SourceDirectoryRequested += OnSourceDirectoryRequested;
            window.RefreshRequested += OnRefreshRequested;
            window.StartReplacementRequested += OnStartReplacementRequested;
            window.Closed += OnWindowClosed;
            window.Activate();

            async void OnSourceDirectoryRequested(object? sender, EventArgs args)
            {
                var directory = await _filePickerService.PickWorkspaceBatchReplacementSourceDirectoryAsync();
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                viewModel.SetSourceDirectory(directory);
                await RefreshPlanAsync(workspaceSummary, workspaceKey, viewModel);
            }

            async void OnRefreshRequested(object? sender, EventArgs args)
            {
                await RefreshPlanAsync(workspaceSummary, workspaceKey, viewModel);
            }

            async void OnStartReplacementRequested(object? sender, EventArgs args)
            {
                await ExecuteReplacementAsync(workspaceSummary, workspaceKey, workspaceDataRootPath, viewModel);
            }

            void OnWindowClosed(object sender, WindowEventArgs args)
            {
                window.SourceDirectoryRequested -= OnSourceDirectoryRequested;
                window.RefreshRequested -= OnRefreshRequested;
                window.StartReplacementRequested -= OnStartReplacementRequested;
                window.Closed -= OnWindowClosed;
                if (ReferenceEquals(_activeWindow, window))
                {
                    _activeWindow = null;
                }

                CancelPendingPlan();
            }
        }

        private bool TryActivateExistingWindow()
        {
            if (_activeWindow is not null)
            {
                _activeWindow.BringToFront();
                return true;
            }

            if (_owner.TryGetHostWindow(out var ownerWindow) &&
                App.CurrentApp.TryGetOwnedAuxiliaryWindow<WorkspaceBatchReplacementWindow>(ownerWindow, out var existingWindow))
            {
                existingWindow.BringToFront();
                return true;
            }

            return false;
        }

        private async Task ExecuteReplacementAsync(
            WorkspaceSummaryDto workspaceSummary,
            string? workspaceKey,
            string? workspaceDataRootPath,
            WorkspaceBatchReplacementDialogViewModel viewModel)
        {
            var selectedCandidates = viewModel.GetSelectedCandidates();
            if (selectedCandidates.Count == 0 || string.IsNullOrWhiteSpace(viewModel.SourceDirectory))
            {
                return;
            }

            viewModel.SetExecuting();
            Result<WorkspaceBatchReplacementResultDto> result;
            try
            {
                result = await _backgroundWorkScheduler.RunAsync(
                    new BackgroundWorkRequest<Result<WorkspaceBatchReplacementResultDto>>(
                        BackgroundWorkProfile.BatchReplacement,
                        context => _workspaceBatchOperationsGateway.ExecuteWorkspaceBatchReplacementAsync(
                            new ExecuteWorkspaceBatchReplacementCommand(
                                workspaceSummary.WorkspaceId,
                                workspaceKey,
                                workspaceDataRootPath,
                                viewModel.SourceDirectory,
                                WorkspaceBatchReplacementMatchingMode.Auto,
                                selectedCandidates,
                                new WorkspaceBatchReplacementProgressBridge(context.Progress, _localizationService)),
                            context.CancellationToken))
                    {
                        SemanticKind = BackgroundWorkSemanticKind.WorkspaceMutation,
                        Priority = BackgroundWorkPriority.Visible,
                        WorkspaceId = workspaceSummary.WorkspaceId.Value,
                        WorkspaceKey = workspaceKey,
                        StatusTitle = GetString(ResourceKeys.WorkspaceBatchReplacementDialogStatusTitle),
                        TotalCount = selectedCandidates.Count,
                        CanCancel = true
                    });
            }
            catch (OperationCanceledException)
            {
                viewModel.ApplyExecutionUnavailable();
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
                viewModel.ApplyExecutionUnavailable();
                return;
            }

            if (!TryGetValue(result, out var replacementResult))
            {
                viewModel.ApplyExecutionUnavailable();
                return;
            }

            viewModel.ApplyExecutionResult(replacementResult);
            var message = _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchReplacementDialogCompletedMessageFormat,
                replacementResult.ReplacedAssetCount,
                replacementResult.SkippedAssetCount,
                replacementResult.FailedAssetCount);
            ShowNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                message,
                AppNotificationSeverity.Success);
        }

        private async Task RefreshPlanAsync(
            WorkspaceSummaryDto workspaceSummary,
            string? workspaceKey,
            WorkspaceBatchReplacementDialogViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.SourceDirectory))
            {
                return;
            }

            var version = Interlocked.Increment(ref _planRequestVersion);
            var cancellationSource = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _planCancellationSource, cancellationSource);
            previous?.Cancel();

            try
            {
                viewModel.SetPlanLoading();
                var result = await _backgroundWorkScheduler.RunAsync(
                    new BackgroundWorkRequest<Result<WorkspaceBatchReplacementPlanDto>>(
                        BackgroundWorkProfile.TransientRead,
                        context => _workspaceBatchOperationsGateway.GetWorkspaceBatchReplacementPlanAsync(
                            new GetWorkspaceBatchReplacementPlanQuery(
                                workspaceSummary.WorkspaceId,
                                workspaceKey,
                                viewModel.SourceDirectory,
                                WorkspaceBatchReplacementMatchingMode.Auto),
                            context.CancellationToken))
                    {
                        SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                        Priority = BackgroundWorkPriority.Background,
                        WorkspaceId = workspaceSummary.WorkspaceId.Value,
                        WorkspaceKey = workspaceKey,
                        ShowInStatusBar = false,
                        CanCancel = true,
                        ReleaseProfileGateOnCallerCancellation = true
                    },
                    cancellationSource.Token);

                if (version != _planRequestVersion)
                {
                    return;
                }

                if (result.IsSuccess && result.Value is not null)
                {
                    viewModel.ApplyPlan(result.Value);
                    return;
                }

                if (!result.IsCancelled)
                {
                    viewModel.ApplyPlanUnavailable();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (version == _planRequestVersion)
                {
                    viewModel.ApplyPlanUnavailable();
                    ShowNotification(
                        GetString(ResourceKeys.AppNotificationErrorTitle),
                        GetString(ResourceKeys.CommonUnexpectedFailure),
                        AppNotificationSeverity.Error);
                }
            }
            finally
            {
                Interlocked.CompareExchange(ref _planCancellationSource, null, cancellationSource);
                cancellationSource.Dispose();
            }
        }

        private void CancelPendingPlan()
        {
            Interlocked.Increment(ref _planRequestVersion);
            var cancellationSource = Interlocked.Exchange(ref _planCancellationSource, null);
            cancellationSource?.Cancel();
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
            ShowNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                message,
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

        private sealed class WorkspaceBatchReplacementProgressBridge : IProgress<WorkspaceBatchReplacementProgressDto>
        {
            private readonly IBackgroundWorkProgress _progress;
            private readonly ILocalizationService _localizationService;

            public WorkspaceBatchReplacementProgressBridge(
                IBackgroundWorkProgress progress,
                ILocalizationService localizationService)
            {
                _progress = progress ?? throw new ArgumentNullException(nameof(progress));
                _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            }

            public void Report(WorkspaceBatchReplacementProgressDto value)
            {
                ArgumentNullException.ThrowIfNull(value);

                var totalCount = Math.Max(1, value.TotalItemCount);
                var completedCount = Math.Clamp(value.CompletedItemCount, 0, totalCount);
                var detail = string.IsNullOrWhiteSpace(value.ItemDisplayName)
                    ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceBatchReplacementDialogStatusPreparing)
                    : _localizationService.GetFormattedString(
                        ResourceKeys.WorkspaceBatchReplacementDialogStatusItemFormat,
                        value.ItemDisplayName,
                        value.SucceededItemCount,
                        value.FailedItemCount);
                _progress.ReportProgress(completedCount, totalCount, detail);
            }
        }
    }
}
