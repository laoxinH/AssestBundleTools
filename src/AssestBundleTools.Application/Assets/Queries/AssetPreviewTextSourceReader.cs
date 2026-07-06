using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

public sealed class AssetPreviewTextSourceReader : IAssetPreviewTextSourceReader
{
    private readonly IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> _loadPreviewTextChunkHandler;

    public AssetPreviewTextSourceReader(
        IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> loadPreviewTextChunkHandler)
    {
        _loadPreviewTextChunkHandler = loadPreviewTextChunkHandler
                                       ?? throw new ArgumentNullException(nameof(loadPreviewTextChunkHandler));
    }

    public TextReader OpenTextReader(AssetPreviewTextSourceDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return !string.IsNullOrEmpty(source.InlineText)
            ? new StringReader(source.InlineText)
            : new ChunkedTextReader(_loadPreviewTextChunkHandler, source);
    }

    private sealed class ChunkedTextReader : TextReader
    {
        private readonly IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> _chunkHandler;
        private readonly AssetPreviewTextSourceDto _source;
        private string _currentChunk = string.Empty;
        private int _currentChunkIndex;
        private int _nextOffset;
        private bool _isEndOfText;
        private bool _isDisposed;

        public ChunkedTextReader(
            IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> chunkHandler,
            AssetPreviewTextSourceDto source)
        {
            _chunkHandler = chunkHandler;
            _source = source;
        }

        public override int Peek()
        {
            ThrowIfDisposed();
            if (!EnsureChunk())
            {
                return -1;
            }

            return _currentChunk[_currentChunkIndex];
        }

        public override int Read()
        {
            ThrowIfDisposed();
            if (!EnsureChunk())
            {
                return -1;
            }

            return _currentChunk[_currentChunkIndex++];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (buffer.Length - index < count)
            {
                throw new ArgumentException("The destination range is outside the buffer bounds.", nameof(buffer));
            }

            ThrowIfDisposed();
            if (count == 0)
            {
                return 0;
            }

            var copied = 0;
            while (copied < count)
            {
                if (!EnsureChunk())
                {
                    break;
                }

                var available = _currentChunk.Length - _currentChunkIndex;
                var take = Math.Min(available, count - copied);
                _currentChunk.CopyTo(_currentChunkIndex, buffer, index + copied, take);
                _currentChunkIndex += take;
                copied += take;
            }

            return copied;
        }

        protected override void Dispose(bool disposing)
        {
            _isDisposed = true;
            base.Dispose(disposing);
        }

        private bool EnsureChunk()
        {
            if (_currentChunkIndex < _currentChunk.Length)
            {
                return true;
            }

            if (_isEndOfText)
            {
                return false;
            }

            if (_source.Reference is null || string.IsNullOrWhiteSpace(_source.WorkspaceDataRootPath))
            {
                throw new InvalidDataException(ApplicationErrors.AssetPreviewFullContentReferenceInvalid().Code);
            }

            var result = _chunkHandler.HandleAsync(new LoadPreviewTextChunkQuery(
                    _source.WorkspaceDataRootPath,
                    _source.Reference,
                    _nextOffset,
                    LoadPreviewTextChunkHandler.MaxChunkLength))
                .GetAwaiter()
                .GetResult();
            if (!result.IsSuccess || result.Value is null)
            {
                var error = result.Errors.FirstOrDefault();
                throw new InvalidDataException(error?.Code ?? ApplicationErrors.AssetPreviewFullContentReferenceInvalid().Code);
            }

            var chunk = result.Value;
            _currentChunk = chunk.Text ?? string.Empty;
            _currentChunkIndex = 0;
            _nextOffset = chunk.Offset + chunk.Length;
            _isEndOfText = !chunk.HasMore || chunk.Length <= 0;
            return _currentChunk.Length > 0;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ChunkedTextReader));
            }
        }
    }
}
