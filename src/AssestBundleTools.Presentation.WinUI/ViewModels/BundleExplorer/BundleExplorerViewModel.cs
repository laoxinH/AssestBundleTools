using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 维护工作台页面的整体展示状态与页面级交互流程。
/// </summary>
/// <remarks>
/// 这个 ViewModel 的职责是把多个只读用例组织成一个稳定的 UI 工作流：
/// 先打开工作区，再加载左树，再加载资产列表，最后同步右侧详情与轻量操作反馈。
/// 这样后续接入真实 AB 解析时，页面结构无需重写，只需要替换应用层实现。
/// </remarks>
public sealed class BundleExplorerViewModel : ObservableObject
{
    private readonly IWorkspaceBrowserService _workspaceBrowserService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IWorkspaceImportCoordinatorService _workspaceImportCoordinatorService;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;
    private readonly BundleExplorerWorkspaceShellViewModel _workspaceShell;
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly InspectorPanelViewModel _emptyInspector;
    private bool _isInitialized;
    private bool _isBusy;
    private bool _isRestoringWorkspaceBrowserState;
    private CancellationTokenSource? _workspaceBrowserStatePersistenceCts;
    private int _primaryPaneStateSynchronizationDepth;
    private int _assetDocumentStateSynchronizationDepth;

    public BundleExplorerViewModel(
        IWorkspaceBrowserService workspaceBrowserService,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IPathNavigationService pathNavigationService)
    {
        _workspaceBrowserService = workspaceBrowserService ?? throw new ArgumentNullException(nameof(workspaceBrowserService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceImportCoordinatorService = workspaceImportCoordinatorService ?? throw new ArgumentNullException(nameof(workspaceImportCoordinatorService));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
        _workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            _localizationService,
            _workspaceContextService,
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));
        _workspaceShell.PropertyChanged += OnWorkspaceShellPropertyChanged;
        _bundlePane = new BundleExplorerBundlePaneViewModel(_localizationService);
        _bundlePane.PropertyChanged += OnBundlePanePropertyChanged;
        _bundlePane.BrowserStateChanged += OnBundlePaneBrowserStateChanged;
        _documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        _documentWorkspace.PropertyChanged += OnDocumentWorkspacePropertyChanged;

        PrimaryPane = new AssetPaneViewModel();
        _emptyInspector = new InspectorPanelViewModel();
        PrimaryPane.SupportsBatchExtractionQuickAction = true;

        RefreshCommand = new AsyncRelayCommand(
            executeAsync: RefreshAsync,
            canExecute: () => HasWorkspace && !IsBusy);

        PrimaryPane.ReplaceAssetTypeOptions(CreateAssetTypeOptions());
        PrimaryPane.ConfigurePane(PaneId.New(), GetString(ResourceKeys.BundleExplorerPrimaryPaneTitle), GetString(ResourceKeys.BundleExplorerNoAssets));
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        _bundlePane.UpdateInteractionState(HasWorkspace, IsBusy);
    }

    public ObservableCollection<BundleGroupViewModel> BundleGroups => _bundlePane.BundleGroups;

    public ObservableCollection<BundleDocumentTabViewModel> BundleTabs => _documentWorkspace.BundleTabs;

    public ObservableCollection<WorkspaceStatusChipViewModel> WorkspaceStatusChips => _workspaceShell.WorkspaceStatusChips;

    public AssetPaneViewModel PrimaryPane { get; }

    public ObservableCollection<AssetDocumentTabViewModel> CurrentAssetTabs =>
        _documentWorkspace.CurrentAssetTabs;

    public AssetDocumentTabViewModel? ActiveAssetDocumentTab => _documentWorkspace.ActiveAssetDocumentTab;

    public bool HasAssetDocumentTabs => _documentWorkspace.HasAssetDocumentTabs;

    public Visibility AssetDocumentTabStripVisibility => _documentWorkspace.AssetDocumentTabStripVisibility;

