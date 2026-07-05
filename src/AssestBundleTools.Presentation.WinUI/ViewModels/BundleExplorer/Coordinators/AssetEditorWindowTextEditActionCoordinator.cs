using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
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

    public async Task<TextAssetEditDraftViewModel?> OpenOrCreateTextAssetEditDraftAsync()
    {
        if (!_tryGetAssetCommandContext(
                static (_, details) => IsTextEditorSupported(details),
                out var workspaceSummary,
                out _,
                out var details))
        {
            return null;
        }

        var workspaceKey = workspaceSummary.WorkspaceKey?.Trim();
        var workspaceDataRootPath = workspaceSummary.WorkspaceDataRootPath?.Trim();
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return null;
        }

        _setBusy(true);
        try
        {
            var activeAssetTab = _activeAssetTabProvider();
            var detailsResult = await LoadCurrentDetailsAsync(workspaceSummary, activeAssetTab, details);
            if (!detailsResult.IsSuccess || detailsResult.Value is null)
            {
                _operationFeedbackBridge.AppendResultFailure(detailsResult.Errors);
                return null;
            }

            details = detailsResult.Value;
            activeAssetTab?.ApplyDetails(details);

            var result = await OpenExistingDraftOrCreateNewAsync(
                workspaceSummary,
                workspaceKey,
                workspaceDataRootPath,
                activeAssetTab,
                details);
            if (!result.IsSuccess || result.Value is null)
            {
                _operationFeedbackBridge.AppendResultFailure(result.Errors);
                return null;
            }

            var draftViewModel = CreateDraftViewModel(
                result.Value,
                workspaceDataRootPath,
                details.Name);
            await draftViewModel.InitializeAsync();
            return draftViewModel;
        }
        finally
        {
            _setBusy(false);
        }
    }

    private Task<Result<AssetDetailsDto>> LoadCurrentDetailsAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel? assetTab,
        AssetDetailsDto details)
    {
        var bundleId = assetTab?.BundleId ?? details.BundleId;
        var assetId = assetTab?.AssetId ?? details.AssetId;
        return _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
                new GetAssetDetailsQuery(
                    workspaceSummary.WorkspaceId,
                    bundleId,
                    assetId),
                token));
    }

    private async Task<Result<AssetTextEditDraftDto>> OpenExistingDraftOrCreateNewAsync(
        WorkspaceSummaryDto workspaceSummary,
        string workspaceKey,
        string workspaceDataRootPath,
        AssetDocumentTabViewModel? assetTab,
        AssetDetailsDto details)
    {
        var currentAppliedReference = AssetDocumentTextEditEffectiveReferenceResolver.CreateAppliedReference(
            details,
            assetTab?.EditDraftReference);
        if (currentAppliedReference is not null)
        {
            var appliedDraftResult = await OpenDraftReferenceAsync(
                workspaceSummary,
                workspaceKey,
                workspaceDataRootPath,
                details,
                currentAppliedReference,
                isAppliedBaseline: true);
            if (!appliedDraftResult.IsSuccess &&
                appliedDraftResult.Errors.Any(static error =>
                    string.Equals(error.Code, "Asset.EditDraft.SourceMismatch", StringComparison.Ordinal)))
            {
                assetTab?.ClearEditDraftReference();
                return await CreateTextAssetEditDraftAsync(
                    workspaceSummary,
                    workspaceKey,
                    workspaceDataRootPath,
                    details);
            }

            return appliedDraftResult;
        }

        if (assetTab?.EditDraftReference is { } draftReference)
        {
            if (draftReference.IsAppliedBaseline &&
                !AssetDocumentTextEditEffectiveReferenceResolver.DetailsPointToDraft(details, draftReference))
            {
                assetTab.ClearEditDraftReference();
            }
            else if (!string.IsNullOrWhiteSpace(draftReference.DraftId) &&
                !string.IsNullOrWhiteSpace(draftReference.DraftMode))
            {
                return await OpenDraftReferenceAsync(
                    workspaceSummary,
                    workspaceKey,
                    workspaceDataRootPath,
                    details,
                    draftReference,
                    isAppliedBaseline: false);
            }
        }

        return await CreateTextAssetEditDraftAsync(
            workspaceSummary,
            workspaceKey,
            workspaceDataRootPath,
            details);
    }

    private async Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
        WorkspaceSummaryDto workspaceSummary,
        string workspaceKey,
        string workspaceDataRootPath,
        AssetDetailsDto details)
    {
        var createCommand = new CreateTextAssetEditDraftCommand(
            workspaceSummary.WorkspaceId,
            workspaceKey,
            workspaceDataRootPath,
            details.BundleId,
            details.AssetId);
        return await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetOperationGateway.CreateTextAssetEditDraftAsync(createCommand, token));
    }

    private Task<Result<AssetTextEditDraftDto>> OpenDraftReferenceAsync(
        WorkspaceSummaryDto workspaceSummary,
        string workspaceKey,
        string workspaceDataRootPath,
        AssetDetailsDto details,
        AssetDocumentEditDraftReference draftReference,
        bool isAppliedBaseline)
    {
        var command = new OpenTextAssetEditDraftCommand(
            workspaceSummary.WorkspaceId,
            workspaceKey,
            workspaceDataRootPath,
            details.BundleId,
            details.AssetId,
            draftReference.DraftId,
            draftReference.DraftMode,
            draftReference.SourceFingerprint,
            draftReference.DraftRevision,
            isAppliedBaseline);
        return _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetOperationGateway.OpenTextAssetEditDraftAsync(command, token));
    }

    private TextAssetEditDraftViewModel CreateDraftViewModel(
        AssetTextEditDraftDto draft,
        string workspaceDataRootPath,
        string titleText)
    {
        return new TextAssetEditDraftViewModel(
            draft,
            _workspaceAssetInspectionGateway,
            _backgroundTaskRunner,
            workspaceDataRootPath,
            titleText,
            GetString(ResourceKeys.TextAssetEditorReadOnlyHint),
            GetString(ResourceKeys.TextAssetEditorInlineEditHint),
            GetString(ResourceKeys.TextAssetEditorDeferredEditHint),
            GetString(ResourceKeys.TextAssetEditorLineStatusFormat));
    }

    public async Task<bool> ApplyTextAssetEditDraftAsync()
    {
        if (!TryGetActiveTextEditContext(
                out var workspaceSummary,
                out var workspaceKey,
                out var workspaceDataRootPath,
                out var assetTab,
                out var details,
                out var draft))
        {
            return false;
        }

        _setBusy(true);
        try
        {
            int? appliedDraftRevision = null;
            if (draft.HasUnsavedChanges)
            {
                var updateCommand = new UpdateTextAssetEditDraftCommand(
                    workspaceSummary.WorkspaceId,
                    workspaceKey,
                    workspaceDataRootPath,
                    details.BundleId,
                    details.AssetId,
                    draft.DraftId,
                    draft.DraftMode,
                    TextContent: null,
                    RangeEdits: draft.CreateSaveRangeEdits(),
                    PatchBaseline: draft.CreateSavePatchBaseline(),
                    BaseDraftRevision: draft.Reference.DraftRevision);
                var updateResult = await _backgroundTaskRunner.RunAsync(
                    token => _workspaceAssetOperationGateway.UpdateTextAssetEditDraftAsync(updateCommand, token));
                if (!updateResult.IsSuccess)
                {
                    _operationFeedbackBridge.AppendResultFailure(updateResult.Errors);
                    return false;
                }

                appliedDraftRevision = updateResult.Value?.DraftRevision;
                draft.MarkSaved(updateResult.Value?.Reference);
            }

            var command = new ApplyTextAssetEditDraftCommand(
                workspaceSummary.WorkspaceId,
                workspaceKey,
                workspaceDataRootPath,
                details.BundleId,
                details.AssetId,
                draft.DraftId,
                draft.DraftMode,
                appliedDraftRevision ?? draft.Reference.DraftRevision);
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetOperationGateway.ApplyTextAssetEditDraftAsync(command, token));
            if (!result.IsSuccess)
            {
                _operationFeedbackBridge.AppendResultFailure(result.Errors);
                return false;
            }

            if (result.Value is { } appliedResult)
            {
                assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
                    appliedResult.DraftId,
                    appliedResult.DraftMode,
                    draft.SourceFingerprint,
                    draft.SourceDescription,
                    IsAppliedBaseline: true,
                    DraftRevision: appliedResult.DraftRevision));
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
