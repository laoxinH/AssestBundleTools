using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 解析修改阶段的当前有效替换，并把替换来源投影为目标资产预览。
/// </summary>
internal sealed class ReplacementEffectivePreviewResolver
{
    private const string TextContentType = "text/plain; charset=utf-8";

    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IReplacementPreviewSourceProvider? _replacementPreviewSourceProvider;

    public ReplacementEffectivePreviewResolver(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IReplacementPreviewSourceProvider? replacementPreviewSourceProvider)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider;
    }

    public async Task<Result<AssetPreviewDto>?> TryLoadAsync(
        GetAssetPreviewQuery query,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        HashSet<string> replacementPreviewStack,
        Func<GetAssetPreviewQuery, HashSet<string>, CancellationToken, Task<Result<AssetPreviewDto>>> loadPreviewAsync,
        CancellationToken cancellationToken)
    {
        if (ParseAssetType(assetCacheEntry.AssetType) != AssetType.Texture2D ||
            !_workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                query.BundleId,
                query.AssetId,
                out var details) ||
            details is null)
        {
            return null;
        }

        var targetKey = CreateTargetKey(query.BundleId.Value, query.AssetId.Value);
        if (!replacementPreviewStack.Add(targetKey))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(details.FileReplacementSourcePath))
            {
                return await TryLoadFileReplacementPreviewAsync(
                    query,
                    details,
                    details.FileReplacementSourcePath,
                    cancellationToken);
            }

            if (details.WorkspaceReplacementSourceBundleId is { } sourceBundleId &&
                details.WorkspaceReplacementSourceAssetId is { } sourceAssetId)
            {
                return await TryLoadWorkspaceReplacementPreviewAsync(
                    query,
                    sourceBundleId,
                    sourceAssetId,
                    replacementPreviewStack,
                    loadPreviewAsync,
                    cancellationToken);
            }
        }
        finally
        {
            replacementPreviewStack.Remove(targetKey);
        }

        return null;
    }

    private async Task<Result<AssetPreviewDto>?> TryLoadFileReplacementPreviewAsync(
        GetAssetPreviewQuery query,
        AssetDetailsDto details,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (_replacementPreviewSourceProvider is null)
        {
            return null;
        }

        var previewContentResult = await _replacementPreviewSourceProvider.LoadFilePreviewAsync(
            details,
            sourceFilePath,
            cancellationToken);
        if (previewContentResult.IsCancelled)
        {
            return Result<AssetPreviewDto>.Cancelled();
        }

        if (!previewContentResult.IsSuccess || previewContentResult.Value is null)
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.GenerationFailed,
                new AssetPreviewDiagnosticDto(
                    AssetPreviewCacheStatusKind.Missing,
                    AssetPreviewFallbackReasonKind.GenerationFailed,
                    AppErrorLocalizedMessageMapper.CreateFromFirstError(previewContentResult.Errors))));
        }

        return Result<AssetPreviewDto>.Success(MapReplacementPreviewDto(
            query,
            previewContentResult.Value,
            sourceFilePath,
            isFromCache: false));
    }

    private async Task<Result<AssetPreviewDto>?> TryLoadWorkspaceReplacementPreviewAsync(
        GetAssetPreviewQuery targetQuery,
        BundleId sourceBundleId,
        AssetId sourceAssetId,
        HashSet<string> replacementPreviewStack,
        Func<GetAssetPreviewQuery, HashSet<string>, CancellationToken, Task<Result<AssetPreviewDto>>> loadPreviewAsync,
        CancellationToken cancellationToken)
    {
        var sourceQuery = targetQuery with
        {
            BundleId = sourceBundleId,
            AssetId = sourceAssetId
        };
        var sourcePreviewResult = await loadPreviewAsync(
            sourceQuery,
            replacementPreviewStack,
            cancellationToken);
        if (!sourcePreviewResult.IsSuccess || sourcePreviewResult.Value is null)
        {
            return sourcePreviewResult;
        }

        return Result<AssetPreviewDto>.Success(sourcePreviewResult.Value with
        {
            WorkspaceId = targetQuery.WorkspaceId,
            BundleId = targetQuery.BundleId,
            AssetId = targetQuery.AssetId
        });
    }

    private static AssetPreviewDto MapReplacementPreviewDto(
        GetAssetPreviewQuery query,
        AssetPreviewContentDto previewContent,
        string? previewPayloadFilePath,
        bool isFromCache)
    {
        var usesBinaryPayload = previewContent.Kind is
            AssetPreviewKind.Image or
            AssetPreviewKind.Audio or
            AssetPreviewKind.Binary;
        var fullTextContent = usesBinaryPayload
            ? null
            : Encoding.UTF8.GetString(previewContent.Content);

        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: previewContent.Kind,
            ContentType: previewContent.ContentType,
            BinaryContent: usesBinaryPayload ? previewContent.Content : null,
            TextContent: fullTextContent,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems: previewContent.PreviewInfoItems,
            PreviewMessage: previewContent.PreviewMessage,
            PreferredDisplaySizeInBytes: previewContent.PreferredDisplaySizeInBytes,
            IsFromCache: isFromCache,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            ImageOverlay: previewContent.ImageOverlay,
            ImageFocus: previewContent.ImageFocus)
        {
            FullTextContent = fullTextContent
        };
    }

    private static AssetPreviewDto BuildFallbackSummaryPreview(
        GetAssetPreviewQuery query,
        AssetPreviewMessageKey messageKey,
        AssetPreviewDiagnosticDto diagnostic)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: new AssetPreviewMessageDto(messageKey),
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: diagnostic,
            ImageOverlay: null,
            ImageFocus: null);
    }

    private static AssetType ParseAssetType(string assetType)
    {
        return Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType)
            ? parsedAssetType
            : AssetType.Unknown;
    }

    private static string CreateTargetKey(string bundleId, string assetId) =>
        $"{bundleId}\u001f{assetId}";
}
