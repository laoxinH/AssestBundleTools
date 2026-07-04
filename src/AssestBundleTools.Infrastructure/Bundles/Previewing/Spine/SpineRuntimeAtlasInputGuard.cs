namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal static class SpineRuntimeAtlasInputGuard
{
    private const int MaxRuntimeAtlasTextCharacters = 4_000_000;
    private const int MaxRuntimeAtlasLineCount = 120_000;

    public static bool CanAttemptRuntimeParse(IReadOnlyList<string>? atlasTexts)
    {
        if (atlasTexts is not { Count: > 0 })
        {
            return false;
        }

        var hasCandidate = false;
        foreach (var atlasText in atlasTexts)
        {
            if (string.IsNullOrWhiteSpace(atlasText))
            {
                continue;
            }

            hasCandidate = true;
            if (!CanAttemptRuntimeParse(atlasText))
            {
                return false;
            }
        }

        return hasCandidate;
    }

    private static bool CanAttemptRuntimeParse(string atlasText)
    {
        if (atlasText.Length > MaxRuntimeAtlasTextCharacters ||
            LooksLikeJsonObject(atlasText))
        {
            return false;
        }

        return CountLinesUpToLimit(atlasText, MaxRuntimeAtlasLineCount + 1) <= MaxRuntimeAtlasLineCount;
    }

    private static bool LooksLikeJsonObject(string atlasText)
    {
        foreach (var character in atlasText)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '{';
        }

        return false;
    }

    private static int CountLinesUpToLimit(string text, int limit)
    {
        var count = 1;
        foreach (var character in text)
        {
            if (character != '\n')
            {
                continue;
            }

            count++;
            if (count >= limit)
            {
                return count;
            }
        }

        return count;
    }
}
