using AssestBundleTools.Application.Assets.Dtos;
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
/// 更新已有 TextAsset 文本草稿 payload，并追加可恢复草稿更新事实。
/// </summary>
public sealed class UpdateTextAssetEditDraftHandler
    : ICommandHandler<UpdateTextAssetEditDraftCommand, Result<AssetTextEditDraftUpdatedResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly WorkspaceTextEditOperationRecorder? _operationRecorder;

    public UpdateTextAssetEditDraftHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        WorkspaceTextEditOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetTextEditDraftUpdatedResultDto>> HandleAsync(
        UpdateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Cancelled());
        }

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Failure(validationError));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (targetDetails.AssetType != AssetType.TextAsset)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(AssetType.TextAsset, targetDetails.AssetType)));
        }

        var workspaceKey = command.WorkspaceKey.Trim();
        var draftId = command.DraftId.Trim();
        var draftMode = NormalizeDraftMode(command.DraftMode);
        var updatedAtUtc = DateTimeOffset.UtcNow;
        var contentType = ResolveContentType(targetDetails.AssetTypeDisplayName);
        var normalizedTextContent = NormalizeTextContent(command.TextContent);
        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        draftTextStore.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            workspaceKey,
            draftId,
            draftMode,
            contentType,
            normalizedTextContent,
            updatedAtUtc,
            SourceFingerprint: WorkspaceTextEditOperationRecorder.CreateSourceFingerprint(
                targetDetails,
                targetDetails.PreferredDisplaySizeInBytes ?? targetDetails.SizeInBytes),
            SourceDescription: targetDetails.Name,
            Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString()));

        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            contentType,
            System.Text.Encoding.UTF8.GetByteCount(normalizedTextContent),
            workspaceKey,
            DraftId: draftId,
            DraftMode: draftMode);
        var result = new AssetTextEditDraftUpdatedResultDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            targetDetails.Name,
            targetDetails.AssetType,
            targetDetails.AssetTypeDisplayName,
            draftId,
            draftMode,
            reference,
            updatedAtUtc);

        _operationRecorder?.RecordDraftUpdated(command, targetDetails, result);
        return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Success(result));
    }

    private static AppError? ValidateCommand(UpdateTextAssetEditDraftCommand command)
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

        if (string.IsNullOrWhiteSpace(command.DraftId))
        {
            return ApplicationErrors.AssetEditDraftIdRequired();
        }

        return command.TextContent.Length > AssetTextEditPolicy.InlineEditableTextLength
            ? ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength)
            : null;
    }

    private static string NormalizeDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();

    private static string ResolveContentType(string? assetTypeDisplayName) =>
        string.Equals(assetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase)
            ? "text/x-shader; charset=utf-8"
            : "text/plain; charset=utf-8";

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
