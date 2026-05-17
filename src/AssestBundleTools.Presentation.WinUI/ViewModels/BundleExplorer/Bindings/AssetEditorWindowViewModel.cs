using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
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
public sealed partial class AssetEditorWindowViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly ILocalizationService _localizationService;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly Func<AssetDocumentTabViewModel, Task<bool>> _moveAssetDocumentToMainWindowAsync;
    private readonly Func<AssetDocumentGroupTransfer, Task<bool>> _moveAssetDocumentGroupToMainWindowAsync;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly SpineAssetGroupInspectorPresenter _spineGroupInspectorPresenter;
    private readonly ISpineAssetGroupDetailsLoader _spineGroupDetailsLoader;
    private readonly BundleExplorerAssetPreviewLoadCoordinator _assetPreviewLoadCoordinator;
    private readonly AssetEditorWindowAssetActionCoordinator _assetActionCoordinator;
    private readonly WorkspaceOperationChangeRefreshCoordinator _operationChangeRefreshCoordinator;
    private readonly InspectorInformationTextSet _inspectorInformationTextSet;
    private readonly InspectorPanelViewModel _emptyInspector;
    private bool _isBusy;

    public AssetEditorWindowViewModel(
        AssetDocumentTabViewModel initialAssetTab,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IWorkspaceOperationChangeNotifier workspaceOperationChangeNotifier,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        Func<AssetDocumentTabViewModel, Task<bool>> moveAssetDocumentToMainWindowAsync,
        Func<AssetDocumentGroupTransfer, Task<bool>> moveAssetDocumentGroupToMainWindowAsync,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<int>? previewWorkerCountProvider = null)
        : this(
            CreateSingleAssetGroup(initialAssetTab),
            workspaceAssetInspectionGateway,
            workspaceAssetOperationGateway,
            workspaceOperationChangeNotifier,
            appLogService,
            appNotificationService,
            localizationService,
            filePickerService,
            workspaceContextService,
            backgroundTaskRunner,
            pathNavigationService,
            workspaceShell,
            bundlePane,
            moveAssetDocumentToMainWindowAsync,
            moveAssetDocumentGroupToMainWindowAsync,
            backgroundTaskStatusService,
            backgroundWorkScheduler,
            previewWorkerCountProvider)
    {
    }

    public AssetEditorWindowViewModel(
        AssetDocumentGroupTransfer initialAssetGroup,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IWorkspaceOperationChangeNotifier workspaceOperationChangeNotifier,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        Func<AssetDocumentTabViewModel, Task<bool>> moveAssetDocumentToMainWindowAsync,
        Func<AssetDocumentGroupTransfer, Task<bool>> moveAssetDocumentGroupToMainWindowAsync,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<int>? previewWorkerCountProvider = null)
    {
        ArgumentNullException.ThrowIfNull(initialAssetGroup);
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        ArgumentNullException.ThrowIfNull(workspaceAssetOperationGateway);
        ArgumentNullException.ThrowIfNull(workspaceOperationChangeNotifier);
        ArgumentNullException.ThrowIfNull(appLogService);
        ArgumentNullException.ThrowIfNull(appNotificationService);
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(workspaceContextService);
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        ArgumentNullException.ThrowIfNull(pathNavigationService);
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        ArgumentNullException.ThrowIfNull(bundlePane);
        _moveAssetDocumentToMainWindowAsync = moveAssetDocumentToMainWindowAsync ?? throw new ArgumentNullException(nameof(moveAssetDocumentToMainWindowAsync));
        _moveAssetDocumentGroupToMainWindowAsync = moveAssetDocumentGroupToMainWindowAsync ?? throw new ArgumentNullException(nameof(moveAssetDocumentGroupToMainWindowAsync));

        _operationFeedbackBridge = new BundleExplorerOperationFeedbackBridge(
            appLogService,
            appNotificationService,
            _localizationService);
        _assetInspectorPresenter = new BundleExplorerAssetInspectorPresenter(
            bundlePane,
            _localizationService);
        _spineGroupInspectorPresenter = new SpineAssetGroupInspectorPresenter(
            bundlePane,
            _localizationService);
        _spineGroupDetailsLoader = new SpineAssetGroupDetailsLoader(
            _workspaceAssetInspectionGateway,
            workspaceContextService,
            _workspaceShell,
            _backgroundTaskRunner,
            backgroundWorkScheduler,
            _localizationService);
        var assetPreviewCoordinator = new BundleExplorerAssetPreviewCoordinator(
            _workspaceAssetInspectionGateway,
            workspaceContextService,
            _workspaceShell,
            appLogService,
            _localizationService,
            _backgroundTaskRunner,
            previewWorkerCountProvider: previewWorkerCountProvider,
            backgroundWorkScheduler: backgroundWorkScheduler);
        _assetPreviewLoadCoordinator = new BundleExplorerAssetPreviewLoadCoordinator(
            assetPreviewCoordinator,
            () => DocumentGroup.ActiveAssetTab,
            _operationFeedbackBridge.FormatFirstErrorMessage);
        var modificationActionHandler = new BundleExplorerAssetModificationActionHandler(
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            _localizationService,
            filePickerService,
            _backgroundTaskRunner,
            backgroundTaskStatusService,
            workspaceOperationChangeNotifier,
            backgroundWorkScheduler);
        _assetActionCoordinator = new AssetEditorWindowAssetActionCoordinator(
            _workspaceShell,
            pathNavigationService,
            appLogService,
            _localizationService,
            _operationFeedbackBridge,
            modificationActionHandler,
            () => DocumentGroup.ActiveAssetTab,
            () => DocumentGroup.ActiveSpineGroupTab,
            () => Inspector,
            () => IsBusy,
            value => IsBusy = value,
            ReapplyAssetDocumentState);
        _inspectorInformationTextSet = InspectorInformationTextSetFactory.Create(_localizationService);
        _emptyInspector = new InspectorPanelViewModel(_inspectorInformationTextSet);
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));

        DocumentGroup.AddDocumentTabs(initialAssetGroup);
        SyncActiveDocumentState();
        _operationChangeRefreshCoordinator = new WorkspaceOperationChangeRefreshCoordinator(
            workspaceOperationChangeNotifier,
            () => _workspaceShell.WorkspaceSummary?.WorkspaceId,
            () => IsBusy,
            RefreshOperationAffectedStateAsync);
    }

    public event EventHandler? EmptyGroupRequested;

    public AssetDocumentGroupViewModel DocumentGroup { get; } = new();

    public InspectorPanelViewModel Inspector => DocumentGroup.ActiveDocumentTab?.Inspector ?? _emptyInspector;

    public string WindowTitle => _localizationService.GetFormattedString(
        ResourceKeys.AssetEditorWindowTitleFormat,
        DocumentGroup.ActiveDocumentTab?.DisplayName ?? GetString(ResourceKeys.AssetEditorWindowTitleFallback));

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyActionStates();
                _operationChangeRefreshCoordinator.HandleBusyStateChanged(value);
            }
        }
    }

    public void Dispose() => _operationChangeRefreshCoordinator.Dispose();

    public async Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null)
        {
            return;
        }

        var previousActiveAssetTab = DocumentGroup.ActiveAssetTab;
        var wasAlreadyActiveAssetTab = ReferenceEquals(previousActiveAssetTab, assetTab);
        DocumentGroup.SetActiveAssetTab(assetTab, cancelPreviousAsset: false);
        SyncActiveDocumentState();

        if (CanReuseLoadedAssetDocumentTabContent(assetTab))
        {
            _assetPreviewLoadCoordinator.LoadForegroundPreview(assetTab, promoteLoadingPreview: !wasAlreadyActiveAssetTab);
            if (!wasAlreadyActiveAssetTab)
            {
                _assetPreviewLoadCoordinator.DemotePreviewToBackgroundPrewarm(
                    previousActiveAssetTab,
                    DocumentGroup.AssetTabs);
            }

            _assetPreviewLoadCoordinator.PrewarmBackgroundPreviews(DocumentGroup.AssetTabs, assetTab);
            return;
        }

        await LoadAssetDocumentTabAsync(
            assetTab,
            promotePreviewAfterLoad: !wasAlreadyActiveAssetTab,
            previousActiveAssetTab);
    }

    public Task ActivateDocumentTabAsync(AssetDocumentTabItemViewModel? documentTab)
    {
        if (documentTab?.AssetTab is { } assetTab)
        {
            return ActivateAssetDocumentTabAsync(assetTab);
        }

        return documentTab?.SpineGroupTab is { } spineGroupTab
            ? ActivateSpineGroupDocumentTabAsync(spineGroupTab)
            : Task.CompletedTask;
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
        if (!DocumentGroup.HasDocumentTabs)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (wasActive)
        {
            await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
            return;
        }

        SyncActiveDocumentState();
    }

    public async Task CloseDocumentTabAsync(AssetDocumentTabItemViewModel? documentTab)
    {
        if (documentTab?.AssetTab is { } assetTab)
        {
            await CloseAssetDocumentTabAsync(assetTab);
            return;
        }

        if (documentTab?.SpineGroupTab is { } spineGroupTab)
        {
            await CloseSpineGroupDocumentTabAsync(spineGroupTab);
        }
    }

    public void ReorderDocumentTabs(IReadOnlyList<AssetDocumentTabItemViewModel> orderedTabs)
    {
        DocumentGroup.ReorderDocumentTabs(orderedTabs);
    }

    public void ReorderAssetDocumentTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedTabs)
    {
        DocumentGroup.ReorderAssetTabs(orderedTabs);
    }

    public void OpenCurrentPreviewLocation() => _assetActionCoordinator.OpenCurrentPreviewLocation();

    public Task ExtractSelectedAssetAsync() => _assetActionCoordinator.ExtractSelectedAssetAsync();

    public Task SelectWorkspaceReplacementSourceAsync() =>
        _assetActionCoordinator.SelectWorkspaceReplacementSourceAsync();

    public Task SelectFileReplacementSourceAsync() =>
        _assetActionCoordinator.SelectFileReplacementSourceAsync();

    public Task ApplyPendingReplacementAsync() => _assetActionCoordinator.ApplyPendingReplacementAsync();

    public Task UndoAppliedReplacementAsync() => _assetActionCoordinator.UndoAppliedReplacementAsync();

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
        bool promotePreviewAfterLoad,
        AssetDocumentTabViewModel? previousActiveAssetTab)
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
        if (!ReferenceEquals(previousActiveAssetTab, assetTab))
        {
            _assetPreviewLoadCoordinator.DemotePreviewToBackgroundPrewarm(
                previousActiveAssetTab,
                DocumentGroup.AssetTabs);
        }

        _assetPreviewLoadCoordinator.PrewarmBackgroundPreviews(DocumentGroup.AssetTabs, assetTab);
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

    private void ReapplySpineGroupDocumentState(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        NotifyActionStates();
        OnPropertyChanged(nameof(Inspector));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private async Task RefreshOperationAffectedStateAsync()
    {
        var assetTabs = DocumentGroup.AssetTabs.ToArray();
        foreach (var assetTab in assetTabs)
        {
            await RefreshAssetDocumentDetailsAsync(assetTab);
        }

        var spineGroupTabs = DocumentGroup.SpineGroupTabs.ToArray();
        foreach (var spineGroupTab in spineGroupTabs)
        {
            await RefreshSpineGroupDocumentDetailsAsync(spineGroupTab);
        }
    }

    private async Task RefreshAssetDocumentDetailsAsync(AssetDocumentTabViewModel assetTab)
    {
        if (_workspaceShell.WorkspaceSummary is not { } workspaceSummary)
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
        if (TryGetValue(detailsResult, out var details))
        {
            ReapplyAssetDocumentState(assetTab, details);
        }
    }

    private async Task RefreshSpineGroupDocumentDetailsAsync(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        var loadResult = await _spineGroupDetailsLoader.LoadAsync(spineGroupTab);
        if (loadResult is not null)
        {
            spineGroupTab.ApplyDetails(
                loadResult.Details,
                loadResult.TexturePreview,
                loadResult.AnimationTextures);
        }

        ReapplySpineGroupDocumentState(spineGroupTab);
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
        else if (DocumentGroup.ActiveSpineGroupTab is { } spineGroupTab)
        {
            _spineGroupInspectorPresenter.SyncInspectorActionStates(Inspector, spineGroupTab, IsBusy);
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

    private static AssetDocumentGroupTransfer CreateSingleAssetGroup(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        return new AssetDocumentGroupTransfer([assetTab], assetTab);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
