using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed class TextAssetEditDraftPatchState
{
    private IReadOnlyList<TextAssetEditDraftRangeEditDto>? _pendingRangeEdits;
    private TextAssetEditDraftPatchBaselineDto? _pendingPatchBaseline;
    private TextAssetEditDraftPatchBaselineDto? _savedPatchBaseline;

    public bool HasPendingRangeEdits => (_pendingRangeEdits?.Count ?? 0) > 0;

    public void Clear()
    {
        _pendingRangeEdits = null;
        _pendingPatchBaseline = null;
    }

    public void SetSavedBaseline(TextAssetEditDraftPatchBaselineDto baseline)
    {
        _savedPatchBaseline = baseline;
    }

    public void Set(
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        TextAssetEditDraftPatchBaselineDto baseline)
    {
        _pendingRangeEdits = rangeEdits.Count > 0
            ? rangeEdits.ToArray()
            : null;
        _pendingPatchBaseline = rangeEdits.Count > 0 ? baseline : null;
    }

    public IReadOnlyList<TextAssetEditDraftRangeEditDto> CreateSaveRangeEdits(
        string lastSavedText,
        string editableText)
    {
        var baseline = CreateBaseline(lastSavedText);
        return HasPendingRangeEdits &&
            _pendingPatchBaseline is not null &&
            baseline.ContentLength == _pendingPatchBaseline.ContentLength &&
            string.Equals(baseline.ContentSha256, _pendingPatchBaseline.ContentSha256, StringComparison.OrdinalIgnoreCase)
            ? _pendingRangeEdits!
            : [new TextAssetEditDraftRangeEditDto(0, baseline.ContentLength, editableText)];
    }

    public TextAssetEditDraftPatchBaselineDto CreateSavePatchBaseline(string lastSavedText) =>
        HasPendingRangeEdits && _pendingPatchBaseline is not null
            ? _pendingPatchBaseline
            : CreateBaseline(lastSavedText);

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

    private TextAssetEditDraftPatchBaselineDto CreateBaseline(string textContent) =>
        _savedPatchBaseline ?? TextAssetEditDraftPatchBaseline.Create(textContent);
}
