using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 负责已选择替换来源的应用、撤销和重做。
/// </summary>
internal sealed class BundleExplorerReplacementActionHandler
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly WorkspaceOperationPendingTracker? _operationPendingTracker;

    public BundleExplorerReplacementActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        BundleExplorerFileReplacementActionExecutor fileReplacementActionExecutor,
        WorkspaceOperationPendingTracker? operationPendingTracker = null)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _fileReplacementActionExecutor = fileReplacementActionExecutor ?? throw new ArgumentNullException(nameof(fileReplacementActionExecutor));
        _operationPendingTracker = operationPendingTracker;
    }

    public async Task ApplyPendingReplacementAsync(
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
            if (assetTab.PendingFileReplacementCandidate is not null)
            {
                if (await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                        workspaceSummary,
                        details,
                        assetTab.PendingFileReplacementCandidate,
                        appendResultFailure))
                {
                    assetTab.ClearPendingReplacementCandidates();
                }

                return;
            }

            if (assetTab.PendingWorkspaceReplacementCandidate is null)
            {
                return;
            }

            var workspaceResult = await _workspaceAssetOperationGateway.ReplaceAssetFromWorkspaceAsync(
                new ReplaceAssetFromWorkspaceCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceBundleId,
                    assetTab.PendingWorkspaceReplacementCandidate.SourceAssetId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath));

            if (!TryGetValue(workspaceResult, out var replacementResult, appendResultFailure))
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
        }
        finally
        {
            setBusyState(false);
        }
    }

    public Task UndoAppliedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        string? replacementOperationId = null) =>
        UndoReplacementAsync(
            workspaceSummary,
            details.BundleId,
            details.AssetId,
            details.Name,
            replacementOperationId,
            setBusyState,
            appendResultFailure,
            assetTab.ClearPendingReplacementCandidates);

    public Task UndoProjectedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        UndoReplacementAsync(
            workspaceSummary,
            target.BundleId,
            target.AssetId,
            target.AssetName,
            replacementOperationId,
            setBusyState,
            appendResultFailure,
            clearPendingReplacementCandidates: null);

    public Task RedoAppliedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        string? replacementOperationId = null) =>
        RedoReplacementAsync(
            workspaceSummary,
            details.BundleId,
            details.AssetId,
            details.Name,
            replacementOperationId,
            setBusyState,
            appendResultFailure,
            assetTab.ClearPendingReplacementCandidates);

    public Task RedoProjectedReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        RedoReplacementAsync(
            workspaceSummary,
            target.BundleId,
            target.AssetId,
            target.AssetName,
            replacementOperationId,
            setBusyState,
            appendResultFailure,
            clearPendingReplacementCandidates: null);

    private async Task UndoReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        BundleId bundleId,
        AssetId assetId,
        string targetAssetName,
        string? replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Action? clearPendingReplacementCandidates)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAssetName);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        using var pendingScope = BeginPendingScope(workspaceSummary, replacementOperationId);
        try
        {
            var result = await _workspaceAssetOperationGateway.UndoAssetReplacementAsync(
                new UndoAssetReplacementCommand(
                    workspaceSummary.WorkspaceId,
                    bundleId,
                    assetId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath,
                    replacementOperationId));

            if (!TryGetValue(result, out var undoResult, appendResultFailure))
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

            clearPendingReplacementCandidates?.Invoke();
        }
        finally
        {
            setBusyState(false);
        }
    }

    private async Task RedoReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        BundleId bundleId,
        AssetId assetId,
        string targetAssetName,
        string? replacementOperationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Action? clearPendingReplacementCandidates)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAssetName);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.RedoAssetReplacementAsync(
                new RedoAssetReplacementCommand(
                    workspaceSummary.WorkspaceId,
                    bundleId,
                    assetId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath,
                    replacementOperationId));

            if (!TryGetValue(result, out var redoResult, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogReplacementRedone,
                redoResult.TargetAssetName));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationReplacementRedone,
                    redoResult.TargetAssetName),
                AppNotificationSeverity.Informational);

            clearPendingReplacementCandidates?.Invoke();
        }
        finally
        {
            setBusyState(false);
        }
    }

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

    private IDisposable? BeginPendingScope(WorkspaceSummaryDto workspaceSummary, string? operationId)
    {
        return string.IsNullOrWhiteSpace(operationId)
            ? null
            : _operationPendingTracker?.Begin(workspaceSummary.WorkspaceId, operationId);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
