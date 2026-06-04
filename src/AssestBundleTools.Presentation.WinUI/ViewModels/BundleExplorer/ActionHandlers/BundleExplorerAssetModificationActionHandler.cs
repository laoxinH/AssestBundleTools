using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 聚合资产级替换与撤销动作，并兼容旧的资产提取入口委托。
/// </summary>
internal sealed class BundleExplorerAssetModificationActionHandler
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly BundleExplorerAssetExtractionActionHandler _extractionActionHandler;
    private readonly BundleExplorerSpriteEditActionHandler _spriteEditActionHandler;
    private readonly BundleExplorerSpriteTextureReplacementActionHandler _spriteTextureReplacementActionHandler;
    private readonly BundleExplorerReplacementActionHandler _replacementActionHandler;
    private readonly BundleExplorerChangeRecordActionHandler _changeRecordActionHandler;
    private readonly BundleExplorerSpineEditActionHandler _spineEditActionHandler;

    public BundleExplorerAssetModificationActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IBackgroundTaskRunner? backgroundTaskRunner = null,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(filePickerService);
        _fileReplacementActionExecutor = new BundleExplorerFileReplacementActionExecutor(
            _workspaceAssetOperationGateway,
            _appLogService,
            _appNotificationService,
            _localizationService,
            filePickerService);
        _extractionActionHandler = new BundleExplorerAssetExtractionActionHandler(
            _workspaceAssetOperationGateway,
            _appLogService,
            _appNotificationService,
            _localizationService,
            filePickerService,
            backgroundTaskRunner,
            backgroundTaskStatusService,
            operationChangeNotifier,
            backgroundWorkScheduler);
        _spriteEditActionHandler = new BundleExplorerSpriteEditActionHandler(
            _workspaceAssetOperationGateway,
            _appLogService,
            _appNotificationService,
            _localizationService);
        _spriteTextureReplacementActionHandler = new BundleExplorerSpriteTextureReplacementActionHandler(
            _fileReplacementActionExecutor,
            _appNotificationService,
            _localizationService);
        _replacementActionHandler = new BundleExplorerReplacementActionHandler(
            _workspaceAssetOperationGateway,
            _appLogService,
            _appNotificationService,
            _localizationService,
            _fileReplacementActionExecutor);
        _changeRecordActionHandler = new BundleExplorerChangeRecordActionHandler(
            _workspaceAssetOperationGateway,
            _appLogService,
            _appNotificationService,
            _localizationService);
        _spineEditActionHandler = new BundleExplorerSpineEditActionHandler(
            _fileReplacementActionExecutor,
            filePickerService,
            _appLogService,
            _appNotificationService,
            _localizationService,
            operationChangeNotifier);
    }

    public Task ExtractSelectedAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _extractionActionHandler.ExtractSelectedAssetAsync(
            workspaceSummary,
            details,
            setBusyState,
            appendResultFailure);

    public Task ExtractSelectedAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetListEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _extractionActionHandler.ExtractSelectedAssetsAsync(
            workspaceSummary,
            selectedEntries,
            defaultBundleDisplayName,
            setBusyState,
            appendResultFailure);

    public Task ExtractSpineAssetGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _extractionActionHandler.ExtractSpineAssetGroupAsync(
            workspaceSummary,
            spineGroupTab,
            setBusyState,
            appendResultFailure);

    public Task ExtractSelectedSpineAssetGroupsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<SpineAssetGroupEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _extractionActionHandler.ExtractSelectedSpineAssetGroupsAsync(
            workspaceSummary,
            selectedEntries,
            defaultBundleDisplayName,
            setBusyState,
            appendResultFailure);

    public async Task SelectWorkspaceReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(reapplyAssetDocumentState);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.GetWorkspaceReplacementCandidatesAsync(
                new GetWorkspaceReplacementCandidatesQuery(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId));

            if (!TryGetValue(result, out var candidates, appendResultFailure))
            {
                return;
            }

            var selectedCandidate = candidates[0];
            assetTab.UpdatePendingWorkspaceReplacementCandidate(selectedCandidate);
            reapplyAssetDocumentState(assetTab, details);

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
            setBusyState(false);
        }
    }

    public async Task SelectFileReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<AssetDocumentTabViewModel, AssetDetailsDto> reapplyAssetDocumentState,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(reapplyAssetDocumentState);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var candidate = await _fileReplacementActionExecutor.PickFileReplacementCandidateAsync(
                workspaceSummary,
                details,
                appendResultFailure);
            if (candidate is null)
            {
                return;
            }

            assetTab.UpdatePendingFileReplacementCandidate(candidate);
            reapplyAssetDocumentState(assetTab, details);

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
            setBusyState(false);
        }
    }

    public async Task ModifyFromFileReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var candidate = await _fileReplacementActionExecutor.PickFileReplacementCandidateAsync(
                workspaceSummary,
                details,
                appendResultFailure);
            if (candidate is null)
            {
                return;
            }

            if (await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                    workspaceSummary,
                    details,
                    candidate,
                    appendResultFailure))
            {
                assetTab.ClearPendingReplacementCandidates();
            }
        }
        finally
        {
            setBusyState(false);
        }
    }

    public Task SelectSpriteTextureReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _spriteTextureReplacementActionHandler.SelectTextureReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            setBusyState,
            appendResultFailure);

    public Task ApplyPendingReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _replacementActionHandler.ApplyPendingReplacementAsync(
            workspaceSummary,
            assetTab,
            details,
            setBusyState,
            appendResultFailure);

    public Task UndoAppliedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        string? replacementOperationId = null) =>
        _replacementActionHandler.UndoAppliedReplacementAsync(
            workspaceSummary,
            assetTab,
            details,
            setBusyState,
            appendResultFailure,
            replacementOperationId);

    public Task RedoAppliedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        string? replacementOperationId = null) =>
        _replacementActionHandler.RedoAppliedReplacementAsync(
            workspaceSummary,
            assetTab,
            details,
            setBusyState,
            appendResultFailure,
            replacementOperationId);

    public Task UndoProjectedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _replacementActionHandler.UndoProjectedReplacementAsync(
            workspaceSummary,
            target,
            replacementOperationId,
            setBusyState,
            appendResultFailure);

    public Task RedoProjectedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _replacementActionHandler.RedoProjectedReplacementAsync(
            workspaceSummary,
            target,
            replacementOperationId,
            setBusyState,
            appendResultFailure);

    public Task<bool> ApplySpriteEditAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        WorkspaceSpriteEditGeometryDto geometry,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _spriteEditActionHandler.ApplySpriteEditAsync(
            workspaceSummary,
            assetTab,
            details,
            geometry,
            setBusyState,
            appendResultFailure);

    public Task DeleteChangeRecordAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        string operationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _changeRecordActionHandler.DeleteChangeRecordAsync(
            workspaceSummary,
            assetTab,
            details,
            operationId,
            setBusyState,
            appendResultFailure);

    public Task DeleteProjectedChangeRecordAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string operationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _changeRecordActionHandler.DeleteProjectedChangeRecordAsync(
            workspaceSummary,
            target,
            operationId,
            setBusyState,
            appendResultFailure);

    public Task<bool> SelectSpineEditPartFileAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        SpineAssetGroupEditPartViewModel part,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _spineEditActionHandler.SelectSpineEditPartFileAsync(
            workspaceSummary,
            spineGroupTab,
            part,
            setBusyState,
            appendResultFailure);

    public Task<bool> SelectSpineEditBatchSourceDirectoryAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState) =>
        _spineEditActionHandler.SelectSpineEditBatchSourceDirectoryAsync(
            spineGroupTab,
            setBusyState);

    public Task<bool> ApplySpineEditAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _spineEditActionHandler.ApplySpineEditAsync(
            workspaceSummary,
            spineGroupTab,
            setBusyState,
            appendResultFailure);

    private bool TryGetValue<T>(
        Result<T> result,
        [NotNullWhen(true)] out T? value,
        Action<IReadOnlyList<AppError>> appendResultFailure)
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

        appendResultFailure(result.Errors);
        return false;
    }

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
