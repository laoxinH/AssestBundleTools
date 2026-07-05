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
/// 校验并恢复一个已经存在的 TextAsset / Shader 文本编辑草稿。
/// </summary>
public sealed class OpenTextAssetEditDraftHandler : ICommandHandler<OpenTextAssetEditDraftCommand, Result<AssetTextEditDraftDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public OpenTextAssetEditDraftHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public Task<Result<AssetTextEditDraftDto>> HandleAsync(
        OpenTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Cancelled());
        }

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(validationError));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (!CanOpenTextDraft(targetDetails))
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(AssetType.TextAsset, targetDetails.AssetType)));
        }

        var workspaceKey = command.WorkspaceKey.Trim();
        var draftId = command.DraftId.Trim();
        var draftMode = NormalizeDraftMode(command.DraftMode);
        var changeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(command.WorkspaceDataRootPath.Trim());
        var changeSet = changeStore.Load(workspaceKey);
        if (changeSet is null ||
            !string.Equals(changeSet.WorkspaceKey?.Trim(), workspaceKey, StringComparison.Ordinal))
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetEditDraftNotFound(workspaceKey, draftId, draftMode)));
        }

        var descriptor = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .FirstOrDefault(item =>
                string.Equals(item.DraftId?.Trim(), draftId, StringComparison.Ordinal) &&
                string.Equals(item.DraftMode?.Trim(), draftMode, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetEditDraftNotFound(workspaceKey, draftId, draftMode)));
        }

        if (!string.IsNullOrWhiteSpace(command.SourceFingerprint) &&
            !string.Equals(
                command.SourceFingerprint.Trim(),
                descriptor.SourceFingerprint?.Trim(),
                StringComparison.Ordinal))
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetEditDraftSourceMismatch(workspaceKey, draftId, draftMode)));
        }

        if (descriptor.ExpiresAtUtc is { } expiresAtUtc &&
            expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetEditDraftExpired(workspaceKey, draftId, draftMode)));
        }

        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(command.WorkspaceDataRootPath.Trim());
        var firstChunk = draftTextStore.LoadTextChunk(
            workspaceKey,
            draftId,
            draftMode,
            offset: 0,
            length: 1);
        if (firstChunk is null)
        {
            return Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(draftId)));
        }

        var contentLength = firstChunk.ContentLength;
        var fullCharacterCount = ResolveFullCharacterCount(firstChunk);
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            firstChunk.Descriptor.ContentType,
            contentLength,
            workspaceKey,
            DraftId: draftId,
            DraftMode: draftMode);

        var draft = new AssetTextEditDraftDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            targetDetails.Name,
            targetDetails.AssetType,
            targetDetails.AssetTypeDisplayName,
            draftId,
            draftMode,
            reference,
            descriptor.UpdatedAtUtc,
            CanApply: targetDetails.AssetType == AssetType.TextAsset,
            Summary: descriptor.Summary ?? targetDetails.AssetTypeDisplayName ?? targetDetails.AssetType.ToString(),
            CanEditInline: targetDetails.AssetType == AssetType.TextAsset &&
                fullCharacterCount <= AssetTextEditPolicy.InlineEditableTextLength,
            IsAppliedBaseline: IsCurrentAppliedDraft(targetDetails, draftId, draftMode),
            SourceFingerprint: descriptor.SourceFingerprint?.Trim(),
            SourceDescription: descriptor.SourceDescription?.Trim());

        return Task.FromResult(Result<AssetTextEditDraftDto>.Success(draft));
    }

    private static AppError? ValidateCommand(OpenTextAssetEditDraftCommand command)
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

        return string.IsNullOrWhiteSpace(command.DraftMode)
            ? ApplicationErrors.AssetEditDraftModeRequired()
            : null;
    }

    private static bool CanOpenTextDraft(AssetDetailsDto details) =>
        details.AssetType == AssetType.TextAsset ||
        string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase);

    private static int ResolveFullCharacterCount(PreviewCacheTextChunkDto chunk)
    {
        var minimumLength = chunk.Offset + chunk.Length;
        var metadataLength = chunk.Descriptor.PreviewInfoItems?
            .FirstOrDefault(static item => item.Key == AssetPreviewInfoKey.TextFullCharacterCount)
            ?.Int64Value;
        if (metadataLength is >= 0 and <= int.MaxValue)
        {
            return Math.Max((int)metadataLength.Value, minimumLength);
        }

        return Math.Max(chunk.TotalLength, minimumLength);
    }

    private static string NormalizeDraftMode(string draftMode) =>
        string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();

    private static bool IsCurrentAppliedDraft(
        AssetDetailsDto details,
        string draftId,
        string draftMode) =>
        !string.IsNullOrWhiteSpace(details.TextEditDraftId) &&
        string.Equals(details.TextEditDraftId.Trim(), draftId, StringComparison.Ordinal) &&
        string.Equals(
            string.IsNullOrWhiteSpace(details.TextEditDraftMode)
                ? WorkspaceTextEditOperationRecorder.TextDraftMode
                : details.TextEditDraftMode.Trim(),
            draftMode,
            StringComparison.Ordinal);
}