    public InspectorPanelViewModel Inspector => ActiveAssetDocumentTab?.Inspector ?? _emptyInspector;

    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                PrimaryPane.IsBusy = value;
                _bundlePane.UpdateInteractionState(HasWorkspace, value);
                NotifyCommandStates();
                OnPropertyChanged(nameof(CanToggleBundleSearch));
                OnPropertyChanged(nameof(CanToggleBundlePaneCollapse));
            }
        }
    }

    public bool HasWorkspace => _workspaceShell.HasWorkspace;

    /// <summary>
    /// 控制左侧资源包列表是否收缩为窄条摘要。
    /// 收缩后仍保留工作区概况与当前选中资源包摘要，
    /// 这样可以把更多横向空间让给资产列表和右侧详情区。
    /// </summary>
    public bool IsBundlePaneCollapsed => _bundlePane.IsBundlePaneCollapsed;

    public bool IsBundleSearchExpanded => _bundlePane.IsBundleSearchExpanded;

    public string BundleSearchKeyword
    {
        get => _bundlePane.BundleSearchKeyword;
        set => _bundlePane.SetBundleSearchKeyword(value);
    }

    public string BundleSearchToggleGlyph => _bundlePane.BundleSearchToggleGlyph;

    public string BundleSearchToggleToolTip => _bundlePane.BundleSearchToggleToolTip;

    public string BundlePaneCollapseToolTip => _bundlePane.BundlePaneCollapseToolTip;

    public string BundleSearchEmptyStateText => _bundlePane.BundleSearchEmptyStateText;

    public Visibility WorkspaceContentVisibility => _workspaceShell.WorkspaceContentVisibility;

    public Visibility WorkspaceEmptyStateVisibility => _workspaceShell.WorkspaceEmptyStateVisibility;

    /// <summary>
    /// 左侧资源包区域的空状态文案。
    /// 未打开工作区时提示用户先创建或打开工作区；
    /// 已打开但尚未导入资源时，则明确提示当前是空工作区。
    /// </summary>
    public string BundlePaneEmptyStateText => _bundlePane.BundlePaneEmptyStateText;

    public Visibility BundlePaneEmptyStateVisibility => _bundlePane.BundlePaneEmptyStateVisibility;

    public Visibility BundleListVisibility => _bundlePane.BundleListVisibility;

    public Visibility BundleSearchEmptyStateVisibility => _bundlePane.BundleSearchEmptyStateVisibility;

    public bool HasOpenTabs => _documentWorkspace.HasOpenTabs;

    /// <summary>
    /// 右侧资产文档标签宿主在切换资源包标签时，会经历一次 ItemsSource 重绑。
    /// 这段时间内需要抑制控件把“默认第一项”反向写回状态。
    /// </summary>
    public bool IsSynchronizingAssetDocumentHostSelection => _documentWorkspace.IsSynchronizingAssetDocumentHostSelection;

    public BundleDocumentTabViewModel? ActiveBundleTab => _documentWorkspace.ActiveBundleTab;

    public bool HasSelectedAsset => TryGetActiveAssetDocumentDetails(out _, out _);

    public bool CanExtractSelectedAsset =>
        TryGetActiveAssetDocumentDetails(out var assetTab, out var details) &&
        !IsBusy &&
        assetTab.CanEdit &&
        details.CanExtract;

    public bool CanSelectFileReplacementSource =>
        TryGetActiveAssetDocumentDetails(out var assetTab, out var details) &&
        !IsBusy &&
        assetTab.CanEdit &&
        details.CanReplaceFromFile;

    public bool CanSelectWorkspaceReplacementSource =>
        TryGetActiveAssetDocumentDetails(out var assetTab, out var details) &&
        !IsBusy &&
        assetTab.CanEdit &&
        details.CanReplaceFromWorkspace;

    public bool CanToggleBundleSearch => _bundlePane.CanToggleBundleSearch;

    public bool CanToggleBundlePaneCollapse => _bundlePane.CanToggleBundlePaneCollapse;

    public string CollapsedBundlePaneBundleCountText => _bundlePane.CollapsedBundlePaneBundleCountText;

    public string CollapsedBundlePaneGroupCountText => _bundlePane.CollapsedBundlePaneGroupCountText;

    public string CollapsedBundlePaneSelectedBundleShortText => _bundlePane.CollapsedBundlePaneSelectedBundleShortText;

    public string CollapsedBundlePaneSelectedBundleToolTip => _bundlePane.CollapsedBundlePaneSelectedBundleToolTip;

    /// <summary>
    /// 向页面层提供“当前实际已绑定工作区”的稳定导航参数。
    /// 这样窗口注册逻辑可以依赖成功加载后的结果，而不是依赖页面传入的临时请求。
    /// </summary>
    public bool TryGetCurrentWorkspaceNavigationRequest([NotNullWhen(true)] out WorkspaceNavigationRequest? workspaceNavigationRequest) =>
        _workspaceShell.TryGetCurrentWorkspaceNavigationRequest(out workspaceNavigationRequest);

    public async Task InitializeAsync(WorkspaceNavigationRequest? workspaceNavigationRequest = null)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        if (workspaceNavigationRequest is not null)
        {
            await LoadWorkspaceAsync(clearExistingLogs: true, workspaceNavigationRequest);
            return;
        }

        if (_workspaceShell.TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
        {
            await BindWorkspaceAsync(
                clearExistingLogs: true,
                workspaceSummary: currentWorkspaceSummary,
                workspaceNavigationRequest: currentNavigationRequest);
            return;
        }

        ResetWorkspaceToEmptyState();
    }

    public async Task RefreshAsync()
    {
        if (_workspaceShell.WorkspaceSummary is not null)
        {
            await BindWorkspaceAsync(
                clearExistingLogs: false,
                workspaceSummary: _workspaceShell.WorkspaceSummary,
                workspaceNavigationRequest: _workspaceShell.ActiveWorkspaceNavigationRequest);
            return;
        }

        if (_workspaceShell.TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
        {
            await BindWorkspaceAsync(
                clearExistingLogs: true,
                workspaceSummary: currentWorkspaceSummary,
                workspaceNavigationRequest: currentNavigationRequest);
            return;
        }

        if (_workspaceShell.ActiveWorkspaceNavigationRequest is not null)
        {
            await LoadWorkspaceAsync(clearExistingLogs: true, _workspaceShell.ActiveWorkspaceNavigationRequest);
            return;
        }

        ResetWorkspaceToEmptyState();
    }

    public async Task OpenBundleInTabAsync(BundleNodeViewModel bundleNode)
    {
        await OpenBundleAsync(bundleNode, openAsPinnedTab: true);
    }

    public async Task HandlePrimaryPaneSelectionChangedAsync()
    {
        await HandleAssetSelectionAsync(PrimaryPane);
        CaptureActiveTabState();
    }

    public async Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || ActiveBundleTab is null)
        {
            return;
        }

        await SetActiveAssetDocumentTabAsync(
            ActiveBundleTab,
            assetTab,
            synchronizePaneSelection: true,
            writeLog: false);
    }

    public async Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = ActiveBundleTab;
        var removedIndex = ownerTab.AssetTabs.IndexOf(assetTab);
        if (removedIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(ownerTab.ActiveAssetTab, assetTab);
        ownerTab.RemoveAssetTab(assetTab);

        if (!wasActive)
        {
            NotifyAssetDocumentHostStateChanged();
            RequestWorkspaceBrowserStatePersistence();
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

        PrimaryPane.ClearSelection();
        NotifyAssetDocumentHostStateChanged();
        RequestWorkspaceBrowserStatePersistence();
    }

    /// <summary>
    /// 在当前活动资源包下，仅保留指定资产详情标签。
    /// 这样可以快速收拢右侧详情区，同时保留用户明确选中的目标上下文。
    /// </summary>
    public async Task CloseOtherAssetDocumentTabsAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = ActiveBundleTab;
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

        NotifyAssetDocumentHostStateChanged();
        RequestWorkspaceBrowserStatePersistence();
    }

    /// <summary>
    /// 清空当前资源包下所有已打开的资产详情标签。
    /// 关闭后同时清空列表焦点，避免旧选中项立刻把详情标签重新打开。
    /// </summary>
    public Task CloseAllAssetDocumentTabsAsync()
    {
        if (ActiveBundleTab is null || ActiveBundleTab.AssetTabs.Count == 0)
        {
            return Task.CompletedTask;
        }

        ClearCurrentInspectorHostState(clearAssetTabs: true);
        PrimaryPane.ClearSelection();
        RequestWorkspaceBrowserStatePersistence();
        return Task.CompletedTask;
    }

    public async Task RefreshPrimaryPaneAsync()
    {
        if (IsSynchronizingPrimaryPaneState)
        {
            return;
        }

        CaptureActiveTabState();
        await RefreshPaneAsync(PrimaryPane, clearInspectorOnEmpty: true);
        CaptureActiveTabState();
    }

    public void FlushWorkspaceBrowserState()
    {
        PersistWorkspaceBrowserStateNow();
    }

    public async Task ActivateBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null || _workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        if (ReferenceEquals(ActiveBundleTab, tab))
        {
            _bundlePane.SetSelectedBundle(_bundlePane.FindBundleNode(tab.BundleId));
            return;
        }

        await SetActiveBundleTabAsync(tab, writeSelectionLog: false);
    }

    /// <summary>
    /// 资源包标签顺序属于工作台页面级浏览状态。
    /// 视图层拖拽完成后只上报最终顺序，真正的集合重排和后续状态持久化在这里统一处理。
    /// </summary>
    public void ReorderBundleTabs(IReadOnlyList<BundleDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderBundleTabs(orderedTabs))
        {
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    /// <summary>
    /// 右侧资产详情标签顺序归属当前活动资源包标签页。
    /// 拖拽重排后保持当前活动标签不变，只更新集合顺序并写回工作区浏览状态。
    /// </summary>
    public void ReorderActiveAssetDocumentTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderActiveAssetTabs(orderedTabs))
        {
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    public async Task CloseBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var removedIndex = BundleTabs.IndexOf(tab);
        if (removedIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(ActiveBundleTab, tab);

        BundleTabs.RemoveAt(removedIndex);

        if (BundleTabs.Count == 0)
        {
            ClearActiveTabState();
            return;
        }

        if (!wasActive)
        {
            RequestWorkspaceBrowserStatePersistence();
            return;
        }

        var nextIndex = Math.Min(removedIndex, BundleTabs.Count - 1);
        await SetActiveBundleTabAsync(BundleTabs[nextIndex], writeSelectionLog: false);
    }

    /// <summary>
    /// 保留指定资源包标签，关闭当前工作区中的其他资源包标签。
    /// 如果保留目标当前并非活动标签，也会一并切换过去，避免关闭后焦点落到不可预期的位置。
    /// </summary>
    public async Task CloseOtherBundleTabsAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var retainedTab = BundleTabs.FirstOrDefault(existingTab => ReferenceEquals(existingTab, tab));
        if (retainedTab is null)
        {
            return;
        }

        if (BundleTabs.Count <= 1)
        {
            if (!ReferenceEquals(ActiveBundleTab, retainedTab) && _workspaceShell.WorkspaceSummary is not null)
            {
                await SetActiveBundleTabAsync(retainedTab, writeSelectionLog: false);
            }

            return;
        }

        var tabsToRemove = BundleTabs
            .Where(existingTab => !ReferenceEquals(existingTab, retainedTab))
            .ToArray();

        foreach (var tabToRemove in tabsToRemove)
        {
            BundleTabs.Remove(tabToRemove);
        }

        if (!ReferenceEquals(ActiveBundleTab, retainedTab))
        {
            await SetActiveBundleTabAsync(retainedTab, writeSelectionLog: false);
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    /// <summary>
    /// 关闭当前工作区中所有资源包标签，并把页面浏览上下文重置回“未打开标签”的稳定状态。
    /// </summary>
    public Task CloseAllBundleTabsAsync()
    {
        if (BundleTabs.Count == 0)
        {
            return Task.CompletedTask;
        }

        CaptureActiveTabState();
        BundleTabs.Clear();
        ClearActiveTabState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 切换左侧资源包搜索框的展开状态。
    /// 收起时同步清空关键字，确保列表快速回到完整浏览状态。
    /// </summary>
    public void ToggleBundleSearch()
    {
        _bundlePane.ToggleBundleSearch();
    }

    /// <summary>
    /// 在完整浏览视图与紧凑摘要窄条之间切换左栏形态。
    /// 收缩时同步关闭搜索，避免隐藏输入态残留在页面状态里。
    /// </summary>
    public void ToggleBundlePaneCollapsed()
    {
        _bundlePane.ToggleBundlePaneCollapsed();
    }

    /// <summary>
    /// 资源包导入入口统一交给共享协调服务处理，
    /// 当前阶段只收集来源并记录标准化反馈，为后续真实导入链路预留稳定边界。
    /// </summary>
    public async Task RequestBundleFileImportAsync(CancellationToken cancellationToken = default)
    {
        if (!HasWorkspace)
        {
            return;
        }

        if (await _workspaceImportCoordinatorService.RequestBundleFileImportAsync(cancellationToken))
        {
            await ReloadCurrentWorkspaceAsync(showLoadedNotification: false);
        }
    }

    /// <summary>
    /// 目录导入与文件导入共用同一套协调链路，避免页面散落各自的导入前置逻辑。
    /// </summary>
    public async Task RequestBundleDirectoryImportAsync(CancellationToken cancellationToken = default)
    {
        if (!HasWorkspace)
        {
            return;
        }

        if (await _workspaceImportCoordinatorService.RequestBundleDirectoryImportAsync(cancellationToken))
        {
            await ReloadCurrentWorkspaceAsync(showLoadedNotification: false);
        }
    }

    public async Task RequestDragDropImportAsync(
        IReadOnlyList<string> droppedPaths,
        CancellationToken cancellationToken = default)
    {
        if (!HasWorkspace)
        {
            return;
        }

        if (await _workspaceImportCoordinatorService.RequestDragDropImportAsync(droppedPaths, cancellationToken))
        {
            await ReloadCurrentWorkspaceAsync(showLoadedNotification: false);
        }
    }

    public void OpenBundleSourceLocation(BundleNodeViewModel bundleNode)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (!_pathNavigationService.TryOpenContainingFolder(bundleNode.SourcePath))
        {
            var message = _localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerNotificationBundleOpenLocationFailed,
                bundleNode.DisplayName);
            _appLogService.AppendWarning(message);
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationWarningTitle),
                message,
                AppNotificationSeverity.Warning);
        }
    }

    /// <summary>
    /// 预览文件定位属于当前详情标签页的通用预览动作。
    /// 这里统一读取当前检查器持有的预览缓存 payload 路径，避免图片、音频、文本各自复制一套打开逻辑。
    /// </summary>
    public void OpenCurrentPreviewLocation()
    {
        if (!TryGetActiveAssetDocumentDetails(out _, out var details) ||
            string.IsNullOrWhiteSpace(Inspector.PreviewPayloadFilePath))
        {
            return;
        }

        if (!_pathNavigationService.TryOpenContainingFolder(Inspector.PreviewPayloadFilePath))
        {
            var message = _localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerNotificationPreviewOpenLocationFailed,
                details.Name);
            _appLogService.AppendWarning(message);
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationWarningTitle),
                message,
                AppNotificationSeverity.Warning);
        }
    }

    public async Task RemoveBundleFromWorkspaceAsync(BundleNodeViewModel bundleNode)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (IsBusy ||
            !HasWorkspace ||
            !_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            workspaceEntry is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.RemoveWorkspaceBundleAsync(
                new RemoveWorkspaceBundleCommand(
                    WorkspaceKey: workspaceEntry.WorkspaceKey,
                    WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                    BundleId: bundleNode.BundleId,
                    BundleSourcePath: bundleNode.SourcePath));

            if (!TryGetValue(result, out _))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBundleRemoved,
                bundleNode.DisplayName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBundleRemoved,
                    bundleNode.DisplayName),
                AppNotificationSeverity.Success);

            await ReloadCurrentWorkspaceAsync(
                showLoadedNotification: false,
                allowBusyReentry: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadWorkspaceAsync(
        bool clearExistingLogs,
        WorkspaceNavigationRequest? workspaceNavigationRequest,
        bool showLoadedNotification = true,
        bool allowBusyReentry = false)
    {
        if (IsBusy && !allowBusyReentry)
        {
            return;
        }

        var managesBusyState = !IsBusy;
        if (managesBusyState)
        {
            IsBusy = true;
        }

        try
        {
            if (clearExistingLogs)
            {
                _appLogService.Clear();
            }

            var resolvedNavigationRequest = _workspaceShell.ResolveNavigationRequest(workspaceNavigationRequest);
            if (resolvedNavigationRequest is null)
            {
                ResetWorkspaceToEmptyState();
                return;
            }

            var resolvedWorkspacePath = resolvedNavigationRequest.SourcePath;
            var resolvedWorkspaceDisplayName = resolvedNavigationRequest.WorkspaceDisplayName;
            var resolvedWorkspaceDataRootPath = resolvedNavigationRequest.WorkspaceDataRootPath;

            _appLogService.AppendInfo(GetString(ResourceKeys.BundleExplorerLogInitializing));

            var openResult = await _workspaceBrowserService.OpenWorkspaceAsync(
                new OpenWorkspaceCommand(
                    WorkspacePath: resolvedWorkspacePath,
                    WorkspaceDataRootPath: resolvedWorkspaceDataRootPath,
                    WorkspaceDisplayName: resolvedWorkspaceDisplayName,
                    InitialMode: WorkspaceMode.SinglePane));

            if (!TryGetValue(openResult, out var workspaceSummary))
            {
                ResetWorkspaceToEmptyState();
                return;
            }

            await ApplyWorkspaceAsync(workspaceSummary, resolvedNavigationRequest, showLoadedNotification);
        }
        finally
        {
            if (managesBusyState)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// 在不重新创建工作区会话的前提下，把当前页面重新绑定到已有活动工作区。
    /// 这个流程主要服务于页面切换回来后的状态恢复，以及刷新当前会话数据。
    /// </summary>
    private async Task BindWorkspaceAsync(
        bool clearExistingLogs,
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceNavigationRequest? workspaceNavigationRequest,
        bool showLoadedNotification = true)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (clearExistingLogs)
            {
                _appLogService.Clear();
            }

            _appLogService.AppendInfo(GetString(ResourceKeys.BundleExplorerLogInitializing));

            var resolvedNavigationRequest = workspaceNavigationRequest
                                            ?? _workspaceShell.ResolveNavigationRequest(workspaceNavigationRequest);
            if (resolvedNavigationRequest is null)
            {
                ResetWorkspaceToEmptyState();
                return;
            }

            await ApplyWorkspaceAsync(workspaceSummary, resolvedNavigationRequest, showLoadedNotification);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyWorkspaceAsync(
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceNavigationRequest resolvedNavigationRequest,
        bool showLoadedNotification)
    {
        var appliedNavigationRequest = _workspaceShell.ApplyWorkspace(workspaceSummary, resolvedNavigationRequest);
        var resolvedWorkspaceDataRootPath = appliedNavigationRequest.WorkspaceDataRootPath;

        _workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            resolvedWorkspaceDataRootPath,
            appliedNavigationRequest.AssetBundleDecryptorId);
        PrimaryPane.ConfigurePane(
            workspaceSummary.ActivePaneId,
            GetString(ResourceKeys.BundleExplorerPrimaryPaneTitle),
            GetString(ResourceKeys.BundleExplorerNoAssets));

        var bundleTreeResult = await _workspaceBrowserService.LoadBundleTreeAsync(
            new LoadBundleTreeQuery(workspaceSummary.WorkspaceId));

        if (!TryGetValue(bundleTreeResult, out var bundleTree))
        {
            ResetWorkspaceToEmptyState();
            return;
        }

        _bundlePane.RebuildBundleGroups(bundleTree);
        _documentWorkspace.Clear();

        if (!await TryRestoreWorkspaceBrowserStateAsync())
        {
            var firstBundle = _bundlePane.GetFirstVisibleBundle();
            if (firstBundle is not null)
            {
                await OpenBundleAsync(firstBundle, openAsPinnedTab: false);
            }
            else
            {
                ClearActiveTabState();
            }
        }

        if (showLoadedNotification)
        {
            var workspaceLoadedMessage = _localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogWorkspaceLoaded,
                workspaceSummary.DisplayName,
                workspaceSummary.BundleCount,
                workspaceSummary.AssetCount);

            _appLogService.AppendInfo(workspaceLoadedMessage);
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationWorkspaceLoaded,
                    workspaceSummary.DisplayName,
                    workspaceSummary.BundleCount,
                    workspaceSummary.AssetCount),
                AppNotificationSeverity.Success);
        }
    }

    private async Task ReloadCurrentWorkspaceAsync(
        bool showLoadedNotification,
        bool allowBusyReentry = false)
    {
        if (_workspaceShell.ActiveWorkspaceNavigationRequest is null)
        {
            return;
        }

        await LoadWorkspaceAsync(
            clearExistingLogs: false,
            workspaceNavigationRequest: _workspaceShell.ActiveWorkspaceNavigationRequest,
            showLoadedNotification: showLoadedNotification,
            allowBusyReentry: allowBusyReentry);
    }

    private async Task RefreshPaneAsync(AssetPaneViewModel pane, bool clearInspectorOnEmpty)
    {
        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var bundleNode = _bundlePane.FindBundleNodeByDisplayName(pane.CurrentBundleDisplayName);
        if (bundleNode is null)
        {
            pane.ClearItems(GetString(ResourceKeys.BundleExplorerNoAssets));
            if (clearInspectorOnEmpty && ReferenceEquals(pane, PrimaryPane))
            {
                ClearCurrentInspectorHostState(clearAssetTabs: true);
            }

            return;
        }

        var preferredSelectedAssetId = ReferenceEquals(pane, PrimaryPane)
            ? ActiveBundleTab?.SelectedAssetId
            : null;

        await LoadBundleIntoPaneAsync(
            pane,
            bundleNode,
            clearInspectorOnEmpty,
            preferredSelectedAssetId);

        if (ReferenceEquals(pane, PrimaryPane))
        {
            await SynchronizeInspectorWithActiveBundleTabAsync(writeLog: false);
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogFilterApplied,
            pane.PaneTitle,
            bundleNode.DisplayName));
    }

    private async Task LoadBundleIntoPaneAsync(
        AssetPaneViewModel pane,
        BundleNodeViewModel bundleNode,
        bool clearInspectorOnEmpty,
        string? preferredSelectedAssetId = null)
    {
        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var assetListResult = await _workspaceBrowserService.LoadAssetListAsync(
            new LoadAssetListQuery(
                WorkspaceId: _workspaceShell.WorkspaceSummary.WorkspaceId,
                BundleId: bundleNode.BundleId,
                SearchKeyword: NormalizeSearchKeyword(pane.SearchKeyword),
                AssetTypeFilter: pane.SelectedAssetTypeOption?.AssetType));

        if (!TryGetValue(assetListResult, out var assetList))
        {
            pane.ClearItems(GetString(ResourceKeys.BundleExplorerNoAssets));
            if (clearInspectorOnEmpty && ReferenceEquals(pane, PrimaryPane))
            {
                ClearCurrentInspectorHostState(clearAssetTabs: true);
            }

            return;
        }

        var entries = assetList.Items
            .Select(MapAssetEntry)
            .ToArray();

        pane.ReplaceItems(
            currentBundleDisplayName: bundleNode.DisplayName,
            items: entries,
            emptyStateText: GetString(ResourceKeys.BundleExplorerNoAssets),
            preferredSelectedAssetId: string.IsNullOrWhiteSpace(preferredSelectedAssetId)
                ? null
                : new AssetId(preferredSelectedAssetId));

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogBundleLoaded,
            pane.PaneTitle,
            bundleNode.DisplayName,
            assetList.FilteredCount));
    }

    private async Task HandleAssetSelectionAsync(AssetPaneViewModel pane)
    {
        if (!ReferenceEquals(pane, PrimaryPane))
        {
            return;
        }

        if (IsSynchronizingPrimaryPaneState)
        {
            return;
        }

        if (IsSynchronizingAssetDocumentState)
        {
            CaptureActiveTabState();
            return;
        }

        if (pane.SelectedAsset is null)
        {
            return;
        }

        if (ActiveBundleTab is null)
        {
            return;
        }

        await OpenOrActivateAssetDocumentTabAsync(
            ActiveBundleTab,
            pane.SelectedAsset,
            writeLog: true);
    }

    /// <summary>
    /// 当资源包标签页被切换、恢复或刷新后，
    /// 统一在这里决定右侧应该继续显示哪个资产详情标签。
    /// </summary>
    private async Task SynchronizeInspectorWithActiveBundleTabAsync(bool writeLog)
    {
        if (ActiveBundleTab is null)
        {
            ClearCurrentInspectorHostState(clearAssetTabs: false);
            return;
        }

        // 资源列表为空并不等于“当前详情上下文失效”。
        // 这里常见于用户切换搜索或资源类型筛选后暂时没有命中项，
        // 此时应优先保留已经打开的资产详情标签，而不是把它们一并关闭。
        var preferredAssetTab = ResolvePreferredAssetDocumentTab(ActiveBundleTab);
        if (preferredAssetTab is not null)
        {
            // 只有当资源仍在当前列表里可见时，才回写列表选中态。
            // 若当前列表为空或该资源被筛选隐藏，则保持详情标签独立存在。
            var shouldSynchronizePaneSelection = PrimaryPane.Items.Count > 0 &&
                                                 (PrimaryPane.SelectedAsset is null ||
                                                  !PrimaryPane.SelectedAsset.Item.AssetId.Equals(preferredAssetTab.AssetId));
            await SetActiveAssetDocumentTabAsync(
                ActiveBundleTab,
                preferredAssetTab,
                synchronizePaneSelection: shouldSynchronizePaneSelection,
                writeLog: writeLog);
            return;
        }

        if (PrimaryPane.Items.Count == 0 || PrimaryPane.SelectedAsset is null)
        {
            ClearCurrentInspectorHostState(clearAssetTabs: false);
            return;
        }

        await OpenOrActivateAssetDocumentTabAsync(
            ActiveBundleTab,
            PrimaryPane.SelectedAsset,
            writeLog: writeLog);
    }

    private AssetDocumentTabViewModel? ResolvePreferredAssetDocumentTab(BundleDocumentTabViewModel bundleTab)
    {
        if (bundleTab.ActiveAssetTab is not null)
        {
            return bundleTab.ActiveAssetTab;
        }

        if (PrimaryPane.SelectedAsset is not null)
        {
            return bundleTab.TryGetAssetTab(PrimaryPane.SelectedAsset.Item.AssetId, out var existingAssetTab)
                ? existingAssetTab
                : null;
        }

        // 当当前列表因筛选暂时没有选中项时，仍优先保留已有详情标签，
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
            assetTab = CreateAssetDocumentTab(
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

    private AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName)
    {
        var tab = new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            assetName);
        tab.Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        return tab;
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
            assetTab.CanEdit = ReferenceEquals(bundleTab, ActiveBundleTab);
            NotifyActionStates();
        }
        else
        {
            NotifyAssetDocumentHostStateChanged();
        }

        if (synchronizePaneSelection)
        {
            BeginAssetDocumentStateSynchronization();
            try
            {
                if (!PrimaryPane.TrySelectAsset(assetTab.AssetId))
                {
                    PrimaryPane.ClearSelection();
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

            RequestWorkspaceBrowserStatePersistence();
            return;
        }

        await LoadAssetDocumentTabAsync(bundleTab, assetTab, writeLog);
        RequestWorkspaceBrowserStatePersistence();
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

        var openResult = await _workspaceBrowserService.OpenAssetInPaneAsync(
            new OpenAssetInPaneCommand(
                WorkspaceId: _workspaceShell.WorkspaceSummary.WorkspaceId,
                BundleId: assetTab.BundleId,
                AssetId: assetTab.AssetId,
                PaneId: assetTab.PaneId));

        if (!TryGetValue(openResult, out _))
        {
            return;
        }

        var detailsResult = await _workspaceBrowserService.GetAssetDetailsAsync(
            new GetAssetDetailsQuery(
                WorkspaceId: _workspaceShell.WorkspaceSummary.WorkspaceId,
                BundleId: assetTab.BundleId,
                AssetId: assetTab.AssetId));

        if (!TryGetValue(detailsResult, out var details))
        {
            return;
        }

        ApplyAssetDocumentTab(
            bundleTab,
            assetTab,
            details);

        var previewRequestVersion = assetTab.BeginPreviewRequest();
        assetTab.Inspector.SetPreviewLoading(GetString(ResourceKeys.InspectorPreviewLoadingText));
        _ = LoadInspectorPreviewAsync(assetTab, details, previewRequestVersion);

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
            PrimaryPane.PaneTitle,
            details.Name));
    }

    private void ApplyAssetDocumentTab(
        BundleDocumentTabViewModel bundleTab,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);

        assetTab.ApplyDetails(details);
        assetTab.CanEdit = ReferenceEquals(bundleTab, ActiveBundleTab);

        assetTab.Inspector.Apply(
            assetName: details.Name,
            bundleDisplayName: _bundlePane.ResolveBundleDisplayName(details.BundleId),
            assetTypeText: ResolveAssetTypeDisplayName(details.AssetType, details.AssetTypeDisplayName),
            assetPath: details.AssetPath,
            containerPath: details.ContainerPath,
            sizeText: _localizedValueFormatter.FormatCompactByteSize(ResolveDisplaySizeInBytes(details.SizeInBytes, details.PreferredDisplaySizeInBytes)),
            pathIdText: details.PathId.ToString(CultureInfo.InvariantCulture),
            extractCapabilityText: details.CanExtract
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            fileReplacementCapabilityText: details.CanReplaceFromFile
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            workspaceReplacementCapabilityText: details.CanReplaceFromWorkspace
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            replacementSourceText: BuildReplacementSourceText(
                details,
                assetTab.PendingWorkspaceReplacementCandidate,
                assetTab.PendingFileReplacementCandidate),
            replacementStatusText: BuildReplacementStatusText(
                details,
                assetTab.PendingWorkspaceReplacementCandidate,
                assetTab.PendingFileReplacementCandidate),
            changeSummaryText: BuildChangeSummaryText(details),
            canExtract: assetTab.CanEdit && !IsBusy && details.CanExtract,
            canSelectFileReplacementSource: assetTab.CanEdit && !IsBusy && details.CanReplaceFromFile,
            canSelectReplacementSource: assetTab.CanEdit && !IsBusy && details.CanReplaceFromWorkspace,
            canApplyReplacement: assetTab.CanEdit && !IsBusy &&
                                 (assetTab.PendingWorkspaceReplacementCandidate is not null ||
                                  assetTab.PendingFileReplacementCandidate is not null),
            canUndoReplacement: assetTab.CanEdit && !IsBusy && HasAppliedReplacement(details),
            assetDependencies: details.Dependencies.Select(MapDependency),
            referencedByDependencies: (details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            dependencyClosureItems: (details.DependencyClosure ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            bundleExternalDependencies: (details.BundleExternalDependencies ?? Array.Empty<BundleExternalDependencyDto>())
                .Select(MapBundleExternalDependency));

        NotifyActionStates();
    }

    private void ClearCurrentInspectorHostState(bool clearAssetTabs)
    {
        if (clearAssetTabs)
        {
            ActiveBundleTab?.ClearAssetTabs();
        }
        else
        {
            ActiveBundleTab?.SetActiveAssetTab(null);
        }

        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        NotifyAssetDocumentHostStateChanged();
    }

    public async Task ExtractSelectedAssetAsync()
    {
        if (IsBusy ||
            _workspaceShell.WorkspaceSummary is null ||
            !TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.ExtractAssetAsync(
                new ExtractAssetCommand(
                    _workspaceShell.WorkspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var extractionResult))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetExtracted,
                extractionResult.AssetName,
                extractionResult.SuggestedOutputPath));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationAssetExtracted,
                    extractionResult.AssetName),
                AppNotificationSeverity.Success);

            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 处理资产列表中的多选快捷提取。
    /// 当前先复用单资源提取用例逐项执行，并在页面层汇总结果提示，
    /// 后续如果批量提取演进为独立用例，再把编排逻辑下沉到 Application。
    /// </summary>
    public async Task ExtractSelectedAssetsAsync()
    {
        if (IsBusy || _workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var selectedEntries = PrimaryPane.SelectedAssets
            .GroupBy(static entry => entry.Item.AssetId.Value, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        if (selectedEntries.Length <= 1)
        {
            await ExtractSelectedAssetAsync();
            return;
        }

        IsBusy = true;
        try
        {
            var extractedResults = new List<AssetExtractionResultDto>(selectedEntries.Length);

            foreach (var entry in selectedEntries)
            {
                var result = await _workspaceBrowserService.ExtractAssetAsync(
                    new ExtractAssetCommand(
                        _workspaceShell.WorkspaceSummary.WorkspaceId,
                        entry.Item.BundleId,
                        entry.Item.AssetId));

                if (!TryGetValue(result, out var extractionResult))
                {
                    continue;
                }

                extractedResults.Add(extractionResult);
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogAssetExtracted,
                    extractionResult.AssetName,
                    extractionResult.SuggestedOutputPath));
            }

            if (extractedResults.Count == 0)
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBatchAssetsExtracted,
                extractedResults.Count,
                extractedResults[0].SuggestedOutputPath));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBatchAssetsExtracted,
                    extractedResults.Count),
                AppNotificationSeverity.Success);

            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectWorkspaceReplacementSourceAsync()
    {
        if (IsBusy ||
            _workspaceShell.WorkspaceSummary is null ||
            !TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.GetWorkspaceReplacementCandidatesAsync(
                new GetWorkspaceReplacementCandidatesQuery(
                    _workspaceShell.WorkspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var candidates))
            {
                return;
            }

            var selectedCandidate = candidates[0];
            assetTab.UpdatePendingWorkspaceReplacementCandidate(selectedCandidate);
            ApplyAssetDocumentTab(ActiveBundleTab!, assetTab, details);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementSourceSelected,
                selectedCandidate.SourceBundleDisplayName,
                selectedCandidate.SourceAssetName,
                details.Name));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementSourceSelected,
                    selectedCandidate.SourceAssetName),
                AppNotificationSeverity.Informational);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectFileReplacementSourceAsync()
    {
        if (IsBusy ||
            _workspaceShell.WorkspaceSummary is null ||
            !TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            return;
        }

        IsBusy = true;
        try
        {
            string? selectedFilePath;
            try
            {
                selectedFilePath = await _filePickerService.PickReplacementSourceFileAsync(details.AssetType);
            }
            catch (Exception ex)
            {
                var failureMessage = GetString(ResourceKeys.BundleExplorerNotificationFilePickerOpenFailed);
                _appLogService.AppendError(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFilePickerOpenFailedFormat,
                    ex.GetType().Name));
                ShowOperationNotification(
                    GetString(ResourceKeys.AppNotificationErrorTitle),
                    failureMessage,
                    AppNotificationSeverity.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFileReplacementSelectionCancelled,
                    details.Name));
                return;
            }

            var result = await _workspaceBrowserService.GetFileReplacementCandidateAsync(
                new GetFileReplacementCandidateQuery(
                    _workspaceShell.WorkspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    selectedFilePath));

            if (!TryGetValue(result, out var candidate))
            {
                return;
            }

            assetTab.UpdatePendingFileReplacementCandidate(candidate);
            ApplyAssetDocumentTab(ActiveBundleTab!, assetTab, details);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFileReplacementSourceSelected,
                candidate.SourceFileName,
                candidate.SourceFilePath,
                details.Name));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationFileReplacementSourceSelected,
                    candidate.SourceFileName),
                AppNotificationSeverity.Informational);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyPendingReplacementAsync()
    {
        if (IsBusy ||
            _workspaceShell.WorkspaceSummary is null ||
            !TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (assetTab.PendingFileReplacementCandidate is not null)
            {
                var fileResult = await _workspaceBrowserService.ReplaceAssetFromFileAsync(
                    new ReplaceAssetFromFileCommand(
                        _workspaceShell.WorkspaceSummary.WorkspaceId,
                        details.BundleId,
                        details.AssetId,
                        assetTab.PendingFileReplacementCandidate.SourceFilePath,
                        assetTab.PendingFileReplacementCandidate.SourceAssetType));

                if (!TryGetValue(fileResult, out var fileReplacementResult))
                {
                    return;
                }

                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogFileReplacementApplied,
                    fileReplacementResult.TargetAssetName,
                    fileReplacementResult.SourceFileName,
                    fileReplacementResult.SourceFilePath));

                ShowOperationNotification(
                    GetString(ResourceKeys.AppNotificationSuccessTitle),
                    _localizationService.GetFormattedString(
                        ResourceKeys.BundleExplorerNotificationFileReplacementApplied,
                        fileReplacementResult.TargetAssetName),
                    AppNotificationSeverity.Success);

                assetTab.ClearPendingReplacementCandidates();
                await RefreshPrimaryPaneAsync();
                return;
            }

            if (assetTab.PendingWorkspaceReplacementCandidate is null)
            {
                return;
            }

            var workspaceResult = await _workspaceBrowserService.ReplaceAssetFromWorkspaceAsync(
                new ReplaceAssetFromWorkspaceCommand(
                    _workspaceShell.WorkspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceBundleId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceAssetId));

            if (!TryGetValue(workspaceResult, out var replacementResult))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementApplied,
                replacementResult.TargetAssetName,
                replacementResult.SourceBundleDisplayName,
                replacementResult.SourceAssetName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementApplied,
                    replacementResult.TargetAssetName),
                AppNotificationSeverity.Success);

            assetTab.ClearPendingReplacementCandidates();
            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UndoAppliedReplacementAsync()
    {
        if (IsBusy ||
            _workspaceShell.WorkspaceSummary is null ||
            !TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.UndoAssetReplacementAsync(
                new UndoAssetReplacementCommand(
                    _workspaceShell.WorkspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var undoResult))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementUndone,
                undoResult.TargetAssetName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementUndone,
                    undoResult.TargetAssetName),
                AppNotificationSeverity.Informational);

            assetTab.ClearPendingReplacementCandidates();
            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 统一处理资源包标签页打开策略。
    /// 当前主交互已经收敛为“单击左侧资源包列表即打开固定标签页”，
    /// 预览标签页仅保留给初始化和后续恢复流程使用。
    /// </summary>
    private async Task OpenBundleAsync(BundleNodeViewModel bundleNode, bool openAsPinnedTab)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var targetTab = _documentWorkspace.OpenBundle(bundleNode, openAsPinnedTab);
        await SetActiveBundleTabAsync(targetTab, writeSelectionLog: false);
    }

    private async Task SetActiveBundleTabAsync(BundleDocumentTabViewModel tab, bool writeSelectionLog)
    {
        CaptureCurrentBundleTabStateBeforeActivation(tab);
        _documentWorkspace.BeginAssetDocumentHostSynchronization();
        try
        {
            _documentWorkspace.SetActiveBundleTab(tab);
            var bundleNode = _bundlePane.FindBundleNode(tab.BundleId);
            _bundlePane.SetSelectedBundle(bundleNode);

            BeginPrimaryPaneStateSynchronization();
            try
            {
                tab.ApplyToPane(PrimaryPane);

                if (bundleNode is null)
                {
                    ClearActiveTabState();
                    return;
                }

                await LoadBundleIntoPaneAsync(
                    PrimaryPane,
                    bundleNode,
                    clearInspectorOnEmpty: true,
                    preferredSelectedAssetId: tab.SelectedAssetId);
            }
            finally
            {
                EndPrimaryPaneStateSynchronization();
            }

            await SynchronizeInspectorWithActiveBundleTabAsync(writeLog: writeSelectionLog);

            if (!writeSelectionLog)
            {
                RequestWorkspaceBrowserStatePersistence();
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBundleLoaded,
                PrimaryPane.PaneTitle,
                bundleNode.DisplayName,
                PrimaryPane.Items.Count));
            RequestWorkspaceBrowserStatePersistence();
        }
        finally
        {
            _documentWorkspace.EndAssetDocumentHostSynchronization();
        }
    }

    /// <summary>
    /// 资源包标签切换前统一先抓取当前主窗格状态。
    /// 这样左侧资源包列表和顶部资源包标签会共用同一条保存链路，
    /// 避免“关闭某个筛选后切到别处，又被旧的开启态覆盖回来”。
    /// </summary>
    private void CaptureCurrentBundleTabStateBeforeActivation(BundleDocumentTabViewModel nextTab)
    {
        ArgumentNullException.ThrowIfNull(nextTab);

        if (ActiveBundleTab is null || ReferenceEquals(ActiveBundleTab, nextTab))
        {
            return;
        }

        CaptureActiveTabState(requestPersistence: false);
    }

    private void CaptureActiveTabState(bool requestPersistence = true)
    {
        ActiveBundleTab?.CaptureFromPane(PrimaryPane);

        if (requestPersistence)
        {
            RequestWorkspaceBrowserStatePersistence();
        }
    }

    private bool IsSynchronizingPrimaryPaneState => _primaryPaneStateSynchronizationDepth > 0;

    private void BeginPrimaryPaneStateSynchronization()
    {
        _primaryPaneStateSynchronizationDepth++;
    }

    private void EndPrimaryPaneStateSynchronization()
    {
        if (_primaryPaneStateSynchronizationDepth <= 0)
        {
            _primaryPaneStateSynchronizationDepth = 0;
            return;
        }

        _primaryPaneStateSynchronizationDepth--;
    }

    private bool IsSynchronizingAssetDocumentState => _assetDocumentStateSynchronizationDepth > 0;

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

    private void ClearActiveTabState()
    {
        _documentWorkspace.SetActiveBundleTab(null);
        _bundlePane.SetSelectedBundle(null);
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        PrimaryPane.ClearItems(ResolvePrimaryPaneEmptyStateText());
        NotifyAssetDocumentHostStateChanged();
        RequestWorkspaceBrowserStatePersistence();
    }

    private AssetListEntryViewModel MapAssetEntry(AssetListItemDto item) =>
        new(
            item: item,
            assetTypeText: ResolveAssetTypeDisplayName(item.AssetType, item.AssetTypeDisplayName),
            sizeText: _localizedValueFormatter.FormatCompactByteSize(ResolveDisplaySizeInBytes(item.SizeInBytes, item.PreferredDisplaySizeInBytes)),
            stateText: item.IsModified
                ? GetString(ResourceKeys.AssetStateModified)
                : GetString(ResourceKeys.AssetStateOriginal));

    private AssetDependencyViewModel MapDependency(AssetDependencyDto dependency) =>
        new(
            Name: dependency.Name,
            BundleName: _bundlePane.ResolveBundleDisplayName(dependency.BundleId),
            MetadataText: BuildDependencyMetadataText(dependency),
            SupplementaryText: BuildDependencySupplementaryText(dependency));

    /// <summary>
    /// Bundle 外部依赖的友好名称已经在基础设施层做过一轮解析，
    /// Presentation 这里只负责补本地化类型说明和最终显示兜底。
    /// </summary>
    private BundleExternalDependencyViewModel MapBundleExternalDependency(BundleExternalDependencyDto dependency)
    {
        var referencePath = string.IsNullOrWhiteSpace(dependency.ReferencePath)
            ? string.Empty
            : dependency.ReferencePath.Trim().Replace('\\', '/');
        var referenceDisplayName = string.IsNullOrWhiteSpace(dependency.ReferenceDisplayName)
            ? Path.GetFileName(referencePath)
            : dependency.ReferenceDisplayName.Trim();
        var displayName = string.IsNullOrWhiteSpace(dependency.ResolvedBundleDisplayName)
            ? dependency.DisplayName
            : dependency.ResolvedBundleDisplayName;
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(referencePath)
            : displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(referencePath)
                ? GetString(ResourceKeys.BundleExplorerSelectionNone)
                : referencePath;
        }

        var kindText = GetBundleExternalDependencyKindText(dependency.Kind);
        // 当当前工作区已经能把 CAB 标识反查到具体资源包时，
        // 副标题继续保留 CAB 摘要，避免丢失更底层的 serialized file 定位信息。
        if (!string.IsNullOrWhiteSpace(dependency.ResolvedBundleDisplayName) &&
            !string.IsNullOrWhiteSpace(referenceDisplayName) &&
            !string.Equals(displayName, referenceDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            kindText = $"{kindText} · {referenceDisplayName}";
        }

        return new BundleExternalDependencyViewModel(
            DisplayName: displayName,
            KindText: kindText,
            ReferencePath: referencePath);
    }

    private string BuildDependencyMetadataText(AssetDependencyDto dependency)
    {
        var assetTypeText = ResolveAssetTypeDisplayName(dependency.AssetType, dependency.AssetTypeDisplayName);
        // Unity 的 PathId 是有符号 long，负数同样是合法对象标识。
        // 依赖卡片只应在 PathId 为 0（未解析 / 无效）时隐藏该字段。
        if (dependency.PathId != 0)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorDependencyMetadataWithPathIdFormat,
                assetTypeText,
                dependency.PathId.ToString(CultureInfo.InvariantCulture));
        }

        return assetTypeText;
    }

    private string BuildDependencySupplementaryText(AssetDependencyDto dependency)
    {
        if (dependency.DependencyDepth is not { } depth || depth <= 0)
        {
            return string.Empty;
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorDependencyDepthFormat,
            depth.ToString(CultureInfo.CurrentCulture));
    }

    private string GetBundleExternalDependencyKindText(BundleExternalDependencyKind kind)
    {
        return kind switch
        {
            BundleExternalDependencyKind.ArchiveSerializedFile => GetString(ResourceKeys.InspectorBundleExternalDependencyKindArchiveSerializedFile),
            BundleExternalDependencyKind.FilePath => GetString(ResourceKeys.InspectorBundleExternalDependencyKindFilePath),
            _ => GetString(ResourceKeys.InspectorBundleExternalDependencyKindUnknown)
        };
    }

    private IEnumerable<AssetTypeOptionViewModel> CreateAssetTypeOptions()
    {
        return new[]
        {
            new AssetTypeOptionViewModel(GetString(ResourceKeys.AssetTypeFilterAll), null),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Texture2D), AssetType.Texture2D),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Sprite), AssetType.Sprite),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.TextAsset), AssetType.TextAsset),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.AudioClip), AssetType.AudioClip),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Mesh), AssetType.Mesh),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Material), AssetType.Material)
        };
    }

    private string GetAssetTypeDisplayName(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.Sprite => GetString(ResourceKeys.AssetTypeDisplaySprite),
            AssetType.TextAsset => GetString(ResourceKeys.AssetTypeDisplayTextAsset),
            AssetType.AudioClip => GetString(ResourceKeys.AssetTypeDisplayAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.AssetTypeDisplayMesh),
            AssetType.Material => GetString(ResourceKeys.AssetTypeDisplayMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };

    private string ResolveAssetTypeDisplayName(AssetType assetType, string? assetTypeDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return GetAssetTypeDisplayName(assetType);
    }

    private void ResetWorkspaceToEmptyState()
    {
        CancelWorkspaceBrowserStatePersistence();
        _workspaceShell.Reset();
        _workspaceContextService.ClearCurrentWorkspace();
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        _bundlePane.Clear();
        _documentWorkspace.Clear();
        ClearActiveTabState();
    }

    /// <summary>
    /// 尝试从工作区元数据恢复左侧资源包搜索、已打开标签页和当前活动标签页。
    /// 如果没有可恢复状态，则由上层回退到默认首个资源包预览流程。
    /// </summary>
    private async Task<bool> TryRestoreWorkspaceBrowserStateAsync()
    {
        var browserState = _workspaceContextService.GetCurrentWorkspaceBrowserState();
        if (browserState is null)
        {
            return false;
        }

        _isRestoringWorkspaceBrowserState = true;
        try
        {
            _bundlePane.RestoreSearchState(browserState.BundleSearchKeyword);

            var restoredTabs = RestoreBundleTabsFromBrowserState(browserState);
            if (restoredTabs.Count == 0)
            {
                return await TryRestoreLastBundleFromBrowserStateAsync(browserState);
            }

            var activeTab = restoredTabs.FirstOrDefault(tab =>
                               string.Equals(tab.BundleId.Value, browserState.ActiveBundleTabId, StringComparison.Ordinal))
                           ?? restoredTabs.FirstOrDefault(tab =>
                               string.Equals(tab.BundleId.Value, browserState.LastBundleId, StringComparison.Ordinal))
                           ?? restoredTabs[0];

            await SetActiveBundleTabAsync(activeTab, writeSelectionLog: false);
            return true;
        }
        finally
        {
            _isRestoringWorkspaceBrowserState = false;
            PersistWorkspaceBrowserStateNow();
        }
    }

    private List<BundleDocumentTabViewModel> RestoreBundleTabsFromBrowserState(
        WorkspaceBrowserStateSnapshotDto browserState)
    {
        var restoredTabs = new List<BundleDocumentTabViewModel>();
        var restoredBundleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tabState in browserState.OpenBundleTabs)
        {
            if (string.IsNullOrWhiteSpace(tabState.BundleId) || !restoredBundleIds.Add(tabState.BundleId))
            {
                continue;
            }

            var bundleNode = _bundlePane.FindBundleNode(new BundleId(tabState.BundleId)) ??
                             _bundlePane.FindBundleNodeByLogicalPath(tabState.LogicalPath);
            if (bundleNode is null)
            {
                continue;
            }

            var tab = new BundleDocumentTabViewModel(
                bundleNode.BundleId,
                bundleNode.DisplayName,
                bundleNode.LogicalPath,
                tabState.IsPreview);
            tab.RestoreViewState(
                searchKeyword: tabState.SearchKeyword,
                selectedAssetType: ParseAssetTypeFilter(tabState.AssetTypeFilter),
                selectedAssetId: tabState.SelectedAssetId,
                showModifiedOnly: tabState.ShowModifiedOnly,
                showPreviewableOnly: tabState.ShowPreviewableOnly);
            tab.RestoreAssetTabs(
                RestoreAssetTabsForBundle(bundleNode.BundleId, tabState.OpenAssetTabs),
                tabState.ActiveAssetTabId);

            BundleTabs.Add(tab);
            restoredTabs.Add(tab);
        }

        return restoredTabs;
    }

    private async Task<bool> TryRestoreLastBundleFromBrowserStateAsync(WorkspaceBrowserStateSnapshotDto browserState)
    {
        if (string.IsNullOrWhiteSpace(browserState.LastBundleId))
        {
            return false;
        }

        var bundleNode = _bundlePane.FindBundleNode(new BundleId(browserState.LastBundleId));
        if (bundleNode is null)
        {
            return false;
        }

        var restoredTab = new BundleDocumentTabViewModel(
            bundleNode.BundleId,
            bundleNode.DisplayName,
            bundleNode.LogicalPath,
            isPreview: true);
        restoredTab.RestoreViewState(
            searchKeyword: browserState.AssetSearchKeyword,
            selectedAssetType: ParseAssetTypeFilter(browserState.AssetTypeFilter),
            selectedAssetId: browserState.LastAssetId,
            showModifiedOnly: browserState.ShowModifiedOnly,
            showPreviewableOnly: browserState.ShowPreviewableOnly);

        BundleTabs.Add(restoredTab);
        await SetActiveBundleTabAsync(restoredTab, writeSelectionLog: false);
        return true;
    }

    /// <summary>
    /// 节流当前工作区浏览状态的落盘动作，避免搜索输入时频繁写文件。
    /// 真实的元数据更新仍通过工作区上下文服务统一完成。
    /// </summary>
    private void RequestWorkspaceBrowserStatePersistence()
    {
        if (_workspaceShell.WorkspaceSummary is null || _isRestoringWorkspaceBrowserState)
        {
            return;
        }

        CancelWorkspaceBrowserStatePersistence();

        var cancellationSource = new CancellationTokenSource();
        _workspaceBrowserStatePersistenceCts = cancellationSource;
        _ = PersistWorkspaceBrowserStateDebouncedAsync(cancellationSource);
    }

    private async Task PersistWorkspaceBrowserStateDebouncedAsync(CancellationTokenSource cancellationSource)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(260), cancellationSource.Token);
            PersistWorkspaceBrowserStateCore();
        }
        catch (OperationCanceledException)
        {
            // 连续状态变化时允许新的保存请求接管，取消旧请求属于预期行为。
        }
        finally
        {
            if (ReferenceEquals(_workspaceBrowserStatePersistenceCts, cancellationSource))
            {
                _workspaceBrowserStatePersistenceCts = null;
            }

            cancellationSource.Dispose();
        }
    }

    private void PersistWorkspaceBrowserStateNow()
    {
        if (_workspaceShell.WorkspaceSummary is null || _isRestoringWorkspaceBrowserState)
        {
            return;
        }

        CancelWorkspaceBrowserStatePersistence();
        PersistWorkspaceBrowserStateCore();
    }

    private void PersistWorkspaceBrowserStateCore()
    {
        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        ActiveBundleTab?.CaptureFromPane(PrimaryPane);
        var snapshot = BuildWorkspaceBrowserStateSnapshot();
        _workspaceContextService.UpdateCurrentWorkspaceBrowserState(snapshot);
    }

    private WorkspaceBrowserStateSnapshotDto BuildWorkspaceBrowserStateSnapshot()
    {
        var activeTab = ActiveBundleTab;

        return new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: BundleSearchKeyword,
            AssetSearchKeyword: activeTab?.SearchKeyword ?? string.Empty,
            AssetTypeFilter: activeTab?.SelectedAssetType?.ToString(),
            ShowModifiedOnly: activeTab?.ShowModifiedOnly ?? false,
            ShowPreviewableOnly: activeTab?.ShowPreviewableOnly ?? false,
            OpenBundleTabs: BundleTabs
                .Select(CreateBundleTabMetadata)
                .ToArray(),
            ActiveBundleTabId: activeTab?.BundleId.Value,
            LastBundleId: activeTab?.BundleId.Value,
            LastAssetId: activeTab?.ActiveAssetTab?.AssetId.Value ?? activeTab?.SelectedAssetId);
    }

    private WorkspaceMetadataBundleTabDto CreateBundleTabMetadata(BundleDocumentTabViewModel tab)
    {
        return new WorkspaceMetadataBundleTabDto(
            BundleId: tab.BundleId.Value,
            LogicalPath: tab.LogicalPath,
            IsPreview: tab.IsPreview,
            SelectedAssetId: tab.SelectedAssetId,
            SearchKeyword: tab.SearchKeyword,
            AssetTypeFilter: tab.SelectedAssetType?.ToString(),
            ShowModifiedOnly: tab.ShowModifiedOnly,
            ShowPreviewableOnly: tab.ShowPreviewableOnly,
            OpenAssetTabs: tab.AssetTabs
                .Select(CreateAssetTabMetadata)
                .ToArray(),
            ActiveAssetTabId: tab.ActiveAssetTab?.DocumentId);
    }

    private IReadOnlyList<AssetDocumentTabViewModel> RestoreAssetTabsForBundle(
        BundleId bundleId,
        IReadOnlyList<WorkspaceMetadataAssetTabDto>? assetTabStates)
    {
        if (assetTabStates is null || assetTabStates.Count == 0)
        {
            return Array.Empty<AssetDocumentTabViewModel>();
        }

        var restoredTabs = new List<AssetDocumentTabViewModel>(assetTabStates.Count);
        var restoredAssetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assetTabState in assetTabStates)
        {
            if (string.IsNullOrWhiteSpace(assetTabState.AssetId) ||
                !restoredAssetIds.Add(assetTabState.AssetId))
            {
                continue;
            }

            var assetTab = CreateAssetDocumentTab(
                bundleId,
                new AssetId(assetTabState.AssetId),
                assetTabState.AssetName);
            assetTab.RestoreSelectedInspectorSection(assetTabState.SelectedInspectorSection);
            restoredTabs.Add(assetTab);
        }

        return restoredTabs;
    }

    private WorkspaceMetadataAssetTabDto CreateAssetTabMetadata(AssetDocumentTabViewModel tab)
    {
        return new WorkspaceMetadataAssetTabDto(
            AssetId: tab.AssetId.Value,
            AssetName: tab.DisplayName,
            SelectedInspectorSection: tab.Inspector.SelectedSectionKey);
    }

    private void CancelWorkspaceBrowserStatePersistence()
    {
        if (_workspaceBrowserStatePersistenceCts is null)
        {
            return;
        }

        _workspaceBrowserStatePersistenceCts.Cancel();
        _workspaceBrowserStatePersistenceCts.Dispose();
        _workspaceBrowserStatePersistenceCts = null;
    }

    private static AssetType? ParseAssetTypeFilter(string? assetTypeFilter)
    {
        if (string.IsNullOrWhiteSpace(assetTypeFilter))
        {
            return null;
        }

        return Enum.TryParse<AssetType>(assetTypeFilter, ignoreCase: true, out var parsedAssetType)
            ? parsedAssetType
            : null;
    }

    private string ResolvePrimaryPaneEmptyStateText()
        => _bundlePane.ResolvePrimaryPaneEmptyStateText(
            noWorkspaceText: GetString(ResourceKeys.BundleExplorerNoWorkspace),
            emptyWorkspaceText: GetString(ResourceKeys.BundleExplorerEmptyWorkspace),
            noOpenTabsText: GetString(ResourceKeys.BundleExplorerNoOpenTabs));

    private async Task LoadInspectorPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        long previewRequestVersion)
    {
        if (!TryCreateAssetPreviewQuery(details, out var previewQuery))
        {
            if (assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
            {
                assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            }

            return;
        }

        var previewResult = await _workspaceBrowserService.GetAssetPreviewAsync(previewQuery);
        if (previewResult.IsCancelled || !assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
        {
            return;
        }

        if (!previewResult.IsSuccess || previewResult.Value is null)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                details.Name,
                FormatFirstErrorMessage(previewResult.Errors)));
            assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            return;
        }

        var preview = previewResult.Value;
        var previewInfoRows = _assetPreviewDisplayFormatter.BuildPreviewInfoRows(preview.PreviewInfoItems);
        if (preview.PreferredDisplaySizeInBytes is { } preferredDisplaySizeInBytes && preferredDisplaySizeInBytes > 0)
        {
            assetTab.Inspector.OverrideSizeText(_localizedValueFormatter.FormatCompactByteSize(preferredDisplaySizeInBytes));
        }

        try
        {
            switch (preview.Kind)
            {
                case AssetPreviewKind.Image:
                {
                    var imageSource = await CreatePreviewImageSourceAsync(preview.BinaryContent);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
                    {
                        return;
                    }

                    if (imageSource is null)
                    {
                        _appLogService.AppendWarning(_localizationService.GetFormattedString(
                            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                            details.Name,
                            GetString(ResourceKeys.BundleExplorerPreviewImageDecodeFailed)));
                        assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyImagePreview(
                        imageSource,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Text:
                {
                    if (string.IsNullOrWhiteSpace(preview.TextContent))
                    {
                        assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyTextPreview(
                        preview.TextContent,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Audio:
                {
                    var audioPreviewSource = await CreatePreviewAudioSourceAsync(preview.BinaryContent, preview.ContentType);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
                    {
                        audioPreviewSource?.Stream.Dispose();
                        return;
                    }

                    if (audioPreviewSource is null)
                    {
                        _appLogService.AppendWarning(_localizationService.GetFormattedString(
                            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                            details.Name,
                            GetString(ResourceKeys.BundleExplorerPreviewAudioDecodeFailed)));
                        assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyAudioPreview(
                        audioPreviewSource.MediaSource,
                        audioPreviewSource.Stream,
                        previewInfoRows,
                        _assetPreviewDisplayFormatter.BuildAudioPreviewDisplayState(
                            preview.PreviewInfoItems,
                            preview.PreferredDisplaySizeInBytes),
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Summary:
                {
                    var summaryText = !string.IsNullOrWhiteSpace(preview.TextContent)
                        ? preview.TextContent
                        : _assetPreviewDisplayFormatter.BuildPreviewMessageText(preview.PreviewMessage);

                    if (string.IsNullOrWhiteSpace(summaryText))
                    {
                        assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplySummaryPreview(
                        summaryText,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                default:
                    assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
            }
        }
        catch (Exception)
        {
            if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
            {
                return;
            }

            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                details.Name,
                GetString(ResourceKeys.BundleExplorerPreviewUnexpectedDisplayFailure)));
            assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
        }
    }

    private bool TryCreateAssetPreviewQuery(
        AssetDetailsDto details,
        [NotNullWhen(true)] out GetAssetPreviewQuery? previewQuery)
    {
        if (_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) &&
            workspaceEntry is not null &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceKey) &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceDataRootPath))
        {
            previewQuery = new GetAssetPreviewQuery(
                WorkspaceId: details.WorkspaceId,
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                BundleId: details.BundleId,
                AssetId: details.AssetId);
            return true;
        }

        var activeWorkspaceNavigationRequest = _workspaceShell.ActiveWorkspaceNavigationRequest;
        if (activeWorkspaceNavigationRequest is null ||
            string.IsNullOrWhiteSpace(activeWorkspaceNavigationRequest.WorkspaceDataRootPath))
        {
            previewQuery = null;
            return false;
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            activeWorkspaceNavigationRequest.SourcePath,
            activeWorkspaceNavigationRequest.WorkspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            previewQuery = null;
            return false;
        }

        previewQuery = new GetAssetPreviewQuery(
            WorkspaceId: details.WorkspaceId,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: activeWorkspaceNavigationRequest.WorkspaceDataRootPath,
            BundleId: details.BundleId,
            AssetId: details.AssetId);
        return true;
    }

    private static async Task<BitmapImage?> CreatePreviewImageSourceAsync(byte[]? imagePayload)
    {
        if (imagePayload is not { Length: > 0 })
        {
            return null;
        }

        var bitmapImage = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(imagePayload);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    private static async Task<AudioPreviewSource?> CreatePreviewAudioSourceAsync(byte[]? audioPayload, string contentType)
    {
        if (audioPayload is not { Length: > 0 } || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();

        try
        {
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(audioPayload);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var mediaSource = MediaSource.CreateFromStream(stream, contentType.Trim());
            return new AudioPreviewSource(mediaSource, stream);
        }
        catch
        {
            stream.Dispose();
            return null;
        }
    }

    /// <summary>
    /// 应用层预览诊断只暴露稳定枚举和结构化 detail message，
    /// 真正面向用户的中文文案在 Presentation 层完成映射，便于后续工作台和诊断页复用同一份诊断语义。
    /// </summary>
    private void ApplyPreviewDiagnostic(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ApplyPreviewDiagnostic(
            cacheStatusText: GetPreviewCacheStatusText(diagnostic.CacheStatus),
            fallbackReasonText: GetPreviewFallbackReasonText(diagnostic.FallbackReason),
            diagnosticDetailText: LocalizedMessageDisplayFormatter.Format(
                diagnostic.DetailMessage,
                _localizationService));
    }

    private string GetPreviewCacheStatusText(AssetPreviewCacheStatusKind cacheStatus)
    {
        return cacheStatus switch
        {
            AssetPreviewCacheStatusKind.Hit => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusHit),
            AssetPreviewCacheStatusKind.Missing => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusMissing),
            AssetPreviewCacheStatusKind.IdentityChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusIdentityChanged),
            AssetPreviewCacheStatusKind.SourceFingerprintChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusSourceFingerprintChanged),
            AssetPreviewCacheStatusKind.GeneratorVersionChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusGeneratorVersionChanged),
            AssetPreviewCacheStatusKind.AudioTranscoderChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusAudioTranscoderChanged),
            _ => cacheStatus.ToString()
        };
    }

    private string GetPreviewFallbackReasonText(AssetPreviewFallbackReasonKind fallbackReason)
    {
        return fallbackReason switch
        {
            AssetPreviewFallbackReasonKind.None => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonNone),
            AssetPreviewFallbackReasonKind.MissingLocator => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonMissingLocator),
            AssetPreviewFallbackReasonKind.DecryptorResolveFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonDecryptorResolveFailed),
            AssetPreviewFallbackReasonKind.DecryptionFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonDecryptionFailed),
            AssetPreviewFallbackReasonKind.GenerationFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonGenerationFailed),
            _ => fallbackReason.ToString()
        };
    }

    private string BuildReplacementSourceText(
        AssetDetailsDto details,
        WorkspaceReplacementCandidateDto? pendingWorkspaceCandidate,
        FileReplacementCandidateDto? pendingFileCandidate)
    {
        if (pendingFileCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceFileCandidateFormat,
                pendingFileCandidate.SourceFileName,
                pendingFileCandidate.SourceFilePath,
                GetAssetTypeDisplayName(pendingFileCandidate.SourceAssetType));
        }

        if (pendingWorkspaceCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceCandidateFormat,
                pendingWorkspaceCandidate.SourceBundleDisplayName,
                pendingWorkspaceCandidate.SourceAssetName,
                GetAssetTypeDisplayName(pendingWorkspaceCandidate.SourceAssetType));
        }

        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName) &&
            !string.IsNullOrWhiteSpace(details.FileReplacementSourcePath) &&
            details.FileReplacementSourceAssetType is { } fileAssetType)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceFileAppliedFormat,
                details.FileReplacementSourceFileName,
                details.FileReplacementSourcePath,
                GetAssetTypeDisplayName(fileAssetType));
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) &&
            !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceBundleDisplayName) &&
            details.WorkspaceReplacementSourceAssetType is { } sourceAssetType)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceAppliedFormat,
                details.WorkspaceReplacementSourceBundleDisplayName,
                details.WorkspaceReplacementSourceAssetName,
                GetAssetTypeDisplayName(sourceAssetType));
        }

        return GetString(ResourceKeys.InspectorReplacementSourceNone);
    }

    private sealed record AudioPreviewSource(
        MediaSource MediaSource,
        IRandomAccessStream Stream);

    private string BuildReplacementStatusText(
        AssetDetailsDto details,
        WorkspaceReplacementCandidateDto? pendingWorkspaceCandidate,
        FileReplacementCandidateDto? pendingFileCandidate)
    {
        if (pendingFileCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusFilePendingFormat,
                pendingFileCandidate.SourceFileName);
        }

        if (pendingWorkspaceCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusPendingFormat,
                pendingWorkspaceCandidate.SourceAssetName);
        }

        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusFileAppliedFormat,
                details.FileReplacementSourceFileName);
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusAppliedFormat,
                details.WorkspaceReplacementSourceAssetName);
        }

        return GetString(ResourceKeys.InspectorReplacementStatusNone);
    }

    private string BuildChangeSummaryText(AssetDetailsDto details)
    {
        var hasFileReplacement = !string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName) &&
                                 !string.IsNullOrWhiteSpace(details.FileReplacementSourcePath);
        var hasReplacement = !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) &&
                             !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceBundleDisplayName);
        var hasExtraction = details.LastExtractedAtUtc.HasValue;

        if (hasFileReplacement && hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryFileReplacementAndExtractFormat,
                details.FileReplacementSourceFileName!,
                FormatTimestamp(details.LastExtractedAtUtc!.Value));
        }

        if (hasFileReplacement)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryFileReplacementOnlyFormat,
                details.FileReplacementSourceFileName!);
        }

        if (hasReplacement && hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryReplacementAndExtractFormat,
                details.WorkspaceReplacementSourceBundleDisplayName!,
                details.WorkspaceReplacementSourceAssetName!,
                FormatTimestamp(details.LastExtractedAtUtc!.Value));
        }

        if (hasReplacement)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryReplacementOnlyFormat,
                details.WorkspaceReplacementSourceBundleDisplayName!,
                details.WorkspaceReplacementSourceAssetName!);
        }

        if (hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryExtractOnlyFormat,
                FormatTimestamp(details.LastExtractedAtUtc!.Value));
        }

        return GetString(ResourceKeys.InspectorChangeSummaryNone);
    }

    private static bool HasAppliedReplacement(AssetDetailsDto details) =>
        !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
        !string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName);

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private static long ResolveDisplaySizeInBytes(long sizeInBytes, long? preferredDisplaySizeInBytes)
    {
        return preferredDisplaySizeInBytes is { } preferredSize && preferredSize > 0
            ? preferredSize
            : sizeInBytes;
    }

    private string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

    private string? NormalizeSearchKeyword(string searchKeyword) =>
        string.IsNullOrWhiteSpace(searchKeyword) ? null : searchKeyword.Trim();

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

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

        AppendResultFailure(result.Errors);
        return false;
    }

    private void AppendResultFailure(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var firstError = errors[0];
        var message = firstError.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                firstError.MessageToken,
                firstError.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : _localizationService.GetRequiredString(firstError.MessageToken);

        _appLogService.AppendError(message);
        ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationErrorTitle),
            message,
            AppNotificationSeverity.Error);
    }

    private string FormatFirstErrorMessage(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return GetString(ResourceKeys.CommonUnexpectedFailure);
        }

        var firstError = errors[0];
        return firstError.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                firstError.MessageToken,
                firstError.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : _localizationService.GetRequiredString(firstError.MessageToken);
    }

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        NotifyActionStates();
    }

    private void NotifyAssetDocumentHostStateChanged()
    {
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
    }

    private void OnBundlePaneBrowserStateChanged(object? sender, EventArgs eventArgs)
    {
        RequestWorkspaceBrowserStatePersistence();
    }

    /// <summary>
    /// 主页面仍保留兼容旧绑定入口，因此这里把子 ViewModel 的状态变化桥接回外观层属性。
    /// 等页面绑定逐步迁移到更细粒度对象后，这层桥接可以继续收缩。
    /// </summary>
    private void OnBundlePanePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.PropertyName))
        {
            return;
        }

        OnPropertyChanged(eventArgs.PropertyName);
    }

    private void OnWorkspaceShellPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.PropertyName))
        {
            return;
        }

        switch (eventArgs.PropertyName)
        {
            case nameof(BundleExplorerWorkspaceShellViewModel.HasWorkspace):
                _bundlePane.UpdateInteractionState(HasWorkspace, IsBusy);
                NotifyCommandStates();
                OnPropertyChanged(nameof(HasWorkspace));
                break;
            case nameof(BundleExplorerWorkspaceShellViewModel.WorkspaceContentVisibility):
                OnPropertyChanged(nameof(WorkspaceContentVisibility));
                break;
            case nameof(BundleExplorerWorkspaceShellViewModel.WorkspaceEmptyStateVisibility):
                OnPropertyChanged(nameof(WorkspaceEmptyStateVisibility));
                break;
            case nameof(BundleExplorerWorkspaceShellViewModel.WorkspaceStatusChips):
                OnPropertyChanged(nameof(WorkspaceStatusChips));
                break;
        }
    }

    private void OnDocumentWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.PropertyName))
        {
            return;
        }

        OnPropertyChanged(eventArgs.PropertyName);

        switch (eventArgs.PropertyName)
        {
            case nameof(BundleExplorerDocumentWorkspaceViewModel.ActiveBundleTab):
            case nameof(BundleExplorerDocumentWorkspaceViewModel.HasOpenTabs):
                break;
            case nameof(BundleExplorerDocumentWorkspaceViewModel.CurrentAssetTabs):
            case nameof(BundleExplorerDocumentWorkspaceViewModel.ActiveAssetDocumentTab):
            case nameof(BundleExplorerDocumentWorkspaceViewModel.HasAssetDocumentTabs):
            case nameof(BundleExplorerDocumentWorkspaceViewModel.AssetDocumentTabStripVisibility):
                OnPropertyChanged(nameof(Inspector));
                NotifyActionStates();
                break;
        }
    }

    private void NotifyActionStates()
    {
        SyncInspectorActionStates();
        OnPropertyChanged(nameof(HasSelectedAsset));
        OnPropertyChanged(nameof(CanExtractSelectedAsset));
        OnPropertyChanged(nameof(CanSelectFileReplacementSource));
        OnPropertyChanged(nameof(CanSelectWorkspaceReplacementSource));
    }

    /// <summary>
    /// 顶部命令区和右侧检查器共用同一组选中状态，
    /// 因此当忙碌状态、候选替换源或选中资源变化时，需要同步刷新右侧按钮可用性。
    /// </summary>
    private void SyncInspectorActionStates()
    {
        if (!TryGetActiveAssetDocumentDetails(out var assetTab, out var details))
        {
            Inspector.CanExtract = false;
            Inspector.CanSelectFileReplacementSource = false;
            Inspector.CanSelectReplacementSource = false;
            Inspector.CanApplyReplacement = false;
            Inspector.CanUndoReplacement = false;
            return;
        }

        Inspector.CanExtract = !IsBusy && assetTab.CanEdit && details.CanExtract;
        Inspector.CanSelectFileReplacementSource = !IsBusy && assetTab.CanEdit && details.CanReplaceFromFile;
        Inspector.CanSelectReplacementSource = !IsBusy && assetTab.CanEdit && details.CanReplaceFromWorkspace;
        Inspector.CanApplyReplacement = !IsBusy &&
                                       assetTab.CanEdit &&
                                       (assetTab.PendingWorkspaceReplacementCandidate is not null ||
                                        assetTab.PendingFileReplacementCandidate is not null);
        Inspector.CanUndoReplacement = !IsBusy && assetTab.CanEdit && HasAppliedReplacement(details);
    }

    private bool TryGetActiveAssetDocumentDetails(
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        assetTab = ActiveAssetDocumentTab;
        if (assetTab?.Details is null)
        {
            details = null;
            return false;
        }

        details = assetTab.Details;
        return true;
    }
}
