using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 独立资源编辑窗口中的资产动作编排，避免窗口 ViewModel 继续承载提取和替换细节。
/// </summary>
internal sealed class AssetEditorWindowAssetActionCoordinator
{
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly BundleExplorerAssetModificationActionHandler _modificationActionHandler;
    private readonly Func<AssetDocumentTabViewModel?> _activeAssetTabProvider;
    private readonly Func<SpineAssetGroupDocumentTabViewModel?> _activeSpineGroupTabProvider;
    private readonly Func<InspectorPanelViewModel> _inspectorProvider;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusy;
    private readonly Action<AssetDocumentTabViewModel, AssetDetailsDto> _reapplyAssetDocumentState;

    public AssetEditorWindowAssetActionCoordinator(
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IPathNavigationService pathNavigationService,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        BundleExplorerAssetModificationActionHandler modificationActionHandler,
        Func<AssetDocumentTabViewModel?> activeAssetTabProvider,
        Func<SpineAssetGroupDocumentTabViewModel?> activeSpineGroupTabProvider,
        Func<InspectorPanelViewModel> inspectorProvider,
        Func<bool> isBusyProvider,
        Action<bool> setBusy,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState)
    {
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _modificationActionHandler = modificationActionHandler ?? throw new ArgumentNullException(nameof(modificationActionHandler));
        _activeAssetTabProvider = activeAssetTabProvider ?? throw new ArgumentNullException(nameof(activeAssetTabProvider));
        _activeSpineGroupTabProvider = activeSpineGroupTabProvider ?? throw new ArgumentNullException(nameof(activeSpineGroupTabProvider));
        _inspectorProvider = inspectorProvider ?? throw new ArgumentNullException(nameof(inspectorProvider));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusy = setBusy ?? throw new ArgumentNullException(nameof(setBusy));
        _reapplyAssetDocumentState = reapplyAssetDocumentState ?? throw new ArgumentNullException(nameof(reapplyAssetDocumentState));
    }

    public void OpenCurrentPreviewLocation()
    {
        if (!TryGetActiveAssetDocumentState(out _, out var details))
        {
            return;
        }

        var previewPayloadFilePath = _inspectorProvider().Preview.PreviewPayloadFilePath;
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
                _setBusy,
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
            _setBusy,
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
            _setBusy,
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
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task<bool> SelectSpineEditPartFileAsync(SpineAssetGroupEditPartViewModel? part)
    {
        if (part is null ||
            !TryGetActiveSpineGroupEditContext(out var workspaceSummary, out var spineGroupTab))
        {
            return false;
        }

        return await _modificationActionHandler.SelectSpineEditPartFileAsync(
            workspaceSummary,
            spineGroupTab,
            part,
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task<bool> SelectSpineEditBatchSourceDirectoryAsync()
    {
        if (!TryGetActiveSpineGroupEditContext(out _, out var spineGroupTab))
        {
            return false;
        }

        return await _modificationActionHandler.SelectSpineEditBatchSourceDirectoryAsync(
            spineGroupTab,
            _setBusy);
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
            _setBusy,
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
            _setBusy,
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
                _setBusy,
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
            _setBusy,
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
                _setBusy,
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
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task<bool> ApplySpriteEditAsync()
    {
        if (!TryGetActiveAssetCommandContext(
                static (_, details) => details.AssetType == Domain.Assets.AssetType.Sprite,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            return false;
        }

        var geometry = assetTab.Inspector.Preview.Image.CreateSpriteEditGeometry();
        if (geometry is null)
        {
            return false;
        }

        return await _modificationActionHandler.ApplySpriteEditAsync(
            workspaceSummary,
            assetTab,
            details,
            geometry,
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task<bool> ApplySpineEditAsync()
    {
        if (!TryGetActiveSpineGroupEditContext(out var workspaceSummary, out var spineGroupTab))
        {
            return false;
        }

        return await _modificationActionHandler.ApplySpineEditAsync(
            workspaceSummary,
            spineGroupTab,
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    public async Task DeleteChangeRecordAsync(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        if (TryGetActiveAssetCommandContext(
                static (_, _) => true,
                out var workspaceSummary,
                out var assetTab,
                out var details))
        {
            await _modificationActionHandler.DeleteChangeRecordAsync(
                workspaceSummary,
                assetTab,
                details,
                operationId,
                _setBusy,
                _operationFeedbackBridge.AppendResultFailure);
            return;
        }

        if (!TryGetProjectedSpineChangeRecordContext(
                operationId,
                out workspaceSummary,
                out var target))
        {
            return;
        }

        await _modificationActionHandler.DeleteProjectedChangeRecordAsync(
            workspaceSummary,
            target,
            operationId!,
            _setBusy,
            _operationFeedbackBridge.AppendResultFailure);
    }

    private bool TryGetActiveAssetDocumentState(
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details)
    {
        assetTab = _activeAssetTabProvider();
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
        var spineGroupTab = _activeSpineGroupTabProvider();
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            string.IsNullOrWhiteSpace(operationId) ||
            spineGroupTab is null)
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
        spineGroupTab = _activeSpineGroupTabProvider();
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            spineGroupTab is null)
        {
            workspaceSummary = null;
            spineGroupTab = null;
            return false;
        }

        return true;
    }

    private bool TryGetActiveSpineGroupEditContext(
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        workspaceSummary = _workspaceShell.WorkspaceSummary;
        spineGroupTab = _activeSpineGroupTabProvider();
        if (_isBusyProvider() ||
            workspaceSummary is null ||
            spineGroupTab?.SpineEditDraft is null)
        {
            workspaceSummary = null;
            spineGroupTab = null;
            return false;
        }

        return true;
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
