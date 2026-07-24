using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 把已确认的行级拖入请求适配到现有普通资产和 Spine 替换执行器。
/// </summary>
internal sealed class BundleExplorerDragReplacementActionHandler
{
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly BundleExplorerSpineEditActionHandler _spineEditActionHandler;
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;

    public BundleExplorerDragReplacementActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceOperationChangeNotifier operationChangeNotifier)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
        _fileReplacementActionExecutor = new BundleExplorerFileReplacementActionExecutor(
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            localizationService,
            filePickerService);
        _spineEditActionHandler = new BundleExplorerSpineEditActionHandler(
            _fileReplacementActionExecutor,
            workspaceAssetOperationGateway,
            filePickerService,
            appLogService,
            appNotificationService,
            localizationService,
            operationChangeNotifier);
    }

    public async Task<AssetReplacementSourceMatchPlanDto?> PrepareAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetListEntryViewModel> targets,
        IReadOnlyList<SpineAssetGroupEntryViewModel> spineGroups,
        IReadOnlyList<string> sourcePaths,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(spineGroups);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var result = await _workspaceAssetOperationGateway.MatchAssetReplacementSourcesAsync(
                new(
                    workspaceSummary.WorkspaceId,
                    workspaceSummary.WorkspaceKey ?? string.Empty,
                    workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                    targets.Select(static target => target.Item).ToArray(),
                    spineGroups.Select(static group => group.Item).ToArray(),
                    sourcePaths));
            if (result.IsSuccess && result.Value is not null)
            {
                return result.Value;
            }

            if (!result.IsCancelled)
            {
                appendResultFailure(result.Errors);
            }

            return null;
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task<bool> ApplyAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetReplacementSourceMatchDto> matches,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        using var notificationBatch = _operationChangeNotifier.BeginNotificationBatch();
        try
        {
            foreach (var match in matches)
            {
                var candidate = await _fileReplacementActionExecutor.GetFileReplacementCandidateAsync(
                    workspaceSummary,
                    match.Target.BundleId,
                    match.Target.AssetId,
                    match.SourceFilePath,
                    appendResultFailure);
                if (candidate is null || !await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                        workspaceSummary,
                        match.Target.BundleId,
                        match.Target.AssetId,
                        string.IsNullOrWhiteSpace(match.Target.DisplayName)
                            ? match.Target.Name
                            : match.Target.DisplayName,
                        candidate,
                        appendResultFailure,
                        requireSpineSkeletonFormatMatch: match.RequireSpineSkeletonFormatMatch))
                {
                    return false;
                }
            }

            return matches.Count > 0;
        }
        finally
        {
            setBusyState(false);
        }
    }

    public Task<SpineAssetGroupEditDraftViewModel?> PrepareSpineGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupEntryViewModel target,
        IReadOnlyList<string> sourcePaths,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(target);
        return _spineEditActionHandler.CreateSourceMatchDraftAsync(
            workspaceSummary,
            target.Item,
            sourcePaths,
            appendResultFailure);
    }

    public Task<bool> ApplySpineGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupEditDraftViewModel draft,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        _spineEditActionHandler.ApplySpineEditAsync(
            workspaceSummary,
            draft,
            setBusyState,
            appendResultFailure);
}
