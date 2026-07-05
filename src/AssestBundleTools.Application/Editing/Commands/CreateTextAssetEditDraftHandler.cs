using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
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
/// 创建 TextAsset 编辑草稿；大文本先记录源引用，等用户明确进入全文编辑时再物化 payload。
/// </summary>
public sealed class CreateTextAssetEditDraftHandler : ICommandHandler<CreateTextAssetEditDraftCommand, Result<AssetTextEditDraftDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly GetAssetPreviewHandler _getAssetPreviewHandler;
    private readonly LoadPreviewTextChunkHandler _loadPreviewTextChunkHandler;
    private readonly WorkspaceTextEditOperationRecorder? _operationRecorder;

    public CreateTextAssetEditDraftHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        GetAssetPreviewHandler getAssetPreviewHandler,
        LoadPreviewTextChunkHandler loadPreviewTextChunkHandler,
        WorkspaceTextEditOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _getAssetPreviewHandler = getAssetPreviewHandler ?? throw new ArgumentNullException(nameof(getAssetPreviewHandler));
        _loadPreviewTextChunkHandler = loadPreviewTextChunkHandler ?? throw new ArgumentNullException(nameof(loadPreviewTextChunkHandler));
        _operationRecorder = operationRecorder;
    }

    public async Task<Result<AssetTextEditDraftDto>> HandleAsync(
        CreateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetTextEditDraftDto>.Cancelled();
        }

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Result<AssetTextEditDraftDto>.Failure(validationError);
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Result<AssetTextEditDraftDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId));
        }

        if (!CanCreateTextDraft(targetDetails))
        {
            return Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(AssetType.TextAsset, targetDetails.AssetType));
        }

        var previewResult = await _getAssetPreviewHandler.HandleAsync(
            new GetAssetPreviewQuery(
                command.WorkspaceId,
                command.WorkspaceKey,
                command.WorkspaceDataRootPath,
                command.BundleId,
                command.AssetId),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return Result<AssetTextEditDraftDto>.Cancelled();
        }

        if (!previewResult.IsSuccess || previewResult.Value is null)
        {
            return Result<AssetTextEditDraftDto>.Failure(
                previewResult.Errors.Count > 0
                    ? previewResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(command.AssetId.Value)]);
        }

        var preview = previewResult.Value;
        var updatedAtUtc = DateTimeOffset.UtcNow;
        var draftId = NormalizeOptional(command.DraftId) ?? CreateDraftId();
        var draftMode = NormalizeOptional(command.DraftMode) ?? WorkspaceTextEditOperationRecorder.TextDraftMode;
        var contentType = string.IsNullOrWhiteSpace(preview.ContentType)
            ? "text/plain; charset=utf-8"
            : preview.ContentType.Trim();
        var sourceFingerprint = CreatePreviewFingerprint(preview);
        var createReferenceResult = await CreateDraftReferenceAsync(
            command,
            targetDetails,
            preview,
            draftId,
            draftMode,
            contentType,
            updatedAtUtc,
            sourceFingerprint,
            cancellationToken);
        if (createReferenceResult.IsCancelled)
        {
            return Result<AssetTextEditDraftDto>.Cancelled();
        }

        if (!createReferenceResult.IsSuccess || createReferenceResult.Value is not { } draftReference)
        {
            return Result<AssetTextEditDraftDto>.Failure(
                createReferenceResult.Errors.Count > 0
                    ? createReferenceResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(command.AssetId.Value)]);
        }

        var draft = new AssetTextEditDraftDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            targetDetails.Name,
            targetDetails.AssetType,
            targetDetails.AssetTypeDisplayName,
            draftId,
            draftMode,
            draftReference.Reference,
            updatedAtUtc,
            CanApply: targetDetails.AssetType == AssetType.TextAsset,
            Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString(),
            CanEditInline: targetDetails.AssetType == AssetType.TextAsset &&
                draftReference.Reference.ContentLength <= AssetTextEditPolicy.InlineEditableTextLength,
            SourceFingerprint: sourceFingerprint,
            SourceDescription: targetDetails.Name,
            IsMaterialized: draftReference.IsMaterialized);

        _operationRecorder?.RecordDraftCreated(command, targetDetails, draft);
        return Result<AssetTextEditDraftDto>.Success(draft);
    }

    private async Task<Result<DraftReferenceCreationResult>> CreateDraftReferenceAsync(
        CreateTextAssetEditDraftCommand command,
        AssetDetailsDto targetDetails,
        AssetPreviewDto preview,
        string draftId,
        string draftMode,
        string contentType,
        DateTimeOffset updatedAtUtc,
        string sourceFingerprint,
        CancellationToken cancellationToken)
    {
        if (!ShouldMaterializeOnCreate(preview) &&
            preview.FullContentReference is { } sourceReference)
        {
            return Result<DraftReferenceCreationResult>.Success(new DraftReferenceCreationResult(
                sourceReference,
                IsMaterialized: false));
        }

        var textContentResult = await ResolveDraftTextContentAsync(
            command.WorkspaceDataRootPath,
            preview,
            cancellationToken);
        if (textContentResult.IsCancelled)
        {
            return Result<DraftReferenceCreationResult>.Cancelled();
        }

        var textContent = textContentResult.Value;
        if (textContent is null)
        {
            return Result<DraftReferenceCreationResult>.Failure(
                textContentResult.Errors.Count > 0
                    ? textContentResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(command.AssetId.Value)]);
        }

        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        draftTextStore.SaveTextPayload(new AssetEditDraftTextPayloadDto(
            command.WorkspaceKey.Trim(),
            draftId,
            draftMode,
            contentType,
            textContent,
            updatedAtUtc,
            SourceFingerprint: sourceFingerprint,
            SourceDescription: targetDetails.Name,
            Summary: targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString()));

        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            contentType,
            System.Text.Encoding.UTF8.GetByteCount(textContent),
            command.WorkspaceKey.Trim(),
            DraftId: draftId,
            DraftMode: draftMode);
        return Result<DraftReferenceCreationResult>.Success(new DraftReferenceCreationResult(
            reference,
            IsMaterialized: true));
    }

    private static AppError? ValidateCommand(CreateTextAssetEditDraftCommand command)
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

        return string.IsNullOrWhiteSpace(command.AssetId.Value)
            ? ApplicationErrors.AssetIdRequired()
            : null;
    }

    private static bool CanCreateTextDraft(AssetDetailsDto details) =>
        details.AssetType == AssetType.TextAsset ||
        string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldMaterializeOnCreate(AssetPreviewDto preview) =>
        preview.FullContentReference is null ||
        preview.FullContentReference.ContentLength <= AssetTextEditPolicy.AutoMaterializeTextLength;

    private async Task<Result<string>> ResolveDraftTextContentAsync(
        string workspaceDataRootPath,
        AssetPreviewDto preview,
        CancellationToken cancellationToken)
    {
        if (preview.FullContentReference is not null)
        {
            return await LoadFullContentFromReferenceAsync(
                workspaceDataRootPath,
                preview.FullContentReference,
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(preview.FullTextContent))
        {
            return Result<string>.Success(preview.FullTextContent);
        }

        if (!string.IsNullOrEmpty(preview.TextContent))
        {
            return Result<string>.Success(preview.TextContent);
        }

        return Result<string>.Failure(ApplicationErrors.AssetPreviewFullContentUnavailable(preview.AssetId.Value));
    }

    private async Task<Result<string>> LoadFullContentFromReferenceAsync(
        string workspaceDataRootPath,
        AssetPreviewFullContentReferenceDto reference,
        CancellationToken cancellationToken)
    {
        var builder = new System.Text.StringBuilder();
        var offset = 0;
        while (true)
        {
            var chunkResult = await _loadPreviewTextChunkHandler.HandleAsync(
                new LoadPreviewTextChunkQuery(
                    workspaceDataRootPath,
                    reference,
                    offset,
                    LoadPreviewTextChunkHandler.MaxChunkLength),
                cancellationToken);
            if (chunkResult.IsCancelled)
            {
                return Result<string>.Cancelled();
            }

            if (!chunkResult.IsSuccess || chunkResult.Value is null)
            {
                return Result<string>.Failure(chunkResult.Errors);
            }

            var chunk = chunkResult.Value;
            builder.Append(chunk.Text);
            offset = chunk.Offset + chunk.Length;
            if (!chunk.HasMore || chunk.Length == 0)
            {
                return Result<string>.Success(builder.ToString());
            }
        }
    }

    private static string CreatePreviewFingerprint(AssetPreviewDto preview) =>
        string.Concat(
            preview.AssetId.Value,
            ":",
            preview.GeneratedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            ":",
            (preview.FullContentReference?.ContentLength ?? preview.TextContent?.Length ?? 0).ToString(CultureInfo.InvariantCulture));

    private static string CreateDraftId()
    {
        var ticks = DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return $"draft_text_{ticks}_{suffix}";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DraftReferenceCreationResult(
        AssetPreviewFullContentReferenceDto Reference,
        bool IsMaterialized);
}
