namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class ApplicationUpdateReleaseNotesFormatter
{
    internal const int MaximumCharacterCount = 4000;
    internal const int MaximumLineCount = 40;

    public static string Format(string? releaseNotes, string emptyText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyText);
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return emptyText;
        }

        var normalizedLines = releaseNotes
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(NormalizeLine)
            .Take(MaximumLineCount)
            .ToArray();
        var normalized = string.Join(Environment.NewLine, normalizedLines).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return emptyText;
        }

        return normalized.Length <= MaximumCharacterCount
            ? normalized
            : normalized[..(MaximumCharacterCount - 1)] + "…";
    }

    private static string NormalizeLine(string line)
    {
        var visibleCharacters = line
            .Where(static character => !char.IsControl(character) || character == '\t')
            .ToArray();
        var normalized = new string(visibleCharacters).Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        normalized = normalized.TrimStart('#').TrimStart();
        if (normalized.StartsWith("- ", StringComparison.Ordinal) ||
            normalized.StartsWith("* ", StringComparison.Ordinal))
        {
            normalized = $"• {normalized[2..]}";
        }

        return normalized;
    }
}
