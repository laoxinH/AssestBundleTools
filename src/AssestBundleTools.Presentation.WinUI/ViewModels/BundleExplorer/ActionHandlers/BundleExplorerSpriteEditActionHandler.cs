using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 执行 Sprite 编辑几何的工作区保存动作。
/// </summary>
internal sealed class BundleExplorerSpriteEditActionHandler
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;

    public BundleExplorerSpriteEditActionHandler(
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

    public async Task<bool> ApplySpriteEditAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        WorkspaceSpriteEditGeometryDto geometry,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.ApplySpriteEditAsync(
                new ApplySpriteEditCommand(
                    workspaceSummary.WorkspaceId,
                    details.BundleId,
                    details.AssetId,
                    geometry,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath));

            if (!TryGetValue(result, out var editResult, appendResultFailure))
            {
                return false;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogSpriteEditApplied,
                editResult.AssetName));

            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationSpriteEditApplied,
                    editResult.AssetName),
                AppNotificationSeverity.Success);

            assetTab.ClearPendingReplacementCandidates();
            return true;
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
