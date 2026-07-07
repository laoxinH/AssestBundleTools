using AssetsTools.NET;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class AssetsToolsAudioClipPayloadWriter
{
    private const int AudioClipLoadTypeDecompressOnLoad = 0;
    private const int AudioClipCompressionFormatVorbis = 1;
    private const int AudioPayloadHeaderProbeSize = 64;

    public static Result<object> Apply(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath,
        AssetsToolsBundleResourceDataWriteContext resourceDataContext,
        WorkspaceBundleExportAudioWriteModeDto audioWriteMode)
    {
        if (writeItem.AssetType != AssetType.AudioClip)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.UnsupportedAudioClipPayload"));
        }

        var resourceField = AssetsToolsFieldReader.TryGetField(baseField, "m_Resource");
        if (resourceField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.AudioResourceUnavailable"));
        }

        var payload = File.ReadAllBytes(payloadPath);
        var replacementContainer = DetectContainer(payload);
        var originalContainer = ResolveOriginalContainer(baseField, resourceField, resourceDataContext);
        if (!CanWritePayload(originalContainer, replacementContainer, audioWriteMode))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    BuildContainerMismatchDetail(originalContainer, replacementContainer)));
        }

        var originalSource = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(resourceField, "m_Source"));
        var appendResult = resourceDataContext.AppendPayload(originalSource, payload, writeItem.AssetName);
        if (appendResult.IsFailure)
        {
            return Result<object>.Failure(appendResult.Errors);
        }

        ApplyResourceReference(resourceField, appendResult.Value!);
        if (audioWriteMode == WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer)
        {
            ApplyAudioClipEncodingHints(baseField);
        }

        ClearInlineAudioData(baseField);

        return Result<object>.Success(new object());
    }

    private static void ApplyResourceReference(
        AssetTypeValueField resourceField,
        BundleResourceAppendResult appendResult)
    {
        var sourceField = AssetsToolsFieldReader.TryGetField(resourceField, "m_Source");
        if (sourceField is not null)
        {
            sourceField.AsString = appendResult.ResourceSource;
        }

        SetInt64(resourceField, "m_Offset", appendResult.Offset);
        SetInt64(resourceField, "m_Size", appendResult.Size);
    }

    private static void ApplyAudioClipEncodingHints(AssetTypeValueField baseField)
    {
        var loadTypeField = AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType");
        if (loadTypeField is not null)
        {
            loadTypeField.AsInt = AudioClipLoadTypeDecompressOnLoad;
        }

        var compressionFormatField = AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat");
        if (compressionFormatField is not null)
        {
            compressionFormatField.AsInt = AudioClipCompressionFormatVorbis;
        }
    }

    private static void ClearInlineAudioData(AssetTypeValueField baseField)
    {
        var audioDataField = AssetsToolsFieldReader.TryGetField(baseField, "m_AudioData");
        if (audioDataField is null)
        {
            return;
        }

        try
        {
            audioDataField.AsByteArray = Array.Empty<byte>();
        }
        catch (Exception)
        {
        }
    }

    private static void SetInt64(
        AssetTypeValueField parent,
        string fieldName,
        long value)
    {
        var field = AssetsToolsFieldReader.TryGetField(parent, fieldName);
        if (field is not null)
        {
            field.AsLong = value;
        }
    }

    private static bool CanWritePayload(
        AudioPayloadContainer originalContainer,
        AudioPayloadContainer replacementContainer,
        WorkspaceBundleExportAudioWriteModeDto audioWriteMode)
    {
        if (audioWriteMode == WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer)
        {
            return replacementContainer != AudioPayloadContainer.Unknown;
        }

        return originalContainer != AudioPayloadContainer.Unknown &&
               originalContainer == replacementContainer;
    }

    private static AudioPayloadContainer ResolveOriginalContainer(
        AssetTypeValueField baseField,
        AssetTypeValueField resourceField,
        AssetsToolsBundleResourceDataWriteContext resourceDataContext)
    {
        var inlinePayload = AssetsToolsFieldReader.ReadByteArray(AssetsToolsFieldReader.TryGetField(baseField, "m_AudioData"));
        if (inlinePayload is { Length: > 0 })
        {
            return DetectContainer(inlinePayload);
        }

        var originalSource = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(resourceField, "m_Source"));
        var originalOffset = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Offset"));
        var originalSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Size"));
        return originalOffset.HasValue &&
               originalSize.HasValue &&
               resourceDataContext.TryReadPayloadHeader(
                   originalSource,
                   originalOffset.Value,
                   originalSize.Value,
                   AudioPayloadHeaderProbeSize,
                   out var header)
            ? DetectContainer(header)
            : AudioPayloadContainer.Unknown;
    }

    private static string BuildContainerMismatchDetail(
        AudioPayloadContainer originalContainer,
        AudioPayloadContainer replacementContainer) =>
        $"Save.BundleWrite.AudioPayloadContainerMismatch; Original={originalContainer}; Replacement={replacementContainer}";

    private static AudioPayloadContainer DetectContainer(byte[] content)
    {
        if (IsWave(content))
        {
            return AudioPayloadContainer.Wav;
        }

        if (StartsWithAscii(content, "OggS"))
        {
            return AudioPayloadContainer.Ogg;
        }

        if (StartsWithAscii(content, "fLaC"))
        {
            return AudioPayloadContainer.Flac;
        }

        if (IsMp4(content))
        {
            return AudioPayloadContainer.Mp4;
        }

        if (StartsWithAscii(content, "FSB5"))
        {
            return AudioPayloadContainer.Fsb5;
        }

        if (StartsWithAscii(content, "ID3") || HasFrameSync(content))
        {
            return AudioPayloadContainer.Mp3OrAac;
        }

        return AudioPayloadContainer.Unknown;
    }

    private static bool IsWave(byte[] content) =>
        content.Length >= 12 &&
        StartsWithAscii(content, "RIFF") &&
        content[8] == (byte)'W' &&
        content[9] == (byte)'A' &&
        content[10] == (byte)'V' &&
        content[11] == (byte)'E';

    private static bool IsMp4(byte[] content) =>
        content.Length >= 12 &&
        content[4] == (byte)'f' &&
        content[5] == (byte)'t' &&
        content[6] == (byte)'y' &&
        content[7] == (byte)'p';

    private static bool HasFrameSync(byte[] content) =>
        content.Length >= 2 &&
        content[0] == 0xFF &&
        (content[1] & 0xE0) == 0xE0;

    private static bool StartsWithAscii(byte[] content, string value)
    {
        if (content.Length < value.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (content[index] != (byte)value[index])
            {
                return false;
            }
        }

        return true;
    }

    private enum AudioPayloadContainer
    {
        Unknown = 0,
        Wav = 1,
        Ogg = 2,
        Mp3OrAac = 3,
        Flac = 4,
        Mp4 = 5,
        Fsb5 = 6
    }
}
