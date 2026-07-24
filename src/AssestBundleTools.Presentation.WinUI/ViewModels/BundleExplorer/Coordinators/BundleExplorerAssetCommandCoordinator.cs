using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 聚合资产级命令的前置条件判定与页面动作编排，避免工作流主类继续堆积命令入口细节。
/// </summary>
internal sealed class BundleExplorerAssetCommandCoordinator
{
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly BundleExplorerAssetModificationActionHandler _modificationActionHandler;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;
    private readonly Func<InspectorPanelViewModel> _getInspector;
    private readonly Func<AssetDocumentTabViewModel?> _getActiveAssetDocumentTab;
    private readonly Func<SpineAssetGroupDocumentTabViewModel?> _getActiveSpineGroupDocumentTab;
    private readonly Action<AssetDocumentTabViewModel, AssetDetailsDto> _reapplyAssetDocumentState;

    public BundleExplorerAssetCommandCoordinator(
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        AssetPaneViewModel primaryPane,
        BundleExplorerAssetModificationActionHandler modificationActionHandler,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState,
        Func<InspectorPanelViewModel> getInspector,
        Func<AssetDocumentTabViewModel?> getActiveAssetDocumentTab,
        Func<SpineAssetGroupDocumentTabViewModel?> getActiveSpineGroupDocumentTab,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState)
    {
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _modificationActionHandler = modificationActionHandler ?? throw new ArgumentNullException(nameof(modificationActionHandler));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
        _getInspector = getInspector ?? throw new ArgumentNullException(nameof(getInspector));
        _getActiveAssetDocumentTab = getActiveAssetDocumentTab ?? throw new ArgumentNullException(nameof(getActiveAssetDocumentTab));
        _getActiveSpineGroupDocumentTab = getActiveSpineGroupDocumentTab ?? throw new ArgumentNullException(nameof(getActiveSpineGroupDocumentTab));
        _reapplyAssetDocumentState = reapplyAssetDocumentState ?? throw new ArgumentNullException(nameof(reapplyAssetDocumentState));
    }

    public bool HasSelectedAsset =>
        TryGetActiveAssetDocumentState(out _, out _) ||
        TryGetActiveSpineGroupDocumentState(out _);

    public bool CanExtractSelectedAsset =>
        CanExecuteActiveAssetCommand(static (_, details) => details.CanExtract) ||
        CanExtractActiveSpineGroup();

    public bool CanSelectFileReplacementSource => CanExecuteActiveAssetCommand(static (_, details) => details.CanReplaceFromFile);

    public bool CanSelectWorkspaceReplacementSource => CanExecuteActiveAssetCommand(static (_, details) => details.CanReplaceFromWorkspace);

    public bool TryGetActiveAssetDocumentState(
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        assetTab = _getActiveAssetDocumentTab();
        if (assetTab?.Details is null)
        {
            details = null;
            return false;
        }

        details = assetTab.Details;
        return true;
    }

    public bool TryGetActiveSpineGroupDocumentState(
        [NotNullWhen(true)] out SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        spineGroupTab = _getActiveSpineGroupDocumentTab();
        return spineGroupTab is not null;
    }

