using System.Globalization;
using System.Collections.ObjectModel;
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
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly List<BundleGroupViewModel> _allBundleGroups = new();
    private readonly Dictionary<string, BundleNodeViewModel> _bundleLookup = new(StringComparer.Ordinal);
    private bool _isInitialized;
    private bool _isBusy;
    private bool _hasWorkspace;
    private bool _isBundlePaneCollapsed;
    private bool _isBundleSearchExpanded;
    private string _bundleSearchKeyword = string.Empty;
    private WorkspaceSummaryDto? _workspaceSummary;
    private BundleDocumentTabViewModel? _activeBundleTab;
    private BundleNodeViewModel? _selectedBundleNode;
    private AssetDetailsDto? _selectedAssetDetails;
    private FileReplacementCandidateDto? _pendingFileReplacementCandidate;
    private WorkspaceReplacementCandidateDto? _pendingWorkspaceReplacementCandidate;
    private bool _canEditSelectedAsset;
    private bool _isRestoringWorkspaceBrowserState;
    private CancellationTokenSource? _workspaceBrowserStatePersistenceCts;
    private WorkspaceNavigationRequest? _activeWorkspaceNavigationRequest;
    private long _previewRequestVersion;
    private int _primaryPaneStateSynchronizationDepth;

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
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));

        PrimaryPane = new AssetPaneViewModel();
        Inspector = new InspectorPanelViewModel();
        PrimaryPane.SupportsBatchExtractionQuickAction = true;

        RefreshCommand = new AsyncRelayCommand(
            executeAsync: RefreshAsync,
            canExecute: () => !IsBusy);

        PrimaryPane.ReplaceAssetTypeOptions(CreateAssetTypeOptions());
        PrimaryPane.ConfigurePane(PaneId.New(), GetString(ResourceKeys.BundleExplorerPrimaryPaneTitle), GetString(ResourceKeys.BundleExplorerNoAssets));
        Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        UpdateWorkspaceState(hasWorkspace: false);
        RefreshWorkspaceStatusChips();
    }

    public ObservableCollection<BundleGroupViewModel> BundleGroups { get; } = new();

    public ObservableCollection<BundleDocumentTabViewModel> BundleTabs { get; } = new();

    public ObservableCollection<WorkspaceStatusChipViewModel> WorkspaceStatusChips { get; } = new();

    public AssetPaneViewModel PrimaryPane { get; }

    public InspectorPanelViewModel Inspector { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                PrimaryPane.IsBusy = value;
                NotifyCommandStates();
                OnPropertyChanged(nameof(CanToggleBundleSearch));
                OnPropertyChanged(nameof(CanToggleBundlePaneCollapse));
            }
        }
    }

    public bool HasWorkspace
    {
        get => _hasWorkspace;
        private set
        {
            if (SetProperty(ref _hasWorkspace, value))
            {
                OnPropertyChanged(nameof(WorkspaceContentVisibility));
                OnPropertyChanged(nameof(WorkspaceEmptyStateVisibility));
                OnPropertyChanged(nameof(BundlePaneEmptyStateText));
                OnPropertyChanged(nameof(BundlePaneEmptyStateVisibility));
                OnPropertyChanged(nameof(BundleListVisibility));
                OnPropertyChanged(nameof(BundleSearchEmptyStateVisibility));
                OnPropertyChanged(nameof(CanToggleBundleSearch));
                OnPropertyChanged(nameof(CanToggleBundlePaneCollapse));
                RefreshWorkspaceStatusChips();
            }
        }
    }

    /// <summary>
    /// 控制左侧资源包列表是否收缩为窄条摘要。
    /// 收缩后仍保留工作区概况与当前选中资源包摘要，
    /// 这样可以把更多横向空间让给资产列表和右侧详情区。
    /// </summary>
    public bool IsBundlePaneCollapsed
    {
        get => _isBundlePaneCollapsed;
        private set
        {
            if (SetProperty(ref _isBundlePaneCollapsed, value))
            {
                OnPropertyChanged(nameof(BundlePaneCollapseToolTip));
            }
        }
    }

    public bool IsBundleSearchExpanded
    {
        get => _isBundleSearchExpanded;
        private set
        {
            if (SetProperty(ref _isBundleSearchExpanded, value))
            {
                OnPropertyChanged(nameof(BundleSearchToggleGlyph));
                OnPropertyChanged(nameof(BundleSearchToggleToolTip));
            }
        }
    }

    /// <summary>
    /// 当前阶段摘要文本。
    /// 这里保留为一条短说明，避免再次回到“大段状态说明”造成的拥挤感。
    /// </summary>
    public string StageStatusText => GetString(ResourceKeys.BundleExplorerStageStatusText);

    public string BundleSearchKeyword
    {
        get => _bundleSearchKeyword;
        set
        {
            if (SetProperty(ref _bundleSearchKeyword, value))
            {
                OnPropertyChanged(nameof(BundleSearchEmptyStateText));
                ApplyBundleGroupFilter();
                RequestWorkspaceBrowserStatePersistence();
            }
        }
    }

    public string BundleSearchToggleGlyph => IsBundleSearchExpanded ? "\uE711" : "\uE721";

    public string BundleSearchToggleToolTip => IsBundleSearchExpanded
        ? GetString(ResourceKeys.BundleExplorerPageBundleSearchToggleButtonCloseToolTip)
        : GetString(ResourceKeys.BundleExplorerPageBundleSearchToggleButtonOpenToolTip);

    public string BundlePaneCollapseToolTip => IsBundlePaneCollapsed
        ? GetString(ResourceKeys.BundleExplorerPageBundlePaneCollapseButtonExpandToolTip)
        : GetString(ResourceKeys.BundleExplorerPageBundlePaneCollapseButtonCollapseToolTip);

    public string BundleSearchEmptyStateText =>
        _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerBundleSearchEmptyStateFormat,
            string.IsNullOrWhiteSpace(BundleSearchKeyword) ? GetString(ResourceKeys.BundleExplorerSelectionNone) : BundleSearchKeyword.Trim());

    public Visibility WorkspaceContentVisibility => HasWorkspace ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkspaceEmptyStateVisibility => HasWorkspace ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// 左侧资源包区域的空状态文案。
    /// 未打开工作区时提示用户先创建或打开工作区；
    /// 已打开但尚未导入资源时，则明确提示当前是空工作区。
    /// </summary>
    public string BundlePaneEmptyStateText => HasWorkspace
        ? GetString(ResourceKeys.BundleExplorerEmptyWorkspace)
        : GetString(ResourceKeys.BundleExplorerNoWorkspace);

    public Visibility BundlePaneEmptyStateVisibility =>
        !HasWorkspace || (HasWorkspace && _allBundleGroups.Count == 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BundleListVisibility => HasWorkspace && BundleGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BundleSearchEmptyStateVisibility =>
        HasWorkspace &&
        _allBundleGroups.Count > 0 &&
        BundleGroups.Count == 0 &&
        !string.IsNullOrWhiteSpace(BundleSearchKeyword)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool HasOpenTabs => _activeBundleTab is not null;

    public BundleDocumentTabViewModel? ActiveBundleTab
    {
        get => _activeBundleTab;
        private set
        {
            if (SetProperty(ref _activeBundleTab, value))
            {
                OnPropertyChanged(nameof(HasOpenTabs));
                RefreshWorkspaceStatusChips();
            }
        }
    }

    public bool HasSelectedAsset => _selectedAssetDetails is not null;

    public bool CanExtractSelectedAsset => !IsBusy && _canEditSelectedAsset && _selectedAssetDetails?.CanExtract == true;

    public bool CanSelectFileReplacementSource => !IsBusy && _canEditSelectedAsset && _selectedAssetDetails?.CanReplaceFromFile == true;

    public bool CanSelectWorkspaceReplacementSource => !IsBusy && _canEditSelectedAsset && _selectedAssetDetails?.CanReplaceFromWorkspace == true;

    public bool CanToggleBundleSearch => HasWorkspace && !IsBusy;

    public bool CanToggleBundlePaneCollapse => HasWorkspace && !IsBusy;

    public string CollapsedBundlePaneBundleCountText => GetVisibleBundleCount().ToString(CultureInfo.CurrentCulture);

    public string CollapsedBundlePaneGroupCountText => BundleGroups.Count.ToString(CultureInfo.CurrentCulture);

    public string CollapsedBundlePaneSelectedBundleShortText => BuildCollapsedBundlePaneSelectedBundleShortText();

    public string CollapsedBundlePaneSelectedBundleToolTip =>
        _selectedBundleNode?.DisplayName ?? GetString(ResourceKeys.BundleExplorerSelectionNone);

    /// <summary>
    /// 向页面层提供“当前实际已绑定工作区”的稳定导航参数。
    /// 这样窗口注册逻辑可以依赖成功加载后的结果，而不是依赖页面传入的临时请求。
    /// </summary>
    public bool TryGetCurrentWorkspaceNavigationRequest([NotNullWhen(true)] out WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        workspaceNavigationRequest = _activeWorkspaceNavigationRequest
                                     ?? BuildNavigationRequestFromWorkspaceSummary(_workspaceSummary);
        return workspaceNavigationRequest is not null;
    }

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

        if (TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
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
        if (_workspaceSummary is not null)
        {
            await BindWorkspaceAsync(
                clearExistingLogs: false,
                workspaceSummary: _workspaceSummary,
                workspaceNavigationRequest: _activeWorkspaceNavigationRequest);
            return;
        }

        if (TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
        {
            await BindWorkspaceAsync(
                clearExistingLogs: true,
                workspaceSummary: currentWorkspaceSummary,
                workspaceNavigationRequest: currentNavigationRequest);
            return;
        }

        if (_activeWorkspaceNavigationRequest is not null)
        {
            await LoadWorkspaceAsync(clearExistingLogs: true, _activeWorkspaceNavigationRequest);
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
        if (tab is null || _workspaceSummary is null)
        {
            return;
        }

        if (ReferenceEquals(ActiveBundleTab, tab))
        {
            SetSelectedBundle(FindBundleNode(tab.BundleId));
            return;
        }

        CaptureActiveTabState();
        await SetActiveBundleTabAsync(tab, writeSelectionLog: false);
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
        if (wasActive)
        {
            CaptureActiveTabState();
        }

        BundleTabs.RemoveAt(removedIndex);

        if (BundleTabs.Count == 0)
        {
            ClearActiveTabState();
            return;
        }

        if (!wasActive)
        {
            RefreshWorkspaceStatusChips();
            RequestWorkspaceBrowserStatePersistence();
            return;
        }

        var nextIndex = Math.Min(removedIndex, BundleTabs.Count - 1);
        await SetActiveBundleTabAsync(BundleTabs[nextIndex], writeSelectionLog: false);
    }

    /// <summary>
    /// 切换左侧资源包搜索框的展开状态。
    /// 收起时同步清空关键字，确保列表快速回到完整浏览状态。
    /// </summary>
    public void ToggleBundleSearch()
    {
        if (!CanToggleBundleSearch)
        {
            return;
        }

        if (IsBundleSearchExpanded)
        {
            BundleSearchKeyword = string.Empty;
            IsBundleSearchExpanded = false;
            return;
        }

        IsBundleSearchExpanded = true;
    }

    /// <summary>
    /// 在完整浏览视图与紧凑摘要窄条之间切换左栏形态。
    /// 收缩时同步关闭搜索，避免隐藏输入态残留在页面状态里。
    /// </summary>
    public void ToggleBundlePaneCollapsed()
    {
        if (!CanToggleBundlePaneCollapse)
        {
            return;
        }

        if (!IsBundlePaneCollapsed)
        {
            BundleSearchKeyword = string.Empty;
            IsBundleSearchExpanded = false;
        }

        IsBundlePaneCollapsed = !IsBundlePaneCollapsed;
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

            var resolvedNavigationRequest = workspaceNavigationRequest
                                            ?? _activeWorkspaceNavigationRequest
                                            ?? BuildNavigationRequestFromWorkspaceSummary(_workspaceContextService.CurrentWorkspaceSummary);
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
                                            ?? BuildNavigationRequestFromWorkspaceSummary(workspaceSummary);
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
        var resolvedWorkspaceDataRootPath = resolvedNavigationRequest.WorkspaceDataRootPath;

        _workspaceSummary = workspaceSummary;
        _activeWorkspaceNavigationRequest = resolvedNavigationRequest with
        {
            SourcePath = workspaceSummary.SourcePath,
            WorkspaceDisplayName = workspaceSummary.DisplayName,
            WorkspaceDataRootPath = resolvedWorkspaceDataRootPath ?? string.Empty
        };

        _workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            resolvedWorkspaceDataRootPath,
            resolvedNavigationRequest.AssetBundleDecryptorId);
        UpdateWorkspaceState(hasWorkspace: true);
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

        RebuildBundleGroups(bundleTree);
        BundleTabs.Clear();
        ActiveBundleTab = null;

        if (!await TryRestoreWorkspaceBrowserStateAsync())
        {
            var firstBundle = BundleGroups.SelectMany(static group => group.Bundles).FirstOrDefault();
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
        if (_activeWorkspaceNavigationRequest is null)
        {
            return;
        }

        await LoadWorkspaceAsync(
            clearExistingLogs: false,
            workspaceNavigationRequest: _activeWorkspaceNavigationRequest,
            showLoadedNotification: showLoadedNotification,
            allowBusyReentry: allowBusyReentry);
    }

    private async Task RefreshPaneAsync(AssetPaneViewModel pane, bool clearInspectorOnEmpty)
    {
        if (_workspaceSummary is null)
        {
            return;
        }

        var bundleNode = FindBundleNodeByDisplayName(pane.CurrentBundleDisplayName);
        if (bundleNode is null)
        {
            pane.ClearItems(GetString(ResourceKeys.BundleExplorerNoAssets));
            if (clearInspectorOnEmpty)
            {
                ClearSelectedAssetState();
                Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
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
        if (_workspaceSummary is null)
        {
            return;
        }

        var assetListResult = await _workspaceBrowserService.LoadAssetListAsync(
            new LoadAssetListQuery(
                WorkspaceId: _workspaceSummary.WorkspaceId,
                BundleId: bundleNode.BundleId,
                SearchKeyword: NormalizeSearchKeyword(pane.SearchKeyword),
                AssetTypeFilter: pane.SelectedAssetTypeOption?.AssetType));

        if (!TryGetValue(assetListResult, out var assetList))
        {
            pane.ClearItems(GetString(ResourceKeys.BundleExplorerNoAssets));
            if (clearInspectorOnEmpty)
            {
                ClearSelectedAssetState();
                Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
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

        if (pane.SelectedAsset is not null)
        {
            await SelectAssetAsync(pane, pane.SelectedAsset, writeLog: true);
        }
        else if (clearInspectorOnEmpty)
        {
            ClearSelectedAssetState();
            Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogBundleLoaded,
            pane.PaneTitle,
            bundleNode.DisplayName,
            assetList.FilteredCount));
    }

    private async Task HandleAssetSelectionAsync(AssetPaneViewModel pane)
    {
        if (pane.SelectedAsset is null)
        {
            if (ReferenceEquals(pane, PrimaryPane))
            {
                ClearSelectedAssetState();
                Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
            }

            return;
        }

        await SelectAssetAsync(pane, pane.SelectedAsset, writeLog: true);
    }

    private async Task SelectAssetAsync(AssetPaneViewModel pane, AssetListEntryViewModel entry, bool writeLog)
    {
        if (_workspaceSummary is null)
        {
            return;
        }

        var openResult = await _workspaceBrowserService.OpenAssetInPaneAsync(
            new OpenAssetInPaneCommand(
                WorkspaceId: _workspaceSummary.WorkspaceId,
                BundleId: entry.Item.BundleId,
                AssetId: entry.Item.AssetId,
                PaneId: pane.PaneId));

        if (!TryGetValue(openResult, out _))
        {
            return;
        }

        var detailsResult = await _workspaceBrowserService.GetAssetDetailsAsync(
            new GetAssetDetailsQuery(
                WorkspaceId: _workspaceSummary.WorkspaceId,
                BundleId: entry.Item.BundleId,
                AssetId: entry.Item.AssetId));

        if (!TryGetValue(detailsResult, out var details))
        {
            return;
        }

        var isSameTargetAsset = _selectedAssetDetails is not null &&
                                _selectedAssetDetails.BundleId.Equals(details.BundleId) &&
                                _selectedAssetDetails.AssetId.Equals(details.AssetId);

        if (!isSameTargetAsset)
        {
            _pendingFileReplacementCandidate = null;
            _pendingWorkspaceReplacementCandidate = null;
        }

        ApplyInspector(
            details,
            isSameTargetAsset ? _pendingWorkspaceReplacementCandidate : null,
            isSameTargetAsset ? _pendingFileReplacementCandidate : null,
            enableActions: ReferenceEquals(pane, PrimaryPane));
        var previewRequestVersion = BeginPreviewRequest();
        Inspector.SetPreviewLoading(GetString(ResourceKeys.InspectorPreviewLoadingText));
        _ = LoadInspectorPreviewAsync(details, previewRequestVersion);

        if (ReferenceEquals(pane, PrimaryPane))
        {
            CaptureActiveTabState();
        }

        if (writeLog)
        {
            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetFocused,
                pane.PaneTitle,
                details.Name));
        }
    }

    public async Task ExtractSelectedAssetAsync()
    {
        if (IsBusy || _workspaceSummary is null || _selectedAssetDetails is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.ExtractAssetAsync(
                new ExtractAssetCommand(
                    _workspaceSummary.WorkspaceId,
                    _selectedAssetDetails.BundleId,
                    _selectedAssetDetails.AssetId));

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
        if (IsBusy || _workspaceSummary is null)
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
                        _workspaceSummary.WorkspaceId,
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
        if (IsBusy || _workspaceSummary is null || _selectedAssetDetails is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.GetWorkspaceReplacementCandidatesAsync(
                new GetWorkspaceReplacementCandidatesQuery(
                    _workspaceSummary.WorkspaceId,
                    _selectedAssetDetails.BundleId,
                    _selectedAssetDetails.AssetId));

            if (!TryGetValue(result, out var candidates))
            {
                return;
            }

            var selectedCandidate = candidates[0];
            _pendingFileReplacementCandidate = null;
            _pendingWorkspaceReplacementCandidate = selectedCandidate;
            ApplyInspector(
                _selectedAssetDetails,
                _pendingWorkspaceReplacementCandidate,
                pendingFileCandidate: null,
                enableActions: _canEditSelectedAsset);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementSourceSelected,
                selectedCandidate.SourceBundleDisplayName,
                selectedCandidate.SourceAssetName,
                _selectedAssetDetails.Name));

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
        if (IsBusy || _workspaceSummary is null || _selectedAssetDetails is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string? selectedFilePath;
            try
            {
                selectedFilePath = await _filePickerService.PickReplacementSourceFileAsync(_selectedAssetDetails.AssetType);
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
                    _selectedAssetDetails.Name));
                return;
            }

            var result = await _workspaceBrowserService.GetFileReplacementCandidateAsync(
                new GetFileReplacementCandidateQuery(
                    _workspaceSummary.WorkspaceId,
                    _selectedAssetDetails.BundleId,
                    _selectedAssetDetails.AssetId,
                    selectedFilePath));

            if (!TryGetValue(result, out var candidate))
            {
                return;
            }

            _pendingWorkspaceReplacementCandidate = null;
            _pendingFileReplacementCandidate = candidate;
            ApplyInspector(
                _selectedAssetDetails,
                pendingWorkspaceCandidate: null,
                pendingFileCandidate: _pendingFileReplacementCandidate,
                enableActions: _canEditSelectedAsset);

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFileReplacementSourceSelected,
                candidate.SourceFileName,
                candidate.SourceFilePath,
                _selectedAssetDetails.Name));

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
        if (IsBusy || _workspaceSummary is null || _selectedAssetDetails is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_pendingFileReplacementCandidate is not null)
            {
                var fileResult = await _workspaceBrowserService.ReplaceAssetFromFileAsync(
                    new ReplaceAssetFromFileCommand(
                        _workspaceSummary.WorkspaceId,
                        _selectedAssetDetails.BundleId,
                        _selectedAssetDetails.AssetId,
                        _pendingFileReplacementCandidate.SourceFilePath,
                        _pendingFileReplacementCandidate.SourceAssetType));

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

                _pendingFileReplacementCandidate = null;
                _pendingWorkspaceReplacementCandidate = null;
                await RefreshPrimaryPaneAsync();
                return;
            }

            if (_pendingWorkspaceReplacementCandidate is null)
            {
                return;
            }

            var workspaceResult = await _workspaceBrowserService.ReplaceAssetFromWorkspaceAsync(
                new ReplaceAssetFromWorkspaceCommand(
                    _workspaceSummary.WorkspaceId,
                    _selectedAssetDetails.BundleId,
                    _selectedAssetDetails.AssetId,
                    _pendingWorkspaceReplacementCandidate.SourceBundleId,
                    _pendingWorkspaceReplacementCandidate.SourceAssetId));

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

            _pendingWorkspaceReplacementCandidate = null;
            _pendingFileReplacementCandidate = null;
            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UndoAppliedReplacementAsync()
    {
        if (IsBusy || _workspaceSummary is null || _selectedAssetDetails is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.UndoAssetReplacementAsync(
                new UndoAssetReplacementCommand(
                    _workspaceSummary.WorkspaceId,
                    _selectedAssetDetails.BundleId,
                    _selectedAssetDetails.AssetId));

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

            _pendingFileReplacementCandidate = null;
            _pendingWorkspaceReplacementCandidate = null;
            await RefreshPrimaryPaneAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildBundleGroups(IEnumerable<BundleTreeNodeDto> bundleTree)
    {
        _allBundleGroups.Clear();
        _bundleLookup.Clear();

        foreach (var groupNode in bundleTree.Where(static node => node.IsVirtualFolder))
        {
            var bundles = groupNode.Children
                .Where(static child => !child.IsVirtualFolder)
                .Select(child =>
                {
                    var node = new BundleNodeViewModel(
                        child.BundleId,
                        child.DisplayName,
                        child.LogicalPath,
                        child.SourcePath,
                        child.AssetCount);

                    _bundleLookup[node.BundleId.Value] = node;
                    return node;
                })
                .ToArray();

            if (bundles.Length > 0)
            {
                _allBundleGroups.Add(new BundleGroupViewModel(groupNode.DisplayName, bundles));
            }
        }

        ApplyBundleGroupFilter();
    }

    /// <summary>
    /// 统一处理资源包标签页打开策略。
    /// 当前主交互已经收敛为“单击左侧资源包列表即打开固定标签页”，
    /// 预览标签页仅保留给初始化和后续恢复流程使用。
    /// </summary>
    private async Task OpenBundleAsync(BundleNodeViewModel bundleNode, bool openAsPinnedTab)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (_workspaceSummary is null)
        {
            return;
        }

        if (TryGetBundleTab(bundleNode.BundleId, out var existingTab))
        {
            if (openAsPinnedTab)
            {
                existingTab.PromoteToPinnedTab();
            }

            await SetActiveBundleTabAsync(existingTab, writeSelectionLog: false);
            return;
        }

        if (openAsPinnedTab)
        {
            CaptureActiveTabState();
            var pinnedTab = CreateBundleTab(bundleNode, isPreview: false);
            BundleTabs.Add(pinnedTab);
            await SetActiveBundleTabAsync(pinnedTab, writeSelectionLog: false);
            return;
        }

        if (ActiveBundleTab is { IsPreview: true } previewTab)
        {
            previewTab.RebindBundle(
                bundleNode.BundleId,
                bundleNode.DisplayName,
                bundleNode.LogicalPath,
                isPreview: true);

            await SetActiveBundleTabAsync(previewTab, writeSelectionLog: false);
            return;
        }

        CaptureActiveTabState();
        var newPreviewTab = CreateBundleTab(bundleNode, isPreview: true);
        BundleTabs.Add(newPreviewTab);
        await SetActiveBundleTabAsync(newPreviewTab, writeSelectionLog: false);
    }

    private BundleNodeViewModel? FindBundleNodeByDisplayName(string displayName) =>
        _allBundleGroups
            .SelectMany(static group => group.Bundles)
            .FirstOrDefault(bundle => string.Equals(bundle.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));

    private BundleNodeViewModel? FindBundleNodeByLogicalPath(string logicalPath) =>
        _allBundleGroups
            .SelectMany(static group => group.Bundles)
            .FirstOrDefault(bundle => string.Equals(bundle.LogicalPath, logicalPath, StringComparison.OrdinalIgnoreCase));

    private BundleNodeViewModel? FindBundleNode(BundleId bundleId) =>
        _bundleLookup.TryGetValue(bundleId.Value, out var bundleNode)
            ? bundleNode
            : null;

    private BundleDocumentTabViewModel CreateBundleTab(BundleNodeViewModel bundleNode, bool isPreview) =>
        new(
            bundleNode.BundleId,
            bundleNode.DisplayName,
            bundleNode.LogicalPath,
            isPreview);

    private bool TryGetBundleTab(BundleId bundleId, [NotNullWhen(true)] out BundleDocumentTabViewModel? bundleTab)
    {
        bundleTab = BundleTabs.FirstOrDefault(tab => tab.BundleId.Equals(bundleId));
        return bundleTab is not null;
    }

    private async Task SetActiveBundleTabAsync(BundleDocumentTabViewModel tab, bool writeSelectionLog)
    {
        ActiveBundleTab = tab;
        SetSelectedBundle(FindBundleNode(tab.BundleId));
        var bundleNode = FindBundleNode(tab.BundleId);

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

    private void CaptureActiveTabState()
    {
        ActiveBundleTab?.CaptureFromPane(PrimaryPane);
        RequestWorkspaceBrowserStatePersistence();
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

    private void ClearActiveTabState()
    {
        ActiveBundleTab = null;
        SetSelectedBundle(null);
        ClearSelectedAssetState();
        PrimaryPane.ClearItems(ResolvePrimaryPaneEmptyStateText());
        Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        RequestWorkspaceBrowserStatePersistence();
    }

    private AssetListEntryViewModel MapAssetEntry(AssetListItemDto item) =>
        new(
            Item: item,
            AssetTypeText: ResolveAssetTypeDisplayName(item.AssetType, item.AssetTypeDisplayName),
            SizeText: FormatSize(ResolveDisplaySizeInBytes(item.SizeInBytes, item.PreferredDisplaySizeInBytes)),
            StateText: item.IsModified
                ? GetString(ResourceKeys.AssetStateModified)
                : GetString(ResourceKeys.AssetStateOriginal));

    private AssetDependencyViewModel MapDependency(AssetDependencyDto dependency) =>
        new(
            Name: dependency.Name,
            BundleName: ResolveBundleDisplayName(dependency.BundleId),
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

    private string ResolveBundleDisplayName(BundleId bundleId) =>
        _bundleLookup.TryGetValue(bundleId.Value, out var bundleNode)
            ? bundleNode.DisplayName
            : bundleId.Value;

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

    private void UpdateWorkspaceState(bool hasWorkspace)
    {
        HasWorkspace = hasWorkspace;
        if (!hasWorkspace)
        {
            if (!string.IsNullOrWhiteSpace(BundleSearchKeyword))
            {
                BundleSearchKeyword = string.Empty;
            }

            IsBundleSearchExpanded = false;
            IsBundlePaneCollapsed = false;
        }
    }

    private void ResetWorkspaceToEmptyState()
    {
        CancelWorkspaceBrowserStatePersistence();
        _activeWorkspaceNavigationRequest = null;
        _workspaceSummary = null;
        _workspaceContextService.ClearCurrentWorkspace();
        UpdateWorkspaceState(hasWorkspace: false);
        SetSelectedBundle(null);
        ClearSelectedAssetState();
        _allBundleGroups.Clear();
        BundleGroups.Clear();
        BundleTabs.Clear();
        ClearActiveTabState();
        NotifyBundleCollectionStateChanged();
    }

    /// <summary>
    /// 尝试获取当前已经打开的活动工作区会话。
    /// 成功时说明当前页面可以直接绑定现有会话，而不必重新执行“打开工作区”流程。
    /// </summary>
    private bool TryGetCurrentWorkspaceSessionContext(
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        workspaceSummary = _workspaceContextService.CurrentWorkspaceSummary;
        if (workspaceSummary is null)
        {
            workspaceNavigationRequest = null;
            return false;
        }

        workspaceNavigationRequest = BuildNavigationRequestFromWorkspaceSummary(workspaceSummary);
        return workspaceNavigationRequest is not null;
    }

    private WorkspaceNavigationRequest? BuildNavigationRequestFromWorkspaceSummary(WorkspaceSummaryDto? workspaceSummary)
    {
        if (workspaceSummary is null)
        {
            return null;
        }

        var currentWorkspaceEntry = _workspaceContextService.WorkspaceEntries.FirstOrDefault(entry =>
            string.Equals(
                entry.WorkspaceSummary?.WorkspaceId.Value,
                workspaceSummary.WorkspaceId.Value,
                StringComparison.Ordinal));

        return new WorkspaceNavigationRequest(
            SourcePath: workspaceSummary.SourcePath,
            WorkspaceDisplayName: workspaceSummary.DisplayName,
            WorkspaceDataRootPath: currentWorkspaceEntry?.WorkspaceDataRootPath ?? string.Empty,
            OpenTarget: WorkspaceOpenTarget.CurrentWindow,
            AssetBundleDecryptorId: currentWorkspaceEntry?.AssetBundleDecryptorId);
    }

    private string ResolveCurrentDecryptorDisplayName()
    {
        var decryptorId = _activeWorkspaceNavigationRequest?.AssetBundleDecryptorId;
        if (string.IsNullOrWhiteSpace(decryptorId) ||
            string.Equals(decryptorId, AssetBundleDecryptorIds.BuiltInAutoDetect, StringComparison.OrdinalIgnoreCase))
        {
            decryptorId = _workspaceContextService.WorkspaceEntries
                .FirstOrDefault(entry =>
                    string.Equals(
                        entry.WorkspaceSummary?.WorkspaceId.Value,
                        _workspaceSummary?.WorkspaceId.Value,
                        StringComparison.Ordinal))?
                .AssetBundleDecryptorId;
        }

        return AssetBundleDecryptorDisplayText.GetDisplayName(
            _localizationService,
            _assetBundleDecryptorRegistry,
            decryptorId);
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
            BundleSearchKeyword = browserState.BundleSearchKeyword ?? string.Empty;
            IsBundleSearchExpanded = !string.IsNullOrWhiteSpace(BundleSearchKeyword);

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

            var bundleNode = FindBundleNode(new BundleId(tabState.BundleId)) ??
                             FindBundleNodeByLogicalPath(tabState.LogicalPath);
            if (bundleNode is null)
            {
                continue;
            }

            var tab = CreateBundleTab(bundleNode, tabState.IsPreview);
            tab.RestoreViewState(
                searchKeyword: tabState.SearchKeyword,
                selectedAssetType: ParseAssetTypeFilter(tabState.AssetTypeFilter),
                selectedAssetId: tabState.SelectedAssetId,
                showModifiedOnly: tabState.ShowModifiedOnly,
                showPreviewableOnly: tabState.ShowPreviewableOnly);

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

        var bundleNode = FindBundleNode(new BundleId(browserState.LastBundleId));
        if (bundleNode is null)
        {
            return false;
        }

        var restoredTab = CreateBundleTab(bundleNode, isPreview: true);
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
        if (_workspaceSummary is null || _isRestoringWorkspaceBrowserState)
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
        if (_workspaceSummary is null || _isRestoringWorkspaceBrowserState)
        {
            return;
        }

        CancelWorkspaceBrowserStatePersistence();
        PersistWorkspaceBrowserStateCore();
    }

    private void PersistWorkspaceBrowserStateCore()
    {
        if (_workspaceSummary is null)
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
            LastAssetId: activeTab?.SelectedAssetId);
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
            ShowPreviewableOnly: tab.ShowPreviewableOnly);
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

    private void SetSelectedBundle(BundleNodeViewModel? bundleNode)
    {
        if (ReferenceEquals(_selectedBundleNode, bundleNode))
        {
            return;
        }

        if (_selectedBundleNode is not null)
        {
            _selectedBundleNode.IsSelected = false;
        }

        _selectedBundleNode = bundleNode;
        if (_selectedBundleNode is not null)
        {
            _selectedBundleNode.IsSelected = true;
        }

        NotifyCollapsedBundlePaneSummaryChanged();
    }

    /// <summary>
    /// 根据资源包搜索关键字重建左侧可见分组。
    /// 过滤只影响浏览可见性，不改变工作区中真正加载的资源包集合。
    /// </summary>
    private void ApplyBundleGroupFilter()
    {
        var searchKeyword = NormalizeSearchKeyword(BundleSearchKeyword);
        BundleGroups.Clear();

        foreach (var group in _allBundleGroups)
        {
            var visibleBundles = GroupMatchesSearch(group, searchKeyword)
                ? group.Bundles.ToArray()
                : group.Bundles
                    .Where(bundle => BundleMatchesSearch(bundle, searchKeyword))
                    .ToArray();

            if (visibleBundles.Length > 0)
            {
                BundleGroups.Add(new BundleGroupViewModel(group.DisplayName, visibleBundles));
            }
        }

        NotifyBundleCollectionStateChanged();
    }

    private void NotifyBundleCollectionStateChanged()
    {
        OnPropertyChanged(nameof(BundlePaneEmptyStateVisibility));
        OnPropertyChanged(nameof(BundleListVisibility));
        OnPropertyChanged(nameof(BundleSearchEmptyStateVisibility));
        NotifyCollapsedBundlePaneSummaryChanged();
    }

    private string ResolvePrimaryPaneEmptyStateText()
    {
        if (!HasWorkspace)
        {
            return GetString(ResourceKeys.BundleExplorerNoWorkspace);
        }

        return _allBundleGroups.Count == 0
            ? GetString(ResourceKeys.BundleExplorerEmptyWorkspace)
            : GetString(ResourceKeys.BundleExplorerNoOpenTabs);
    }

    private int GetVisibleBundleCount() => BundleGroups.Sum(static group => group.Bundles.Count);

    /// <summary>
    /// 收缩窄条依赖的都是派生摘要字段，
    /// 因此在资源包集合或当前选中资源包变化时集中刷新即可。
    /// </summary>
    private void NotifyCollapsedBundlePaneSummaryChanged()
    {
        OnPropertyChanged(nameof(CollapsedBundlePaneBundleCountText));
        OnPropertyChanged(nameof(CollapsedBundlePaneGroupCountText));
        OnPropertyChanged(nameof(CollapsedBundlePaneSelectedBundleShortText));
        OnPropertyChanged(nameof(CollapsedBundlePaneSelectedBundleToolTip));
    }

    /// <summary>
    /// 为收缩态窄条生成一个简短的资源包缩写，
    /// 既尽量保留辨识度，也避免长文件名把窄条挤坏。
    /// </summary>
    private string BuildCollapsedBundlePaneSelectedBundleShortText()
    {
        if (string.IsNullOrWhiteSpace(_selectedBundleNode?.DisplayName))
        {
            return GetString(ResourceKeys.BundleExplorerCollapsedPaneSelectedBundleShortNone);
        }

        var displayName = _selectedBundleNode.DisplayName.Trim();
        var normalizedDisplayName = displayName.EndsWith(".ab", StringComparison.OrdinalIgnoreCase)
            ? displayName[..^3]
            : displayName;

        var compactText = new string(normalizedDisplayName
            .Where(static character => !char.IsWhiteSpace(character) && character is not '_' and not '-')
            .ToArray());

        if (string.IsNullOrWhiteSpace(compactText))
        {
            return GetString(ResourceKeys.BundleExplorerCollapsedPaneSelectedBundleShortNone);
        }

        var shortText = compactText.Length > 2
            ? compactText[..2]
            : compactText;

        return shortText.ToUpper(CultureInfo.CurrentCulture);
    }

    private bool GroupMatchesSearch(BundleGroupViewModel group, string? searchKeyword) =>
        searchKeyword is not null &&
        group.DisplayName.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase);

    private bool BundleMatchesSearch(BundleNodeViewModel bundle, string? searchKeyword)
    {
        if (searchKeyword is null)
        {
            return true;
        }

        return bundle.DisplayName.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.LogicalPath.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.BundleId.Value.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 将顶部工作区摘要拆成若干标签，降低密集长句对浏览区域的视觉干扰。
    /// </summary>
    private void RefreshWorkspaceStatusChips()
    {
        WorkspaceStatusChips.Clear();

        if (_workspaceSummary is null)
        {
            WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
                GetString(ResourceKeys.BundleExplorerStatusWorkspaceLabel),
                GetString(ResourceKeys.BundleExplorerStatusNotLoaded),
                "\uE8B7"));

            WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
                GetString(ResourceKeys.BundleExplorerStatusWorkspaceSessionCountLabel),
                _workspaceContextService.WorkspaceEntryCount.ToString(CultureInfo.CurrentCulture),
                "\uE71D"));

            WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
                GetString(ResourceKeys.BundleExplorerStatusOpenTabCountLabel),
                BundleTabs.Count.ToString(CultureInfo.CurrentCulture),
                "\uE7C3"));

            WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
                GetString(ResourceKeys.BundleExplorerStatusCurrentDecryptorLabel),
                GetString(ResourceKeys.BundleExplorerStatusNotLoaded),
                "\uE9CA"));
            return;
        }

        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusWorkspaceLabel),
            _workspaceSummary.DisplayName,
            "\uE8B7"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusBundleCountLabel),
            _workspaceSummary.BundleCount.ToString(CultureInfo.CurrentCulture),
            "\uE8B7"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusAssetCountLabel),
            _workspaceSummary.AssetCount.ToString(CultureInfo.CurrentCulture),
            "\uE8A5"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusWorkspaceSessionCountLabel),
            _workspaceContextService.WorkspaceEntryCount.ToString(CultureInfo.CurrentCulture),
            "\uE71D"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusOpenTabCountLabel),
            BundleTabs.Count.ToString(CultureInfo.CurrentCulture),
            "\uE7C3"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusSourceLabel),
            string.IsNullOrWhiteSpace(_workspaceSummary.SourcePath)
                ? GetString(ResourceKeys.WorkspaceSourceNotImported)
                : _workspaceSummary.SourcePath,
            "\uE71D"));
        WorkspaceStatusChips.Add(new WorkspaceStatusChipViewModel(
            GetString(ResourceKeys.BundleExplorerStatusCurrentDecryptorLabel),
            ResolveCurrentDecryptorDisplayName(),
            "\uE9CA"));
    }

    private void ApplyInspector(
        AssetDetailsDto details,
        WorkspaceReplacementCandidateDto? pendingWorkspaceCandidate,
        FileReplacementCandidateDto? pendingFileCandidate,
        bool enableActions)
    {
        _selectedAssetDetails = details;
        _canEditSelectedAsset = enableActions;
        Inspector.Apply(
            assetName: details.Name,
            bundleDisplayName: ResolveBundleDisplayName(details.BundleId),
            assetTypeText: ResolveAssetTypeDisplayName(details.AssetType, details.AssetTypeDisplayName),
            assetPath: details.AssetPath,
            containerPath: details.ContainerPath,
            sizeText: FormatSize(ResolveDisplaySizeInBytes(details.SizeInBytes, details.PreferredDisplaySizeInBytes)),
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
            replacementSourceText: BuildReplacementSourceText(details, pendingWorkspaceCandidate, pendingFileCandidate),
            replacementStatusText: BuildReplacementStatusText(details, pendingWorkspaceCandidate, pendingFileCandidate),
            changeSummaryText: BuildChangeSummaryText(details),
            canExtract: enableActions && !IsBusy && details.CanExtract,
            canSelectFileReplacementSource: enableActions && !IsBusy && details.CanReplaceFromFile,
            canSelectReplacementSource: enableActions && !IsBusy && details.CanReplaceFromWorkspace,
            canApplyReplacement: enableActions && !IsBusy && (pendingWorkspaceCandidate is not null || pendingFileCandidate is not null),
            canUndoReplacement: enableActions && !IsBusy && HasAppliedReplacement(details),
            assetDependencies: details.Dependencies.Select(MapDependency),
            referencedByDependencies: (details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            dependencyClosureItems: (details.DependencyClosure ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            bundleExternalDependencies: (details.BundleExternalDependencies ?? Array.Empty<BundleExternalDependencyDto>())
                .Select(MapBundleExternalDependency));

        NotifyActionStates();
    }

    private void ClearSelectedAssetState()
    {
        BeginPreviewRequest();
        _selectedAssetDetails = null;
        _pendingFileReplacementCandidate = null;
        _pendingWorkspaceReplacementCandidate = null;
        _canEditSelectedAsset = false;
        NotifyActionStates();
    }

    private async Task LoadInspectorPreviewAsync(AssetDetailsDto details, long previewRequestVersion)
    {
        if (!TryCreateAssetPreviewQuery(details, out var previewQuery))
        {
            if (IsPreviewRequestCurrent(details, previewRequestVersion))
            {
                Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            }

            return;
        }

        var previewResult = await _workspaceBrowserService.GetAssetPreviewAsync(previewQuery);
        if (previewResult.IsCancelled || !IsPreviewRequestCurrent(details, previewRequestVersion))
        {
            return;
        }

        if (!previewResult.IsSuccess || previewResult.Value is null)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                details.Name,
                FormatFirstErrorMessage(previewResult.Errors)));
            Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            return;
        }

        var preview = previewResult.Value;
        if (preview.PreferredDisplaySizeInBytes is { } preferredDisplaySizeInBytes && preferredDisplaySizeInBytes > 0)
        {
            Inspector.OverrideSizeText(FormatSize(preferredDisplaySizeInBytes));
        }

        try
        {
            switch (preview.Kind)
            {
                case AssetPreviewKind.Image:
                {
                    var imageSource = await CreatePreviewImageSourceAsync(preview.BinaryContent);
                    if (!IsPreviewRequestCurrent(details, previewRequestVersion))
                    {
                        return;
                    }

                    if (imageSource is null)
                    {
                        _appLogService.AppendWarning(_localizationService.GetFormattedString(
                            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                            details.Name,
                            "Image payload could not be decoded."));
                        Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(preview.Diagnostic);
                        return;
                    }

                    Inspector.ApplyImagePreview(imageSource, preview.PreviewInfoText);
                    ApplyPreviewDiagnostic(preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Text:
                {
                    if (string.IsNullOrWhiteSpace(preview.TextContent))
                    {
                        Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(preview.Diagnostic);
                        return;
                    }

                    Inspector.ApplyTextPreview(preview.TextContent, preview.PreviewInfoText);
                    ApplyPreviewDiagnostic(preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Summary:
                {
                    if (string.IsNullOrWhiteSpace(preview.TextContent))
                    {
                        Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                        ApplyPreviewDiagnostic(preview.Diagnostic);
                        return;
                    }

                    Inspector.ApplySummaryPreview(preview.TextContent, preview.PreviewInfoText);
                    ApplyPreviewDiagnostic(preview.Diagnostic);
                    return;
                }
                default:
                    Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
                    ApplyPreviewDiagnostic(preview.Diagnostic);
                    return;
            }
        }
        catch (Exception ex)
        {
            if (!IsPreviewRequestCurrent(details, previewRequestVersion))
            {
                return;
            }

            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                details.Name,
                ex.Message));
            Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            ApplyPreviewDiagnostic(preview.Diagnostic);
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

        if (_activeWorkspaceNavigationRequest is null ||
            string.IsNullOrWhiteSpace(_activeWorkspaceNavigationRequest.WorkspaceDataRootPath))
        {
            previewQuery = null;
            return false;
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            _activeWorkspaceNavigationRequest.SourcePath,
            _activeWorkspaceNavigationRequest.WorkspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            previewQuery = null;
            return false;
        }

        previewQuery = new GetAssetPreviewQuery(
            WorkspaceId: details.WorkspaceId,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: _activeWorkspaceNavigationRequest.WorkspaceDataRootPath,
            BundleId: details.BundleId,
            AssetId: details.AssetId);
        return true;
    }

    private bool IsPreviewRequestCurrent(AssetDetailsDto details, long previewRequestVersion)
    {
        return _previewRequestVersion == previewRequestVersion &&
               _selectedAssetDetails is not null &&
               _selectedAssetDetails.BundleId.Equals(details.BundleId) &&
               _selectedAssetDetails.AssetId.Equals(details.AssetId);
    }

    private long BeginPreviewRequest()
    {
        _previewRequestVersion++;
        return _previewRequestVersion;
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

    /// <summary>
    /// 应用层预览诊断只暴露稳定枚举和原始 detail，
    /// 真正面向用户的中文文案在 Presentation 层完成映射，便于后续工作台和诊断页复用同一份诊断语义。
    /// </summary>
    private void ApplyPreviewDiagnostic(AssetPreviewDiagnosticDto diagnostic)
    {
        Inspector.ApplyPreviewDiagnostic(
            cacheStatusText: GetPreviewCacheStatusText(diagnostic.CacheStatus),
            fallbackReasonText: GetPreviewFallbackReasonText(diagnostic.FallbackReason),
            diagnosticDetailText: string.IsNullOrWhiteSpace(diagnostic.Detail)
                ? string.Empty
                : diagnostic.Detail.Trim());
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

    private string FormatSize(long sizeInBytes)
    {
        if (sizeInBytes >= 1024 * 1024)
        {
            return $"{sizeInBytes / 1024d / 1024d:F1} MB";
        }

        if (sizeInBytes >= 1024)
        {
            return $"{sizeInBytes / 1024d:F1} KB";
        }

        return $"{sizeInBytes:N0} B";
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
            return "unexpected failure";
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
        if (!Inspector.HasSelection || _selectedAssetDetails is null)
        {
            Inspector.CanExtract = false;
            Inspector.CanSelectFileReplacementSource = false;
            Inspector.CanSelectReplacementSource = false;
            Inspector.CanApplyReplacement = false;
            Inspector.CanUndoReplacement = false;
            return;
        }

        Inspector.CanExtract = !IsBusy && _canEditSelectedAsset && _selectedAssetDetails.CanExtract;
        Inspector.CanSelectFileReplacementSource = !IsBusy && _canEditSelectedAsset && _selectedAssetDetails.CanReplaceFromFile;
        Inspector.CanSelectReplacementSource = !IsBusy && _canEditSelectedAsset && _selectedAssetDetails.CanReplaceFromWorkspace;
        Inspector.CanApplyReplacement = !IsBusy && _canEditSelectedAsset && (_pendingWorkspaceReplacementCandidate is not null || _pendingFileReplacementCandidate is not null);
        Inspector.CanUndoReplacement = !IsBusy && _canEditSelectedAsset && HasAppliedReplacement(_selectedAssetDetails);
    }
}
