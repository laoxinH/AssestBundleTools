using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed class AssetPreviewStructuredTextLoader
{
    public const int MaxStructuredJsonCharacters = 16 * 1024 * 1024;

    private readonly IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> _loadPreviewTextChunkHandler;
    private readonly IAssetPreviewTextSourceReader _textSourceReader;

    public AssetPreviewStructuredTextLoader(
        IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>> loadPreviewTextChunkHandler)
    {
        _loadPreviewTextChunkHandler = loadPreviewTextChunkHandler
                                       ?? throw new ArgumentNullException(nameof(loadPreviewTextChunkHandler));
        _textSourceReader = new AssetPreviewTextSourceReader(_loadPreviewTextChunkHandler);
    }

    public IAssetPreviewTextSourceReader TextSourceReader => _textSourceReader;

    public bool TryCreateSource(
        Result<AssetPreviewDto> previewResult,
        string workspaceDataRootPath,
        out AssetPreviewTextSourceDto source) =>
        AssetPreviewStructuredTextReader.TryCreateTextSource(previewResult, workspaceDataRootPath, out source);

    public AssetPreviewTextSourceReadResult ReadText(
        AssetPreviewTextSourceDto source,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cancellationToken.IsCancellationRequested)
        {
            return AssetPreviewTextSourceReadResult.Cancelled;
        }

        if (!string.IsNullOrWhiteSpace(source.InlineText))
        {
            return source.InlineText.Length <= maxCharacters
                ? AssetPreviewTextSourceReadResult.Success(source.InlineText)
                : AssetPreviewTextSourceReadResult.Failure(Array.Empty<AppError>());
        }

        if (source.Reference is null ||
            string.IsNullOrWhiteSpace(source.WorkspaceDataRootPath) ||
            source.ContentLength is > 0 && source.ContentLength > maxCharacters)
        {
            return AssetPreviewTextSourceReadResult.Failure(Array.Empty<AppError>());
        }

        var builder = new System.Text.StringBuilder(capacity: Math.Min(maxCharacters, LoadPreviewTextChunkHandler.MaxChunkLength));
        var offset = 0;
        while (builder.Length <= maxCharacters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return AssetPreviewTextSourceReadResult.Cancelled;
            }

            var result = _loadPreviewTextChunkHandler.HandleAsync(
                    new LoadPreviewTextChunkQuery(
                        source.WorkspaceDataRootPath,
                        source.Reference,
                        offset,
                        Math.Min(LoadPreviewTextChunkHandler.MaxChunkLength, maxCharacters - builder.Length + 1)),
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (result.IsCancelled)
            {
                return AssetPreviewTextSourceReadResult.Cancelled;
            }

            if (!result.IsSuccess || result.Value is null)
            {
                return AssetPreviewTextSourceReadResult.Failure(result.Errors);
            }

            var chunk = result.Value;
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                builder.Append(chunk.Text);
            }

            if (builder.Length > maxCharacters)
            {
                return AssetPreviewTextSourceReadResult.Failure(Array.Empty<AppError>());
            }

            if (!chunk.HasMore || chunk.Length <= 0)
            {
                return AssetPreviewTextSourceReadResult.Success(builder.ToString());
            }

            offset = chunk.Offset + chunk.Length;
        }

        return AssetPreviewTextSourceReadResult.Failure(Array.Empty<AppError>());
    }

    public IReadOnlyList<SpineAtlasPageDto> ParseAtlas(
        AssetPreviewTextSourceDto source,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested ||
            !SpineAtlasTextParser.CanAttemptStructuredParse(source))
        {
            return Array.Empty<SpineAtlasPageDto>();
        }

        using var reader = _textSourceReader.OpenTextReader(source);
        return SpineAtlasTextParser.Parse(reader);
    }
}
