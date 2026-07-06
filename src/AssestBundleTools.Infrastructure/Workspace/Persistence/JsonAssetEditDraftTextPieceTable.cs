using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal static class JsonAssetEditDraftTextPieceTable
{
    public static IReadOnlyList<JsonAssetEditDraftTextPiece> Build(
        JsonAssetEditDraftTextPatchManifestDto manifest)
    {
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces =
        [
            JsonAssetEditDraftTextPiece.Base(0, manifest.BaseTextLength)
        ];
        var currentLength = manifest.BaseTextLength;
        foreach (var patch in manifest.Patches ?? Array.Empty<JsonAssetEditDraftTextPatchEntryDto>())
        {
            var nextPieces = TryApplyRangeEdits(
                pieces,
                patch.RangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>(),
                currentLength,
                out _);
            if (nextPieces is null)
            {
                return pieces;
            }

            pieces = nextPieces;
            currentLength = pieces.Sum(static piece => piece.Length);
        }

        return pieces;
    }

    public static JsonAssetEditDraftTextPieceCursor ResolveCursor(
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces,
        int targetOffset)
    {
        if (targetOffset <= 0)
        {
            return new JsonAssetEditDraftTextPieceCursor(0, 0, 0, 0);
        }

        var compositeOffset = 0;
        for (var index = 0; index < pieces.Count; index++)
        {
            var piece = pieces[index];
            var nextCompositeOffset = compositeOffset + piece.Length;
            if (targetOffset < nextCompositeOffset)
            {
                return new JsonAssetEditDraftTextPieceCursor(
                    index,
                    targetOffset - compositeOffset,
                    ResolveBasePosition(pieces, index, targetOffset - compositeOffset),
                    targetOffset);
            }

            compositeOffset = nextCompositeOffset;
        }

        return new JsonAssetEditDraftTextPieceCursor(
            pieces.Count,
            0,
            ResolveLastBaseEnd(pieces),
            compositeOffset);
    }

    private static int ResolveBasePosition(
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces,
        int pieceIndex,
        int pieceOffset)
    {
        var piece = pieces[pieceIndex];
        if (piece.InsertedText is null)
        {
            return piece.BaseStart + pieceOffset;
        }

        for (var index = pieceIndex + 1; index < pieces.Count; index++)
        {
            var nextPiece = pieces[index];
            if (nextPiece.InsertedText is null)
            {
                return nextPiece.BaseStart;
            }
        }

        return ResolveLastBaseEnd(pieces);
    }

    private static int ResolveLastBaseEnd(IReadOnlyList<JsonAssetEditDraftTextPiece> pieces)
    {
        for (var index = pieces.Count - 1; index >= 0; index--)
        {
            var piece = pieces[index];
            if (piece.InsertedText is null)
            {
                return piece.BaseStart + piece.Length;
            }
        }

        return 0;
    }

    public static IReadOnlyList<JsonAssetEditDraftTextPiece>? TryApplyRangeEdits(
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces,
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        int currentLength,
        out AssetEditDraftTextPatchResultDto? failure)
    {
        failure = null;
        var ordered = rangeEdits
            .OrderBy(static edit => edit.Start)
            .ToArray();
        var previousEnd = 0;
        foreach (var edit in ordered)
        {
            if (edit.Start < previousEnd ||
                edit.Start > currentLength ||
                edit.Length < 0 ||
                edit.Length > currentLength - edit.Start)
            {
                failure = AssetEditDraftTextPatchResultDto.RangeInvalid(edit.Start, edit.Length);
                return null;
            }

            previousEnd = edit.Start + edit.Length;
        }

        var result = new List<JsonAssetEditDraftTextPiece>();
        var position = 0;
        foreach (var edit in ordered)
        {
            AppendSlice(result, pieces, position, edit.Start - position);
            var normalizedText = NormalizeTextContent(edit.Text);
            if (normalizedText.Length > 0)
            {
                result.Add(JsonAssetEditDraftTextPiece.Insert(normalizedText));
            }

            position = edit.Start + edit.Length;
        }

        AppendSlice(result, pieces, position, currentLength - position);
        return result;
    }

    private static void AppendSlice(
        List<JsonAssetEditDraftTextPiece> result,
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces,
        int start,
        int length)
    {
        if (length <= 0)
        {
            return;
        }

        var remainingStart = start;
        var remainingLength = length;
        foreach (var piece in pieces)
        {
            if (remainingStart >= piece.Length)
            {
                remainingStart -= piece.Length;
                continue;
            }

            var take = Math.Min(piece.Length - remainingStart, remainingLength);
            result.Add(piece.Slice(remainingStart, take));
            remainingLength -= take;
            if (remainingLength <= 0)
            {
                return;
            }

            remainingStart = 0;
        }
    }

    private static string NormalizeTextContent(string textContent) =>
        (textContent ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}

internal sealed record JsonAssetEditDraftTextPiece(
    int BaseStart,
    int Length,
    string? InsertedText)
{
    public static JsonAssetEditDraftTextPiece Base(int start, int length) =>
        new(start, length, null);

    public static JsonAssetEditDraftTextPiece Insert(string text) =>
        new(0, text.Length, text);

    public JsonAssetEditDraftTextPiece Slice(int start, int length) =>
        InsertedText is null
            ? Base(BaseStart + start, length)
            : Insert(InsertedText.Substring(start, length));
}

internal readonly record struct JsonAssetEditDraftTextPieceCursor(
    int PieceIndex,
    int PieceOffset,
    int BasePosition,
    int CompositeOffset);
