using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupDetailsHandlerTests
{
    private static AssetPreviewDto CreateTextPreview(
        GetAssetPreviewQuery query,
        string text,
        string? fullTextContent = null,
        bool isDisplayTruncated = false,
        AssetPreviewFullContentReferenceDto? fullContentReference = null) =>
        CreatePreview(query, AssetPreviewKind.Text, text) with
        {
            FullTextContent = fullTextContent,
            FullContentReference = fullContentReference,
            TextDisplay = new AssetPreviewTextDisplayDto(
                DisplayCharacterCount: text.Length,
                FullCharacterCount: fullContentReference is not null
                    ? (int)Math.Min(fullContentReference.ContentLength, int.MaxValue)
                    : isDisplayTruncated ? text.Length + 1 : text.Length,
                DisplayLineCount: CountLines(text),
                EstimatedFullLineCount: CountLines(text) + (isDisplayTruncated ? 1 : 0),
                IsTruncated: isDisplayTruncated,
                MaxDisplayCharacters: AssetPreviewTextDisplayPolicy.MaxDisplayCharacters,
                MaxDisplayLineCount: AssetPreviewTextDisplayPolicy.MaxDisplayLineCount)
        };

    private static AssetPreviewDto CreateSummaryPreview(GetAssetPreviewQuery query, string text) =>
        CreatePreview(query, AssetPreviewKind.Summary, text);

    private static AssetPreviewDto CreateBinaryPreview(GetAssetPreviewQuery query, byte[] content) =>
        new(
            query.WorkspaceId,
            query.BundleId,
            query.AssetId,
            AssetPreviewKind.Binary,
            ContentType: "application/octet-stream",
            BinaryContent: content,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: null,
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: content.LongLength,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UnixEpoch,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));

    private static AssetPreviewDto CreateImagePreview(GetAssetPreviewQuery query, int width, int height) =>
        new(
            query.WorkspaceId,
            query.BundleId,
            query.AssetId,
            AssetPreviewKind.Image,
            ContentType: "image/png",
            BinaryContent: Array.Empty<byte>(),
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.TextureWidth,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: width),
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.TextureHeight,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: height),
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.ImageDimensions,
                    AssetPreviewInfoValueKind.Text,
                    TextValue: FormattableString.Invariant($"{width} x {height}"))
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UnixEpoch,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));

    private static AssetPreviewDto CreatePreview(
        GetAssetPreviewQuery query,
        AssetPreviewKind kind,
        string text) =>
        new(
            query.WorkspaceId,
            query.BundleId,
            query.AssetId,
            kind,
            ContentType: "text/plain",
            BinaryContent: null,
            TextContent: text,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: null,
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UnixEpoch,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lineCount = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    private static LoadSpineAssetGroupDetailsQuery CreateQuery(
        WorkspaceId workspaceId,
        BundleId spineBundleId) =>
        new(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\WorkspaceData",
            spineBundleId,
            GroupId: "spine-group:bundle.spine:assets/hero/hero");

    private static SpineAssetGroupDiagnosticDto FindDiagnostic(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        SpineAssetGroupDiagnosticKind kind)
    {
        var matches = diagnostics.Where(diagnostic => diagnostic.Kind == kind).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Expected exactly one {kind} diagnostic but found {matches.Length}.");
        }

        return matches[0];
    }

    private static void AssertUnsupportedFeature(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        SpineAnimationUnsupportedFeatureKind feature)
    {
        TestAssert.True(
            diagnostics.Any(diagnostic => diagnostic.AnimationUnsupportedFeature == feature),
            $"Expected unsupported Spine animation feature diagnostic: {feature}.");
    }

    private static AssetListResultDto CreateAssetList(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string bundleDisplayName,
        params AssetListItemDto[] items)
    {
        return new AssetListResultDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            BundleDisplayName: bundleDisplayName,
            SearchKeyword: null,
            AssetTypeFilter: null,
            TotalCount: items.Length,
            FilteredCount: items.Length,
            Items: items);
    }

    private static AssetListItemDto CreateItem(
        BundleId bundleId,
        string assetId,
        string assetPath,
        AssetType assetType)
    {
        var assetName = assetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        return new AssetListItemDto(
            AssetId: new AssetId(assetId),
            BundleId: bundleId,
            Name: assetName,
            AssetPath: assetPath,
            AssetType: assetType,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: assetType.ToString());
    }

    private sealed class TestAssetPreviewHandler : IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>>
    {
        private readonly Func<GetAssetPreviewQuery, Result<AssetPreviewDto>> _handler;

        public TestAssetPreviewHandler(Func<GetAssetPreviewQuery, Result<AssetPreviewDto>> handler)
        {
            _handler = handler;
        }

        public int HandleCallCount { get; private set; }

        public Task<Result<AssetPreviewDto>> HandleAsync(
            GetAssetPreviewQuery query,
            CancellationToken cancellationToken = default)
        {
            HandleCallCount++;
            return Task.FromResult(_handler(query));
        }
    }

    private sealed class TestSpineAnimationRuntimePreviewService : ISpineAnimationRuntimePreviewService
    {
        private readonly Func<SpineAnimationRuntimePreviewRequestDto, SpineAnimationRuntimePreviewResultDto> _handler;

        public TestSpineAnimationRuntimePreviewService(
            Func<SpineAnimationRuntimePreviewRequestDto, SpineAnimationRuntimePreviewResultDto> handler)
        {
            _handler = handler;
        }

        public int HandleCallCount { get; private set; }

        public SpineAnimationRuntimePreviewRequestDto? LastRequest { get; private set; }

        public Task<SpineAnimationRuntimePreviewResultDto> TryBuildPreviewAsync(
            SpineAnimationRuntimePreviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            HandleCallCount++;
            LastRequest = request;
            return Task.FromResult(cancellationToken.IsCancellationRequested
                ? SpineAnimationRuntimePreviewResultDto.Cancelled
                : _handler(request));
        }
    }

    private sealed class TestPreviewTextChunkHandler :
        IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>>
    {
        private readonly Dictionary<string, string> _textByKey = new(StringComparer.Ordinal);

        public int HandleCallCount { get; private set; }

        public void Add(string key, string text)
        {
            _textByKey[key] = text;
        }

        public Task<Result<AssetPreviewTextChunkDto>> HandleAsync(
            LoadPreviewTextChunkQuery query,
            CancellationToken cancellationToken = default)
        {
            HandleCallCount++;
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<AssetPreviewTextChunkDto>.Cancelled());
            }

            var key = query.Reference.PreviewCacheKey ??
                      query.Reference.SourcePath ??
                      query.Reference.DraftId ??
                      string.Empty;
            if (!_textByKey.TryGetValue(key, out var text))
            {
                return Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                    new Domain.Common.Errors.AppError(
                        "Test.TextMissing",
                        Domain.Common.Errors.ErrorCategory.NotFound,
                        Domain.Common.Errors.ErrorSeverity.Error,
                        "Test.TextMissing")));
            }

            var offset = Math.Clamp(query.Offset, 0, text.Length);
            var length = Math.Min(query.Length, text.Length - offset);
            var chunkText = length <= 0 ? string.Empty : text.Substring(offset, length);
            return Task.FromResult(Result<AssetPreviewTextChunkDto>.Success(new AssetPreviewTextChunkDto(
                query.Reference,
                chunkText,
                offset,
                chunkText.Length,
                text.Length,
                offset + chunkText.Length < text.Length)));
        }
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly WorkspaceId _workspaceId;
        private readonly IReadOnlyDictionary<string, AssetListResultDto> _assetListsByBundleId;
        private readonly IReadOnlyDictionary<string, AssetDetailsDto> _assetDetailsByAssetKey;

        public TestWorkspaceSessionRegistry(
            WorkspaceId workspaceId,
            IReadOnlyList<AssetListResultDto> assetLists,
            IReadOnlyList<AssetDetailsDto>? assetDetails = null)
        {
            _workspaceId = workspaceId;
            _assetListsByBundleId = assetLists.ToDictionary(
                static list => list.BundleId.Value,
                StringComparer.Ordinal);
            _assetDetailsByAssetKey = (assetDetails ?? Array.Empty<AssetDetailsDto>())
                .ToDictionary(
                    static details => CreateAssetKey(details.BundleId, details.AssetId),
                    StringComparer.OrdinalIgnoreCase);
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) =>
            _workspaceId.Equals(workspaceId);

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) =>
            ContainsWorkspace(workspaceId) &&
            _assetListsByBundleId.ContainsKey(bundleId.Value);

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            ContainsBundle(workspaceId, bundleId) &&
            _assetListsByBundleId[bundleId.Value].Items.Any(item => item.AssetId.Equals(assetId));

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            if (!ContainsWorkspace(workspaceId))
            {
                bundleTree = Array.Empty<BundleTreeNodeDto>();
                return false;
            }

            bundleTree = _assetListsByBundleId.Values
                .Select(static list => new BundleTreeNodeDto(
                    BundleId: list.BundleId,
                    DisplayName: list.BundleDisplayName,
                    LogicalPath: list.BundleDisplayName,
                    SourcePath: list.BundleDisplayName,
                    IsVirtualFolder: false,
                    AssetCount: list.TotalCount,
                    Children: Array.Empty<BundleTreeNodeDto>()))
                .ToArray();
            return true;
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult)
        {
            if (!ContainsWorkspace(workspaceId))
            {
                assetListResult = null;
                return false;
            }

            return _assetListsByBundleId.TryGetValue(bundleId.Value, out assetListResult);
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            if (!ContainsWorkspace(workspaceId))
            {
                assetDetails = null;
                return false;
            }

            return _assetDetailsByAssetKey.TryGetValue(
                CreateAssetKey(bundleId, assetId),
                out assetDetails);
        }

        private static string CreateAssetKey(BundleId bundleId, AssetId assetId) =>
            string.Concat(bundleId.Value, "\n", assetId.Value);

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = null;
            return false;
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            return false;
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = Array.Empty<WorkspaceReplacementCandidateDto>();
            return false;
        }

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            return false;
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            return false;
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = null;
            return false;
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            return false;
        }
    }
}
