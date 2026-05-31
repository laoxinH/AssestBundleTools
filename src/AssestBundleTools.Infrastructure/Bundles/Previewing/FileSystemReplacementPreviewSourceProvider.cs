using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
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
}
