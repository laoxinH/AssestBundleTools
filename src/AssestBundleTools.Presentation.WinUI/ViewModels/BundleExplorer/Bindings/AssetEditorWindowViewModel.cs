using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 独立资源编辑窗口的资产文档组状态与资源级动作编排。
/// </summary>
public sealed class AssetEditorWindowViewModel : ObservableObject
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly Func<AssetDocumentTabViewModel, Task<bool>> _moveAssetDocumentToMainWindowAsync;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly BundleExplorerAssetPreviewLoadCoordinator _assetPreviewLoadCoordinator;
    private readonly BundleExplorerAssetModificationActionHandler _modificationActionHandler;
    private readonly InspectorInformationTextSet _inspectorInformationTextSet;
    private readonly InspectorPanelViewModel _emptyInspector;
    private bool _isBusy;

    public AssetEditorWindowViewModel(
        AssetDocumentTabViewModel initialAssetTab,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        Func<AssetDocumentTabViewModel, Task<bool>> moveAssetDocumentToMainWindowAsync)
    {
        ArgumentNullException.ThrowIfNull(initialAssetTab);
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        ArgumentNullException.ThrowIfNull(workspaceAssetOperationGateway);
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        ArgumentNullException.ThrowIfNull(appNotificationService);
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(workspaceContextService);
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        ArgumentNullException.ThrowIfNull(bundlePane);
        _moveAssetDocumentToMainWindowAsync = moveAssetDocumentToMainWindowAsync ?? throw new ArgumentNullException(nameof(moveAssetDocumentToMainWindowAsync));

        _operationFeedbackBridge = new BundleExplorerOperationFeedbackBridge(
            _appLogService,
            appNotificationService,
            _localizationService);
        _assetInspectorPresenter = new BundleExplorerAssetInspectorPresenter(
            bundlePane,
            _localizationService);
        var assetPreviewCoordinator = new BundleExplorerAssetPreviewCoordinator(
            _workspaceAssetInspectionGateway,
            workspaceContextService,
            _workspaceShell,
            _appLogService,
            _localizationService,
            _backgroundTaskRunner);
        _assetPreviewLoadCoordinator = new BundleExplorerAssetPreviewLoadCoordinator(
            assetPreviewCoordinator,
            () => DocumentGroup.ActiveAssetTab,
            _operationFeedbackBridge.FormatFirstErrorMessage);
        _modificationActionHandler = new BundleExplorerAssetModificationActionHandler(
            workspaceAssetOperationGateway,
            _appLogService,
            appNotificationService,
            _localizationService,
            filePickerService);
        _inspectorInformationTextSet = InspectorInformationTextSetFactory.Create(_localizationService);
        _emptyInspector = new InspectorPanelViewModel(_inspectorInformationTextSet);
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));

        DocumentGroup.AddAssetTab(initialAssetTab, activate: true);
        SyncActiveDocumentState();
    }

    public event EventHandler? EmptyGroupRequested;

    public AssetDocumentGroupViewModel DocumentGroup { get; } = new();

    public InspectorPanelViewModel Inspector => DocumentGroup.ActiveAssetTab?.Inspector ?? _emptyInspector;

    public string WindowTitle => _localizationService.GetFormattedString(
        ResourceKeys.AssetEditorWindowTitleFormat,
        DocumentGroup.ActiveAssetTab?.DisplayName ?? GetString(ResourceKeys.AssetEditorWindowTitleFallback));

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyActionStates();
            }
        }
    }

    public async Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null)
        {
            return;
        }

        var wasAlreadyActiveAssetTab = ReferenceEquals(DocumentGroup.ActiveAssetTab, assetTab);
        DocumentGroup.SetActiveAssetTab(assetTab);
        SyncActiveDocumentState();

        if (CanReuseLoadedAssetDocumentTabContent(assetTab))
        {
            _assetPreviewLoadCoordinator.LoadForegroundPreview(assetTab, promoteLoadingPreview: !wasAlreadyActiveAssetTab);
            _assetPreviewLoadCoordinator.PrewarmBackgroundPreviews(DocumentGroup.AssetTabs, assetTab);
            return;
        }

        await LoadAssetDocumentTabAsync(assetTab, promotePreviewAfterLoad: !wasAlreadyActiveAssetTab);
    }

    public async Task OpenResolvedAssetAsync(ResolvedAssetNavigationTarget? target)
    {
        if (target is null || IsBusy)
        {
            return;
        }

        if (DocumentGroup.TryGetAssetTab(target.BundleId, target.AssetId, out var existingAssetTab))
        {
            await ActivateAssetDocumentTabAsync(existingAssetTab);
            return;
        }

        var assetTab = CreateAssetDocumentTab(target.BundleId, target.AssetId, target.DisplayName);
        DocumentGroup.AddAssetTab(assetTab, activate: false);
        await ActivateAssetDocumentTabAsync(assetTab);
    }

    public async Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null)
        {
            return;
        }

        var wasActive = ReferenceEquals(DocumentGroup.ActiveAssetTab, assetTab);
        DocumentGroup.RemoveAssetTab(assetTab);
        if (!DocumentGroup.HasAssetTabs)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (wasActive)
        {
            await ActivateAssetDocumentTabAsync(DocumentGroup.ActiveAssetTab);
            return;
        }

        SyncActiveDocumentState();
    }

    public void ReorderAssetDocumentTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedTabs)
    {
        DocumentGroup.ReorderAssetTabs(orderedTabs);
    }

    public async Task MoveAssetDocumentToMainWindowAsync(AssetDocumentTabViewModel? assetTab)
    {
        var detachedAssetTab = await DetachAssetDocumentTabForExternalHostAsync(assetTab);
        if (detachedAssetTab is null)
        {
            return;
        }

        if (await _moveAssetDocumentToMainWindowAsync(detachedAssetTab))
        {
            await CompleteExternalAssetDocumentMoveAsync();
            return;
        }

        await RestoreDetachedAssetDocumentTabAsync(detachedAssetTab);
    }

    public Task MoveActiveAssetDocumentToMainWindowAsync() =>
        MoveAssetDocumentToMainWindowAsync(DocumentGroup.ActiveAssetTab);

    public async Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForExternalHostAsync(
        AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || IsBusy || !DocumentGroup.DetachAssetTab(assetTab))
        {
            return null;
        }

        if (DocumentGroup.HasAssetTabs)
        {
            await ActivateAssetDocumentTabAsync(DocumentGroup.ActiveAssetTab);
        }
        else
        {
            SyncActiveDocumentState();
        }

        return assetTab;
    }

    public async Task<bool> AttachAssetDocumentTabFromExternalHostAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || IsBusy)
        {
            return false;
        }

        if (DocumentGroup.TryGetAssetTab(assetTab.BundleId, assetTab.AssetId, out var existingAssetTab))
        {
            assetTab.CancelPreviewRequest();
            await ActivateAssetDocumentTabAsync(existingAssetTab);
            return true;
        }

        DocumentGroup.AddAssetTab(assetTab, activate: false);
        await ActivateAssetDocumentTabAsync(assetTab);
        return true;
    }

    public async Task RestoreDetachedAssetDocumentTabAsync(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        DocumentGroup.AddAssetTab(assetTab, activate: false);
        await ActivateAssetDocumentTabAsync(assetTab);
    }

    public async Task CompleteExternalAssetDocumentMoveAsync()
    {
        if (!DocumentGroup.HasAssetTabs)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        await ActivateAssetDocumentTabAsync(DocumentGroup.ActiveAssetTab);
    }

    public void OpenCurrentPreviewLocation()
    {
        if (!TryGetActiveAssetDocumentState(out _, out var details))
        {
            return;
        }

        var previewPayloadFilePath = Inspector.Preview.PreviewPayloadFilePath;
        if (string.IsNullOrWhiteSpace(previewPayloadFilePath))
        {
            return;
        }

        if (!_pathNavigationService.TryOpenContainingFolder(previewPayloadFilePath))
        {
            var message = _localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerNotificationPreviewOpenLocationFailed,
                details.Name);
            _appLogService.AppendWarning(message);
            _operationFeedbackBridge.ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationWarningTitle),
                message,
                AppNotificationSeverity.Warning);
        }
    }

    public async Task ExtractSelectedAssetAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.CanExtract,
                out var workspaceSummary,
                out _,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.ExtractSelectedAssetAsync(
            workspaceSummary,
            details,
            value => IsBusy = value,
            ReloadActiveAssetDocumentAsync,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task SelectWorkspaceReplacementSourceAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.CanReplaceFromWorkspace,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.SelectWorkspaceReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            ReapplyAssetDocumentState,
            value => IsBusy = value,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task SelectFileReplacementSourceAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.CanReplaceFromFile,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.SelectFileReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            ReapplyAssetDocumentState,
            value => IsBusy = value,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task ApplyPendingReplacementAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (assetTab, _) => assetTab.PendingWorkspaceReplacementCandidate is not null ||
                                         assetTab.PendingFileReplacementCandidate is not null,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.ApplyPendingReplacementAsync(
            workspaceSummary,
            assetTab,
            details,
            value => IsBusy = value,
            ReloadActiveAssetDocumentAsync,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task UndoAppliedReplacementAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => HasAppliedReplacement(details),
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.UndoAppliedReplacementAsync(
            workspaceSummary,
            assetTab,
            details,
            value => IsBusy = value,
            ReloadActiveAssetDocumentAsync,
            _operationFeedbackBridge.AppendResultFailure);
    }

    private AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName)
    {
        var tab = new AssetDocumentTabViewModel(bundleId, assetId, assetName, _inspectorInformationTextSet);
        tab.Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        return tab;
    }

    private async Task LoadAssetDocumentTabAsync(
        AssetDocumentTabViewModel assetTab,
        bool promotePreviewAfterLoad)
    {
        if (_workspaceShell.WorkspaceSummary is not { } workspaceSummary)
        {
            return;
        }

        var openResult = await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.OpenAssetInPaneAsync(
                new OpenAssetInPaneCommand(
                    WorkspaceId: workspaceSummary.WorkspaceId,
                    BundleId: assetTab.BundleId,
                    AssetId: assetTab.AssetId,
                    PaneId: assetTab.PaneId),
                token));
        if (!TryGetValue(openResult, out _))
        {
            return;
        }

        var detailsResult = await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
                new GetAssetDetailsQuery(
                    WorkspaceId: workspaceSummary.WorkspaceId,
                    BundleId: assetTab.BundleId,
                    AssetId: assetTab.AssetId),
                token));
        if (!TryGetValue(detailsResult, out var details))
        {
            return;
        }

        ReapplyAssetDocumentState(assetTab, details);
        _assetPreviewLoadCoordinator.LoadForegroundPreview(assetTab, promotePreviewAfterLoad);
        _assetPreviewLoadCoordinator.PrewarmBackgroundPreviews(DocumentGroup.AssetTabs, assetTab);
    }

    private async Task ReloadActiveAssetDocumentAsync()
    {
        if (DocumentGroup.ActiveAssetTab is not { } assetTab)
        {
            return;
        }

        assetTab.Inspector.Preview.ResetState();
        await LoadAssetDocumentTabAsync(assetTab, promotePreviewAfterLoad: true);
    }

    private void ReapplyAssetDocumentState(AssetDocumentTabViewModel assetTab, AssetDetailsDto details)
    {
        _assetInspectorPresenter.PresentAssetDocumentTab(
            assetTab,
            details,
            canEdit: ReferenceEquals(assetTab, DocumentGroup.ActiveAssetTab),
            isBusy: IsBusy);
        NotifyActionStates();
    }

    private void SyncActiveDocumentState()
    {
        foreach (var assetTab in DocumentGroup.AssetTabs)
        {
            assetTab.CanEdit = ReferenceEquals(assetTab, DocumentGroup.ActiveAssetTab);
        }

        NotifyActionStates();
        OnPropertyChanged(nameof(Inspector));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void NotifyActionStates()
    {
        if (TryGetActiveAssetDocumentState(out var assetTab, out var details))
        {
            _assetInspectorPresenter.SyncInspectorActionStates(Inspector, assetTab, details, IsBusy);
        }
        else
        {
            _assetInspectorPresenter.SyncInspectorActionStates(Inspector, null, null, IsBusy);
        }
    }

    private bool TryGetActiveAssetDocumentState(
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        assetTab = DocumentGroup.ActiveAssetTab;
        if (assetTab?.Details is null)
        {
            details = null;
            return false;
        }

        details = assetTab.Details;
        return true;
    }

    private bool TryGetActiveAssetCommandContext(
        Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        ArgumentNullException.ThrowIfNull(canExecute);

        workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (IsBusy ||
            workspaceSummary is null ||
            !TryGetActiveAssetDocumentState(out assetTab, out details) ||
            !assetTab.CanEdit ||
            !canExecute(assetTab, details))
        {
            workspaceSummary = null;
            assetTab = null;
            details = null;
            return false;
        }

        return true;
    }

    private bool TryGetValue<T>(Result<T> result, [NotNullWhen(true)] out T? value)
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

        _operationFeedbackBridge.AppendResultFailure(result.Errors);
        return false;
    }

    private static bool CanReuseLoadedAssetDocumentTabContent(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        return assetTab.Details is { } details &&
               details.BundleId.Equals(assetTab.BundleId) &&
               details.AssetId.Equals(assetTab.AssetId);
    }

    private static bool HasAppliedReplacement(AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
               !string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
