using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

public static class TextAssetEditDraftPatchBaseline
{
    public static TextAssetEditDraftPatchBaselineDto Create(string textContent)
    {
        var normalizedText = NormalizeTextContent(textContent ?? string.Empty);
        var bytes = Encoding.UTF8.GetBytes(normalizedText);
        return new TextAssetEditDraftPatchBaselineDto(
            normalizedText.Length,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    public static bool Matches(string textContent, TextAssetEditDraftPatchBaselineDto baseline)
    {
        var current = Create(textContent);
        return current.ContentLength == baseline.ContentLength &&
            string.Equals(current.ContentSha256, baseline.ContentSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
