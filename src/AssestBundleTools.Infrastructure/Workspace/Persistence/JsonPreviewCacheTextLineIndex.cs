using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 维护预览缓存文本 payload 的稀疏行索引 sidecar。
/// </summary>
internal static class JsonPreviewCacheTextLineIndex
{
    public const int DefaultAnchorInterval = 512;

    private const int SchemaVersion = 1;
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
            index.PayloadLength != payloadLength ||
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
        var totalLineCount = content.Length == 0 ? 0 : 1;
        if (content.Length > 0)
        {
            anchors.Add(new JsonPreviewCacheTextLineIndexAnchorDto(LineIndex: 0, ByteOffset: 0));
        }

        var byteIndex = 0;
        while (byteIndex < content.Length)
        {
            var current = content[byteIndex];
            if (current is not ((byte)'\r') and not ((byte)'\n'))
            {
                byteIndex++;
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

            totalLineCount++;
            if (byteIndex < content.Length &&
                nextLineIndex % DefaultAnchorInterval == 0)
            {
                anchors.Add(new JsonPreviewCacheTextLineIndexAnchorDto(nextLineIndex, byteIndex));
            }
        }

        return new JsonPreviewCacheTextLineIndexDto(
            SchemaVersion,
            previewCacheKey,
            relativePayloadPath,
            content.LongLength,
            DefaultAnchorInterval,
            totalLineCount,
            anchors,
            DateTimeOffset.UtcNow);
    }
}

internal sealed record JsonPreviewCacheTextLineIndexDto(
    int SchemaVersion,
    string PreviewCacheKey,
    string RelativePayloadPath,
    long PayloadLength,
    int AnchorInterval,
    int TotalLineCount,
    IReadOnlyList<JsonPreviewCacheTextLineIndexAnchorDto> Anchors,
    DateTimeOffset CreatedAtUtc);

internal sealed record JsonPreviewCacheTextLineIndexAnchorDto(
    int LineIndex,
    long ByteOffset);
