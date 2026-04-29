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
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资产文档标签的激活、同步、加载与关闭编排。
/// </summary>
internal sealed class BundleExplorerAssetDocumentTabCoordinator
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly BundleExplorerAssetPreviewCoordinator _assetPreviewCoordinator;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly BundleExplorerAssetDocumentHostCoordinator _assetDocumentHostCoordinator;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly Action _notifyActionStates;
    private readonly Action<IReadOnlyList<AppError>> _appendResultFailure;
    private readonly Func<IReadOnlyList<AppError>, string> _formatFirstErrorMessage;
    private int _assetDocumentStateSynchronizationDepth;

    public BundleExplorerAssetDocumentTabCoordinator(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        BundleExplorerAssetInspectorPresenter assetInspectorPresenter,
        BundleExplorerAssetPreviewCoordinator assetPreviewCoordinator,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        AssetPaneViewModel primaryPane,
        BundleExplorerAssetDocumentHostCoordinator assetDocumentHostCoordinator,
        Func<bool> isBusyProvider,
        Action requestWorkspaceBrowserStatePersistence,
        Action notifyActionStates,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetInspectorPresenter = assetInspectorPresenter ?? throw new ArgumentNullException(nameof(assetInspectorPresenter));
        _assetPreviewCoordinator = assetPreviewCoordinator ?? throw new ArgumentNullException(nameof(assetPreviewCoordinator));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _assetDocumentHostCoordinator = assetDocumentHostCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentHostCoordinator));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _notifyActionStates = notifyActionStates ?? throw new ArgumentNullException(nameof(notifyActionStates));
        _appendResultFailure = appendResultFailure ?? throw new ArgumentNullException(nameof(appendResultFailure));
        _formatFirstErrorMessage = formatFirstErrorMessage ?? throw new ArgumentNullException(nameof(formatFirstErrorMessage));
    }

    public bool IsSynchronizingAssetDocumentState => _assetDocumentStateSynchronizationDepth > 0;

    public async Task HandlePrimaryPaneSelectionChangedAsync(bool isSynchronizingPrimaryPaneState, Action captureActiveTabState)
    {
        ArgumentNullException.ThrowIfNull(captureActiveTabState);

        if (isSynchronizingPrimaryPaneState)
        {
            return;
        }

        if (IsSynchronizingAssetDocumentState)
        {
            captureActiveTabState();
            return;
        }

        if (_primaryPane.SelectedAsset is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        await OpenOrActivateAssetDocumentTabAsync(
            _documentWorkspace.ActiveBundleTab,
            _primaryPane.SelectedAsset,
            writeLog: true);
    }

    /// <summary>
    /// 当资源包标签页被切换、恢复或刷新后，
    /// 统一在这里决定右侧应该继续显示哪个资产详情标签。
    /// </summary>
    public async Task SynchronizeInspectorWithActiveBundleTabAsync(bool writeLog)
    {
        var activeBundleTab = _documentWorkspace.ActiveBundleTab;
        if (activeBundleTab is null)
        {
            _assetDocumentHostCoordinator.ClearCurrentInspectorHostState(clearAssetTabs: false);
            return;
        }

        // 资源列表为空并不等于“当前详情上下文失效”。
        // 这里常见于用户切换搜索或资源类型筛选后暂时没有命中项，
        // 此时应优先保留已经打开的资产详情标签，而不是把它们一并关闭。
        var preferredAssetTab = ResolvePreferredAssetDocumentTab(activeBundleTab);
        if (preferredAssetTab is not null)
        {
            // 只有当资源仍在当前列表里可见时，才回写列表选中态。
            // 若该资源已被筛选隐藏，则保持详情标签独立存在。
            var shouldSynchronizePaneSelection =
                _primaryPane.Items.Any(item => item.Item.AssetId.Equals(preferredAssetTab.AssetId)) &&
                (_primaryPane.SelectedAsset is null ||
                 !_primaryPane.SelectedAsset.Item.AssetId.Equals(preferredAssetTab.AssetId));
            await SetActiveAssetDocumentTabAsync(
                activeBundleTab,
                preferredAssetTab,
                synchronizePaneSelection: shouldSynchronizePaneSelection,
                writeLog: writeLog);
            return;
        }

        if (_primaryPane.Items.Count == 0 || _primaryPane.SelectedAsset is null)
        {
            _assetDocumentHostCoordinator.ClearCurrentInspectorHostState(clearAssetTabs: false);
            return;
        }

        await OpenOrActivateAssetDocumentTabAsync(
            activeBundleTab,
            _primaryPane.SelectedAsset,
            writeLog: writeLog);
    }

    public async Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        await SetActiveAssetDocumentTabAsync(
            _documentWorkspace.ActiveBundleTab,
            assetTab,
            synchronizePaneSelection: true,
            writeLog: false);
    }

    public async Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        var removedIndex = ownerTab.AssetTabs.IndexOf(assetTab);
        if (removedIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(ownerTab.ActiveAssetTab, assetTab);
        ownerTab.RemoveAssetTab(assetTab);

        if (!wasActive)
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
            _requestWorkspaceBrowserStatePersistence();
            _notifyActionStates();
            return;
        }

        if (ownerTab.ActiveAssetTab is not null)
        {
            await SetActiveAssetDocumentTabAsync(
                ownerTab,
                ownerTab.ActiveAssetTab,
                synchronizePaneSelection: true,
                writeLog: false);
            return;
        }

        _primaryPane.ClearSelection();
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
    }

    /// <summary>
    /// 在当前活动资源包下，仅保留指定资产详情标签。
    /// 这样可以快速收拢右侧详情区，同时保留用户明确选中的目标上下文。
    /// </summary>
    public async Task CloseOtherAssetDocumentTabsAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        if (!ownerTab.AssetTabs.Contains(assetTab))
        {
            return;
        }

        if (ownerTab.AssetTabs.Count <= 1)
        {
            if (!ReferenceEquals(ownerTab.ActiveAssetTab, assetTab))
            {
                await SetActiveAssetDocumentTabAsync(
                    ownerTab,
                    assetTab,
                    synchronizePaneSelection: true,
                    writeLog: false);
            }

            return;
        }

        var tabsToRemove = ownerTab.AssetTabs
            .Where(openedTab => !ReferenceEquals(openedTab, assetTab))
            .ToArray();

        foreach (var tabToRemove in tabsToRemove)
        {
            ownerTab.RemoveAssetTab(tabToRemove);
        }

        if (!ReferenceEquals(ownerTab.ActiveAssetTab, assetTab))
        {
            await SetActiveAssetDocumentTabAsync(
                ownerTab,
                assetTab,
                synchronizePaneSelection: true,
                writeLog: false);
            return;
        }

        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
    }

    /// <summary>
    /// 清空当前资源包下所有已打开的资产详情标签。
    /// 关闭后同时清空列表焦点，避免旧选中项立刻把详情标签重新打开。
    /// </summary>
    public Task CloseAllAssetDocumentTabsAsync()
    {
        if (_documentWorkspace.ActiveBundleTab is null || _documentWorkspace.ActiveBundleTab.AssetTabs.Count == 0)
        {
            return Task.CompletedTask;
        }

        _assetDocumentHostCoordinator.ClearCurrentInspectorHostState(clearAssetTabs: true);
        _primaryPane.ClearSelection();
        _requestWorkspaceBrowserStatePersistence();
        return Task.CompletedTask;
    }

    public void ReapplyAssetDocumentTab(AssetDocumentTabViewModel assetTab, AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);

        if (_documentWorkspace.ActiveBundleTab is null)
        {
            _notifyActionStates();
            return;
        }

        _assetInspectorPresenter.PresentAssetDocumentTab(
            assetTab,
            details,
            canEdit: ReferenceEquals(assetTab, _documentWorkspace.ActiveAssetDocumentTab),
            isBusy: _isBusyProvider());
        _notifyActionStates();
    }

    private AssetDocumentTabViewModel? ResolvePreferredAssetDocumentTab(BundleDocumentTabViewModel bundleTab)
    {
        if (bundleTab.ActiveAssetTab is not null)
        {
            return bundleTab.ActiveAssetTab;
        }

        if (_primaryPane.SelectedAsset is not null)
        {
            return bundleTab.TryGetAssetTab(_primaryPane.SelectedAsset.Item.AssetId, out var existingAssetTab)
                ? existingAssetTab
                : null;
        }

        // 当当前列表因筛选暂时没有选中项时，仍优先保留已有详情标签。
        // 避免“列表为空”把右侧资产文档上下文误清空。
        return bundleTab.AssetTabs.FirstOrDefault();
    }

    private async Task OpenOrActivateAssetDocumentTabAsync(
        BundleDocumentTabViewModel bundleTab,
        AssetListEntryViewModel entry,
        bool writeLog)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(entry);

        if (!bundleTab.TryGetAssetTab(entry.Item.AssetId, out var assetTab))
        {
            assetTab = _assetDocumentHostCoordinator.CreateAssetDocumentTab(
                entry.Item.BundleId,
                entry.Item.AssetId,
                entry.Item.Name);
            bundleTab.AddAssetTab(assetTab, activate: false);
        }

        ArgumentNullException.ThrowIfNull(assetTab);

        await SetActiveAssetDocumentTabAsync(
            bundleTab,
            assetTab,
            synchronizePaneSelection: false,
            writeLog: writeLog);
    }

    private async Task SetActiveAssetDocumentTabAsync(
        BundleDocumentTabViewModel bundleTab,
        AssetDocumentTabViewModel assetTab,
        bool synchronizePaneSelection,
        bool writeLog)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(assetTab);

        var wasAlreadyActiveAssetTab = ReferenceEquals(bundleTab.ActiveAssetTab, assetTab);
        bundleTab.SetActiveAssetTab(assetTab);

        if (wasAlreadyActiveAssetTab)
        {
            // 筛选刷新、同标签回切等场景不应把右侧详情宿主整块当成“新标签”重绑，
            // 否则会导致图片预览闪烁并把详情页滚动位置重置。
            assetTab.CanEdit = ReferenceEquals(bundleTab, _documentWorkspace.ActiveBundleTab);
            _notifyActionStates();
        }
        else
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        }

        if (synchronizePaneSelection)
        {
            BeginAssetDocumentStateSynchronization();
            try
            {
                if (!_primaryPane.TrySelectAsset(assetTab.AssetId))
                {
                    _primaryPane.ClearSelection();
                }
            }
            finally
            {
                EndAssetDocumentStateSynchronization();
            }
        }

        if (CanReuseLoadedAssetDocumentTabContent(assetTab))
        {
            if (writeLog && assetTab.Details is not null)
            {
                AppendAssetFocusedLog(assetTab.Details);
            }

            _requestWorkspaceBrowserStatePersistence();
            return;
        }

        await LoadAssetDocumentTabAsync(bundleTab, assetTab, writeLog);
        _requestWorkspaceBrowserStatePersistence();
    }

    private async Task LoadAssetDocumentTabAsync(
        BundleDocumentTabViewModel bundleTab,
        AssetDocumentTabViewModel assetTab,
        bool writeLog)
    {
        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var openResult = await _workspaceAssetInspectionGateway.OpenAssetInPaneAsync(
            new OpenAssetInPaneCommand(
                WorkspaceId: _workspaceShell.WorkspaceSummary.WorkspaceId,
                BundleId: assetTab.BundleId,
                AssetId: assetTab.AssetId,
                PaneId: assetTab.PaneId));

        if (!TryGetValue(openResult, out _))
        {
            return;
        }

        var detailsResult = await _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
            new GetAssetDetailsQuery(
                WorkspaceId: _workspaceShell.WorkspaceSummary.WorkspaceId,
                BundleId: assetTab.BundleId,
                AssetId: assetTab.AssetId));

        if (!TryGetValue(detailsResult, out var details))
        {
            return;
        }

        _assetInspectorPresenter.PresentAssetDocumentTab(
            assetTab,
            details,
            canEdit: ReferenceEquals(bundleTab, _documentWorkspace.ActiveBundleTab),
            isBusy: _isBusyProvider());
        _notifyActionStates();
        _ = _assetPreviewCoordinator.LoadInspectorPreviewAsync(assetTab, details, _formatFirstErrorMessage);

        if (writeLog)
        {
            AppendAssetFocusedLog(details);
        }
    }

    /// <summary>
    /// 资产文档标签已经持有详情与预览状态时，激活该标签应尽量直接复用现有内容。
    /// 这样筛选变化只刷新列表，不会重新闪烁图片预览，也不会打断用户当前浏览位置。
    /// </summary>
    private static bool CanReuseLoadedAssetDocumentTabContent(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        return assetTab.Details is { } details &&
               details.BundleId.Equals(assetTab.BundleId) &&
               details.AssetId.Equals(assetTab.AssetId);
    }

    private void AppendAssetFocusedLog(AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogAssetFocused,
            _primaryPane.PaneTitle,
            details.Name));
    }

    private void BeginAssetDocumentStateSynchronization()
    {
        _assetDocumentStateSynchronizationDepth++;
    }

    private void EndAssetDocumentStateSynchronization()
    {
        if (_assetDocumentStateSynchronizationDepth <= 0)
        {
            _assetDocumentStateSynchronizationDepth = 0;
            return;
        }

        _assetDocumentStateSynchronizationDepth--;
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

        _appendResultFailure(result.Errors);
        return false;
    }
}

