using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
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
            if (_owner.XamlRoot is null || !TryResolveWorkspaceScope(out var workspaceSummary, out var workspaceKey))
            {
                return;
            }

            if (_activeWindow is not null)
            {
                _activeWindow.BringToFront();
                return;
            }

            var bundleNodes = _owner.ViewModel.BundlePane.BundleGroups
                .SelectMany(static group => group.Bundles)
                .ToArray();
            var viewModel = new WorkspaceBatchReplacementDialogViewModel(_localizationService, bundleNodes);
            var window = new WorkspaceBatchReplacementWindow(viewModel);
            _activeWindow = window;
            window.SourceDirectoryRequested += OnSourceDirectoryRequested;
            window.RefreshRequested += OnRefreshRequested;
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

            void OnWindowClosed(object sender, WindowEventArgs args)
            {
                window.SourceDirectoryRequested -= OnSourceDirectoryRequested;
                window.RefreshRequested -= OnRefreshRequested;
                window.Closed -= OnWindowClosed;
                if (ReferenceEquals(_activeWindow, window))
                {
                    _activeWindow = null;
                }

                CancelPendingPlan();
            }
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
                    _appNotificationService.Show(
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

        private bool TryResolveWorkspaceScope(out WorkspaceSummaryDto workspaceSummary, out string? workspaceKey)
        {
            var resolvedWorkspaceSummary = _workspaceContextService.CurrentWorkspaceSummary
                                           ?? _owner.ViewModel.WorkspaceShell.WorkspaceSummary;
            if (resolvedWorkspaceSummary is null)
            {
                workspaceSummary = null!;
                workspaceKey = null;
                return false;
            }

            workspaceSummary = resolvedWorkspaceSummary;
            workspaceKey = _workspaceContextService.TryGetCurrentWorkspaceEntry(out var entry) && entry is not null
                ? entry.WorkspaceKey
                : workspaceSummary.WorkspaceKey;
            return true;
        }

        private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
    }
}
