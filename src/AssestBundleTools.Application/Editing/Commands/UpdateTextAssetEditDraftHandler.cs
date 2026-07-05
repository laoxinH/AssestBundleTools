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
        var sourceFingerprint = ResolveDraftSourceFingerprint(workspaceKey, command.WorkspaceDataRootPath, draftId, draftMode) ??
            WorkspaceTextEditOperationRecorder.CreateSourceFingerprint(
                targetDetails,
                targetDetails.PreferredDisplaySizeInBytes ?? targetDetails.SizeInBytes);
        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        var contentLengthResult = SaveUpdatedTextContent(
            command,
            workspaceKey,
            draftId,
            draftMode,
            contentType,
            updatedAtUtc,
            sourceFingerprint,
            targetDetails,
            draftTextStore);
        if (!contentLengthResult.IsSuccess)
        {
            return Task.FromResult(Result<AssetTextEditDraftUpdatedResultDto>.Failure(contentLengthResult.Errors));
        }

        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            contentType,
            contentLengthResult.Value,
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

        if (hasTextContent && command.PatchBaseline is not null)
        {
            return ApplicationErrors.AssetEditDraftTextUpdatePayloadInvalid();
        }

        if (hasRangeEdits && !IsValidPatchBaseline(command.PatchBaseline))
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

    private static bool IsValidPatchBaseline(TextAssetEditDraftPatchBaselineDto? baseline) =>
        baseline is not null &&
        baseline.ContentLength >= 0 &&
        baseline.ContentSha256.Length == 64 &&
        baseline.ContentSha256.All(static character =>
            character is >= '0' and <= '9' ||
            character is >= 'a' and <= 'f' ||
            character is >= 'A' and <= 'F');

    private static Result<long> SaveUpdatedTextContent(
        UpdateTextAssetEditDraftCommand command,
        string workspaceKey,
        string draftId,
        string draftMode,
        string contentType,
        DateTimeOffset updatedAtUtc,
        string sourceFingerprint,
        AssetDetailsDto targetDetails,
        IAssetEditDraftTextStore draftTextStore)
    {
        if (command.TextContent is not null)
        {
            var normalizedText = NormalizeTextContent(command.TextContent);
            draftTextStore.SaveTextPayload(new AssetEditDraftTextPayloadDto(
                workspaceKey,
                draftId,
                draftMode,
                contentType,
                normalizedText,
                updatedAtUtc,
                SourceFingerprint: sourceFingerprint,
                SourceDescription: targetDetails.Name,
                Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString()));
            return Result<long>.Success(Encoding.UTF8.GetByteCount(normalizedText));
        }

        var patchResult = draftTextStore.ApplyTextPatch(new AssetEditDraftTextPatchDto(
            workspaceKey,
            draftId,
            draftMode,
            contentType,
            command.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>(),
            command.PatchBaseline!,
            updatedAtUtc,
            AssetTextEditPolicy.InlineEditableTextLength,
            SourceFingerprint: sourceFingerprint,
            SourceDescription: targetDetails.Name,
            Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString()));
        return patchResult.Status switch
        {
            AssetEditDraftTextPatchStatus.Applied => Result<long>.Success(patchResult.ContentLength),
            AssetEditDraftTextPatchStatus.NotFound => Result<long>.Failure(
                ApplicationErrors.AssetEditDraftNotFound(workspaceKey, draftId, draftMode)),
            AssetEditDraftTextPatchStatus.BaselineMismatch => Result<long>.Failure(
                ApplicationErrors.AssetEditDraftTextPatchBaselineMismatch()),
            AssetEditDraftTextPatchStatus.RangeInvalid => Result<long>.Failure(
                ApplicationErrors.AssetEditDraftTextPatchRangeInvalid(
                    patchResult.InvalidRangeStart ?? -1,
                    patchResult.InvalidRangeLength ?? -1)),
            AssetEditDraftTextPatchStatus.LengthExceeded => Result<long>.Failure(
                ApplicationErrors.AssetEditDraftTextLengthExceeded(AssetTextEditPolicy.InlineEditableTextLength)),
            _ => Result<long>.Failure(ApplicationErrors.AssetEditDraftTextUpdatePayloadInvalid())
        };
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
