using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed class JsonAssetEditDraftTextCompositeReader
{
    private readonly Func<TextReader?> _baseReaderFactory;
    private readonly JsonAssetEditDraftTextPatchManifestDto _manifest;
    private readonly IReadOnlyList<JsonAssetEditDraftTextPiece> _pieces;

    public JsonAssetEditDraftTextCompositeReader(
        Func<TextReader?> baseReaderFactory,
        JsonAssetEditDraftTextPatchManifestDto manifest)
    {
        _baseReaderFactory = baseReaderFactory ?? throw new ArgumentNullException(nameof(baseReaderFactory));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _pieces = JsonAssetEditDraftTextPieceTable.Build(manifest);
    }

    public PreviewCacheTextChunkDto? LoadTextChunk(
        PreviewCacheDescriptorDto descriptor,
        int offset,
        int length)
    {
        if (offset < 0 || length <= 0)
        {
            return null;
        }

        using var reader = CreateReader();
        if (reader is null)
        {
            return null;
        }

        var skipped = JsonAssetEditDraftTextReadSupport.SkipCharacters(reader, offset);
        var text = JsonAssetEditDraftTextReadSupport.ReadCharacters(reader, length, out var readLength);
        return new PreviewCacheTextChunkDto(
            descriptor,
            text,
            skipped,
            readLength,
            Math.Max(_manifest.TextLength, skipped + readLength),
            _manifest.ContentLength);
    }

    public PreviewCacheTextLineRangeDto? LoadTextLines(
        PreviewCacheDescriptorDto descriptor,
        int startLineIndex,
        int lineCount)
    {
        if (startLineIndex < 0 || lineCount <= 0)
        {
            return null;
        }

        using var reader = CreateReader();
        if (reader is null)
        {
            return null;
        }

        var lineReader = new NormalizedLineReader(reader);
        var skipped = JsonAssetEditDraftTextReadSupport.SkipLines(lineReader, startLineIndex);
        var lines = JsonAssetEditDraftTextReadSupport.ReadLines(lineReader, lineCount);
        return new PreviewCacheTextLineRangeDto(
            descriptor,
            lines,
            skipped,
            lines.Count,
            Math.Max(_manifest.LineCount, skipped + lines.Count),
            _manifest.ContentLength);
    }

    public PreviewCacheTextSearchResultDto? SearchText(
        PreviewCacheDescriptorDto descriptor,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase)
    {
        if (string.IsNullOrEmpty(searchText) ||
            startLineIndex < 0 ||
            maxMatchCount <= 0)
        {
            return null;
        }

        using var reader = CreateReader();
        if (reader is null)
        {
            return null;
        }

        var lineReader = new NormalizedLineReader(reader);
        var skipped = JsonAssetEditDraftTextReadSupport.SkipLines(lineReader, startLineIndex);
        var search = JsonAssetEditDraftTextReadSupport.SearchLines(
            lineReader,
            searchText,
            skipped,
            maxMatchCount,
            matchCase);
        return new PreviewCacheTextSearchResultDto(
            descriptor,
            search.Matches,
            skipped,
            search.ScannedLineCount,
            search.HasMore,
            _manifest.ContentLength);
    }

    public TextAssetEditDraftPatchBaselineDto? CreateBaseline()
    {
        using var reader = CreateReader();
        return reader is null
            ? null
            : JsonAssetEditDraftTextReadSupport.ComputeBaseline(reader);
    }

    public TextAssetEditDraftPatchStatisticsDto? CalculatePatchedStatistics(
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        int maxTextLength,
        out AssetEditDraftTextPatchResultDto? failure)
    {
        var pieces = JsonAssetEditDraftTextPieceTable.TryApplyRangeEdits(
            _pieces,
            rangeEdits ?? Array.Empty<TextAssetEditDraftRangeEditDto>(),
            _manifest.TextLength,
            out failure);
        if (pieces is null)
        {
            return null;
        }

        using var reader = CreateReader(pieces);
        if (reader is null)
        {
            failure = AssetEditDraftTextPatchResultDto.NotFound();
            return null;
        }

        return JsonAssetEditDraftTextReadSupport.CalculateStatistics(
            reader,
            maxTextLength,
            out failure);
    }

    public TextReader? CreateReader() => CreateReader(_pieces);

    private TextReader? CreateReader(IReadOnlyList<JsonAssetEditDraftTextPiece> pieces)
    {
        var baseReader = _baseReaderFactory();
        return baseReader is null
            ? null
            : new JsonAssetEditDraftTextPieceReader(baseReader, pieces);
    }
}

internal sealed record TextAssetEditDraftPatchStatisticsDto(
    int TextLength,
    int LineCount,
    long ContentLength,
    string ContentSha256);
