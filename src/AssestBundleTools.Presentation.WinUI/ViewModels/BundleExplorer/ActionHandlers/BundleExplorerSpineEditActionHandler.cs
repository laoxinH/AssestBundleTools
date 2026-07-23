using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
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
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceOperationChangeNotifier? _operationChangeNotifier;

    public BundleExplorerSpineEditActionHandler(
        BundleExplorerFileReplacementActionExecutor fileReplacementActionExecutor,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IFilePickerService filePickerService,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier)
    {
        _fileReplacementActionExecutor = fileReplacementActionExecutor ?? throw new ArgumentNullException(nameof(fileReplacementActionExecutor));
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
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

            if (part.Part.Role == SpineAssetGroupPartRole.Skeleton)
            {
                var compatibility = await _workspaceAssetOperationGateway.CheckSpineSkeletonReplacementCompatibilityAsync(
                    new CheckSpineSkeletonReplacementCompatibilityQuery(
                        workspaceSummary.WorkspaceId,
                        workspaceSummary.WorkspaceKey ?? string.Empty,
                        workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                        part.BundleId,
                        new AssetId(part.AssetId),
                        selectedFilePath));
                if (!compatibility.IsSuccess || compatibility.Value is null)
                {
                    if (!compatibility.IsCancelled)
                    {
                        appendResultFailure(compatibility.Errors);
                    }

                    return false;
                }

                if (!compatibility.Value.IsCompatible)
                {
                    appendResultFailure([
                        ApplicationErrors.SpineSkeletonFormatMismatch(
                            compatibility.Value.TargetFormat.ToString(),
                            compatibility.Value.SourceFormat.ToString())]);
                    return false;
                }
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
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        setBusyState(true);
        try
        {
            var selectedDirectoryPath = await _filePickerService.PickSpineReplacementSourceDirectoryAsync();
            if (string.IsNullOrWhiteSpace(selectedDirectoryPath))
            {
                return false;
            }

            var matches = await MatchSourcesAsync(
                workspaceSummary,
                spineGroupTab.Item,
                [selectedDirectoryPath],
                appendResultFailure);
            var changed = matches is not null &&
                          spineGroupTab.SpineEditDraft?.ApplyMatches(matches) == true;
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

    public async Task<SpineAssetGroupEditDraftViewModel?> CreateSourceMatchDraftAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDto group,
        IReadOnlyList<string> sourcePaths,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(appendResultFailure);
        if (sourcePaths.Count == 0)
        {
            return null;
        }

        var matches = await MatchSourcesAsync(workspaceSummary, group, sourcePaths, appendResultFailure);
        if (matches is null)
        {
            return null;
        }

        var draft = new SpineAssetGroupEditDraftViewModel(group, _localizationService);
        return draft.ApplyMatches(matches) ? draft : null;
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

        return await ApplySpineEditCoreAsync(
            workspaceSummary,
            draft.Group,
            draft,
            setBusyState,
            appendResultFailure,
            spineGroupTab.NotifySpineEditStateChanged);
    }

    public Task<bool> ApplySpineEditAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupEditDraftViewModel draft,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return ApplySpineEditCoreAsync(
            workspaceSummary,
            draft.Group,
            draft,
            setBusyState,
            appendResultFailure,
            notifyDraftStateChanged: null);
    }

    private async Task<bool> ApplySpineEditCoreAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDto group,
        SpineAssetGroupEditDraftViewModel draft,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        Action? notifyDraftStateChanged)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (!draft.HasChanges)
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
                    CreateSpineReplacementOperationContext(group, draft),
                    requireSpineSkeletonFormatMatch: part.Part.Role == SpineAssetGroupPartRole.Skeleton);
                if (!applied)
                {
                    return false;
                }

                appliedAny = true;
            }

            if (appliedAny)
            {
                draft.MarkSaved();
                notifyDraftStateChanged?.Invoke();
            }

            return appliedAny;
        }
        finally
        {
            setBusyState(false);
        }
    }

    private async Task<IReadOnlyList<SpineReplacementSourceMatchDto>?> MatchSourcesAsync(
        WorkspaceSummaryDto workspaceSummary,
        SpineAssetGroupDto group,
        IReadOnlyList<string> sourcePaths,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        var result = await _workspaceAssetOperationGateway.MatchSpineReplacementSourcesAsync(
            new MatchSpineReplacementSourcesQuery(
                workspaceSummary.WorkspaceId,
                workspaceSummary.WorkspaceKey ?? string.Empty,
                workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                group,
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

    private static ReplacementOperationContextDto CreateSpineReplacementOperationContext(
        SpineAssetGroupDto group,
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
                        BundleId: group.BundleId.Value,
                        AssetName: group.DisplayName,
                        AssetType: "SpineAssetGroup",
                        SourceFingerprint: group.GroupId))
            ]);

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
