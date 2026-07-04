using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过 Application 边界在完整文本预览载荷中执行有界搜索。
/// </summary>
public sealed class SearchPreviewTextHandler : IQueryHandler<SearchPreviewTextQuery, Result<AssetPreviewTextSearchResultDto>>
{
    public const int MaxSearchTextLength = 256;
    public const int MaxMatchCount = 200;

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IReplacementPreviewSourceProvider? _replacementPreviewSourceProvider;

    public SearchPreviewTextHandler(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IReplacementPreviewSourceProvider? replacementPreviewSourceProvider = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider;
    }

    public Task<Result<AssetPreviewTextSearchResultDto>> HandleAsync(
        SearchPreviewTextQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.WorkspaceOpenDataRootPathRequired()));
        }

        if (query.Reference is null)
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        if (query.StartLineIndex < 0 || query.MaxMatchCount <= 0)
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid()));
        }

        if (!IsValidSearchText(query.SearchText))
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewTextSearchQueryInvalid()));
        }

        var requestedMatchCount = Math.Min(query.MaxMatchCount, MaxMatchCount);
        return query.Reference.Kind switch
        {
            AssetPreviewFullContentReferenceKind.PreviewCache => SearchPreviewCacheText(query, requestedMatchCount),
            AssetPreviewFullContentReferenceKind.FileReplacement => SearchFileReplacementTextAsync(
                query,
                requestedMatchCount,
                cancellationToken),
            AssetPreviewFullContentReferenceKind.EditDraft => SearchEditDraftText(query, requestedMatchCount),
            _ => Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()))
        };
    }

    private Task<Result<AssetPreviewTextSearchResultDto>> SearchPreviewCacheText(
        SearchPreviewTextQuery query,
        int requestedMatchCount)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.PreviewCacheKey))
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var searchResult = previewCacheStore.SearchText(
            query.Reference.WorkspaceKey,
            query.Reference.PreviewCacheKey,
            query.SearchText,
            query.StartLineIndex,
            requestedMatchCount,
            query.MatchCase);
        if (searchResult is null)
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.PreviewCacheKey)));
        }

        var effectiveReference = query.Reference with
        {
            ContentType = searchResult.Descriptor.ContentType,
            ContentLength = searchResult.ContentLength
        };

        return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Success(new AssetPreviewTextSearchResultDto(
            effectiveReference,
            query.SearchText,
            query.MatchCase,
            searchResult.Matches,
            searchResult.StartLineIndex,
            searchResult.TotalScannedLineCount,
            searchResult.HasMore)));
    }

    private async Task<Result<AssetPreviewTextSearchResultDto>> SearchFileReplacementTextAsync(
        SearchPreviewTextQuery query,
        int requestedMatchCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.SourcePath))
        {
            return Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid());
        }

        if (_replacementPreviewSourceProvider is null)
        {
            return Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath));
        }

        var searchResult = await _replacementPreviewSourceProvider.SearchFileTextAsync(
            query.Reference.SourcePath,
            query.SearchText,
            query.StartLineIndex,
            requestedMatchCount,
            query.MatchCase,
            cancellationToken);
        if (searchResult.IsCancelled)
        {
            return Result<AssetPreviewTextSearchResultDto>.Cancelled();
        }

        if (!searchResult.IsSuccess || searchResult.Value is null)
        {
            return Result<AssetPreviewTextSearchResultDto>.Failure(
                searchResult.Errors.Count > 0
                    ? searchResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath)]);
        }

        var result = searchResult.Value;
        var effectiveReference = query.Reference with
        {
            ContentType = result.ContentType,
            ContentLength = result.ContentLength,
            SourcePath = result.SourcePath
        };

        return Result<AssetPreviewTextSearchResultDto>.Success(new AssetPreviewTextSearchResultDto(
            effectiveReference,
            query.SearchText,
            query.MatchCase,
            result.Matches,
            result.StartLineIndex,
            result.TotalScannedLineCount,
            result.HasMore));
    }

    private Task<Result<AssetPreviewTextSearchResultDto>> SearchEditDraftText(
        SearchPreviewTextQuery query,
        int requestedMatchCount)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftId) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftMode))
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(query.WorkspaceDataRootPath);
        var result = draftTextStore.SearchText(
            query.Reference.WorkspaceKey,
            query.Reference.DraftId,
            query.Reference.DraftMode,
            query.SearchText,
            query.StartLineIndex,
            requestedMatchCount,
            query.MatchCase);
        if (result is null)
        {
            return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.DraftId)));
        }

        var effectiveReference = query.Reference with
        {
            ContentType = result.Descriptor.ContentType,
            ContentLength = result.ContentLength,
            DraftId = query.Reference.DraftId,
            DraftMode = query.Reference.DraftMode
        };

        return Task.FromResult(Result<AssetPreviewTextSearchResultDto>.Success(new AssetPreviewTextSearchResultDto(
            effectiveReference,
            query.SearchText,
            query.MatchCase,
            result.Matches,
            result.StartLineIndex,
            result.TotalScannedLineCount,
            result.HasMore)));
    }

    private static bool IsValidSearchText(string? searchText)
    {
        return !string.IsNullOrEmpty(searchText) &&
               searchText.Length <= MaxSearchTextLength &&
               searchText.IndexOfAny(['\r', '\n']) < 0;
    }
}
