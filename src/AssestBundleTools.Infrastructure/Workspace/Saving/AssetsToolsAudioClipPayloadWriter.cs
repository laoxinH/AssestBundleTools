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

    public static Result<object> Apply(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath,
        AssetsToolsBundleResourceDataWriteContext resourceDataContext)
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
        if (!IsFsb5(payload))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.AudioPayloadRequiresFsb5"));
        }

        var originalSource = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(resourceField, "m_Source"));
        var appendResult = resourceDataContext.AppendPayload(originalSource, payload, writeItem.AssetName);
        if (appendResult.IsFailure)
        {
            return Result<object>.Failure(appendResult.Errors);
        }

        ApplyResourceReference(resourceField, appendResult.Value!);
        ApplyAudioClipEncodingHints(baseField);
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

    private static bool IsFsb5(byte[] content) =>
        content.Length >= 4 &&
        content[0] == (byte)'F' &&
        content[1] == (byte)'S' &&
        content[2] == (byte)'B' &&
        content[3] == (byte)'5';
}
