namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal sealed class JsonAssetEditDraftTextPieceReader : TextReader
{
    private readonly TextReader _baseReader;
    private readonly IReadOnlyList<JsonAssetEditDraftTextPiece> _pieces;
    private int _pieceIndex;
    private int _pieceOffset;
    private int _basePosition;
    private int? _peekedValue;

    public JsonAssetEditDraftTextPieceReader(
        TextReader baseReader,
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces)
        : this(
            baseReader,
            pieces,
            new JsonAssetEditDraftTextPieceCursor(0, 0, 0, 0),
            baseCharacterOffset: 0)
    {
    }

    public JsonAssetEditDraftTextPieceReader(
        TextReader baseReader,
        IReadOnlyList<JsonAssetEditDraftTextPiece> pieces,
        JsonAssetEditDraftTextPieceCursor cursor,
        int baseCharacterOffset)
    {
        _baseReader = baseReader;
        _pieces = pieces;
        _pieceIndex = cursor.PieceIndex;
        _pieceOffset = cursor.PieceOffset;
        _basePosition = baseCharacterOffset;
    }

    public override int Peek()
    {
        _peekedValue ??= ReadCore();
        return _peekedValue.Value;
    }

    public override int Read()
    {
        if (_peekedValue is { } value)
        {
            _peekedValue = null;
            return value;
        }

        return ReadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseReader.Dispose();
        }

        base.Dispose(disposing);
    }

    private int ReadCore()
    {
        while (_pieceIndex < _pieces.Count)
        {
            var piece = _pieces[_pieceIndex];
            if (_pieceOffset >= piece.Length)
            {
                _pieceIndex++;
                _pieceOffset = 0;
                continue;
            }

            if (piece.InsertedText is not null)
            {
                return piece.InsertedText[_pieceOffset++];
            }

            var targetBaseStart = piece.BaseStart + _pieceOffset;
            if (_basePosition < targetBaseStart)
            {
                SkipBase(targetBaseStart - _basePosition);
            }

            if (!JsonAssetEditDraftTextReadSupport.TryReadNormalizedCharacter(_baseReader, out var character))
            {
                return -1;
            }

            _basePosition++;
            _pieceOffset++;
            return character;
        }

        return -1;
    }

    private void SkipBase(int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (!JsonAssetEditDraftTextReadSupport.TryReadNormalizedCharacter(_baseReader, out _))
            {
                return;
            }

            _basePosition++;
        }
    }
}
