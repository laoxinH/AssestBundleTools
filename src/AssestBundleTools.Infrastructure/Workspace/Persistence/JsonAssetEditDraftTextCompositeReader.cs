using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed class JsonAssetEditDraftTextCompositeReader
{
    private readonly JsonAssetEditDraftTextBaseReaderFactory _baseReaderFactory;
    private readonly JsonAssetEditDraftTextPatchManifestDto _manifest;
    private readonly IReadOnlyList<JsonAssetEditDraftTextPiece> _pieces;

    public JsonAssetEditDraftTextCompositeReader(
        Func<TextReader?> baseReaderFactory,
        JsonAssetEditDraftTextPatchManifestDto manifest)
        : this(JsonAssetEditDraftTextBaseReaderFactory.FromTextReaderFactory(baseReaderFactory), manifest)
    {
    }

    public JsonAssetEditDraftTextCompositeReader(
        JsonAssetEditDraftTextBaseReaderFactory baseReaderFactory,
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

        var cursor = JsonAssetEditDraftTextPieceTable.ResolveCursor(_pieces, offset);
        using var reader = CreateReader(cursor);
        if (reader is null)
        {
            return null;
        }

        var text = JsonAssetEditDraftTextReadSupport.ReadCharacters(reader, length, out var readLength);
        return new PreviewCacheTextChunkDto(
            descriptor,
            text,
            cursor.CompositeOffset,
            readLength,
            Math.Max(_manifest.TextLength, cursor.CompositeOffset + readLength),
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

    private TextReader? CreateReader(JsonAssetEditDraftTextPieceCursor cursor)
    {
        var baseReader = _baseReaderFactory.CreateReader(cursor.BasePosition);
        return baseReader is null
            ? null
            : new JsonAssetEditDraftTextPieceReader(
                baseReader.Value.Reader,
                _pieces,
                cursor,
                baseReader.Value.CharacterOffset);
    }

    private TextReader? CreateReader(IReadOnlyList<JsonAssetEditDraftTextPiece> pieces)
    {
        var baseReader = _baseReaderFactory.CreateReader(characterOffset: 0);
        return baseReader is null
            ? null
            : new JsonAssetEditDraftTextPieceReader(baseReader.Value.Reader, pieces);
    }
}

internal sealed record TextAssetEditDraftPatchStatisticsDto(
    int TextLength,
    int LineCount,
    long ContentLength,
    string ContentSha256);

internal sealed class JsonAssetEditDraftTextBaseReaderFactory
{
    private readonly Func<int, JsonAssetEditDraftTextBaseReader?> _readerFactory;

    private JsonAssetEditDraftTextBaseReaderFactory(Func<int, JsonAssetEditDraftTextBaseReader?> readerFactory)
    {
        _readerFactory = readerFactory;
    }

    public static JsonAssetEditDraftTextBaseReaderFactory FromTextReaderFactory(Func<TextReader?> readerFactory)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        return new JsonAssetEditDraftTextBaseReaderFactory(_ =>
        {
            var reader = readerFactory();
            return reader is null
                ? null
                : new JsonAssetEditDraftTextBaseReader(reader, CharacterOffset: 0);
        });
    }

    public static JsonAssetEditDraftTextBaseReaderFactory FromUtf8File(
        string filePath,
        PreviewCacheDescriptorDto descriptor,
        long payloadLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(descriptor);
        return new JsonAssetEditDraftTextBaseReaderFactory(characterOffset =>
            CreateUtf8FileReader(filePath, descriptor, payloadLength, characterOffset));
    }

    public JsonAssetEditDraftTextBaseReader? CreateReader(int characterOffset) =>
        _readerFactory(Math.Max(0, characterOffset));

    private static JsonAssetEditDraftTextBaseReader? CreateUtf8FileReader(
        string filePath,
        PreviewCacheDescriptorDto descriptor,
        long payloadLength,
        int characterOffset)
    {
        try
        {
            var index = JsonPreviewCacheTextLineIndex.LoadOrCreate(
                descriptor,
                filePath,
                payloadLength);
            var anchor = JsonPreviewCacheTextLineIndex.ResolveCharacterAnchor(index, characterOffset);
            var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(filePath);
            if (anchor.ByteOffset > 0)
            {
                stream.Seek(anchor.ByteOffset, SeekOrigin.Begin);
            }

            return new JsonAssetEditDraftTextBaseReader(
                JsonAssetEditDraftTextSourceReader.CreateUtf8Reader(stream),
                anchor.CharacterOffset);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

internal readonly record struct JsonAssetEditDraftTextBaseReader(
    TextReader Reader,
    int CharacterOffset);
