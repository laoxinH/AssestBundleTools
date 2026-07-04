using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 统一资源文本预览的可绑定展示预算。
/// </summary>
public static class AssetPreviewTextDisplayPolicy
{
    public const int MaxDisplayCharacters = 24000;
    public const int MaxDisplayLineCount = 1000;

    public static AssetPreviewTextDisplayResult CreateDisplay(string textContent)
    {
        ArgumentNullException.ThrowIfNull(textContent);

        var normalizedText = NormalizeLineEndings(textContent);
        var displayLimit = ResolveDisplayLimit(normalizedText);
        var displayText = displayLimit >= normalizedText.Length
            ? normalizedText
            : normalizedText[..displayLimit];
        var metadata = new AssetPreviewTextDisplayDto(
            DisplayCharacterCount: displayText.Length,
            FullCharacterCount: normalizedText.Length,
            DisplayLineCount: CountLines(displayText),
            EstimatedFullLineCount: CountLines(normalizedText),
            IsTruncated: displayText.Length < normalizedText.Length,
            MaxDisplayCharacters: MaxDisplayCharacters,
            MaxDisplayLineCount: MaxDisplayLineCount);

        return new AssetPreviewTextDisplayResult(displayText, metadata);
    }

    private static string NormalizeLineEndings(string textContent) =>
        textContent.Replace("\r\n", "\n").Replace('\r', '\n');

    private static int ResolveDisplayLimit(string normalizedText)
    {
        var characterLimit = Math.Min(normalizedText.Length, MaxDisplayCharacters);
        var lineLimit = ResolveLineLimit(normalizedText);
        return Math.Min(characterLimit, lineLimit);
    }

    private static int ResolveLineLimit(string normalizedText)
    {
        if (normalizedText.Length == 0)
        {
            return 0;
        }

        var currentLine = 1;
        for (var index = 0; index < normalizedText.Length; index++)
        {
            if (normalizedText[index] != '\n')
            {
                continue;
            }

            if (currentLine >= MaxDisplayLineCount)
            {
                return index;
            }

            currentLine++;
        }

        return normalizedText.Length;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lineCount = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }
}

public sealed record AssetPreviewTextDisplayResult(
    string TextContent,
    AssetPreviewTextDisplayDto Metadata);
