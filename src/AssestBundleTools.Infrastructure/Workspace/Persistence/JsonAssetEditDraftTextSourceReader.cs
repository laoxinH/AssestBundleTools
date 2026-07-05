using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed class JsonAssetEditDraftTextSourceReader
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;
    private readonly Func<string, string, string, int?, JsonAssetEditDraftTextPayloadReference?> _draftResolver;

    public JsonAssetEditDraftTextSourceReader(
        JsonWorkspaceStoragePaths storagePaths,
        Func<string, string, string, int?, JsonAssetEditDraftTextPayloadReference?> draftResolver)
    {
        _storagePaths = storagePaths ?? throw new ArgumentNullException(nameof(storagePaths));
        _draftResolver = draftResolver ?? throw new ArgumentNullException(nameof(draftResolver));
    }

    public TextReader? CreateSourceReader(WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        if (!Enum.TryParse<AssetPreviewFullContentReferenceKind>(
                sourceReference.Kind,
                ignoreCase: true,
                out var kind))
        {
            return null;
        }

        return kind switch
        {
            AssetPreviewFullContentReferenceKind.PreviewCache => CreatePreviewCacheSourceReader(sourceReference),
            AssetPreviewFullContentReferenceKind.FileReplacement => CreateFileReplacementSourceReader(sourceReference),
            AssetPreviewFullContentReferenceKind.EditDraft => CreateEditDraftSourceReader(sourceReference),
            _ => null
        };
    }

    public TextAssetEditDraftPatchStatisticsDto? TryResolveSourceStatistics(
        WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        using var reader = CreateSourceReader(sourceReference);
        return reader is null ? null : CalculateReaderStatistics(reader, int.MaxValue);
    }

    public static TextAssetEditDraftPatchStatisticsDto ResolvePayloadBaseStatistics(
        JsonAssetEditDraftTextPayloadReference payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.PayloadFilePath) &&
            File.Exists(payload.PayloadFilePath))
        {
            using var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(payload.PayloadFilePath!);
            using var reader = CreateUtf8Reader(stream);
            return CalculateReaderStatistics(reader, int.MaxValue);
        }

        var lineCount = ResolveInfoInt64(payload.Descriptor, AssetPreviewInfoKey.TextEstimatedFullLineCount);
        var fingerprint = payload.Descriptor.SourceFingerprint;
        var fingerprintFileSize = fingerprint.FileSizeInBytes;
        var fallbackTextLength = fingerprintFileSize >= 0 && fingerprintFileSize <= int.MaxValue
            ? (int)fingerprintFileSize
            : 0;
        return new TextAssetEditDraftPatchStatisticsDto(
            fallbackTextLength,
            lineCount is >= 0 and <= int.MaxValue ? (int)lineCount.Value : 0,
            payload.PayloadLength,
            fingerprint.ContentHash ?? string.Empty);
    }

    public static StreamReader CreateUtf8FileReader(string filePath)
    {
        var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(filePath);
        return CreateUtf8Reader(stream);
    }

    public static long? ResolveInfoInt64(
        PreviewCacheDescriptorDto descriptor,
        AssetPreviewInfoKey key) =>
        descriptor.PreviewInfoItems?
            .FirstOrDefault(item => item.Key == key)
            ?.Int64Value;

    public static TextAssetEditDraftPatchStatisticsDto CalculateReaderStatistics(
        TextReader reader,
        int maxTextLength)
    {
        var length = 0;
        var byteLength = 0L;
        var lineBreaks = 0;
        var hasAnyCharacter = false;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new StringBuilder(4096);

        while (TryReadNormalizedCharacter(reader, out var character))
        {
            byteLength += AppendHashCharacter(hash, buffer, character);
            length++;
            hasAnyCharacter = true;
            if (character == '\n')
            {
                lineBreaks++;
            }

            if (length > maxTextLength)
            {
                break;
            }
        }

        byteLength += FlushHashBuffer(hash, buffer);
        return new TextAssetEditDraftPatchStatisticsDto(
            length,
            hasAnyCharacter ? lineBreaks + 1 : 0,
            byteLength,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private TextReader? CreatePreviewCacheSourceReader(WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference.PreviewCacheKey))
        {
            return null;
        }

        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(
            sourceReference.WorkspaceKey.Trim(),
            sourceReference.PreviewCacheKey.Trim());
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null ||
            !string.Equals(descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payloadFilePath = _storagePaths.GetPreviewPayloadFilePath(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath);
        return File.Exists(payloadFilePath) ? CreateUtf8FileReader(payloadFilePath) : null;
    }

    private static TextReader? CreateFileReplacementSourceReader(
        WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        return string.IsNullOrWhiteSpace(sourceReference.SourcePath) ||
            !File.Exists(sourceReference.SourcePath.Trim())
            ? null
            : CreateUtf8FileReader(sourceReference.SourcePath.Trim());
    }

    private TextReader? CreateEditDraftSourceReader(
        WorkspaceAssetEditDraftSourceReferenceDto sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference.DraftId) ||
            string.IsNullOrWhiteSpace(sourceReference.DraftMode))
        {
            return null;
        }

        var sourcePayload = _draftResolver(
            sourceReference.WorkspaceKey,
            sourceReference.DraftId,
            sourceReference.DraftMode,
            sourceReference.DraftRevision);
        return sourcePayload?.CompositeReader?.CreateReader() ??
            (!string.IsNullOrWhiteSpace(sourcePayload?.PayloadFilePath)
                ? CreateUtf8FileReader(sourcePayload.Value.PayloadFilePath!)
                : null);
    }

    private static StreamReader CreateUtf8Reader(Stream stream) =>
        new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: false);

    private static bool TryReadNormalizedCharacter(TextReader reader, out char character)
    {
        var value = reader.Read();
        if (value < 0)
        {
            character = '\0';
            return false;
        }

        character = (char)value;
        if (character != '\r')
        {
            return true;
        }

        if (reader.Peek() == '\n')
        {
            reader.Read();
        }

        character = '\n';
        return true;
    }

    private static int AppendHashCharacter(
        IncrementalHash hash,
        StringBuilder buffer,
        char character)
    {
        buffer.Append(character);
        if (buffer.Length < 4096 ||
            char.IsHighSurrogate(buffer[^1]))
        {
            return 0;
        }

        return FlushHashBuffer(hash, buffer);
    }

    private static int FlushHashBuffer(
        IncrementalHash hash,
        StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(buffer.ToString());
        hash.AppendData(bytes);
        buffer.Clear();
        return bytes.Length;
    }
}
