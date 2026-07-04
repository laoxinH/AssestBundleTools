using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 从外部文件读取替换来源预览载荷。
/// </summary>
public sealed class FileSystemReplacementPreviewSourceProvider : IReplacementPreviewSourceProvider
{
    private const string BinaryContentType = "application/octet-stream";
    private const string TextContentType = "text/plain; charset=utf-8";

    public async Task<Result<AssetPreviewContentDto>> LoadFilePreviewAsync(
        AssetDetailsDto targetDetails,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetDetails);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement source path is empty."));
        }

        try
        {
            var normalizedPath = sourceFilePath.Trim();
            if (!File.Exists(normalizedPath))
            {
                return Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, "Replacement source file was not found."));
            }

            var content = await File.ReadAllBytesAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            return targetDetails.AssetType switch
            {
                AssetType.Texture2D or AssetType.Sprite => Result<AssetPreviewContentDto>.Success(
                    new AssetPreviewContentDto(
                        Kind: AssetPreviewKind.Image,
                        ContentType: ResolveImageContentType(normalizedPath),
                        Content: content,
                        PreferredDisplaySizeInBytes: content.LongLength)),
                AssetType.TextAsset => Result<AssetPreviewContentDto>.Success(
                    CreateTextAssetPreview(normalizedPath, content)),
                AssetType.AudioClip => Result<AssetPreviewContentDto>.Success(
                    CreateAudioPreview(normalizedPath, content)),
                _ => Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(
                        normalizedPath,
                        "Replacement preview type is not supported."))
            };
        }
        catch (OperationCanceledException)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, ex.GetType().Name));
        }
    }

    public Task<Result<ReplacementTextChunkDto>> LoadFileTextChunkAsync(
        string sourceFilePath,
        int offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<ReplacementTextChunkDto>.Cancelled());
        }

        return Task.Run(() => FileSystemReplacementTextChunkReader.Load(
            sourceFilePath,
            offset,
            length,
            cancellationToken));
    }

    public Task<Result<ReplacementTextLineRangeDto>> LoadFileTextLinesAsync(
        string sourceFilePath,
        int startLineIndex,
        int lineCount,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<ReplacementTextLineRangeDto>.Cancelled());
        }

        return Task.Run(() => FileSystemReplacementTextChunkReader.LoadLines(
            sourceFilePath,
            startLineIndex,
            lineCount,
            cancellationToken));
    }

    private static string ResolveImageContentType(string path)
    {
        return Path.GetExtension(path).Trim().ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".tga" => "image/x-tga",
            ".dds" => "image/vnd-ms.dds",
            _ => "image/png"
        };
    }

    private static AssetPreviewContentDto CreateAudioPreview(
        string sourceFilePath,
        byte[] content)
    {
        var contentType = ResolveAudioContentType(sourceFilePath, content);
        var previewInfoItems = BuildAudioPreviewInfoItems(
            sourceFilePath,
            content,
            contentType);
        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Audio,
            ContentType: contentType,
            Content: content,
            PreviewInfoItems: previewInfoItems,
            PreferredDisplaySizeInBytes: content.LongLength);
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildAudioPreviewInfoItems(
        string sourceFilePath,
        byte[] content,
        string contentType)
    {
        var items = new List<AssetPreviewInfoItemDto>
        {
            new(
                AssetPreviewInfoKey.AudioContainer,
                AssetPreviewInfoValueKind.Text,
                TextValue: ResolveAudioContainerDisplayName(contentType)),
            new(
                AssetPreviewInfoKey.AudioPayloadFile,
                AssetPreviewInfoValueKind.Text,
                TextValue: Path.GetFileName(sourceFilePath)),
            new(
                AssetPreviewInfoKey.AudioPayloadSize,
                AssetPreviewInfoValueKind.ByteSize,
                Int64Value: content.LongLength)
        };

        if (TryReadWaveMetadata(content, out var waveMetadata))
        {
            if (waveMetadata.ChannelCount > 0)
            {
                items.Add(new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AudioChannels,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: waveMetadata.ChannelCount));
            }

            if (waveMetadata.SampleRate > 0)
            {
                items.Add(new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AudioFrequency,
                    AssetPreviewInfoValueKind.FrequencyHertz,
                    Int64Value: waveMetadata.SampleRate));
            }

            if (waveMetadata.BitsPerSample > 0)
            {
                items.Add(new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AudioBitsPerSample,
                    AssetPreviewInfoValueKind.BitsPerSample,
                    Int64Value: waveMetadata.BitsPerSample));
            }

            if (waveMetadata.DurationSeconds > 0)
            {
                items.Add(new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AudioDuration,
                    AssetPreviewInfoValueKind.DurationSeconds,
                    NumberValue: waveMetadata.DurationSeconds));
            }
        }

        return items;
    }

    private static string ResolveAudioContentType(
        string sourceFilePath,
        byte[] content)
    {
        if (IsWave(content))
        {
            return "audio/wav";
        }

        if (IsOgg(content))
        {
            return "audio/ogg";
        }

        if (IsFlac(content))
        {
            return "audio/flac";
        }

        if (IsMp4(content))
        {
            return "audio/mp4";
        }

        if (IsMp3(content))
        {
            return "audio/mpeg";
        }

        return Path.GetExtension(sourceFilePath).Trim().ToLowerInvariant() switch
        {
            ".wav" or ".wave" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".m4a" or ".mp4" => "audio/mp4",
            ".aac" => "audio/aac",
            _ => BinaryContentType
        };
    }

    private static string ResolveAudioContainerDisplayName(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => "WAV",
            "audio/ogg" => "OGG",
            "audio/mpeg" => "MP3",
            "audio/flac" => "FLAC",
            "audio/mp4" => "M4A / MP4",
            "audio/aac" => "AAC",
            _ => "Unknown"
        };
    }

    private static AssetPreviewContentDto CreateTextAssetPreview(
        string sourceFilePath,
        byte[] content)
    {
        if (ShouldKeepBinary(sourceFilePath, content, out var textContent))
        {
            return new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Binary,
                ContentType: BinaryContentType,
                Content: content,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.SerializedObjectSize,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: content.LongLength)
                ],
                PreferredDisplaySizeInBytes: content.LongLength);
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Text,
            ContentType: TextContentType,
            Content: Encoding.UTF8.GetBytes(NormalizeTextContent(textContent)),
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreferredDisplaySizeInBytes: null);
    }

    private static bool ShouldKeepBinary(
        string sourceFilePath,
        byte[] content,
        out string textContent)
    {
        textContent = string.Empty;
        if (content.Length == 0)
        {
            return false;
        }

        if (!TryDecodeText(content, out textContent))
        {
            return true;
        }

        textContent = textContent.TrimStart('\uFEFF').TrimEnd('\0');
        if (!IsReadableText(textContent))
        {
            return true;
        }

        var extension = Path.GetExtension(sourceFilePath);
        return extension.Equals(".skel", StringComparison.OrdinalIgnoreCase) &&
               !LooksLikeJsonObject(textContent);
    }

    private static bool TryDecodeText(byte[] content, out string textContent)
    {
        textContent = string.Empty;
        try
        {
            if (content.Length >= 2 &&
                content[0] == 0xFF &&
                content[1] == 0xFE)
            {
                textContent = new UnicodeEncoding(
                    bigEndian: false,
                    byteOrderMark: true,
                    throwOnInvalidBytes: true).GetString(content);
                return true;
            }

            if (content.Length >= 2 &&
                content[0] == 0xFE &&
                content[1] == 0xFF)
            {
                textContent = new UnicodeEncoding(
                    bigEndian: true,
                    byteOrderMark: true,
                    throwOnInvalidBytes: true).GetString(content);
                return true;
            }

            textContent = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsReadableText(string? textContent)
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            return false;
        }

        foreach (var character in textContent)
        {
            if (character == '\uFFFD')
            {
                return false;
            }

            if (char.IsControl(character) &&
                character is not ('\t' or '\n' or '\r'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeJsonObject(string textContent)
    {
        foreach (var character in textContent)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '{';
        }

        return false;
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static bool IsWave(byte[] content)
    {
        return content.Length >= 12 &&
               content[0] == (byte)'R' &&
               content[1] == (byte)'I' &&
               content[2] == (byte)'F' &&
               content[3] == (byte)'F' &&
               content[8] == (byte)'W' &&
               content[9] == (byte)'A' &&
               content[10] == (byte)'V' &&
               content[11] == (byte)'E';
    }

    private static bool IsOgg(byte[] content)
    {
        return content.Length >= 4 &&
               content[0] == (byte)'O' &&
               content[1] == (byte)'g' &&
               content[2] == (byte)'g' &&
               content[3] == (byte)'S';
    }

    private static bool IsFlac(byte[] content)
    {
        return content.Length >= 4 &&
               content[0] == (byte)'f' &&
               content[1] == (byte)'L' &&
               content[2] == (byte)'a' &&
               content[3] == (byte)'C';
    }

    private static bool IsMp4(byte[] content)
    {
        return content.Length >= 12 &&
               content[4] == (byte)'f' &&
               content[5] == (byte)'t' &&
               content[6] == (byte)'y' &&
               content[7] == (byte)'p';
    }

    private static bool IsMp3(byte[] content)
    {
        return content.Length >= 3 &&
               content[0] == (byte)'I' &&
               content[1] == (byte)'D' &&
               content[2] == (byte)'3';
    }

    private static bool TryReadWaveMetadata(
        byte[] content,
        out WaveMetadata metadata)
    {
        metadata = default;
        if (!IsWave(content))
        {
            return false;
        }

        int? channelCount = null;
        int? sampleRate = null;
        int? bitsPerSample = null;
        long? dataSize = null;
        var offset = 12;
        while (offset + 8 <= content.Length)
        {
            var chunkId = Encoding.ASCII.GetString(content, offset, 4);
            var chunkSize = ReadUInt32LittleEndian(content, offset + 4);
            var dataOffset = offset + 8;
            if (chunkSize > int.MaxValue || dataOffset + chunkSize > content.Length)
            {
                break;
            }

            if (string.Equals(chunkId, "fmt ", StringComparison.Ordinal) &&
                chunkSize >= 16)
            {
                channelCount = ReadUInt16LittleEndian(content, dataOffset + 2);
                sampleRate = checked((int)ReadUInt32LittleEndian(content, dataOffset + 4));
                bitsPerSample = ReadUInt16LittleEndian(content, dataOffset + 14);
            }
            else if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                dataSize = chunkSize;
            }

            offset = dataOffset + checked((int)chunkSize) + ((chunkSize & 1) == 1 ? 1 : 0);
        }

        var durationSeconds = 0d;
        if (dataSize is > 0 &&
            channelCount is > 0 &&
            sampleRate is > 0 &&
            bitsPerSample is > 0)
        {
            durationSeconds = dataSize.Value / (sampleRate.Value * channelCount.Value * (bitsPerSample.Value / 8d));
        }

        metadata = new WaveMetadata(
            channelCount ?? 0,
            sampleRate ?? 0,
            bitsPerSample ?? 0,
            durationSeconds);
        return true;
    }

    private static ushort ReadUInt16LittleEndian(byte[] content, int offset)
    {
        return (ushort)(content[offset] | (content[offset + 1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte[] content, int offset)
    {
        return (uint)(content[offset] |
                      (content[offset + 1] << 8) |
                      (content[offset + 2] << 16) |
                      (content[offset + 3] << 24));
    }

    private readonly record struct WaveMetadata(
        int ChannelCount,
        int SampleRate,
        int BitsPerSample,
        double DurationSeconds);
}
