using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
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
        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        var textResult = ResolveUpdatedTextContent(command, workspaceKey, draftId, draftMode, draftTextStore);
        if (textResult.IsCancelled)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Cancelled());
        }

        if (!textResult.IsSuccess || textResult.Value is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Failure(textResult.Errors));
        }

        var sourceFingerprint = ResolveDraftSourceFingerprint(workspaceKey, command.WorkspaceDataRootPath, draftId, draftMode) ??
            WorkspaceTextEditOperationRecorder.CreateSourceFingerprint(
                targetDetails,
                targetDetails.PreferredDisplaySizeInBytes ?? targetDetails.SizeInBytes);
        draftTextStore.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            workspaceKey,
            draftId,
            draftMode,
            contentType,
            textResult.Value,
            updatedAtUtc,
            SourceFingerprint: sourceFingerprint,
            SourceDescription: targetDetails.Name,
            Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString()));

        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            contentType,
            Encoding.UTF8.GetByteCount(textResult.Value),
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

        var hasTextContent = command.TextContent is not null;
        var rangeEdits = command.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>();
        var hasRangeEdits = rangeEdits.Count > 0;
        if (hasTextContent == hasRangeEdits)
        {
            return ApplicationErrors.AssetEditDraftTextUpdatePayloadInvalid();
        }

        if (hasTextContent && command.TextContent!.Length > AssetTextEditPolicy.InlineEditableTextLength)
        {
            return ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength);
        }

        if (rangeEdits.Count > AssetTextEditPolicy.MaxRangeEditCount)
        {
            return ApplicationErrors.AssetEditDraftTextUpdatePayloadInvalid();
        }

        foreach (var edit in rangeEdits)
        {
            if (edit is null ||
                edit.Start < 0 ||
                edit.Length < 0 ||
                edit.Text is null)
            {
                return ApplicationErrors.AssetEditDraftTextPatchRangeInvalid(edit?.Start ?? -1, edit?.Length ?? -1);
            }
        }

        return null;
    }

    private Result<string> ResolveUpdatedTextContent(
        UpdateTextAssetEditDraftCommand command,
        string workspaceKey,
        string draftId,
        string draftMode,
        IAssetEditDraftTextStore draftTextStore)
    {
        if (command.TextContent is not null)
        {
            return Result<string>.Success(NormalizeTextContent(command.TextContent));
        }

        var currentTextResult = LoadCurrentDraftText(workspaceKey, draftId, draftMode, draftTextStore);
        if (!currentTextResult.IsSuccess || currentTextResult.Value is null)
        {
            return currentTextResult;
        }

        return ApplyRangeEdits(
            currentTextResult.Value,
            command.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>());
    }

    private static Result<string> LoadCurrentDraftText(
        string workspaceKey,
        string draftId,
        string draftMode,
        IAssetEditDraftTextStore draftTextStore)
    {
        var builder = new StringBuilder();
        var offset = 0;

        while (builder.Length <= AssetTextEditPolicy.InlineEditableTextLength)
        {
            var chunk = draftTextStore.LoadTextChunk(
                workspaceKey,
                draftId,
                draftMode,
                offset,
                AssetTextEditPolicy.RangeEditReadChunkLength);
            if (chunk is null)
            {
                return Result<string>.Failure(ApplicationErrors.AssetEditDraftNotFound(workspaceKey, draftId, draftMode));
            }

            if (chunk.TotalLength > AssetTextEditPolicy.InlineEditableTextLength)
            {
                return Result<string>.Failure(
                    ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength));
            }

            builder.Append(chunk.Text);
            offset = chunk.Offset + chunk.Length;
            if (builder.Length > AssetTextEditPolicy.InlineEditableTextLength)
            {
                return Result<string>.Failure(
                    ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength));
            }

            if (offset >= chunk.TotalLength || chunk.Length == 0)
            {
                return Result<string>.Success(builder.ToString());
            }
        }

        return Result<string>.Failure(
            ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength));
    }

    private static Result<string> ApplyRangeEdits(
        string currentText,
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits)
    {
        var builder = new StringBuilder(currentText);
        var orderedEdits = rangeEdits
            .OrderByDescending(static edit => edit.Start)
            .ToArray();
        var nextStart = currentText.Length;

        foreach (var edit in orderedEdits)
        {
            if (edit.Start > currentText.Length ||
                edit.Length > currentText.Length - edit.Start ||
                edit.Start + edit.Length > nextStart)
            {
                return Result<string>.Failure(
                    ApplicationErrors.AssetEditDraftTextPatchRangeInvalid(edit.Start, edit.Length));
            }

            builder.Remove(edit.Start, edit.Length);
            builder.Insert(edit.Start, NormalizeTextContent(edit.Text));
            nextStart = edit.Start;
            if (builder.Length > AssetTextEditPolicy.InlineEditableTextLength)
            {
                return Result<string>.Failure(
                    ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength));
            }
        }

        return Result<string>.Success(builder.ToString());
    }

    private static string NormalizeDraftMode(string? draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();

    private static string ResolveContentType(string? assetTypeDisplayName) =>
        string.Equals(assetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase)
            ? "text/x-shader; charset=utf-8"
            : "text/plain; charset=utf-8";

    private string? ResolveDraftSourceFingerprint(
        string workspaceKey,
        string workspaceDataRootPath,
        string draftId,
        string draftMode)
    {
        var changeSet = _workspaceDataStoreFactory
            .CreateWorkspaceChangeStore(workspaceDataRootPath.Trim())
            .Load(workspaceKey.Trim());
        return (changeSet?.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .FirstOrDefault(descriptor =>
                string.Equals(descriptor.DraftId?.Trim(), draftId, StringComparison.Ordinal) &&
                string.Equals(descriptor.DraftMode?.Trim(), draftMode, StringComparison.Ordinal))
            ?.SourceFingerprint;
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
