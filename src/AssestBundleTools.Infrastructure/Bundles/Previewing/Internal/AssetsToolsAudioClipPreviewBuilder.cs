using System.Globalization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

/// <summary>
/// 负责把 Unity <c>AudioClip</c> 字段树整理成稳定的预览构建上下文。
/// </summary>
/// <remarks>
/// 这里专注于“读取载荷、识别容器、整理展示信息”，
/// 真正是否直通播放、是否走外部转码，由上层预览生成器统一决策。
/// </remarks>
internal static class AssetsToolsAudioClipPreviewBuilder
{
    private const string TextContentType = "text/plain; charset=utf-8";
    private const string DefaultPayloadBaseName = "audio_payload";

    public static AudioClipPreviewSource BuildPreviewSource(
        BundleFileInstance bundleInstance,
        AssetTypeValueField baseField,
        AssetPreviewBuildRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(bundleInstance);
        ArgumentNullException.ThrowIfNull(baseField);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = ReadMetadata(baseField);
        if (!TryReadPayload(bundleInstance, baseField, request.ResolvedBundlePath, out var payload))
        {
            return new AudioClipPreviewSource(
                Metadata: metadata,
                AssetPath: request.AssetPath,
                Payload: null,
                Container: AudioContainerProfile.Unknown);
        }

        return new AudioClipPreviewSource(
            Metadata: metadata,
            AssetPath: request.AssetPath,
            Payload: payload,
            Container: DetectContainer(payload.Content, request.AssetPath));
    }

