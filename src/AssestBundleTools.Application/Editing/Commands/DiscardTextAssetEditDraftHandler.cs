using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 删除 TextAsset 文本草稿 payload，并将丢弃动作追加到操作事实流。
/// </summary>
public sealed class DiscardTextAssetEditDraftHandler : ICommandHandler<DiscardTextAssetEditDraftCommand, Result<AssetTextEditDraftDiscardedResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly WorkspaceTextEditOperationRecorder? _operationRecorder;

    public DiscardTextAssetEditDraftHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        WorkspaceTextEditOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetTextEditDraftDiscardedResultDto>> HandleAsync(
        DiscardTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetTextEditDraftDiscardedResultDto>.Cancelled());
        }

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDiscardedResultDto>.Failure(validationError));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDiscardedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        var draftMode = string.IsNullOrWhiteSpace(command.DraftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : command.DraftMode.Trim();
        var timestamp = DateTimeOffset.UtcNow;
        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        draftTextStore.Delete(command.WorkspaceKey.Trim(), command.DraftId.Trim(), draftMode);

        var result = new AssetTextEditDraftDiscardedResultDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            targetDetails.Name,
            command.DraftId.Trim(),
            draftMode,
            timestamp);
        _operationRecorder?.RecordDraftDiscarded(command, targetDetails, result);
        return Task.FromResult(Result<AssetTextEditDraftDiscardedResultDto>.Success(result));
    }

    private static AppError? ValidateCommand(DiscardTextAssetEditDraftCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return ApplicationErrors.WorkspaceIdRequired();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return ApplicationErrors.AssetEditDraftWorkspaceKeyRequired();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return ApplicationErrors.WorkspaceOpenDataRootPathRequired();
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return ApplicationErrors.BundleIdRequired();
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return ApplicationErrors.AssetIdRequired();
        }

        return string.IsNullOrWhiteSpace(command.DraftId)
            ? ApplicationErrors.AssetEditDraftIdRequired()
            : null;
    }
}
