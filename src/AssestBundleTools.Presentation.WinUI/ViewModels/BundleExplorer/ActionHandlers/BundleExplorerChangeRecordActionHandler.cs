using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 执行资产变更记录管理动作。
/// </summary>
internal sealed class BundleExplorerChangeRecordActionHandler
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly WorkspaceOperationPendingTracker? _operationPendingTracker;

    public BundleExplorerChangeRecordActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        WorkspaceOperationPendingTracker? operationPendingTracker = null)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _operationPendingTracker = operationPendingTracker;
    }

    public async Task DeleteChangeRecordsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<string> operationIds,
        string targetAssetName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Action? clearPendingReplacementCandidates = null,
        bool isRestoreOriginal = false)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAssetName);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        var normalizedOperationIds = operationIds
            .Where(static operationId => !string.IsNullOrWhiteSpace(operationId))
            .Select(static operationId => operationId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedOperationIds.Length == 0)
        {
            return;
        }

        setBusyState(true);
        var pendingScopes = normalizedOperationIds
            .Select(operationId => _operationPendingTracker?.Begin(workspaceSummary.WorkspaceId, operationId))
            .Where(static scope => scope is not null)
            .ToArray();
        try
        {
            var result = await _workspaceAssetOperationGateway.DeleteWorkspaceOperationRecordAsync(
                new DeleteWorkspaceOperationRecordCommand(
                    workspaceSummary.WorkspaceId,
                    normalizedOperationIds[0],
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath,
                    normalizedOperationIds.Skip(1).ToArray()));

            if (!TryGetValue(result, out var deletedResult, appendResultFailure))
            {
                return;
            }

            var deletedCount = deletedResult.DeletedOperationIds?.Count ?? normalizedOperationIds.Length;
            _appLogService.AppendInfo(isRestoreOriginal
                ? _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogOriginalAssetRestored,
                    targetAssetName,
                    deletedCount)
                : _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerLogOperationRecordDeleted,
                    targetAssetName));

            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    isRestoreOriginal
                        ? ResourceKeys.BundleExplorerNotificationOriginalAssetRestored
                        : ResourceKeys.BundleExplorerNotificationOperationRecordDeleted,
                    targetAssetName),
                AppNotificationSeverity.Informational);

            clearPendingReplacementCandidates?.Invoke();
        }
        finally
        {
            foreach (var pendingScope in pendingScopes)
            {
                pendingScope!.Dispose();
            }

            setBusyState(false);
        }
    }

    private static bool TryGetValue<T>(
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
        if (!result.IsCancelled)
        {
            appendResultFailure(result.Errors);
        }

        return false;
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
