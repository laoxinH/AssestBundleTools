using System.Diagnostics;
using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Diagnostics;

internal static class TextEditRangePatchDiagnostics
{
    public static void Write(string message) =>
        Debug.WriteLine("[TextEditRangePatch] " + message);

    public static string FormatRangeEdits(IReadOnlyList<TextAssetEditDraftRangeEditDto>? edits)
    {
        if (edits is null || edits.Count == 0)
        {
            return "[]";
        }

        return "[" + string.Join(
            "; ",
            edits.Take(4).Select(edit =>
                $"start={edit.Start}, length={edit.Length}, textLength={NormalizeTextContent(edit.Text).Length}, head=\"{EscapePreview(edit.Text, 40)}\", tail=\"{EscapeTail(edit.Text, 40)}\"")) +
            (edits.Count > 4 ? $"; +{edits.Count - 4} more" : string.Empty) +
            "]";
    }

    public static string FormatHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return "(empty)";
        }

        return hash.Length <= 12 ? hash : hash[..12];
    }

    private static string EscapePreview(string? text, int maxLength)
    {
        var value = NormalizeTextContent(text ?? string.Empty);
        return value[..Math.Min(value.Length, maxLength)]
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string EscapeTail(string? text, int maxLength)
    {
        var value = NormalizeTextContent(text ?? string.Empty);
        var start = Math.Max(0, value.Length - maxLength);
        return value[start..]
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
