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

    public static AudioClipPreviewSource BuildPreviewSource(
        BundleFileInstance bundleInstance,
        AssetTypeValueField baseField,
        AssetPreviewBuildRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(bundleInstance);
        ArgumentNullException.ThrowIfNull(baseField);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = ReadMetadata(baseField, request.SizeInBytes);
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
            PreviewInfoText: BuildPreviewInfoText(previewSource),
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
            PreviewInfoText: BuildPreviewInfoText(
                previewSource,
                outputContainerDisplayName: ResolveOutputContainerDisplayName(transcodeResult.ContentType),
                transcoder: transcodeResult.Transcoder),
            PreferredDisplaySizeInBytes: transcodeResult.Content.LongLength,
            AudioTranscoderId: transcodeResult.Transcoder?.Id,
            AudioTranscoderVersion: transcodeResult.Transcoder?.Version);
    }

    public static AssetPreviewContentDto BuildSummaryPreview(
        AudioClipPreviewSource previewSource,
        string reason,
        string? diagnosticDetail = null)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            lines.Add(reason.Trim());
        }

        if (!string.IsNullOrWhiteSpace(diagnosticDetail))
        {
            lines.Add(string.Empty);
            lines.Add($"诊断详情: {diagnosticDetail.Trim()}");
        }

        var previewInfoText = BuildPreviewInfoText(previewSource);
        if (!string.IsNullOrWhiteSpace(previewInfoText))
        {
            lines.Add(string.Empty);
            lines.AddRange(previewInfoText.Split(Environment.NewLine, StringSplitOptions.None));
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            Content: System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)),
            PreviewInfoText: null,
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
                ? "audio_payload"
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

    public static string BuildUnsupportedContainerReason(AudioClipPreviewSource previewSource)
    {
        ArgumentNullException.ThrowIfNull(previewSource);

        return previewSource.Container.Kind switch
        {
            AudioContainerKind.Fsb5 => "当前音频容器为 FSB5，需要外部转码后才能播放；若转码不可用则回退为摘要信息。",
            AudioContainerKind.Unknown => "当前音频容器暂未识别，已回退为摘要信息。",
            _ => $"当前音频容器 {previewSource.Container.DisplayName} 暂不支持直接播放，已回退为摘要信息。"
        };
    }

    public static string BuildTranscodeFallbackReason(
        AudioClipPreviewSource previewSource,
        AudioPayloadTranscodeResultDto transcodeResult)
    {
        ArgumentNullException.ThrowIfNull(previewSource);
        ArgumentNullException.ThrowIfNull(transcodeResult);

        return transcodeResult.Status switch
        {
            AudioPayloadTranscodeStatusKind.Unavailable => $"当前音频容器为 {previewSource.Container.DisplayName}，但未发现可用外部转码器，已回退为摘要信息。",
            AudioPayloadTranscodeStatusKind.Failed => $"当前音频容器为 {previewSource.Container.DisplayName}，但外部转码失败，已回退为摘要信息。",
            _ => BuildUnsupportedContainerReason(previewSource)
        };
    }

    private static AudioClipMetadata ReadMetadata(
        AssetTypeValueField baseField,
        long serializedObjectSizeInBytes)
    {
        return new AudioClipMetadata(
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")),
            LoadType: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")),
            Channels: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Channels")),
            Frequency: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Frequency")),
            BitsPerSample: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_BitsPerSample")),
            LengthInSeconds: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_Length")),
            CompressionFormat: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")),
            SerializedObjectSizeInBytes: serializedObjectSizeInBytes);
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

    private static string BuildPreviewInfoText(
        AudioClipPreviewSource previewSource,
        string? outputContainerDisplayName = null,
        AudioPayloadTranscoderDescriptorDto? transcoder = null)
    {
        var lines = new List<string>();

        if (transcoder is null)
        {
            AddLine(lines, "音频容器", previewSource.Container.DisplayName);
        }
        else
        {
            AddLine(lines, "原始音频容器", previewSource.Container.DisplayName);
            AddLine(lines, "预览输出容器", outputContainerDisplayName);
            AddLine(lines, "预览转码器", transcoder.DisplayName);
            AddLine(lines, "转码器版本", transcoder.Version);
        }

        AddLine(lines, "载荷来源", previewSource.Payload?.SourceKind switch
        {
            AudioPayloadSourceKind.Inline => "内嵌音频数据",
            AudioPayloadSourceKind.BundleResource => "资源包附带 .resource",
            AudioPayloadSourceKind.ExternalResource => "外部资源文件",
            _ => null
        });
        AddLine(lines, "载荷文件", previewSource.Payload?.SourceDisplayName);
        AddLine(lines, "音频载荷大小", FormatByteSize(previewSource.Payload?.PayloadSizeInBytes));
        AddLine(lines, "资源记录大小", FormatByteSize(previewSource.Metadata.SerializedObjectSizeInBytes));
        AddLine(lines, "加载类型", previewSource.Metadata.LoadType);
        AddLine(lines, "声道数", previewSource.Metadata.Channels);
        AddLine(lines, "采样频率", previewSource.Metadata.Frequency.HasValue
            ? $"{previewSource.Metadata.Frequency.Value.ToString(CultureInfo.InvariantCulture)} Hz"
            : null);
        AddLine(lines, "位深", previewSource.Metadata.BitsPerSample.HasValue
            ? $"{previewSource.Metadata.BitsPerSample.Value.ToString(CultureInfo.InvariantCulture)} bit"
            : null);
        AddLine(lines, "时长", FormatSeconds(previewSource.Metadata.LengthInSeconds));
        AddLine(lines, "压缩格式 ID", previewSource.Metadata.CompressionFormat);

        return string.Join(Environment.NewLine, lines);
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

    private static void AddLine(ICollection<string> lines, string label, object? value)
    {
        if (value is null)
        {
            return;
        }

        var text = value switch
        {
            string stringValue when string.IsNullOrWhiteSpace(stringValue) => null,
            string stringValue => stringValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lines.Add($"{label}: {text}");
    }

    private static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }

    private static string? FormatSeconds(float? value)
    {
        return value.HasValue
            ? $"{value.Value.ToString("0.###", CultureInfo.InvariantCulture)} 秒"
            : null;
    }

    private static string? FormatByteSize(long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        var rawValue = value.Value;
        var compactText = rawValue switch
        {
            >= 1024L * 1024L => $"{rawValue / 1024d / 1024d:F1} MB",
            >= 1024L => $"{rawValue / 1024d:F1} KB",
            _ => $"{rawValue.ToString("N0", CultureInfo.InvariantCulture)} B"
        };

        return rawValue >= 1024L
            ? $"{compactText} ({rawValue.ToString("N0", CultureInfo.InvariantCulture)} 字节)"
            : compactText;
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
        int? CompressionFormat,
        long SerializedObjectSizeInBytes);

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
        public static AudioContainerProfile Unknown { get; } = new(AudioContainerKind.Unknown, "未知", null, false);

        public static AudioContainerProfile Wav { get; } = new(AudioContainerKind.Wav, "WAV", "audio/wav", true);

        public static AudioContainerProfile Ogg { get; } = new(AudioContainerKind.Ogg, "OGG", "audio/ogg", true);

        public static AudioContainerProfile Mp3 { get; } = new(AudioContainerKind.Mp3, "MP3", "audio/mpeg", true);

        public static AudioContainerProfile Flac { get; } = new(AudioContainerKind.Flac, "FLAC", "audio/flac", true);

        public static AudioContainerProfile Mp4 { get; } = new(AudioContainerKind.Mp4, "M4A / MP4", "audio/mp4", true);

        public static AudioContainerProfile Aac { get; } = new(AudioContainerKind.Aac, "AAC", "audio/aac", true);

        public static AudioContainerProfile Fsb5 { get; } = new(AudioContainerKind.Fsb5, "FSB5", null, false);
    }
}
