using System.Collections.Concurrent;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 将文本编辑草稿载荷写入工作区 staging/drafts，并通过 Application 查询按需读取。
/// </summary>
public sealed class JsonAssetEditDraftTextStore : IAssetEditDraftTextStore
{
    private const string DescriptorFileName = "text-draft.json";
    private const string PayloadFileName = "text.txt";
    private const string BaseSnapshotFileName = "text-base.txt";
    private const string PatchManifestFileName = "text-patches.json";
    private const string GeneratorVersion = "edit-draft-text-v2";
    private const int PatchManifestSchemaVersion = 1;
    private static readonly ConcurrentDictionary<string, object> DraftWriteLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonWorkspaceStoragePaths _storagePaths;
    private readonly JsonAssetEditDraftTextSourceReader _sourceReader;
    private readonly JsonAssetEditDraftTextBaseSnapshotWriter _baseSnapshotWriter;

    public JsonAssetEditDraftTextStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
        _sourceReader = new JsonAssetEditDraftTextSourceReader(
            _storagePaths,
            (workspaceKey, draftId, draftMode, revision) => TryResolvePayload(
                workspaceKey,
                draftId,
                draftMode,
                revision: revision));
        _baseSnapshotWriter = new JsonAssetEditDraftTextBaseSnapshotWriter(_sourceReader);
    }

    public PreviewCacheTextChunkDto? LoadTextChunk(
        string workspaceKey,
        string draftId,
        string draftMode,
        int offset,
        int length,
        int? revision = null)
    {
        if (offset < 0 || length <= 0)
        {
            return null;
        }

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode, revision: revision);
        if (payload is null)
        {
            return null;
        }

        return payload.Value.CompositeReader is not null
            ? payload.Value.CompositeReader.LoadTextChunk(payload.Value.Descriptor, offset, length)
            : JsonPreviewCacheTextPayloadReader.TryReadChunk(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath!,
                payload.Value.PayloadLength,
                offset,
                length,
                LoadIndex(payload.Value));
    }

    public PreviewCacheTextLineRangeDto? LoadTextLines(
        string workspaceKey,
        string draftId,
        string draftMode,
        int startLineIndex,
        int lineCount,
        int? revision = null)
    {
        if (startLineIndex < 0 || lineCount <= 0)
        {
            return null;
        }

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode, revision: revision);
        if (payload is null)
        {
            return null;
        }

        return payload.Value.CompositeReader is not null
            ? payload.Value.CompositeReader.LoadTextLines(payload.Value.Descriptor, startLineIndex, lineCount)
            : JsonPreviewCacheTextPayloadReader.TryReadLines(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath!,
                payload.Value.PayloadLength,
                startLineIndex,
                lineCount,
                LoadIndex(payload.Value));
    }

    public PreviewCacheTextSearchResultDto? SearchText(
        string workspaceKey,
        string draftId,
        string draftMode,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase,
        int? revision = null)
    {
        if (string.IsNullOrEmpty(searchText) ||
            startLineIndex < 0 ||
            maxMatchCount <= 0)
        {
            return null;
        }

        var payload = TryResolvePayload(workspaceKey, draftId, draftMode, revision: revision);
        if (payload is null)
        {
            return null;
        }

        return payload.Value.CompositeReader is not null
            ? payload.Value.CompositeReader.SearchText(
                payload.Value.Descriptor,
                searchText,
                startLineIndex,
                maxMatchCount,
                matchCase)
            : JsonPreviewCacheTextPayloadReader.TrySearch(
                payload.Value.Descriptor,
                payload.Value.PayloadFilePath!,
                payload.Value.PayloadLength,
                searchText,
                startLineIndex,
                maxMatchCount,
                matchCase,
                LoadIndex(payload.Value));
    }

    public void SaveTextPayload(AssetEditDraftTextPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.DraftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.DraftMode);

        var descriptor = CreateDescriptor(payload);
        var draftDirectoryPath = GetDraftDirectoryPath(
            payload.WorkspaceKey,
            payload.DraftId,
            payload.DraftMode);
        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);
        var content = Encoding.UTF8.GetBytes(NormalizeTextContent(payload.TextContent ?? string.Empty));

        lock (GetDraftWriteLock(draftDirectoryPath))
        {
            JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
                descriptorFilePath,
                descriptor,
                JsonWorkspaceSerialization.SerializerOptions);
            JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, content);
            JsonPreviewCacheTextLineIndex.Save(descriptor, payloadFilePath, content);
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(Path.Combine(draftDirectoryPath, PatchManifestFileName));
        }
    }

    public AssetEditDraftTextPatchResultDto ApplyTextPatch(AssetEditDraftTextPatchDto patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.DraftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch.DraftMode);

        var draftDirectoryPath = GetDraftDirectoryPath(
            patch.WorkspaceKey,
            patch.DraftId,
            patch.DraftMode);
        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var patchManifestFilePath = Path.Combine(draftDirectoryPath, PatchManifestFileName);

        lock (GetDraftWriteLock(draftDirectoryPath))
        {
            var payload = TryResolvePayload(
                patch.WorkspaceKey,
                patch.DraftId,
                patch.DraftMode,
                patch.SourceReference,
                patch.BaseRevision);
            if (payload is null ||
                !IsMatchingPayloadDescriptor(payload.Value.Descriptor, patch))
            {
                return AssetEditDraftTextPatchResultDto.NotFound();
            }

            var composite = payload.Value.CompositeReader ?? CreatePayloadCompositeReader(payload.Value);
            var currentBaseline = composite.CreateBaseline();
            if (currentBaseline is null ||
                currentBaseline.ContentLength != patch.Baseline.ContentLength ||
                !string.Equals(currentBaseline.ContentSha256, patch.Baseline.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                return AssetEditDraftTextPatchResultDto.BaselineMismatch();
            }

            var statistics = composite.CalculatePatchedStatistics(
                patch.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>(),
                patch.MaxTextLength,
                out var failure);
            if (statistics is null)
            {
                return failure ?? AssetEditDraftTextPatchResultDto.NotFound();
            }

            var patchedDescriptor = CreateDescriptor(
                patch.WorkspaceKey,
                patch.DraftId,
                patch.DraftMode,
                patch.ContentType,
                statistics.TextLength,
                statistics.LineCount,
                statistics.ContentLength,
                patch.UpdatedAtUtc,
                patch.SourceFingerprint,
                patch.SourceDescription,
                patch.Summary);
            var updatedManifest = CreateUpdatedPatchManifest(
                payload.Value,
                patch,
                statistics);
            JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
                descriptorFilePath,
                patchedDescriptor,
                JsonWorkspaceSerialization.SerializerOptions);
            JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
                patchManifestFilePath,
                updatedManifest,
                JsonWorkspaceSerialization.SerializerOptions);
            return AssetEditDraftTextPatchResultDto.Applied(
                statistics.TextLength,
                statistics.ContentLength,
                updatedManifest.Patches?.Count ?? 0,
                new TextAssetEditDraftPatchBaselineDto(
                    statistics.TextLength,
                    statistics.ContentSha256));
        }
    }

    public void Delete(string workspaceKey, string draftId, string draftMode)
    {
        var draftDirectoryPath = TryGetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        if (string.IsNullOrWhiteSpace(draftDirectoryPath))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(draftDirectoryPath);
    }

    private JsonPreviewCacheTextLineIndexDto? LoadIndex(JsonAssetEditDraftTextPayloadReference payload) =>
        string.IsNullOrWhiteSpace(payload.PayloadFilePath)
            ? null
            : JsonPreviewCacheTextLineIndex.Load(
                payload.Descriptor,
                payload.PayloadFilePath,
                payload.PayloadLength);

    private JsonAssetEditDraftTextPayloadReference? TryResolvePayload(
        string workspaceKey,
        string draftId,
        string draftMode,
        WorkspaceAssetEditDraftSourceReferenceDto? sourceReference = null,
        int? revision = null)
    {
        var draftDirectoryPath = TryGetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        if (string.IsNullOrWhiteSpace(draftDirectoryPath))
        {
            return null;
        }

        var descriptorFilePath = Path.Combine(draftDirectoryPath, DescriptorFileName);
        var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);
        var baseSnapshotFilePath = Path.Combine(draftDirectoryPath, BaseSnapshotFileName);
        var patchManifestFilePath = Path.Combine(draftDirectoryPath, PatchManifestFileName);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        var manifest = JsonWorkspaceFileStorageHelper.TryReadJson<JsonAssetEditDraftTextPatchManifestDto>(
            patchManifestFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (manifest is not null &&
            IsValidPatchManifest(manifest, workspaceKey, draftId, draftMode))
        {
            manifest = ApplyRevision(manifest, revision);
            var baseReaderFactory = CreateBaseReaderFactory(draftDirectoryPath, manifest);
            if (baseReaderFactory is null)
            {
                return null;
            }

            return new JsonAssetEditDraftTextPayloadReference(
                CreateDescriptorFromManifest(manifest),
                ResolveManifestBaseFilePath(draftDirectoryPath, manifest),
                manifest.ContentLength,
                manifest,
                new JsonAssetEditDraftTextCompositeReader(baseReaderFactory, manifest));
        }

        if (descriptor is not null &&
            IsMatchingDescriptor(descriptor, workspaceKey, draftId, draftMode) &&
            File.Exists(payloadFilePath))
        {
            return new JsonAssetEditDraftTextPayloadReference(
                descriptor,
                payloadFilePath,
                GetFileSize(payloadFilePath),
                null,
                null);
        }

        if (sourceReference is null)
        {
            return null;
        }

        var baseSnapshot = _baseSnapshotWriter.TryCreate(baseSnapshotFilePath, sourceReference);
        if (baseSnapshot is null)
        {
            return null;
        }

        var baseManifest = CreateBaseSnapshotManifest(
            workspaceKey,
            draftId,
            draftMode,
            sourceReference,
            baseSnapshot);

        return new JsonAssetEditDraftTextPayloadReference(
            CreateDescriptorFromManifest(baseManifest),
            baseSnapshotFilePath,
            baseManifest.ContentLength,
            baseManifest,
            new JsonAssetEditDraftTextCompositeReader(
                () => JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(baseSnapshotFilePath),
                baseManifest));
    }

    private PreviewCacheDescriptorDto CreateDescriptor(AssetEditDraftTextPayloadDto payload) =>
        CreateDescriptor(
            payload.WorkspaceKey,
            payload.DraftId,
            payload.DraftMode,
            payload.ContentType,
            NormalizeTextContent(payload.TextContent ?? string.Empty),
            payload.UpdatedAtUtc,
            payload.SourceFingerprint,
            payload.SourceDescription,
            payload.Summary);

    private PreviewCacheDescriptorDto CreateDescriptor(
        string workspaceKey,
        string draftId,
        string draftMode,
        string contentType,
        string normalizedText,
        DateTimeOffset updatedAtUtc,
        string? sourceFingerprint,
        string? sourceDescription,
        string? summary)
    {
        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "text/plain; charset=utf-8"
            : contentType.Trim();
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);

        return new PreviewCacheDescriptorDto(
            WorkspaceKey: workspaceKey.Trim(),
            PreviewCacheKey: CreatePreviewCacheKey(draftId, draftMode),
            BundleCacheKey: $"draft:{draftId.Trim()}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: normalizedContentType,
            RelativePayloadPath: PayloadFileName,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: $"draft:{draftId.Trim()}:{draftMode.Trim()}",
                FileSizeInBytes: Encoding.UTF8.GetByteCount(normalizedText),
                LastWriteTimeUtc: updatedAtUtc,
                ContentHash: sourceFingerprint),
            GeneratedAtUtc: updatedAtUtc,
            GeneratorVersion: GeneratorVersion,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata),
            PreferredDisplaySizeInBytes: Encoding.UTF8.GetByteCount(display.TextContent));
    }

    private PreviewCacheDescriptorDto CreateDescriptor(
        string workspaceKey,
        string draftId,
        string draftMode,
        string contentType,
        int textLength,
        int lineCount,
        long contentLength,
        DateTimeOffset updatedAtUtc,
        string? sourceFingerprint,
        string? sourceDescription,
        string? summary)
    {
        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "text/plain; charset=utf-8"
            : contentType.Trim();
        var metadata = new AssetPreviewTextDisplayDto(
            DisplayCharacterCount: Math.Min(textLength, AssetPreviewTextDisplayPolicy.MaxDisplayCharacters),
            FullCharacterCount: textLength,
            DisplayLineCount: Math.Min(lineCount, AssetPreviewTextDisplayPolicy.MaxDisplayLineCount),
            EstimatedFullLineCount: lineCount,
            IsTruncated: textLength > AssetPreviewTextDisplayPolicy.MaxDisplayCharacters ||
                lineCount > AssetPreviewTextDisplayPolicy.MaxDisplayLineCount,
            MaxDisplayCharacters: AssetPreviewTextDisplayPolicy.MaxDisplayCharacters,
            MaxDisplayLineCount: AssetPreviewTextDisplayPolicy.MaxDisplayLineCount);

        return new PreviewCacheDescriptorDto(
            WorkspaceKey: workspaceKey.Trim(),
            PreviewCacheKey: CreatePreviewCacheKey(draftId, draftMode),
            BundleCacheKey: $"draft:{draftId.Trim()}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: normalizedContentType,
            RelativePayloadPath: PayloadFileName,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: $"draft:{draftId.Trim()}:{draftMode.Trim()}",
                FileSizeInBytes: contentLength,
                LastWriteTimeUtc: updatedAtUtc,
                ContentHash: sourceFingerprint),
            GeneratedAtUtc: updatedAtUtc,
            GeneratorVersion: GeneratorVersion,
            PreviewInfoItems: AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                metadata),
            PreferredDisplaySizeInBytes: Math.Min(contentLength, AssetPreviewTextDisplayPolicy.MaxDisplayCharacters));
    }

    private static bool IsMatchingPayloadDescriptor(
        PreviewCacheDescriptorDto descriptor,
        AssetEditDraftTextPatchDto patch) =>
        string.Equals(descriptor.WorkspaceKey, patch.WorkspaceKey.Trim(), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewCacheKey, CreatePreviewCacheKey(patch.DraftId, patch.DraftMode), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsMatchingDescriptor(
        PreviewCacheDescriptorDto descriptor,
        string workspaceKey,
        string draftId,
        string draftMode) =>
        string.Equals(descriptor.WorkspaceKey, workspaceKey.Trim(), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewCacheKey, CreatePreviewCacheKey(draftId, draftMode), StringComparison.Ordinal) &&
        string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase);

    private JsonAssetEditDraftTextPatchManifestDto CreateUpdatedPatchManifest(
        JsonAssetEditDraftTextPayloadReference payload,
        AssetEditDraftTextPatchDto patch,
        TextAssetEditDraftPatchStatisticsDto statistics)
    {
        var baseManifest = payload.PatchManifest ?? CreatePayloadBaseManifest(payload, patch);
        var patches = (baseManifest.Patches ?? Array.Empty<JsonAssetEditDraftTextPatchEntryDto>())
            .Append(new JsonAssetEditDraftTextPatchEntryDto(
                Sequence: (baseManifest.Patches?.Count ?? 0) + 1,
                RangeEdits: (patch.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>())
                    .Select(static edit => new TextAssetEditDraftRangeEditDto(
                        edit.Start,
                        edit.Length,
                        NormalizeTextContent(edit.Text)))
                    .ToArray(),
                Baseline: patch.Baseline,
                AppliedAtUtc: patch.UpdatedAtUtc))
            .ToArray();

        return baseManifest with
        {
            ContentType = string.IsNullOrWhiteSpace(patch.ContentType)
                ? baseManifest.ContentType
                : patch.ContentType.Trim(),
            Patches = patches,
            TextLength = statistics.TextLength,
            ContentLength = statistics.ContentLength,
            LineCount = statistics.LineCount,
            ContentSha256 = statistics.ContentSha256,
            UpdatedAtUtc = patch.UpdatedAtUtc,
            SourceFingerprint = patch.SourceFingerprint ?? baseManifest.SourceFingerprint,
            SourceDescription = patch.SourceDescription ?? baseManifest.SourceDescription,
            Summary = patch.Summary ?? baseManifest.Summary
        };
    }

    private JsonAssetEditDraftTextPatchManifestDto ApplyRevision(
        JsonAssetEditDraftTextPatchManifestDto manifest,
        int? revision)
    {
        if (revision is null)
        {
            return manifest;
        }

        var patchCount = manifest.Patches?.Count ?? 0;
        var normalizedRevision = Math.Clamp(revision.Value, 0, patchCount);
        if (normalizedRevision == patchCount)
        {
            return manifest;
        }

        var revisedManifest = manifest with
        {
            Patches = (manifest.Patches ?? Array.Empty<JsonAssetEditDraftTextPatchEntryDto>())
                .Take(normalizedRevision)
                .ToArray()
        };
        var baseReaderFactory = CreateBaseReaderFactory(
            GetDraftDirectoryPath(manifest.WorkspaceKey, manifest.DraftId, manifest.DraftMode),
            revisedManifest);
        if (baseReaderFactory is null)
        {
            return revisedManifest;
        }

        using var reader = new JsonAssetEditDraftTextCompositeReader(baseReaderFactory, revisedManifest).CreateReader();
        if (reader is null)
        {
            return revisedManifest;
        }

        var statistics = JsonAssetEditDraftTextReadSupport.CalculateStatistics(
            reader,
            int.MaxValue,
            out _);
        return statistics is null
            ? revisedManifest
            : revisedManifest with
            {
                TextLength = statistics.TextLength,
                ContentLength = statistics.ContentLength,
                LineCount = statistics.LineCount,
                ContentSha256 = statistics.ContentSha256,
                UpdatedAtUtc = revisedManifest.Patches.LastOrDefault()?.AppliedAtUtc ?? manifest.UpdatedAtUtc
            };
    }

    private JsonAssetEditDraftTextPatchManifestDto CreatePayloadBaseManifest(
        JsonAssetEditDraftTextPayloadReference payload,
        AssetEditDraftTextPatchDto patch)
    {
        var stats = JsonAssetEditDraftTextSourceReader.ResolvePayloadBaseStatistics(payload);
        return new JsonAssetEditDraftTextPatchManifestDto(
            SchemaVersion: PatchManifestSchemaVersion,
            WorkspaceKey: patch.WorkspaceKey.Trim(),
            DraftId: patch.DraftId.Trim(),
            DraftMode: patch.DraftMode.Trim(),
            ContentType: string.IsNullOrWhiteSpace(patch.ContentType)
                ? payload.Descriptor.ContentType
                : patch.ContentType.Trim(),
            UsesPayloadBase: true,
            BaseTextLength: stats.TextLength,
            BaseContentLength: stats.ContentLength,
            BaseLineCount: stats.LineCount,
            BaseContentSha256: stats.ContentSha256,
            SourceReference: patch.SourceReference,
            Patches: Array.Empty<JsonAssetEditDraftTextPatchEntryDto>(),
            TextLength: stats.TextLength,
            ContentLength: stats.ContentLength,
            LineCount: stats.LineCount,
            ContentSha256: stats.ContentSha256,
            UpdatedAtUtc: patch.UpdatedAtUtc,
            SourceFingerprint: patch.SourceFingerprint,
            SourceDescription: patch.SourceDescription,
            Summary: patch.Summary);
    }

    private JsonAssetEditDraftTextPatchManifestDto CreateBaseSnapshotManifest(
        string workspaceKey,
        string draftId,
        string draftMode,
        WorkspaceAssetEditDraftSourceReferenceDto sourceReference,
        TextAssetEditDraftPatchStatisticsDto baseSnapshot)
    {
        return new JsonAssetEditDraftTextPatchManifestDto(
            SchemaVersion: PatchManifestSchemaVersion,
            WorkspaceKey: workspaceKey.Trim(),
            DraftId: draftId.Trim(),
            DraftMode: draftMode.Trim(),
            ContentType: sourceReference.ContentType.Trim(),
            UsesPayloadBase: false,
            BaseTextLength: baseSnapshot.TextLength,
            BaseContentLength: baseSnapshot.ContentLength,
            BaseLineCount: baseSnapshot.LineCount,
            BaseContentSha256: baseSnapshot.ContentSha256,
            SourceReference: sourceReference,
            Patches: Array.Empty<JsonAssetEditDraftTextPatchEntryDto>(),
            TextLength: baseSnapshot.TextLength,
            ContentLength: baseSnapshot.ContentLength,
            LineCount: baseSnapshot.LineCount,
            ContentSha256: baseSnapshot.ContentSha256,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private Func<TextReader?>? CreateBaseReaderFactory(
        string draftDirectoryPath,
        JsonAssetEditDraftTextPatchManifestDto manifest)
    {
        if (manifest.UsesPayloadBase)
        {
            var payloadFilePath = Path.Combine(draftDirectoryPath, PayloadFileName);
            return File.Exists(payloadFilePath)
                ? () => JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(payloadFilePath)
                : null;
        }

        var baseSnapshotFilePath = Path.Combine(draftDirectoryPath, BaseSnapshotFileName);
        return File.Exists(baseSnapshotFilePath)
            ? () => JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(baseSnapshotFilePath)
            : null;
    }

    private static string? ResolveManifestBaseFilePath(
        string draftDirectoryPath,
        JsonAssetEditDraftTextPatchManifestDto manifest)
    {
        var baseFilePath = Path.Combine(
            draftDirectoryPath,
            manifest.UsesPayloadBase ? PayloadFileName : BaseSnapshotFileName);
        return File.Exists(baseFilePath) ? baseFilePath : null;
    }

    private JsonAssetEditDraftTextCompositeReader CreatePayloadCompositeReader(
        JsonAssetEditDraftTextPayloadReference payload)
    {
        var manifest = payload.PatchManifest ?? new JsonAssetEditDraftTextPatchManifestDto(
            SchemaVersion: PatchManifestSchemaVersion,
            WorkspaceKey: payload.Descriptor.WorkspaceKey,
            DraftId: string.Empty,
            DraftMode: string.Empty,
            ContentType: payload.Descriptor.ContentType,
            UsesPayloadBase: true,
            BaseTextLength: JsonAssetEditDraftTextSourceReader.ResolveInfoInt64(payload.Descriptor, AssetPreviewInfoKey.TextFullCharacterCount) is { } length
                ? (int)Math.Min(length, int.MaxValue)
                : 0,
            BaseContentLength: payload.PayloadLength,
            BaseLineCount: JsonAssetEditDraftTextSourceReader.ResolveInfoInt64(payload.Descriptor, AssetPreviewInfoKey.TextEstimatedFullLineCount) is { } lines
                ? (int)Math.Min(lines, int.MaxValue)
                : 0,
            BaseContentSha256: payload.Descriptor.SourceFingerprint.ContentHash ?? string.Empty,
            SourceReference: null,
            Patches: Array.Empty<JsonAssetEditDraftTextPatchEntryDto>(),
            TextLength: JsonAssetEditDraftTextSourceReader.ResolveInfoInt64(payload.Descriptor, AssetPreviewInfoKey.TextFullCharacterCount) is { } textLength
                ? (int)Math.Min(textLength, int.MaxValue)
                : 0,
            ContentLength: payload.PayloadLength,
            LineCount: JsonAssetEditDraftTextSourceReader.ResolveInfoInt64(payload.Descriptor, AssetPreviewInfoKey.TextEstimatedFullLineCount) is { } lineCount
                ? (int)Math.Min(lineCount, int.MaxValue)
                : 0,
            ContentSha256: payload.Descriptor.SourceFingerprint.ContentHash ?? string.Empty,
            UpdatedAtUtc: payload.Descriptor.GeneratedAtUtc);

        return new JsonAssetEditDraftTextCompositeReader(
            () => string.IsNullOrWhiteSpace(payload.PayloadFilePath)
                ? null
                : JsonAssetEditDraftTextSourceReader.CreateUtf8FileReader(payload.PayloadFilePath),
            manifest);
    }

    private PreviewCacheDescriptorDto CreateDescriptorFromManifest(
        JsonAssetEditDraftTextPatchManifestDto manifest) =>
        CreateDescriptor(
            manifest.WorkspaceKey,
            manifest.DraftId,
            manifest.DraftMode,
            manifest.ContentType,
            manifest.TextLength,
            manifest.LineCount,
            manifest.ContentLength,
            manifest.UpdatedAtUtc,
            manifest.SourceFingerprint,
            manifest.SourceDescription,
            manifest.Summary);

    private static bool IsValidPatchManifest(
        JsonAssetEditDraftTextPatchManifestDto manifest,
        string workspaceKey,
        string draftId,
        string draftMode) =>
        manifest.SchemaVersion == PatchManifestSchemaVersion &&
        string.Equals(manifest.WorkspaceKey, workspaceKey.Trim(), StringComparison.Ordinal) &&
        string.Equals(manifest.DraftId, draftId.Trim(), StringComparison.Ordinal) &&
        string.Equals(manifest.DraftMode, draftMode.Trim(), StringComparison.Ordinal) &&
        manifest.BaseTextLength >= 0 &&
        manifest.BaseContentLength >= 0 &&
        manifest.BaseLineCount >= 0 &&
        manifest.TextLength >= 0 &&
        manifest.ContentLength >= 0 &&
        manifest.LineCount >= 0;


    private string GetDraftDirectoryPath(
        string workspaceKey,
        string draftId,
        string draftMode)
    {
        return _storagePaths.GetAssetEditDraftDirectoryPath(
            workspaceKey.Trim(),
            draftId.Trim(),
            draftMode.Trim());
    }

    private string? TryGetDraftDirectoryPath(
        string workspaceKey,
        string draftId,
        string draftMode)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(draftId) ||
            string.IsNullOrWhiteSpace(draftMode))
        {
            return null;
        }

        try
        {
            return GetDraftDirectoryPath(workspaceKey, draftId, draftMode);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string CreatePreviewCacheKey(string draftId, string draftMode) =>
        string.Concat("draft.", draftMode.Trim(), ".", draftId.Trim());

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static object GetDraftWriteLock(string draftDirectoryPath) =>
        DraftWriteLocks.GetOrAdd(Path.GetFullPath(draftDirectoryPath), static _ => new object());

    private static long GetFileSize(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

}
