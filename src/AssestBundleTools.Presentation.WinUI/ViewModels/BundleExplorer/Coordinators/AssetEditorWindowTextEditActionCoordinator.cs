using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 资源编辑窗口中的文本编辑草稿生命周期编排。
/// </summary>
internal sealed class AssetEditorWindowTextEditActionCoordinator
{
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly Func<AssetDocumentTabViewModel?> _activeAssetTabProvider;
    private readonly TryGetAssetCommandContext _tryGetAssetCommandContext;
    private readonly Action<bool> _setBusy;

    public AssetEditorWindowTextEditActionCoordinator(
        IBundleExplorerWorkspaceShellHost workspaceShell,
        ILocalizationService localizationService,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        Func<AssetDocumentTabViewModel?> activeAssetTabProvider,
        TryGetAssetCommandContext tryGetAssetCommandContext,
        Action<bool> setBusy)
    {
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _activeAssetTabProvider = activeAssetTabProvider ?? throw new ArgumentNullException(nameof(activeAssetTabProvider));
        _tryGetAssetCommandContext = tryGetAssetCommandContext ?? throw new ArgumentNullException(nameof(tryGetAssetCommandContext));
        _setBusy = setBusy ?? throw new ArgumentNullException(nameof(setBusy));
    }

    public async Task<TextAssetEditDraftViewModel?> CreateTextAssetEditDraftAsync()
    {
        if (!_tryGetAssetCommandContext(
                static (_, details) => IsTextEditorSupported(details),
                out var workspaceSummary,
                out _,
                out var details) ||
            string.IsNullOrWhiteSpace(workspaceSummary.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceSummary.WorkspaceDataRootPath))
        {
            return null;
        }

        _setBusy(true);
        try
        {
            var command = new CreateTextAssetEditDraftCommand(
                workspaceSummary.WorkspaceId,
                workspaceSummary.WorkspaceKey,
                workspaceSummary.WorkspaceDataRootPath,
                details.BundleId,
                details.AssetId);
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetOperationGateway.CreateTextAssetEditDraftAsync(command, token));
            if (!result.IsSuccess || result.Value is null)
            {
                _operationFeedbackBridge.AppendResultFailure(result.Errors);
                return null;
            }

            var draftViewModel = new TextAssetEditDraftViewModel(
                result.Value,
                _workspaceAssetInspectionGateway,
                _backgroundTaskRunner,
                workspaceSummary.WorkspaceDataRootPath,
                details.Name,
                GetString(ResourceKeys.TextAssetEditorReadOnlyHint),
                GetString(ResourceKeys.TextAssetEditorLineStatusFormat));
            await draftViewModel.LoadInitialLinesAsync();
            return draftViewModel;
        }
        finally
        {
            _setBusy(false);
        }
    }

    public async Task<bool> ApplyTextAssetEditDraftAsync()
    {
        if (!TryGetActiveTextEditContext(
                out var workspaceSummary,
                out var workspaceKey,
                out var workspaceDataRootPath,
                out _,
                out var details,
                out var draft))
        {
            return false;
        }

        _setBusy(true);
        try
        {
            var command = new ApplyTextAssetEditDraftCommand(
                workspaceSummary.WorkspaceId,
                workspaceKey,
                workspaceDataRootPath,
                details.BundleId,
                details.AssetId,
                draft.DraftId,
                draft.DraftMode);
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetOperationGateway.ApplyTextAssetEditDraftAsync(command, token));
            if (!result.IsSuccess)
            {
                _operationFeedbackBridge.AppendResultFailure(result.Errors);
                return false;
            }

            return true;
        }
        finally
        {
            _setBusy(false);
        }
    }

    public async Task<bool> DiscardTextAssetEditDraftAsync()
    {
        if (!TryGetActiveTextEditContext(
                out var workspaceSummary,
                out var workspaceKey,
                out var workspaceDataRootPath,
                out _,
                out var details,
                out var draft))
        {
            return false;
        }

        _setBusy(true);
        try
        {
            var command = new DiscardTextAssetEditDraftCommand(
                workspaceSummary.WorkspaceId,
                workspaceKey,
                workspaceDataRootPath,
                details.BundleId,
                details.AssetId,
                draft.DraftId,
                draft.DraftMode);
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetOperationGateway.DiscardTextAssetEditDraftAsync(command, token));
            if (!result.IsSuccess)
            {
                _operationFeedbackBridge.AppendResultFailure(result.Errors);
                return false;
            }

            return true;
        }
        finally
        {
            _setBusy(false);
        }
    }

    private bool TryGetActiveTextEditContext(
        [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
        [NotNullWhen(true)] out string? workspaceKey,
        [NotNullWhen(true)] out string? workspaceDataRootPath,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
        [NotNullWhen(true)] out AssetDetailsDto? details,
        [NotNullWhen(true)] out TextAssetEditDraftViewModel? draft)
    {
        workspaceSummary = _workspaceShell.WorkspaceSummary;
        workspaceKey = workspaceSummary?.WorkspaceKey;
        workspaceDataRootPath = workspaceSummary?.WorkspaceDataRootPath;
        assetTab = _activeAssetTabProvider();
        details = assetTab?.Details;
        draft = assetTab?.TextEditDraft;
        if (workspaceSummary is null ||
            string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath) ||
            draft is null ||
            details is null ||
            assetTab is null)
        {
            workspaceSummary = null;
            workspaceKey = null;
            workspaceDataRootPath = null;
            assetTab = null;
            details = null;
            draft = null;
            return false;
        }

        return true;
    }

    private static bool IsTextEditorSupported(AssetDetailsDto details) =>
        details.AssetType == AssetType.TextAsset ||
        string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}

internal delegate bool TryGetAssetCommandContext(
    Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
    [NotNullWhen(true)] out WorkspaceSummaryDto? workspaceSummary,
    [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab,
    [NotNullWhen(true)] out AssetDetailsDto? details);
