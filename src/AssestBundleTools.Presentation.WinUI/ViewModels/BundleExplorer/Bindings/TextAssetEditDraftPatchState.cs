using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed class TextAssetEditDraftPatchState
{
    private IReadOnlyList<TextAssetEditDraftRangeEditDto>? _pendingRangeEdits;
    private int? _pendingRangeEditBaseLength;

    public bool HasPendingRangeEdits => (_pendingRangeEdits?.Count ?? 0) > 0;

    public void Clear()
    {
        _pendingRangeEdits = null;
        _pendingRangeEditBaseLength = null;
    }

    public void Set(
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        int baseLength)
    {
        _pendingRangeEdits = rangeEdits.Count > 0
            ? rangeEdits.ToArray()
            : null;
        _pendingRangeEditBaseLength = rangeEdits.Count > 0 ? baseLength : null;
    }

    public static bool IsMatchingBaseLength(string lastSavedText, int baseLength) =>
        NormalizeTextContent(lastSavedText).Length == baseLength;

    public IReadOnlyList<TextAssetEditDraftRangeEditDto> CreateSaveRangeEdits(
        string lastSavedText,
        string editableText)
    {
        var normalizedSavedText = NormalizeTextContent(lastSavedText);
        return HasPendingRangeEdits && _pendingRangeEditBaseLength == normalizedSavedText.Length
            ? _pendingRangeEdits!
            : [new TextAssetEditDraftRangeEditDto(0, normalizedSavedText.Length, editableText)];
    }

    public string? TryApplyToSavedText(string lastSavedText)
    {
        if (!HasPendingRangeEdits)
        {
            return lastSavedText;
        }

        var normalizedText = NormalizeTextContent(lastSavedText);
        var builder = new System.Text.StringBuilder(normalizedText);
        var orderedEdits = _pendingRangeEdits!
            .OrderByDescending(static edit => edit.Start)
            .ToArray();
        var nextStart = normalizedText.Length;

        foreach (var edit in orderedEdits)
        {
            if (edit.Start > normalizedText.Length ||
                edit.Length > normalizedText.Length - edit.Start ||
                edit.Start + edit.Length > nextStart)
            {
                return null;
            }

            builder.Remove(edit.Start, edit.Length);
            builder.Insert(edit.Start, NormalizeTextContent(edit.Text));
            nextStart = edit.Start;
        }

        return builder.ToString();
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
