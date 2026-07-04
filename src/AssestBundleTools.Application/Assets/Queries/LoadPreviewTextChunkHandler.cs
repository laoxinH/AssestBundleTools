using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过 Application 边界读取完整文本预览载荷的有限片段。
/// </summary>
public sealed class LoadPreviewTextChunkHandler : IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>>
{
    public const int MaxChunkLength = 32768;

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public LoadPreviewTextChunkHandler(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
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

        if (query.Reference is null ||
            query.Reference.Kind != AssetPreviewFullContentReferenceKind.PreviewCache ||
            string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.PreviewCacheKey))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        if (query.Offset < 0 || query.Length <= 0)
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid()));
        }

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var payload = previewCacheStore.Load(query.Reference.WorkspaceKey, query.Reference.PreviewCacheKey);
        if (payload is null ||
            !string.Equals(payload.Descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.PreviewCacheKey)));
        }

        var fullText = Encoding.UTF8.GetString(payload.Content)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var safeOffset = Math.Min(query.Offset, fullText.Length);
        var requestedLength = Math.Min(query.Length, MaxChunkLength);
        var safeLength = Math.Min(requestedLength, fullText.Length - safeOffset);
        var chunkText = safeLength <= 0
            ? string.Empty
            : fullText.Substring(safeOffset, safeLength);
        var totalLength = fullText.Length;
        var effectiveReference = query.Reference with
        {
            ContentType = payload.Descriptor.ContentType,
            ContentLength = payload.Content.LongLength
        };

        return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
            effectiveReference,
            chunkText,
            safeOffset,
            safeLength,
            totalLength,
            safeOffset + safeLength < totalLength)));
    }
}