    public static AssetPreviewContentDto BuildDirectPlayablePreview(AudioClipPreviewSource previewSource)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        if (previewSource.Payload is null || string.IsNullOrWhiteSpace(previewSource.Container.ContentType))
        {
            throw new InvalidOperationException("Direct playable preview requires audio payload and content type.");
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Audio,
            ContentType: previewSource.Container.ContentType,
            Content: previewSource.Payload.Content,
            PreviewInfoItems: BuildPreviewInfoItems(previewSource),
            PreferredDisplaySizeInBytes: previewSource.Payload.PayloadSizeInBytes);
    }

    public static AssetPreviewContentDto BuildTranscodedPreview(
        AudioClipPreviewSource previewSource,
        AudioPayloadTranscodeResultDto transcodeResult)
    {
        ArgumentNullException.ThrowIfNull(previewSource);
        ArgumentNullException.ThrowIfNull(transcodeResult);

        if (!transcodeResult.IsSuccess || transcodeResult.Content is null || string.IsNullOrWhiteSpace(transcodeResult.ContentType))
        {
            throw new InvalidOperationException("Successful transcoded preview requires content and content type.");
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Audio,
            ContentType: transcodeResult.ContentType,
            Content: transcodeResult.Content,
            PreviewInfoItems: BuildPreviewInfoItems(
                previewSource,
                outputContainerDisplayName: ResolveOutputContainerDisplayName(transcodeResult.ContentType),
                transcoder: transcodeResult.Transcoder),
            PreferredDisplaySizeInBytes: transcodeResult.Content.LongLength,
            AudioTranscoderId: transcodeResult.Transcoder?.Id,
            AudioTranscoderVersion: transcodeResult.Transcoder?.Version);
    }

    public static AssetPreviewContentDto BuildSummaryPreview(
        AudioClipPreviewSource previewSource,
        AssetPreviewMessageDto previewMessage)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            Content: Array.Empty<byte>(),
            PreviewInfoItems: BuildPreviewInfoItems(previewSource),
            PreviewMessage: previewMessage,
            PreferredDisplaySizeInBytes: previewSource.Payload?.PayloadSizeInBytes);
    }

    public static bool CanAttemptExternalTranscode(AudioClipPreviewSource previewSource)
    {
        ArgumentNullException.ThrowIfNull(previewSource);
        return previewSource.Payload is not null && previewSource.Container.Kind == AudioContainerKind.Fsb5;
    }

    public static string ResolveSuggestedTranscodeSourceFileName(AudioClipPreviewSource previewSource)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        var baseName = Path.GetFileNameWithoutExtension(previewSource.AssetPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = string.IsNullOrWhiteSpace(previewSource.Metadata.Name)
                ? DefaultPayloadBaseName
                : previewSource.Metadata.Name.Trim();
        }

        var extension = previewSource.Container.Kind switch
        {
            AudioContainerKind.Wav => ".wav",
            AudioContainerKind.Ogg => ".ogg",
            AudioContainerKind.Mp3 => ".mp3",
            AudioContainerKind.Flac => ".flac",
            AudioContainerKind.Mp4 => ".m4a",
            AudioContainerKind.Aac => ".aac",
            AudioContainerKind.Fsb5 => ".fsb",
            _ => Path.GetExtension(previewSource.AssetPath)
        };

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        return $"{SanitizeFileComponent(baseName)}{extension}";
    }

    public static AssetPreviewMessageDto BuildUnsupportedContainerMessage(AudioClipPreviewSource previewSource)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        return previewSource.Container.Kind switch
        {
            AudioContainerKind.Fsb5 => new AssetPreviewMessageDto(AssetPreviewMessageKey.AudioFsb5RequiresExternalTranscode),
            AudioContainerKind.Unknown => new AssetPreviewMessageDto(AssetPreviewMessageKey.AudioUnknownContainerUnsupported),
            _ => new AssetPreviewMessageDto(
                AssetPreviewMessageKey.AudioUnsupportedContainer,
                [previewSource.Container.DisplayName])
        };
    }

    public static AssetPreviewMessageDto BuildTranscodeFallbackMessage(
        AudioClipPreviewSource previewSource,
        AudioPayloadTranscodeResultDto transcodeResult)
    {
        ArgumentNullException.ThrowIfNull(previewSource);
        ArgumentNullException.ThrowIfNull(transcodeResult);

        return transcodeResult.Status switch
        {
            AudioPayloadTranscodeStatusKind.Unavailable => new AssetPreviewMessageDto(
                AssetPreviewMessageKey.AudioTranscodeUnavailable,
                [previewSource.Container.DisplayName]),
            AudioPayloadTranscodeStatusKind.Failed => new AssetPreviewMessageDto(
                AssetPreviewMessageKey.AudioTranscodeFailed,
                [previewSource.Container.DisplayName]),
            _ => BuildUnsupportedContainerMessage(previewSource)
        };
    }

    private static AudioClipMetadata ReadMetadata(AssetTypeValueField baseField)
    {
        return new AudioClipMetadata(
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")),
            LoadType: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")),
            Channels: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Channels")),
            Frequency: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Frequency")),
            BitsPerSample: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_BitsPerSample")),
            LengthInSeconds: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_Length")),
            CompressionFormat: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")));
    }

    private static bool TryReadPayload(
        BundleFileInstance bundleInstance,
        AssetTypeValueField baseField,
        string resolvedBundlePath,
        out AudioPayloadData payload)
    {
        payload = null!;

        var inlinePayload = AssetsToolsFieldReader.ReadByteArray(AssetsToolsFieldReader.TryGetField(baseField, "m_AudioData"));
        if (inlinePayload is { Length: > 0 })
        {
            payload = new AudioPayloadData(
                Content: inlinePayload,
                SourceKind: AudioPayloadSourceKind.Inline,
                SourceDisplayName: "m_AudioData",
                PayloadSizeInBytes: inlinePayload.LongLength);
            return true;
        }

        var resourceField = AssetsToolsFieldReader.TryGetField(baseField, "m_Resource");
        var resourceSource = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(resourceField, "m_Source"));
        var resourceOffset = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Offset"));
        var resourceSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Size"));
        if (string.IsNullOrWhiteSpace(resourceSource) || !resourceOffset.HasValue || !resourceSize.HasValue || resourceSize.Value <= 0)
        {
            return false;
        }

        if (TryReadBundleResourcePayload(bundleInstance, resourceSource, resourceOffset.Value, resourceSize.Value, out payload))
        {
            return true;
        }

        return TryReadExternalResourcePayload(resolvedBundlePath, resourceSource, resourceOffset.Value, resourceSize.Value, out payload);
    }

    private static bool TryReadBundleResourcePayload(
        BundleFileInstance bundleInstance,
        string resourceSource,
        long resourceOffset,
        long resourceSize,
        out AudioPayloadData payload)
    {
        payload = null!;

        var entryName = ResolveBundleEntryName(resourceSource);
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        var fileIndex = bundleInstance.file.GetFileIndex(entryName);
        if (fileIndex < 0)
        {
            return false;
        }

        if (!TryReadBundleFileRange(bundleInstance.file, fileIndex, resourceOffset, resourceSize, out var content))
        {
            return false;
        }

        payload = new AudioPayloadData(
            Content: content,
            SourceKind: AudioPayloadSourceKind.BundleResource,
            SourceDisplayName: entryName,
            PayloadSizeInBytes: content.LongLength);
        return true;
    }

    private static bool TryReadExternalResourcePayload(
        string resolvedBundlePath,
        string resourceSource,
        long resourceOffset,
        long resourceSize,
        out AudioPayloadData payload)
    {
        payload = null!;

        var externalPath = ResolveExternalResourcePath(resolvedBundlePath, resourceSource);
        if (string.IsNullOrWhiteSpace(externalPath) || !File.Exists(externalPath))
        {
            return false;
        }

        if (resourceOffset < 0 || resourceSize <= 0 || resourceSize > int.MaxValue)
        {
            return false;
        }

        using var stream = new FileStream(externalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (resourceOffset > stream.Length || resourceOffset + resourceSize > stream.Length)
        {
            return false;
        }

        stream.Position = resourceOffset;
        var content = new byte[checked((int)resourceSize)];
        var bytesRead = stream.Read(content, 0, content.Length);
        if (bytesRead != content.Length)
        {
            return false;
        }

        payload = new AudioPayloadData(
            Content: content,
            SourceKind: AudioPayloadSourceKind.ExternalResource,
            SourceDisplayName: Path.GetFileName(externalPath),
            PayloadSizeInBytes: content.LongLength);
        return true;
    }

    private static bool TryReadBundleFileRange(
        AssetBundleFile bundleFile,
        int fileIndex,
        long resourceOffset,
        long resourceSize,
        out byte[] content)
    {
        content = Array.Empty<byte>();

        if (resourceOffset < 0 || resourceSize <= 0 || resourceSize > int.MaxValue)
        {
            return false;
        }

        long fileOffset = 0;
        long fileLength = 0;
        bundleFile.GetFileRange(fileIndex, out fileOffset, out fileLength);

        if (resourceOffset > fileLength || resourceOffset + resourceSize > fileLength)
        {
            return false;
        }

        bundleFile.DataReader.Position = fileOffset + resourceOffset;
        content = bundleFile.DataReader.ReadBytes((int)resourceSize);
        return content.LongLength == resourceSize;
    }

    private static AudioContainerProfile DetectContainer(byte[] content, string assetPath)
    {
        if (IsWave(content))
        {
            return AudioContainerProfile.Wav;
        }

        if (IsOgg(content))
        {
            return AudioContainerProfile.Ogg;
        }

        if (IsFlac(content))
        {
            return AudioContainerProfile.Flac;
        }

        if (IsMp4(content))
        {
            return AudioContainerProfile.Mp4;
        }

        if (IsFsb5(content))
        {
            return AudioContainerProfile.Fsb5;
        }

        if (IsMp3(content))
        {
            return AudioContainerProfile.Mp3;
        }

        var extension = Path.GetExtension(assetPath)?.Trim().ToLowerInvariant();
        return extension switch
        {
            ".wav" => AudioContainerProfile.Wav,
            ".ogg" => AudioContainerProfile.Ogg,
            ".mp3" when HasFrameSync(content) || HasId3Header(content) => AudioContainerProfile.Mp3,
            ".flac" => AudioContainerProfile.Flac,
            ".m4a" or ".mp4" => AudioContainerProfile.Mp4,
            ".aac" when HasFrameSync(content) => AudioContainerProfile.Aac,
            _ => AudioContainerProfile.Unknown
        };
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AudioClipPreviewSource previewSource,
        string? outputContainerDisplayName = null,
        AudioPayloadTranscoderDescriptorDto? transcoder = null)
    {
        var items = new List<AssetPreviewInfoItemDto>();

        if (transcoder is null)
        {
            AddTextInfo(items, AssetPreviewInfoKey.AudioContainer, previewSource.Container.DisplayName);
        }
        else
        {
            AddTextInfo(items, AssetPreviewInfoKey.AudioOriginalContainer, previewSource.Container.DisplayName);
            AddTextInfo(items, AssetPreviewInfoKey.AudioOutputContainer, outputContainerDisplayName);
            AddTextInfo(items, AssetPreviewInfoKey.AudioTranscoder, transcoder.DisplayName);
            AddTextInfo(items, AssetPreviewInfoKey.AudioTranscoderVersion, transcoder.Version);
        }

        AddTextInfo(items, AssetPreviewInfoKey.AudioPayloadSource, previewSource.Payload?.SourceKind.ToString());
        AddTextInfo(items, AssetPreviewInfoKey.AudioPayloadFile, previewSource.Payload?.SourceDisplayName);
        AddByteSizeInfo(items, AssetPreviewInfoKey.AudioPayloadSize, previewSource.Payload?.PayloadSizeInBytes);
        AddIntegerInfo(items, AssetPreviewInfoKey.AudioLoadType, previewSource.Metadata.LoadType);
        AddIntegerInfo(items, AssetPreviewInfoKey.AudioChannels, previewSource.Metadata.Channels);
        AddFrequencyInfo(items, AssetPreviewInfoKey.AudioFrequency, previewSource.Metadata.Frequency);
        AddBitsInfo(items, AssetPreviewInfoKey.AudioBitsPerSample, previewSource.Metadata.BitsPerSample);
        AddDurationInfo(items, AssetPreviewInfoKey.AudioDuration, previewSource.Metadata.LengthInSeconds);
        AddIntegerInfo(items, AssetPreviewInfoKey.AudioCompressionFormatId, previewSource.Metadata.CompressionFormat);

        return items;
    }

    private static string? ResolveBundleEntryName(string resourceSource)
    {
        if (string.IsNullOrWhiteSpace(resourceSource))
        {
            return null;
        }

        var normalizedSource = resourceSource.Trim().Replace('\\', '/');
        var lastSlashIndex = normalizedSource.LastIndexOf('/');
        if (lastSlashIndex < 0 || lastSlashIndex >= normalizedSource.Length - 1)
        {
            return Path.GetFileName(normalizedSource);
        }

        return normalizedSource[(lastSlashIndex + 1)..];
    }

    private static string? ResolveExternalResourcePath(string resolvedBundlePath, string resourceSource)
    {
        if (string.IsNullOrWhiteSpace(resourceSource))
        {
            return null;
        }

        if (Path.IsPathRooted(resourceSource))
        {
            return resourceSource;
        }

        var resourceFileName = Path.GetFileName(resourceSource);
        if (string.IsNullOrWhiteSpace(resourceFileName))
        {
            return null;
        }

        var bundleDirectoryPath = Path.GetDirectoryName(resolvedBundlePath);
        return string.IsNullOrWhiteSpace(bundleDirectoryPath)
            ? resourceFileName
            : Path.Combine(bundleDirectoryPath, resourceFileName);
    }

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

    private static bool IsFsb5(byte[] content)
    {
        return content.Length >= 4 &&
               content[0] == (byte)'F' &&
               content[1] == (byte)'S' &&
               content[2] == (byte)'B' &&
               content[3] == (byte)'5';
    }

    private static bool IsMp3(byte[] content)
    {
        return HasId3Header(content);
    }

    private static bool HasId3Header(byte[] content)
    {
        return content.Length >= 3 &&
               content[0] == (byte)'I' &&
               content[1] == (byte)'D' &&
               content[2] == (byte)'3';
    }

    private static bool HasFrameSync(byte[] content)
    {
        return content.Length >= 2 &&
               content[0] == 0xFF &&
               (content[1] & 0xE0) == 0xE0;
    }

    private static string ResolveOutputContainerDisplayName(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => "WAV",
            "audio/ogg" => "OGG",
            "audio/mpeg" => "MP3",
            "audio/flac" => "FLAC",
            "audio/mp4" => "M4A / MP4",
            "audio/aac" => "AAC",
            _ => contentType
        };
    }

    private static void AddTextInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Text,
            TextValue: value.Trim()));
    }

    private static void AddIntegerInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Integer,
            Int64Value: value.Value));
    }

    private static void AddByteSizeInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.ByteSize,
            Int64Value: value.Value));
    }

    private static void AddFrequencyInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.FrequencyHertz,
            Int64Value: value.Value));
    }

    private static void AddBitsInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.BitsPerSample,
            Int64Value: value.Value));
    }

    private static void AddDurationInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.DurationSeconds,
            NumberValue: value.Value));
    }

    private static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }

    internal enum AudioPayloadSourceKind
    {
        Inline = 0,
        BundleResource = 1,
        ExternalResource = 2
    }

    internal sealed record AudioClipMetadata(
        string? Name,
        int? LoadType,
        int? Channels,
        int? Frequency,
        int? BitsPerSample,
        float? LengthInSeconds,
        int? CompressionFormat);

    internal sealed record AudioClipPreviewSource(
        AudioClipMetadata Metadata,
        string AssetPath,
        AudioPayloadData? Payload,
        AudioContainerProfile Container);

    internal sealed record AudioPayloadData(
        byte[] Content,
        AudioPayloadSourceKind SourceKind,
        string SourceDisplayName,
        long PayloadSizeInBytes);

    internal sealed record AudioContainerProfile(
        AudioContainerKind Kind,
        string DisplayName,
        string? ContentType,
        bool IsPlayable)
    {
        public static AudioContainerProfile Unknown { get; } = new(AudioContainerKind.Unknown, nameof(AudioContainerKind.Unknown), null, false);

        public static AudioContainerProfile Wav { get; } = new(AudioContainerKind.Wav, "WAV", "audio/wav", true);

        public static AudioContainerProfile Ogg { get; } = new(AudioContainerKind.Ogg, "OGG", "audio/ogg", true);

        public static AudioContainerProfile Mp3 { get; } = new(AudioContainerKind.Mp3, "MP3", "audio/mpeg", true);

        public static AudioContainerProfile Flac { get; } = new(AudioContainerKind.Flac, "FLAC", "audio/flac", true);

        public static AudioContainerProfile Mp4 { get; } = new(AudioContainerKind.Mp4, "M4A / MP4", "audio/mp4", true);

        public static AudioContainerProfile Aac { get; } = new(AudioContainerKind.Aac, "AAC", "audio/aac", true);

        public static AudioContainerProfile Fsb5 { get; } = new(AudioContainerKind.Fsb5, "FSB5", null, false);
    }
}
