using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed class BundleExplorerWorkspaceExportCoordinator
{
    private readonly BundleExplorerPage _owner;
    private readonly IWorkspaceBatchOperationsGateway _workspaceBatchOperationsGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly ILocalizationService _localizationService;
    private WorkspaceExportWindow? _activeWindow;

    public BundleExplorerWorkspaceExportCoordinator(
        BundleExplorerPage owner,
        IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
        IWorkspaceContextService workspaceContextService,
        ILocalizationService localizationService)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task ShowAsync()
    {
        if (_owner.XamlRoot is null || _activeWindow is not null)
        {
            _activeWindow?.BringToFront();
            return;
        }

        if (_workspaceContextService.CurrentWorkspaceSummary is not { } workspaceSummary)
        {
            return;
        }

        var changeSummaryResult = await _workspaceBatchOperationsGateway.GetWorkspaceChangeSummaryAsync(
            new GetWorkspaceChangeSummaryQuery(workspaceSummary.WorkspaceId));
        if (!TryGetValue(changeSummaryResult, out WorkspaceChangeSummaryDto? changeSummary))
        {
            return;
        }

        var savePreviewResult = await _workspaceBatchOperationsGateway.GetWorkspaceSavePreviewAsync(
            new GetWorkspaceSavePreviewQuery(
                workspaceSummary.WorkspaceId,
                WorkspaceKey: workspaceSummary.WorkspaceKey,
                WorkspaceDataRootPath: workspaceSummary.WorkspaceDataRootPath));
        if (!TryGetValue(savePreviewResult, out var savePreview))
        {
            return;
        }

        var viewModel = new WorkspaceExportDialogViewModel(_localizationService, changeSummary, savePreview);
        var windowNotificationService = new AppNotificationService();
        var window = new WorkspaceExportWindow(viewModel, windowNotificationService);
        App.CurrentApp.RegisterAuxiliaryWindow(window, _owner.TryGetHostWindow(out var hostWindow) ? hostWindow : null);
        _activeWindow = window;
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is WorkspaceExportWindow window)
        {
            window.Closed -= OnWindowClosed;
            if (ReferenceEquals(_activeWindow, window))
            {
                _activeWindow = null;
            }
        }
    }

    private bool TryGetValue<T>(Result<T> result, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;
        return false;
    }
}
