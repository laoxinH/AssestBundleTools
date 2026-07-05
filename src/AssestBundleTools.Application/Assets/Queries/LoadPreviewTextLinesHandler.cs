using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过 Application 边界读取完整文本预览载荷的有限行范围。
/// </summary>
public sealed class LoadPreviewTextLinesHandler : IQueryHandler<LoadPreviewTextLinesQuery, Result<AssetPreviewTextLineRangeDto>>
{
    public const int MaxLineCount = 512;

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IReplacementPreviewSourceProvider? _replacementPreviewSourceProvider;

    public LoadPreviewTextLinesHandler(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IReplacementPreviewSourceProvider? replacementPreviewSourceProvider = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider;
    }

    public Task<Result<AssetPreviewTextLineRangeDto>> HandleAsync(
        LoadPreviewTextLinesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.WorkspaceOpenDataRootPathRequired()));
        }

        if (query.Reference is null)
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        if (query.StartLineIndex < 0 || query.LineCount <= 0)
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid()));
        }

        var requestedLineCount = Math.Min(query.LineCount, MaxLineCount);
        return query.Reference.Kind switch
        {
            AssetPreviewFullContentReferenceKind.PreviewCache => LoadPreviewCacheTextLines(query, requestedLineCount),
            AssetPreviewFullContentReferenceKind.FileReplacement => LoadFileReplacementTextLinesAsync(
                query,
                requestedLineCount,
                cancellationToken),
            AssetPreviewFullContentReferenceKind.EditDraft => LoadEditDraftTextLines(query, requestedLineCount),
            _ => Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()))
        };
    }

    private Task<Result<AssetPreviewTextLineRangeDto>> LoadPreviewCacheTextLines(
        LoadPreviewTextLinesQuery query,
        int requestedLineCount)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.PreviewCacheKey))
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var lines = previewCacheStore.LoadTextLines(
            query.Reference.WorkspaceKey,
            query.Reference.PreviewCacheKey,
            query.StartLineIndex,
            requestedLineCount);
        if (lines is null ||
            !string.Equals(lines.Descriptor.PreviewKind, AssetPreviewKind.Text.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.PreviewCacheKey)));
        }

        var totalLineCount = Math.Max(lines.TotalLineCount, lines.StartLineIndex + lines.LineCount);
        var effectiveReference = query.Reference with
        {
            ContentType = lines.Descriptor.ContentType,
            ContentLength = lines.ContentLength
        };

        return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(new AssetPreviewTextLineRangeDto(
            effectiveReference,
            lines.Lines,
            lines.StartLineIndex,
            lines.LineCount,
            totalLineCount,
            lines.StartLineIndex + lines.LineCount < totalLineCount)));
    }

    private async Task<Result<AssetPreviewTextLineRangeDto>> LoadFileReplacementTextLinesAsync(
        LoadPreviewTextLinesQuery query,
        int requestedLineCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.SourcePath))
        {
            return Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid());
        }

        if (_replacementPreviewSourceProvider is null)
        {
            return Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath));
        }

        var lineResult = await _replacementPreviewSourceProvider.LoadFileTextLinesAsync(
            query.Reference.SourcePath,
            query.StartLineIndex,
            requestedLineCount,
            cancellationToken);
        if (lineResult.IsCancelled)
        {
            return Result<AssetPreviewTextLineRangeDto>.Cancelled();
        }

        if (!lineResult.IsSuccess || lineResult.Value is null)
        {
            return Result<AssetPreviewTextLineRangeDto>.Failure(
                lineResult.Errors.Count > 0
                    ? lineResult.Errors
                    : [ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.SourcePath)]);
        }

        var lines = lineResult.Value;
        var totalLineCount = Math.Max(lines.TotalLineCount, lines.StartLineIndex + lines.LineCount);
        var effectiveReference = query.Reference with
        {
            ContentType = lines.ContentType,
            ContentLength = lines.ContentLength,
            SourcePath = lines.SourcePath
        };

        return Result<AssetPreviewTextLineRangeDto>.Success(new AssetPreviewTextLineRangeDto(
            effectiveReference,
            lines.Lines,
            lines.StartLineIndex,
            lines.LineCount,
            totalLineCount,
            lines.StartLineIndex + lines.LineCount < totalLineCount));
    }

    private Task<Result<AssetPreviewTextLineRangeDto>> LoadEditDraftTextLines(
        LoadPreviewTextLinesQuery query,
        int requestedLineCount)
    {
        if (string.IsNullOrWhiteSpace(query.Reference.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftId) ||
            string.IsNullOrWhiteSpace(query.Reference.DraftMode))
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
        }

        var draftTextStore = _workspaceDataStoreFactory.CreateAssetEditDraftTextStore(query.WorkspaceDataRootPath);
        var lines = draftTextStore.LoadTextLines(
            query.Reference.WorkspaceKey,
            query.Reference.DraftId,
            query.Reference.DraftMode,
            query.StartLineIndex,
            requestedLineCount,
            query.Reference.DraftRevision);
        if (lines is null)
        {
            return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable(query.Reference.DraftId)));
        }

        var totalLineCount = Math.Max(lines.TotalLineCount, lines.StartLineIndex + lines.LineCount);
        var effectiveReference = query.Reference with
        {
            ContentType = lines.Descriptor.ContentType,
            ContentLength = lines.ContentLength,
            DraftId = query.Reference.DraftId,
            DraftMode = query.Reference.DraftMode,
            DraftRevision = query.Reference.DraftRevision
        };

        return Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(new AssetPreviewTextLineRangeDto(
            effectiveReference,
            lines.Lines,
            lines.StartLineIndex,
            lines.LineCount,
            totalLineCount,
            lines.StartLineIndex + lines.LineCount < totalLineCount)));
    }
}
