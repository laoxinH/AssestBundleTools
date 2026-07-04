using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 维护预览缓存文本 payload 的稀疏行索引 sidecar。
/// </summary>
internal static class JsonPreviewCacheTextLineIndex
{
    public const int DefaultAnchorInterval = 512;
    public const int DefaultCharacterAnchorInterval = 4096;

    private const int SchemaVersion = 2;
    private const string SidecarSuffix = ".lines.json";

    public static void Save(
        PreviewCacheDescriptorDto descriptor,
        string payloadFilePath,
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadFilePath);
        ArgumentNullException.ThrowIfNull(content);

        var index = Build(
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath,
            content);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            GetSidecarFilePath(payloadFilePath),
            index,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public static JsonPreviewCacheTextLineIndexDto? Load(
        PreviewCacheDescriptorDto descriptor,
        string payloadFilePath,
        long payloadLength)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadFilePath);

        var index = JsonWorkspaceFileStorageHelper.TryReadJson<JsonPreviewCacheTextLineIndexDto>(
            GetSidecarFilePath(payloadFilePath),
            JsonWorkspaceSerialization.SerializerOptions);
        if (index is null ||
            index.SchemaVersion != SchemaVersion ||
            index.AnchorInterval <= 0 ||
            index.CharacterAnchorInterval <= 0 ||
            index.PayloadLength != payloadLength ||
            index.TotalLineCount < 0 ||
            index.TotalCharacterCount < 0 ||
            !AreLineAnchorsValid(index.Anchors, payloadLength) ||
            !AreCharacterAnchorsValid(index.CharacterAnchors, payloadLength) ||
            !string.Equals(index.PreviewCacheKey, descriptor.PreviewCacheKey, StringComparison.Ordinal) ||
            !string.Equals(index.RelativePayloadPath, descriptor.RelativePayloadPath, StringComparison.Ordinal))
        {
            return null;
        }

        return index;
    }

    public static void Delete(string payloadFilePath)
    {
        if (string.IsNullOrWhiteSpace(payloadFilePath))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(GetSidecarFilePath(payloadFilePath));
    }

    public static long GetSizeInBytes(string payloadFilePath)
    {
        if (string.IsNullOrWhiteSpace(payloadFilePath))
        {
            return 0;
        }

        try
        {
            var sidecarFilePath = GetSidecarFilePath(payloadFilePath);
            return File.Exists(sidecarFilePath) ? new FileInfo(sidecarFilePath).Length : 0;
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

    public static string GetSidecarFilePath(string payloadFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(payloadFilePath);
        return Path.Combine(
            Path.GetDirectoryName(payloadFilePath)!,
            $"{fileNameWithoutExtension}{SidecarSuffix}");
    }

    private static JsonPreviewCacheTextLineIndexDto Build(
        string previewCacheKey,
        string relativePayloadPath,
        byte[] content)
    {
        var anchors = new List<JsonPreviewCacheTextLineIndexAnchorDto>();
        var characterAnchors = new List<JsonPreviewCacheTextCharacterAnchorDto>();
        var totalLineCount = content.Length == 0 ? 0 : 1;
        var totalCharacterCount = 0;
        if (content.Length > 0)
        {
            anchors.Add(new JsonPreviewCacheTextLineIndexAnchorDto(LineIndex: 0, ByteOffset: 0));
            characterAnchors.Add(new JsonPreviewCacheTextCharacterAnchorDto(CharacterOffset: 0, ByteOffset: 0));
        }

        var byteIndex = 0;
        var nextCharacterAnchorOffset = DefaultCharacterAnchorInterval;
        while (byteIndex < content.Length)
        {
            var current = content[byteIndex];
            if (current is not ((byte)'\r') and not ((byte)'\n'))
            {
                var characterByteLength = ResolveUtf8CharacterByteLength(content, byteIndex);
                var characterLength = characterByteLength == 4 ? 2 : 1;
                byteIndex += characterByteLength;
                totalCharacterCount += characterLength;
                TryAddCharacterAnchor(
                    characterAnchors,
                    totalCharacterCount,
                    byteIndex,
                    content.Length,
                    ref nextCharacterAnchorOffset);
                continue;
            }

            var nextLineIndex = totalLineCount;
            if (current == (byte)'\r' &&
                byteIndex + 1 < content.Length &&
                content[byteIndex + 1] == (byte)'\n')
            {
                byteIndex += 2;
            }
            else
            {
                byteIndex++;
            }

            totalCharacterCount++;
            totalLineCount++;
            if (byteIndex < content.Length &&
                nextLineIndex % DefaultAnchorInterval == 0)
            {
                anchors.Add(new JsonPreviewCacheTextLineIndexAnchorDto(nextLineIndex, byteIndex));
            }

            TryAddCharacterAnchor(
                characterAnchors,
                totalCharacterCount,
                byteIndex,
                content.Length,
                ref nextCharacterAnchorOffset);
        }

        return new JsonPreviewCacheTextLineIndexDto(
            SchemaVersion,
            previewCacheKey,
            relativePayloadPath,
            content.LongLength,
            DefaultAnchorInterval,
            DefaultCharacterAnchorInterval,
            totalLineCount,
            totalCharacterCount,
            anchors,
            characterAnchors,
            DateTimeOffset.UtcNow);
    }

    private static bool AreLineAnchorsValid(
        IReadOnlyList<JsonPreviewCacheTextLineIndexAnchorDto>? anchors,
        long payloadLength)
    {
        if (anchors is not { Count: > 0 })
        {
            return payloadLength == 0;
        }

        var previousLineIndex = -1;
        var previousByteOffset = -1L;
        foreach (var anchor in anchors)
        {
            if (anchor.LineIndex < 0 ||
                anchor.ByteOffset < 0 ||
                anchor.ByteOffset > payloadLength ||
                anchor.LineIndex < previousLineIndex ||
                anchor.ByteOffset < previousByteOffset)
            {
                return false;
            }

            previousLineIndex = anchor.LineIndex;
            previousByteOffset = anchor.ByteOffset;
        }

        return true;
    }

    private static bool AreCharacterAnchorsValid(
        IReadOnlyList<JsonPreviewCacheTextCharacterAnchorDto>? anchors,
        long payloadLength)
    {
        if (anchors is not { Count: > 0 })
        {
            return payloadLength == 0;
        }

        var previousCharacterOffset = -1;
        var previousByteOffset = -1L;
        foreach (var anchor in anchors)
        {
            if (anchor.CharacterOffset < 0 ||
                anchor.ByteOffset < 0 ||
                anchor.ByteOffset > payloadLength ||
                anchor.CharacterOffset < previousCharacterOffset ||
                anchor.ByteOffset < previousByteOffset)
            {
                return false;
            }

            previousCharacterOffset = anchor.CharacterOffset;
            previousByteOffset = anchor.ByteOffset;
        }

        return true;
    }

    private static void TryAddCharacterAnchor(
        List<JsonPreviewCacheTextCharacterAnchorDto> anchors,
        int characterOffset,
        int byteOffset,
        int contentLength,
        ref int nextCharacterAnchorOffset)
    {
        if (byteOffset >= contentLength)
        {
            return;
        }

        if (characterOffset >= nextCharacterAnchorOffset)
        {
            anchors.Add(new JsonPreviewCacheTextCharacterAnchorDto(characterOffset, byteOffset));
        }

        while (nextCharacterAnchorOffset <= characterOffset &&
            nextCharacterAnchorOffset <= int.MaxValue - DefaultCharacterAnchorInterval)
        {
            nextCharacterAnchorOffset += DefaultCharacterAnchorInterval;
        }
    }

    private static int ResolveUtf8CharacterByteLength(byte[] content, int byteIndex)
    {
        var first = content[byteIndex];
        if (first < 0x80)
        {
            return 1;
        }

        if (first is >= 0xC2 and <= 0xDF &&
            byteIndex + 1 < content.Length &&
            IsUtf8ContinuationByte(content[byteIndex + 1]))
        {
            return 2;
        }

        if (first is >= 0xE0 and <= 0xEF &&
            byteIndex + 2 < content.Length &&
            IsUtf8ContinuationByte(content[byteIndex + 1]) &&
            IsUtf8ContinuationByte(content[byteIndex + 2]))
        {
            return 3;
        }

        if (first is >= 0xF0 and <= 0xF4 &&
            byteIndex + 3 < content.Length &&
            IsUtf8ContinuationByte(content[byteIndex + 1]) &&
            IsUtf8ContinuationByte(content[byteIndex + 2]) &&
            IsUtf8ContinuationByte(content[byteIndex + 3]))
        {
            return 4;
        }

        return 1;
    }

    private static bool IsUtf8ContinuationByte(byte value) =>
        value is >= 0x80 and <= 0xBF;
}

internal sealed record JsonPreviewCacheTextLineIndexDto(
    int SchemaVersion,
    string PreviewCacheKey,
    string RelativePayloadPath,
    long PayloadLength,
    int AnchorInterval,
    int CharacterAnchorInterval,
    int TotalLineCount,
    int TotalCharacterCount,
    IReadOnlyList<JsonPreviewCacheTextLineIndexAnchorDto> Anchors,
    IReadOnlyList<JsonPreviewCacheTextCharacterAnchorDto> CharacterAnchors,
    DateTimeOffset CreatedAtUtc);

internal sealed record JsonPreviewCacheTextLineIndexAnchorDto(
    int LineIndex,
    long ByteOffset);

internal sealed record JsonPreviewCacheTextCharacterAnchorDto(
    int CharacterOffset,
    long ByteOffset);
