using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

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

    public BundleExplorerChangeRecordActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public Task DeleteChangeRecordAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        string operationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        DeleteChangeRecordAsync(
            workspaceSummary,
            operationId,
            details.Name,
            setBusyState,
            appendResultFailure,
            assetTab.ClearPendingReplacementCandidates);

    public Task DeleteProjectedChangeRecordAsync(
        WorkspaceSummaryDto workspaceSummary,
        ProjectedChangeRecordTarget target,
        string operationId,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        DeleteChangeRecordAsync(
            workspaceSummary,
            operationId,
            target.AssetName,
            setBusyState,
            appendResultFailure,
            clearPendingReplacementCandidates: null);

    private async Task DeleteChangeRecordAsync(
        WorkspaceSummaryDto workspaceSummary,
        string operationId,
        string targetAssetName,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Action? clearPendingReplacementCandidates)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAssetName);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.DeleteWorkspaceOperationRecordAsync(
                new DeleteWorkspaceOperationRecordCommand(
                    workspaceSummary.WorkspaceId,
                    operationId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath));

            if (!TryGetValue(result, out _, appendResultFailure))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogOperationRecordDeleted,
                targetAssetName));

            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationOperationRecordDeleted,
                    targetAssetName),
                AppNotificationSeverity.Informational);

            clearPendingReplacementCandidates?.Invoke();
        }
        finally
        {
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
