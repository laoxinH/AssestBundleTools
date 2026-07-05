using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 解析修改阶段的当前有效替换，并把替换来源投影为目标资产预览。
/// </summary>
internal sealed class ReplacementEffectivePreviewResolver
{
    private const string TextContentType = "text/plain; charset=utf-8";

    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IReplacementPreviewSourceProvider? _replacementPreviewSourceProvider;

    public ReplacementEffectivePreviewResolver(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IReplacementPreviewSourceProvider? replacementPreviewSourceProvider)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider;
    }

    public async Task<Result<AssetPreviewDto>?> TryLoadAsync(
        GetAssetPreviewQuery query,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        HashSet<string> replacementPreviewStack,
        Func<GetAssetPreviewQuery, HashSet<string>, CancellationToken, Task<Result<AssetPreviewDto>>> loadPreviewAsync,
        CancellationToken cancellationToken)
    {
        if (query.PreviewSourceOverride is { } previewSourceOverride &&
            previewSourceOverride.BundleId.Equals(query.BundleId) &&
            previewSourceOverride.AssetId.Equals(query.AssetId))
        {
            var overrideDetails = CreateTargetDetails(query, assetCacheEntry);
            return await TryLoadFileReplacementPreviewAsync(
                query,
                overrideDetails,
                previewSourceOverride.SourceFilePath,
                cancellationToken);
        }

        var assetType = ParseAssetType(assetCacheEntry.AssetType);
        if (!IsFileReplacementPreviewSupported(assetType) ||
            !_workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                query.BundleId,
                query.AssetId,
                out var details) ||
            details is null)
        {
            return null;
        }

        var targetKey = CreateTargetKey(query.BundleId.Value, query.AssetId.Value);
        if (!replacementPreviewStack.Add(targetKey))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(details.FileReplacementSourcePath))
            {
                return await TryLoadFileReplacementPreviewAsync(
                    query,
                    details,
                    details.FileReplacementSourcePath,
                    cancellationToken);
            }

            if (details.WorkspaceReplacementSourceBundleId is { } sourceBundleId &&
                details.WorkspaceReplacementSourceAssetId is { } sourceAssetId)
            {
                return await TryLoadWorkspaceReplacementPreviewAsync(
                    query,
                    sourceBundleId,
                    sourceAssetId,
                    replacementPreviewStack,
                    loadPreviewAsync,
                    cancellationToken);
            }

            if (assetType == AssetType.TextAsset &&
                !string.IsNullOrWhiteSpace(details.TextEditDraftId))
            {
                return TryLoadTextEditDraftPreview(
                    query,
                    details,
                    details.TextEditDraftId,
                    details.TextEditDraftMode,
                    details.TextEditDraftRevision);
            }
        }
        finally
        {
            replacementPreviewStack.Remove(targetKey);
        }

        return null;
    }

    private async Task<Result<AssetPreviewDto>?> TryLoadFileReplacementPreviewAsync(
        GetAssetPreviewQuery query,
        AssetDetailsDto details,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (_replacementPreviewSourceProvider is null)
        {
            return null;
        }

        var previewContentResult = await _replacementPreviewSourceProvider.LoadFilePreviewAsync(
            details,
            sourceFilePath,
            cancellationToken);
        if (previewContentResult.IsCancelled)
        {
            return Result<AssetPreviewDto>.Cancelled();
        }

        if (!previewContentResult.IsSuccess || previewContentResult.Value is null)
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.GenerationFailed,
                new AssetPreviewDiagnosticDto(
                    AssetPreviewCacheStatusKind.Missing,
                    AssetPreviewFallbackReasonKind.GenerationFailed,
                    AppErrorLocalizedMessageMapper.CreateFromFirstError(previewContentResult.Errors))));
        }

        return Result<AssetPreviewDto>.Success(MapReplacementPreviewDto(
            query,
            previewContentResult.Value,
            sourceFilePath,
            isFromCache: false));
    }

    private async Task<Result<AssetPreviewDto>?> TryLoadWorkspaceReplacementPreviewAsync(
        GetAssetPreviewQuery targetQuery,
        BundleId sourceBundleId,
        AssetId sourceAssetId,
        HashSet<string> replacementPreviewStack,
        Func<GetAssetPreviewQuery, HashSet<string>, CancellationToken, Task<Result<AssetPreviewDto>>> loadPreviewAsync,
        CancellationToken cancellationToken)
    {
        var sourceQuery = targetQuery with
        {
            BundleId = sourceBundleId,
            AssetId = sourceAssetId
        };
        var sourcePreviewResult = await loadPreviewAsync(
            sourceQuery,
            replacementPreviewStack,
            cancellationToken);
        if (!sourcePreviewResult.IsSuccess || sourcePreviewResult.Value is null)
        {
            return sourcePreviewResult;
        }

        return Result<AssetPreviewDto>.Success(sourcePreviewResult.Value with
        {
            WorkspaceId = targetQuery.WorkspaceId,
            BundleId = targetQuery.BundleId,
            AssetId = targetQuery.AssetId
        });
    }

    private Result<AssetPreviewDto>? TryLoadTextEditDraftPreview(
        GetAssetPreviewQuery query,
        AssetDetailsDto details,
        string draftId,
        string? draftMode,
        int? draftRevision)
    {
        var normalizedDraftId = draftId.Trim();
        var normalizedDraftMode = string.IsNullOrWhiteSpace(draftMode)
            ? WorkspaceTextEditOperationRecorder.TextDraftMode
            : draftMode.Trim();
        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(query.WorkspaceDataRootPath);
        var chunk = draftTextStore.LoadTextChunk(
            query.WorkspaceKey,
            normalizedDraftId,
            normalizedDraftMode,
            offset: 0,
            length: AssetPreviewTextDisplayPolicy.MaxDisplayCharacters,
            revision: draftRevision);
        if (chunk is null)
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.GenerationFailed,
                new AssetPreviewDiagnosticDto(
                    AssetPreviewCacheStatusKind.Missing,
                    AssetPreviewFallbackReasonKind.GenerationFailed,
                    AppErrorLocalizedMessageMapper.CreateFromFirstError(
                    [
                        ApplicationErrors.AssetPreviewFullContentUnavailable(normalizedDraftId)
                    ]))));
        }

        var fullCharacterCount = ResolveDraftTextFullCharacterCount(chunk);
        var fullDraftText = LoadStructuredDraftText(
            draftTextStore,
            query.WorkspaceKey,
            normalizedDraftId,
            normalizedDraftMode,
            draftRevision,
            chunk.Text,
            fullCharacterCount);
        var isTruncatedByChunk = chunk.Offset + chunk.Length < fullCharacterCount;
        var textDisplay = AssetPreviewTextDisplayPolicy.CreateDisplay(chunk.Text);
        var metadata = textDisplay.Metadata with
        {
            DisplayCharacterCount = textDisplay.TextContent.Length,
            FullCharacterCount = fullCharacterCount,
            IsTruncated = isTruncatedByChunk || textDisplay.Metadata.IsTruncated
        };
        var structuredDraftText = fullDraftText.Length >= fullCharacterCount
            ? fullDraftText
            : null;

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Text,
            ContentType: chunk.Descriptor.ContentType,
            BinaryContent: null,
            TextContent: textDisplay.TextContent,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                chunk.Descriptor.PreviewInfoItems,
                metadata),
            PreviewMessage: chunk.Descriptor.PreviewMessage,
            PreferredDisplaySizeInBytes: details.PreferredDisplaySizeInBytes,
            IsFromCache: false,
            GeneratedAtUtc: details.TextEditAppliedAtUtc ?? DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            ImageOverlay: null,
            ImageFocus: null)
        {
            TextDisplay = metadata,
            FullContentReference = new AssetPreviewFullContentReferenceDto(
                AssetPreviewFullContentReferenceKind.EditDraft,
                chunk.Descriptor.ContentType,
                chunk.ContentLength,
                query.WorkspaceKey,
                DraftId: normalizedDraftId,
                DraftMode: normalizedDraftMode,
                DraftRevision: draftRevision),
            FullTextContent = structuredDraftText
        });
    }

    private static string LoadStructuredDraftText(
        IAssetEditDraftTextStore draftTextStore,
        string workspaceKey,
        string draftId,
        string draftMode,
        int? draftRevision,
        string firstChunkText,
        int fullCharacterCount)
    {
        if (fullCharacterCount <= firstChunkText.Length ||
            fullCharacterCount > AssetTextEditPolicy.InlineEditableTextLength)
        {
            return firstChunkText;
        }

        var builder = new StringBuilder(firstChunkText);
        var offset = firstChunkText.Length;
        while (offset < fullCharacterCount)
        {
            var chunk = draftTextStore.LoadTextChunk(
                workspaceKey,
                draftId,
                draftMode,
                offset,
                Math.Min(LoadPreviewTextChunkHandler.MaxChunkLength, fullCharacterCount - offset),
                revision: draftRevision);
            if (chunk is null || chunk.Length == 0)
            {
                break;
            }

            builder.Append(chunk.Text);
            offset = chunk.Offset + chunk.Length;
        }

        return builder.ToString();
    }

    private static AssetPreviewDto MapReplacementPreviewDto(
        GetAssetPreviewQuery query,
        AssetPreviewContentDto previewContent,
        string? previewPayloadFilePath,
        bool isFromCache)
    {
        var usesBinaryPayload = previewContent.Kind is
            AssetPreviewKind.Image or
            AssetPreviewKind.Audio or
            AssetPreviewKind.Binary;
        var fullTextContent = usesBinaryPayload
            ? null
            : Encoding.UTF8.GetString(previewContent.Content);
        var textDisplay = fullTextContent is null
            ? null
            : AssetPreviewTextDisplayPolicy.CreateDisplay(fullTextContent);

        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: previewContent.Kind,
            ContentType: previewContent.ContentType,
            BinaryContent: usesBinaryPayload ? previewContent.Content : null,
            TextContent: textDisplay?.TextContent,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems: textDisplay is null
                ? previewContent.PreviewInfoItems
                : AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                    previewContent.PreviewInfoItems,
                    textDisplay.Metadata),
            PreviewMessage: previewContent.PreviewMessage,
            PreferredDisplaySizeInBytes: previewContent.PreferredDisplaySizeInBytes,
            IsFromCache: isFromCache,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            ImageOverlay: previewContent.ImageOverlay,
            ImageFocus: previewContent.ImageFocus)
        {
            TextDisplay = textDisplay?.Metadata,
            FullContentReference = textDisplay is null || string.IsNullOrWhiteSpace(previewPayloadFilePath)
                ? null
                : CreateFileReplacementReference(
                    previewContent,
                    previewPayloadFilePath),
            FullTextContent = fullTextContent
        };
    }

    private static AssetPreviewFullContentReferenceDto CreateFileReplacementReference(
        AssetPreviewContentDto previewContent,
        string sourcePath) =>
        new(
            AssetPreviewFullContentReferenceKind.FileReplacement,
            previewContent.ContentType,
            previewContent.Content.LongLength,
            WorkspaceKey: string.Empty,
            SourcePath: sourcePath);

    private static AssetPreviewDto BuildFallbackSummaryPreview(
        GetAssetPreviewQuery query,
        AssetPreviewMessageKey messageKey,
        AssetPreviewDiagnosticDto diagnostic)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: new AssetPreviewMessageDto(messageKey),
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: diagnostic,
            ImageOverlay: null,
            ImageFocus: null);
    }

    private static AssetType ParseAssetType(string assetType)
    {
        return Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType)
            ? parsedAssetType
            : AssetType.Unknown;
    }

    private static string CreateTargetKey(string bundleId, string assetId) =>
        $"{bundleId}\u001f{assetId}";

    private static bool IsFileReplacementPreviewSupported(AssetType assetType) =>
        assetType is AssetType.Texture2D or AssetType.TextAsset or AssetType.AudioClip;

    private static int ResolveDraftTextFullCharacterCount(PreviewCacheTextChunkDto chunk)
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

    private static AssetDetailsDto CreateTargetDetails(
        GetAssetPreviewQuery query,
        BundleIndexAssetCacheEntryDto assetCacheEntry)
    {
        var assetType = ParseAssetType(assetCacheEntry.AssetType);
        return new AssetDetailsDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Name: assetCacheEntry.AssetName,
            AssetPath: assetCacheEntry.AssetPath,
            ContainerPath: assetCacheEntry.ContainerPath ?? string.Empty,
            AssetType: assetType,
            SizeInBytes: assetCacheEntry.SizeInBytes,
            PreferredDisplaySizeInBytes: assetCacheEntry.PreferredDisplaySizeInBytes,
            PathId: assetCacheEntry.PathId,
            IsModified: false,
            CanExtract: assetCacheEntry.CanExtract,
            CanReplaceFromFile: assetCacheEntry.CanReplace,
            CanReplaceFromWorkspace: assetCacheEntry.CanReplace,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetCacheEntry.AssetTypeDisplayName,
            SerializedFileName: assetCacheEntry.SerializedFileName);
    }
}
