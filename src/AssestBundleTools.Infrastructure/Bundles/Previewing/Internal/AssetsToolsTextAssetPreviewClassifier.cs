using System.Text;
using System.Text.Json;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

/// <summary>
/// 判断 TextAsset 中的脚本载荷应按文本展示，还是保留为二进制 payload。
/// </summary>
internal static class AssetsToolsTextAssetPreviewClassifier
{
    public static bool IsSpineSkeletonPath(string? assetPath, string? assetName)
    {
        var path = string.IsNullOrWhiteSpace(assetPath)
            ? assetName
            : assetPath;
        return !string.IsNullOrWhiteSpace(path) &&
               (path.Trim().EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
                path.Trim().EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryDecodeReadableTextPayload(byte[]? payload, out string textContent)
    {
        textContent = string.Empty;
        if (payload is not { Length: > 0 })
        {
            return false;
        }

        if (!TryDecodeStrictTextPayload(payload, out var decodedText))
        {
            return false;
        }

        decodedText = decodedText.TrimStart('\uFEFF').TrimEnd('\0');
        if (!IsReadableText(decodedText))
        {
            return false;
        }

        textContent = decodedText;
        return true;
    }

    public static bool TryDecodeJsonTextPayload(byte[]? payload, out string textContent)
    {
        if (!TryDecodeReadableTextPayload(payload, out textContent))
        {
            return false;
        }

        if (!IsJsonObjectText(textContent))
        {
            textContent = string.Empty;
            return false;
        }

        return true;
    }

    public static bool IsReadableTextContent(string? textContent) =>
        IsReadableText(textContent);

    public static bool IsJsonTextContent(string? textContent) =>
        IsReadableText(textContent) &&
        IsJsonObjectText(textContent);

    private static bool TryDecodeStrictTextPayload(byte[] payload, out string textContent)
    {
        textContent = string.Empty;
        try
        {
            if (payload.Length >= 2 &&
                payload[0] == 0xFF &&
                payload[1] == 0xFE)
            {
                textContent = new UnicodeEncoding(
                    bigEndian: false,
                    byteOrderMark: true,
                    throwOnInvalidBytes: true).GetString(payload);
                return true;
            }

            if (payload.Length >= 2 &&
                payload[0] == 0xFE &&
                payload[1] == 0xFF)
            {
                textContent = new UnicodeEncoding(
                    bigEndian: true,
                    byteOrderMark: true,
                    throwOnInvalidBytes: true).GetString(payload);
                return true;
            }

            textContent = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(payload);
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

    private static bool IsJsonObjectText(string? textContent)
    {
        if (!LooksLikeJsonObject(textContent) ||
            string.IsNullOrWhiteSpace(textContent))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(
                textContent,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeJsonObject(string? textContent)
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            return false;
        }

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
}
