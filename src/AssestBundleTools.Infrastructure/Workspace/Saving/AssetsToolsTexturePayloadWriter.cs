using AssetsTools.NET;
using AssetsTools.NET.Texture;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class AssetsToolsTexturePayloadWriter
{
    private const int TextureEncodeQuality = 90;
    private const TextureFormat TextureEncodeFallbackFormat = TextureFormat.RGBA32;

    public static Result<object> Apply(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath)
    {
        try
        {
            var texture = TextureFile.ReadTextureFile(baseField);
            try
            {
                texture.EncodeTextureImage(payloadPath, TextureEncodeQuality);
            }
            catch (NotSupportedException)
            {
                texture = TextureFile.ReadTextureFile(baseField);
                texture.m_TextureFormat = (int)TextureEncodeFallbackFormat;
                texture.EncodeTextureImage(payloadPath, TextureEncodeQuality);
            }

            texture.WriteTo(baseField);
            return Result<object>.Success(new object());
        }
        catch (Exception exception) when (BundleExportWriteDiagnostics.IsDiagnosableWriteException(exception))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    BuildTextureFailureDetail(baseField, writeItem, payloadPath, exception)));
        }
    }

    private static string BuildTextureFailureDetail(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath,
        Exception exception)
    {
        var textureFormat = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_TextureFormat"));
        var width = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Width"));
        var height = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Height"));
        return string.Join(
            "; ",
            new[]
            {
                "Save.BundleWrite.TextureEncodeFailed",
                $"asset={writeItem.AssetName}",
                $"pathId={writeItem.PathId}",
                $"format={FormatTextureFormat(textureFormat)}",
                $"size={FormatTextureSize(width, height)}",
                $"payload={Path.GetFileName(payloadPath)}",
                $"process={BundleExportWriteDiagnostics.FormatProcessArchitecture()}",
                BundleExportWriteDiagnostics.FormatManagedMemory(),
                BundleExportWriteDiagnostics.FormatExceptionDiagnostic(exception)
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatTextureFormat(int? textureFormat)
    {
        if (!textureFormat.HasValue)
        {
            return "unknown";
        }

        return Enum.IsDefined(typeof(TextureFormat), textureFormat.Value)
            ? $"{(TextureFormat)textureFormat.Value}({textureFormat.Value})"
            : textureFormat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatTextureSize(int? width, int? height) =>
        width.HasValue && height.HasValue
            ? $"{width.Value}x{height.Value}"
            : "unknown";
}
