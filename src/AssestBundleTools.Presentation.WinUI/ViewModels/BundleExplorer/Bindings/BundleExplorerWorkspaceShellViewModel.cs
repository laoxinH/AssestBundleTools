using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 维护工作台页面的工作区级壳层状态。
/// </summary>
/// <remarks>
/// 这层只负责“当前有没有工作区、它的导航参数是什么、顶部状态标签如何展示”。
/// 真正的导入、读取、预览和替换流程仍由外层编排 ViewModel 驱动，避免把流程控制和页面壳层状态继续混在一起。
/// </remarks>
public sealed class BundleExplorerWorkspaceShellViewModel : ObservableObject, IBundleExplorerWorkspaceShellHost
{
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;
    private WorkspaceSummaryDto? _workspaceSummary;
    private WorkspaceNavigationRequest? _activeWorkspaceNavigationRequest;

    public BundleExplorerWorkspaceShellViewModel(
        ILocalizationService localizationService,
        IWorkspaceContextService workspaceContextService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));

        RefreshWorkspaceStatusChips();
    }

    internal event EventHandler<BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange>>? PageStateChanged;

    public ObservableCollection<WorkspaceStatusChipViewModel> WorkspaceStatusChips { get; } = new();

    public WorkspaceSummaryDto? WorkspaceSummary => _workspaceSummary;

    public WorkspaceNavigationRequest? ActiveWorkspaceNavigationRequest => _activeWorkspaceNavigationRequest;

    public bool HasWorkspace => _workspaceSummary is not null;

    public Visibility WorkspaceContentVisibility => HasWorkspace ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkspaceEmptyStateVisibility => HasWorkspace ? Visibility.Collapsed : Visibility.Visible;

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

    /// <summary>
    /// 尝试获取当前已经打开的活动工作区会话。
    /// 成功时说明当前页面可以直接绑定现有会话，而不必重新执行“打开工作区”流程。
    /// </summary>
    public bool TryGetCurrentWorkspaceSessionContext(
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

    public WorkspaceNavigationRequest? ResolveNavigationRequest(WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        return workspaceNavigationRequest
               ?? _activeWorkspaceNavigationRequest
               ?? BuildNavigationRequestFromWorkspaceSummary(_workspaceContextService.CurrentWorkspaceSummary);
    }

    /// <summary>
    /// 绑定已加载工作区后，统一固化当前工作区摘要与导航参数。
    /// </summary>
    public WorkspaceNavigationRequest ApplyWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceNavigationRequest resolvedNavigationRequest)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(resolvedNavigationRequest);

        _workspaceSummary = workspaceSummary;
        _activeWorkspaceNavigationRequest = resolvedNavigationRequest with
        {
            SourcePath = workspaceSummary.SourcePath,
            WorkspaceDisplayName = workspaceSummary.DisplayName,
            WorkspaceDataRootPath = resolvedNavigationRequest.WorkspaceDataRootPath ?? string.Empty,
            ForceNewWorkspaceSession = false
        };

        NotifyWorkspaceShellChanged();
        return _activeWorkspaceNavigationRequest;
    }

    /// <summary>
    /// 工作区被清空或加载失败时，统一把壳层状态重置回未加载态。
    /// </summary>
    public void Reset()
    {
        _activeWorkspaceNavigationRequest = null;
        _workspaceSummary = null;
        NotifyWorkspaceShellChanged();
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
            GetString(ResourceKeys.BundleExplorerStatusCurrentDecryptorLabel),
            ResolveCurrentDecryptorDisplayName(),
            "\uE9CA"));
    }

    private void NotifyWorkspaceShellChanged()
    {
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActiveWorkspaceNavigationRequest));
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(WorkspaceContentVisibility));
        OnPropertyChanged(nameof(WorkspaceEmptyStateVisibility));
        RefreshWorkspaceStatusChips();
        OnPropertyChanged(nameof(WorkspaceStatusChips));
        NotifyPageStateChanged(
            BundleExplorerWorkspaceShellPageStateChange.WorkspaceAvailability |
            BundleExplorerWorkspaceShellPageStateChange.Presentation |
            BundleExplorerWorkspaceShellPageStateChange.StatusChips);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private void NotifyPageStateChanged(BundleExplorerWorkspaceShellPageStateChange changeFlags)
    {
        if (changeFlags == BundleExplorerWorkspaceShellPageStateChange.None)
        {
            return;
        }

        PageStateChanged?.Invoke(this, new BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange>(changeFlags));
    }
}