    public void OpenCurrentPreviewLocation()
    {
        if (!TryGetActiveAssetDocumentState(out _, out var details))
        {
            return;
        }

        var previewPayloadFilePath = _getInspector().Preview.PreviewPayloadFilePath;
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
        if (TryGetActiveAssetCommandContext(
                static (_, details) => details.CanExtract,
                out var workspaceSummary,
                out _,
                out var details))
        {
            await _modificationActionHandler.ExtractSelectedAssetAsync(
                workspaceSummary,
                details,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure);
            return;
        }

        if (!TryGetActiveSpineGroupExtractionContext(
                out workspaceSummary,
                out var spineGroupTab))
        {
            return;
        }

        await _modificationActionHandler.ExtractSpineAssetGroupAsync(
            workspaceSummary,
            spineGroupTab,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    /// <summary>
    /// 当前先按页面多选结果逐项复用单资源提取链路，后续若批量提取演进为独立用例，再继续下沉。
    /// </summary>
    public async Task ExtractSelectedAssetsAsync()
    {
        if (_isBusyProvider() || _workspaceShell.WorkspaceSummary is not { } workspaceSummary)
        {
            return;
        }

        if (_primaryPane.IsSpineGroupViewMode)
        {
            var selectedSpineGroups = _primaryPane.SelectedSpineGroups
                .GroupBy(static entry => string.Concat(
                    entry.Item.BundleId.Value,
                    "\n",
                    entry.Item.GroupId), StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray();

            if (selectedSpineGroups.Length == 0)
            {
                await ExtractSelectedAssetAsync();
                return;
            }

            await _modificationActionHandler.ExtractSelectedSpineAssetGroupsAsync(
                workspaceSummary,
                selectedSpineGroups,
                _primaryPane.CurrentBundleDisplayName,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure);
            return;
        }

        var selectedEntries = _primaryPane.SelectedAssets
            .GroupBy(static entry => entry.Item.AssetId.Value, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        if (selectedEntries.Length == 0)
        {
            await ExtractSelectedAssetAsync();
            return;
        }

        await _modificationActionHandler.ExtractSelectedAssetsAsync(
            workspaceSummary,
            selectedEntries,
            _primaryPane.CurrentBundleDisplayName,
            _setBusyState,
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
            _reapplyAssetDocumentState,
            _setBusyState,
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
            _reapplyAssetDocumentState,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task ModifyFromFileReplacementSourceAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.CanReplaceFromFile,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.ModifyFromFileReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task SelectSpriteTextureReplacementSourceAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.AssetType == Domain.Assets.AssetType.Sprite,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return;
        }

        await _modificationActionHandler.SelectSpriteTextureReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            _setBusyState,
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
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task UndoAppliedReplacementAsync(string? replacementOperationId = null)
    {
        if (TryGetActiveAssetCommandContext(
                (_, details) => !string.IsNullOrWhiteSpace(replacementOperationId) || AssetCommandEligibility.HasAppliedReplacement(details),
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            await _modificationActionHandler.UndoAppliedReplacementAsync(
                workspaceSummary,
                assetTab,
                details,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure,
                replacementOperationId);
            return;
        }

        if (!TryGetProjectedSpineChangeRecordContext(
                replacementOperationId,
                out workspaceSummary,
                out var target))
        {
            return;
        }

        await _modificationActionHandler.UndoProjectedReplacementAsync(
            workspaceSummary,
            target,
            replacementOperationId!,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task RedoAppliedReplacementAsync(string? replacementOperationId = null)
    {
        if (TryGetActiveAssetCommandContext(
                (_, details) => !string.IsNullOrWhiteSpace(replacementOperationId) || details.CanRedoReplacement,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            await _modificationActionHandler.RedoAppliedReplacementAsync(
                workspaceSummary,
                assetTab,
                details,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure,
                replacementOperationId);
            return;
        }

        if (!TryGetProjectedSpineChangeRecordContext(
                replacementOperationId,
                out workspaceSummary,
                out var target))
        {
            return;
        }

        await _modificationActionHandler.RedoProjectedReplacementAsync(
            workspaceSummary,
            target,
            replacementOperationId!,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public Task DeleteChangeRecordsAsync(IReadOnlyList<string>? operationIds, bool isRestoreOriginal = false)
    {
        var inspector = _getInspector();
        var requestedOperationIds = InspectorChangeRecordSelectionResolver.ResolveDeletableOperationIds(
            inspector,
            operationIds);
        var workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            requestedOperationIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var activeAssetTab = _getActiveAssetDocumentTab();
        if (activeAssetTab is { CanEdit: false } ||
            (activeAssetTab is null && !TryGetActiveSpineGroupDocumentState(out _)))
        {
            return Task.CompletedTask;
        }

        Action? clearPendingReplacementCandidates = activeAssetTab is null
            ? null
            : activeAssetTab.ClearPendingReplacementCandidates;
        return _modificationActionHandler.DeleteChangeRecordsAsync(
            workspaceSummary,
            requestedOperationIds,
            inspector.AssetName,
            _setBusyState,
            _operationFeedbackBridge.AppendResultFailure,
            clearPendingReplacementCandidates,
            isRestoreOriginal);
    }

    private bool CanExecuteActiveAssetCommand(Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute)
    {
        ArgumentNullException.ThrowIfNull(canExecute);
        return !_isBusyProvider() &&
               _workspaceShell.WorkspaceSummary is not null &&
               TryGetActiveAssetDocumentState(out var assetTab, out var details) &&
               assetTab.CanEdit &&
               canExecute(assetTab, details);
    }

    private bool CanExtractActiveSpineGroup() =>
        !_isBusyProvider() &&
        _workspaceShell.WorkspaceSummary is not null &&
        TryGetActiveSpineGroupDocumentState(out _);

    /// <summary>
    /// 资产级命令入口共用同一条前置条件判定，避免提取/替换/撤销各自散落重复 guard。
    /// 真正的业务规则仍由能力位和应用层结果决定，这里只做页面级可执行性筛选。
    /// </summary>
    private bool TryGetActiveAssetCommandContext(
        Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        ArgumentNullException.ThrowIfNull(canExecute);

        workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (_isBusyProvider() ||
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

    private bool TryGetProjectedSpineChangeRecordContext(
        string? operationId,
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out ProjectedChangeRecordTarget? target)
    {
        workspaceSummary = _workspaceShell.WorkspaceSummary;
        target = null;
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            string.IsNullOrWhiteSpace(operationId) ||
            !TryGetActiveSpineGroupDocumentState(out var spineGroupTab))
        {
            workspaceSummary = null;
            return false;
        }

        if (!ProjectedChangeRecordTargetResolver.TryResolve(spineGroupTab, operationId, out target))
        {
            workspaceSummary = null;
            return false;
        }

        return true;
    }

    private bool TryGetActiveSpineGroupExtractionContext(
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            !TryGetActiveSpineGroupDocumentState(out spineGroupTab))
        {
            workspaceSummary = null;
            spineGroupTab = null;
            return false;
        }

        return true;
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
