using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 负责 Spine 资源组编辑来源选择与应用。
/// </summary>
internal sealed class BundleExplorerSpineEditActionHandler
{
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceOperationChangeNotifier? _operationChangeNotifier;

    public BundleExplorerSpineEditActionHandler(
        BundleExplorerFileReplacementActionExecutor fileReplacementActionExecutor,
        IFilePickerService filePickerService,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier)
    {
        _fileReplacementActionExecutor = fileReplacementActionExecutor ?? throw new ArgumentNullException(nameof(fileReplacementActionExecutor));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _operationChangeNotifier = operationChangeNotifier;
    }

    public async Task<bool> SelectSpineEditPartFileAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        SpineAssetGroupEditPartViewModel part,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var selectedFilePath = await _filePickerService.PickReplacementSourceFileAsync(part.AssetType);
            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                return false;
            }

            var candidate = await _fileReplacementActionExecutor.GetFileReplacementCandidateAsync(
                workspaceSummary,
                part.BundleId,
                new AssetId(part.AssetId),
                selectedFilePath,
                appendResultFailure);
            if (candidate is null)
            {
                return false;
            }

            var changed = spineGroupTab.SpineEditDraft?.ApplyPartSource(part, candidate.SourceFilePath) == true;
            spineGroupTab.NotifySpineEditStateChanged();
            return changed;
        }
        catch (Exception ex)
        {
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFilePickerOpenFailedFormat,
                ex.GetType().Name));
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                GetString(ResourceKeys.BundleExplorerNotificationFilePickerOpenFailed),
                AppNotificationSeverity.Error);
            return false;
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task<bool> SelectSpineEditBatchSourceDirectoryAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(setBusyState);

        setBusyState(true);
        try
        {
            var selectedDirectoryPath = await _filePickerService.PickSpineReplacementSourceDirectoryAsync();
            if (string.IsNullOrWhiteSpace(selectedDirectoryPath))
            {
                return false;
            }

            var changed = spineGroupTab.SpineEditDraft?.ApplyDirectoryMatches(selectedDirectoryPath) == true;
            spineGroupTab.NotifySpineEditStateChanged();
            return changed;
        }
        catch (Exception ex)
        {
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFilePickerOpenFailedFormat,
                ex.GetType().Name));
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                GetString(ResourceKeys.BundleExplorerNotificationFilePickerOpenFailed),
                AppNotificationSeverity.Error);
            return false;
        }
        finally
        {
            setBusyState(false);
        }
    }

    public async Task<bool> ApplySpineEditAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        var draft = spineGroupTab.SpineEditDraft;
        if (draft is null || !draft.HasChanges)
        {
            return false;
        }

        setBusyState(true);
        var appliedAny = false;
        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();
        try
        {
            foreach (var part in draft.ChangedParts)
            {
                if (string.IsNullOrWhiteSpace(part.SelectedSourceFilePath))
                {
                    continue;
                }

                var candidate = await _fileReplacementActionExecutor.GetFileReplacementCandidateAsync(
                    workspaceSummary,
                    part.BundleId,
                    new AssetId(part.AssetId),
                    part.SelectedSourceFilePath!,
                    appendResultFailure);
                if (candidate is null)
                {
                    return false;
                }

                var applied = await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                    workspaceSummary,
                    part.BundleId,
                    new AssetId(part.AssetId),
                    part.DisplayName,
                    candidate,
                    appendResultFailure,
                    CreateSpineReplacementOperationContext(spineGroupTab, draft));
                if (!applied)
                {
                    return false;
                }

                appliedAny = true;
            }

            if (appliedAny)
            {
                draft.MarkSaved();
                spineGroupTab.NotifySpineEditStateChanged();
            }

            return appliedAny;
        }
        finally
        {
            setBusyState(false);
        }
    }

    private static ReplacementOperationContextDto CreateSpineReplacementOperationContext(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        SpineAssetGroupEditDraftViewModel draft) =>
        new(
            BatchId: draft.BatchId,
            Relations:
            [
                new WorkspaceOperationRelationDto(
                    RelationId: draft.RelationId,
                    RelationKind: WorkspaceOperationRelationNames.LinkedResourceOperation,
                    Role: WorkspaceOperationRelationNames.SubjectRole,
                    RelatedTarget: new WorkspaceOperationTargetDto(
                        BundleId: spineGroupTab.BundleId.Value,
                        AssetName: spineGroupTab.DisplayName,
                        AssetType: "SpineAssetGroup",
                        SourceFingerprint: spineGroupTab.GroupId))
            ]);

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
