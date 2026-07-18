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

    public BundleExplorerDragReplacementActionHandler(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceOperationChangeNotifier operationChangeNotifier)
    {
        _fileReplacementActionExecutor = new BundleExplorerFileReplacementActionExecutor(
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            localizationService,
            filePickerService);
        _spineEditActionHandler = new BundleExplorerSpineEditActionHandler(
            _fileReplacementActionExecutor,
            filePickerService,
            appLogService,
            appNotificationService,
            localizationService,
            operationChangeNotifier);
    }

    public async Task<FileReplacementCandidateDto?> PrepareAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetListEntryViewModel target,
        string sourceFilePath,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            return await _fileReplacementActionExecutor.GetFileReplacementCandidateAsync(
                workspaceSummary,
                target.Item.BundleId,
                target.Item.AssetId,
                sourceFilePath,
                appendResultFailure);
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task<bool> ApplyAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetListEntryViewModel target,
        FileReplacementCandidateDto candidate,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            return await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                workspaceSummary,
                target.Item.BundleId,
                target.Item.AssetId,
                target.DisplayName,
                candidate,
                appendResultFailure);
        }
        finally
        {
            setBusyState(false);
        }
    }

    public SpineAssetGroupEditDraftViewModel? PrepareSpineGroup(
        SpineAssetGroupEntryViewModel target,
        string sourceDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _spineEditActionHandler.CreateDirectoryMatchDraft(
            target.Item,
            sourceDirectoryPath);
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
