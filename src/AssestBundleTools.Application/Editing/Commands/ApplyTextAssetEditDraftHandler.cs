using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 将 TextAsset 文本草稿应用到工作区操作记录事实流。
/// </summary>
public sealed class ApplyTextAssetEditDraftHandler : ICommandHandler<ApplyTextAssetEditDraftCommand, Result<AssetTextEditAppliedResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceTextEditSessionRegistry _textEditSessionRegistry;
    private readonly WorkspaceTextEditOperationRecorder? _operationRecorder;

    public ApplyTextAssetEditDraftHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceTextEditSessionRegistry textEditSessionRegistry,
        WorkspaceTextEditOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _textEditSessionRegistry = textEditSessionRegistry ?? throw new ArgumentNullException(nameof(textEditSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetTextEditAppliedResultDto>> HandleAsync(
        ApplyTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Cancelled());
        }

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Failure(validationError));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (targetDetails.AssetType != AssetType.TextAsset)
        {
            return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(AssetType.TextAsset, targetDetails.AssetType)));
        }

        if (!_textEditSessionRegistry.TryApplyTextEdit(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                command.DraftId.Trim(),
                NormalizeDraftMode(command.DraftMode),
                command.DraftRevision,
                out var result) ||
            result is null)
        {
            return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        _operationRecorder?.RecordDraftApplied(command, targetDetails, result);
        return Task.FromResult(Result<AssetTextEditAppliedResultDto>.Success(result));
    }

    private static AppError? ValidateCommand(ApplyTextAssetEditDraftCommand command)
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

    private static string NormalizeDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();
}
