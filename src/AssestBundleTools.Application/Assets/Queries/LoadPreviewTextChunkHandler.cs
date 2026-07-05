using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过 Application 边界读取完整文本预览载荷的有限片段。
/// </summary>
public sealed class LoadPreviewTextChunkHandler : IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>>
{
    public const int MaxChunkLength = 32768;

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IReplacementPreviewSourceProvider? _replacementPreviewSourceProvider;

    public LoadPreviewTextChunkHandler(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IReplacementPreviewSourceProvider? replacementPreviewSourceProvider = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider;
    }

    public Task<Result<AssetPreviewTextChunkDto>> HandleAsync(
        LoadPreviewTextChunkQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.WorkspaceOpenDataRootPathRequired()));
        }

        if (query.Reference is null)
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        if (query.Offset < 0 || query.Length <= 0)
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid()));
        }

        var requestedLength = Math.Min(query.Length, MaxChunkLength);
        return query.Reference.Kind switch
        {
            AssetPreviewFullContentReferenceKind.PreviewCache => LoadPreviewCacheTextChunk(query, requestedLength),
            AssetPreviewFullContentReferenceKind.FileReplacement => LoadFileReplacementTextChunkAsync(
                query,
                requestedLength,
                cancellationToken),
            AssetPreviewFullContentReferenceKind.EditDraft => LoadEditDraftTextChunk(query, requestedLength),
            _ => Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()))
        };
    }

    private Task<Result<AssetPreviewTextChunkDto>> LoadPreviewCacheTextChunk(
        LoadPreviewTextChunkQuery query,
        int requestedLength)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.PreviewCacheKey))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var chunk = previewCacheStore.LoadTextChunk(
            query.Reference.WorkspaceKey,
            query.Reference.PreviewCacheKey,
            query.Offset,
            requestedLength);
        if (chunk is null ||
            !string.Equals(chunk.Descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.PreviewCacheKey)));
        }

        var totalLength = ResolveTotalLength(chunk);
        var effectiveReference = query.Reference with
        {
            ContentType = chunk.Descriptor.ContentType,
            ContentLength = chunk.ContentLength
        };

        return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
            effectiveReference,
            chunk.Text,
            chunk.Offset,
            chunk.Length,
            totalLength,
            chunk.Offset + chunk.Length < totalLength)));
    }

    private async Task<Result<AssetPreviewTextChunkDto>> LoadFileReplacementTextChunkAsync(
        LoadPreviewTextChunkQuery query,
        int requestedLength,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.SourcePath))
        {
            return Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid());
        }

        if (_replacementPreviewSourceProvider is null)
        {
            return Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath));
        }

        var chunkResult = await _replacementPreviewSourceProvider.LoadFileTextChunkAsync(
            query.Reference.SourcePath,
            query.Offset,
            requestedLength,
            cancellationToken);
        if (chunkResult.IsCancelled)
        {
            return Result<AssetPreviewTextChunkDto>.Cancelled();
        }

        if (!chunkResult.IsSuccess || chunkResult.Value is null)
        {
            return Result<AssetPreviewTextChunkDto>.Failure(
                chunkResult.Errors.Count > 0
                    ? chunkResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath)]);
        }

        var chunk = chunkResult.Value;
        var totalLength = Math.Max(chunk.TotalLength, chunk.Offset + chunk.Length);
        var effectiveReference = query.Reference with
        {
            ContentType = chunk.ContentType,
            ContentLength = chunk.ContentLength,
            SourcePath = chunk.SourcePath
        };

        return Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
            effectiveReference,
            chunk.Text,
            chunk.Offset,
            chunk.Length,
            totalLength,
            chunk.Offset + chunk.Length < totalLength));
    }

    private Task<Result<AssetPreviewTextChunkDto>> LoadEditDraftTextChunk(
        LoadPreviewTextChunkQuery query,
        int requestedLength)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftId) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftMode))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(query.WorkspaceDataRootPath);
        var chunk = draftTextStore.LoadTextChunk(
            query.Reference.WorkspaceKey,
            query.Reference.DraftId,
            query.Reference.DraftMode,
            query.Offset,
            requestedLength,
            query.Reference.DraftRevision);
        if (chunk is null)
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.DraftId)));
        }

        var totalLength = ResolveTotalLength(chunk);
        var effectiveReference = query.Reference with
        {
            ContentType = chunk.Descriptor.ContentType,
            ContentLength = chunk.ContentLength,
            DraftId = query.Reference.DraftId,
            DraftMode = query.Reference.DraftMode,
            DraftRevision = query.Reference.DraftRevision
        };

        return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
            effectiveReference,
            chunk.Text,
            chunk.Offset,
            chunk.Length,
            totalLength,
            chunk.Offset + chunk.Length < totalLength)));
    }

    private static int ResolveTotalLength(PreviewCacheTextChunkDto chunk)
    {
        var minimumLength = chunk.Offset + chunk.Length;
        var metadataLength = chunk.Descriptor.PreviewInfoItems?
            .FirstOrDefault(static item => item.Key == AssetPreviewInfoKey.TextFullCharacterCount)
            ?.Int64Value;
        if (metadataLength is >= 0 and <= int.MaxValue)
        {
            return Math.Max((int)metadataLength.Value, minimumLength);
        }

        return Math.Max(chunk.TotalLength, minimumLength);
    }
}
