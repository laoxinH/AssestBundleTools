using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 通过 Application 预览管线加载 Spine 资源组详情，避免 UI 直接读取 atlas 文本。
/// </summary>
internal sealed class SpineAssetGroupDetailsLoader : ISpineAssetGroupDetailsLoader
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly ILocalizationService? _localizationService;

    public SpineAssetGroupDetailsLoader(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceContextService workspaceContextService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBackgroundTaskRunner backgroundTaskRunner,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        ILocalizationService? localizationService = null)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        ArgumentNullException.ThrowIfNull(backgroundTaskRunner);
        _backgroundWorkScheduler = backgroundWorkScheduler ?? new BackgroundWorkScheduler(
            backgroundTaskRunner,
            NullAppBackgroundTaskStatusService.Instance);
        _localizationService = localizationService;
    }

    public async Task<SpineAssetGroupDetailsLoadResult?> LoadAsync(
        SpineAssetGroupDocumentTabViewModel groupTab,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        if (!TryCreateQuery(groupTab, out var query))
        {
            return null;
        }

        try
        {
            var result = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<SpineAssetGroupDetailsDto>>(
                    BackgroundWorkProfile.PreviewForeground,
                    context => _workspaceAssetInspectionGateway.LoadSpineAssetGroupDetailsAsync(query, context.CancellationToken))
                {
                    SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                    Priority = BackgroundWorkPriority.UserBlocking,
                    WorkspaceId = query.WorkspaceId.Value,
                    WorkspaceKey = query.WorkspaceKey,
                    BundleCacheKey = query.BundleId.Value,
                    ResourceKey = query.GroupId,
                    StatusTitle = CreatePreviewStatusTitle(groupTab.DisplayName),
                    ShowInStatusBar = _localizationService is not null
                },
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return null;
            }

            var animationTextures = await TryLoadAnimationTexturePreviewsSafelyAsync(
                query,
                result.Value,
                cancellationToken);
            if (result.Value.AnimationPreview is not null &&
                animationTextures.Count > 0)
            {
                return new SpineAssetGroupDetailsLoadResult(result.Value, null, animationTextures);
            }

            var diagnosticDetails = await TryLoadInteractiveDiagnosticsAsync(
                query,
                result.Value,
                cancellationToken);
            var details = diagnosticDetails ?? result.Value;
            return new SpineAssetGroupDetailsLoadResult(details, null, animationTextures);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<SpineAssetGroupDetailsDto?> TryLoadInteractiveDiagnosticsAsync(
        LoadSpineAssetGroupDetailsQuery previewQuery,
        SpineAssetGroupDetailsDto previewDetails,
        CancellationToken cancellationToken)
    {
        if (HasDiagnosticDetails(previewDetails))
        {
            return null;
        }

        try
        {
            var diagnosticsQuery = previewQuery with
            {
                LoadMode = SpineAssetGroupDetailsLoadMode.InteractiveDiagnostics
            };
            var result = await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<SpineAssetGroupDetailsDto>>(
                    BackgroundWorkProfile.PreviewForeground,
                    context => _workspaceAssetInspectionGateway.LoadSpineAssetGroupDetailsAsync(
                        diagnosticsQuery,
                        context.CancellationToken))
                {
                    SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                    Priority = BackgroundWorkPriority.UserBlocking,
                    WorkspaceId = diagnosticsQuery.WorkspaceId.Value,
                    WorkspaceKey = diagnosticsQuery.WorkspaceKey,
                    BundleCacheKey = diagnosticsQuery.BundleId.Value,
                    ResourceKey = diagnosticsQuery.GroupId,
                    StatusTitle = CreatePreviewStatusTitle(previewDetails.Group.DisplayName),
                    ShowInStatusBar = _localizationService is not null
                },
                cancellationToken);
            return result.IsSuccess ? result.Value : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<SpineAssetGroupTexturePreviewState?> LoadTexturePreviewAsync(
        SpineAssetGroupDocumentTabViewModel groupTab,
        SpineAtlasPageOptionViewModel atlasPageOption,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupTab);
        ArgumentNullException.ThrowIfNull(atlasPageOption);

        if (!TryCreateQuery(groupTab, out var query))
        {
            return null;
        }

        try
        {
            return await TryLoadTexturePreviewAsync(
                query,
                atlasPageOption.AtlasPage,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, SpineAnimationPreviewTextureState>> TryLoadAnimationTexturePreviewsSafelyAsync(
        LoadSpineAssetGroupDetailsQuery detailsQuery,
        SpineAssetGroupDetailsDto details,
        CancellationToken cancellationToken)
    {
        if (details.AnimationPreview is null)
        {
            return new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
        }

        var atlasPagesByName = details.AtlasPages
            .Where(static page => page.TexturePageAsset is not null)
            .GroupBy(static page => page.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var requiredPageNames = SpineAnimationTexturePageNameResolver.Resolve(details);
        var texturePreviews = new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
        foreach (var pageName in requiredPageNames)
        {
            if (!atlasPagesByName.TryGetValue(pageName, out var atlasPage))
            {
                continue;
            }

            try
            {
                var texturePreview = await TryLoadTexturePreviewAsync(
                    detailsQuery,
                    atlasPage,
                    cancellationToken);
                if (texturePreview is not null)
                {
                    var alphaTexturePreview = await TryLoadAlphaTexturePreviewAsync(
                        detailsQuery,
                        details,
                        atlasPage,
                        cancellationToken);
                    texturePreviews[pageName] = new SpineAnimationPreviewTextureState(
                        pageName,
                        texturePreview,
                        alphaTexturePreview);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                continue;
            }
        }

        return texturePreviews;
    }

    private async Task<SpineAssetGroupTexturePreviewState?> TryLoadAlphaTexturePreviewAsync(
        LoadSpineAssetGroupDetailsQuery detailsQuery,
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageDto atlasPage,
        CancellationToken cancellationToken)
    {
        var alphaTexturePageAsset = SpineAnimationAlphaTexturePageResolver.Resolve(details, atlasPage);
        if (alphaTexturePageAsset is null)
        {
            return null;
        }

        try
        {
            return await TryLoadTexturePreviewAsync(
                detailsQuery,
                atlasPage with { TexturePageAsset = alphaTexturePageAsset },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<SpineAssetGroupTexturePreviewState?> TryLoadTexturePreviewAsync(
        LoadSpineAssetGroupDetailsQuery detailsQuery,
        SpineAtlasPageDto atlasPage,
        CancellationToken cancellationToken)
    {
        if (atlasPage.TexturePageAsset is not { } texturePageAsset)
        {
            return null;
        }

        var previewQuery = new GetAssetPreviewQuery(
            detailsQuery.WorkspaceId,
            detailsQuery.WorkspaceKey,
            detailsQuery.WorkspaceDataRootPath,
            texturePageAsset.BundleId,
            texturePageAsset.AssetId,
            ResolvePreviewSourceOverride(detailsQuery, texturePageAsset.BundleId, texturePageAsset.AssetId));
        var previewResult = await _backgroundWorkScheduler.RunAsync(
            new BackgroundWorkRequest<Result<AssetPreviewDto>>(
                BackgroundWorkProfile.PreviewForeground,
                context => _workspaceAssetInspectionGateway.GetAssetPreviewAsync(previewQuery, context.CancellationToken))
            {
                SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                Priority = BackgroundWorkPriority.UserBlocking,
                WorkspaceId = previewQuery.WorkspaceId.Value,
                WorkspaceKey = previewQuery.WorkspaceKey,
                BundleCacheKey = previewQuery.BundleId.Value,
                ResourceKey = previewQuery.AssetId.Value,
                StatusTitle = CreatePreviewStatusTitle(ResolveTexturePreviewDisplayName(atlasPage)),
                ShowInStatusBar = _localizationService is not null
            },
            cancellationToken);
        if (!previewResult.IsSuccess ||
            previewResult.Value is not { Kind: AssetPreviewKind.Image } preview)
        {
            return null;
        }

        var imageSource = await AssetPreviewMediaSourceFactory.CreateImageSourceAsync(preview.BinaryContent);
        return imageSource is null
            ? null
            : new SpineAssetGroupTexturePreviewState(
                atlasPage,
                preview,
                imageSource,
                imageSource.PixelWidth,
                imageSource.PixelHeight);
    }

    private string? CreatePreviewStatusTitle(string? displayName)
    {
        if (_localizationService is null)
        {
            return null;
        }

        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? _localizationService.GetRequiredString(ResourceKeys.InspectorSpineGroupAssetType)
            : displayName.Trim();
        return _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerStatusLoadPreviewFormat,
            normalizedDisplayName);
    }

    private static string ResolveTexturePreviewDisplayName(SpineAtlasPageDto atlasPage)
    {
        if (!string.IsNullOrWhiteSpace(atlasPage.TexturePageAsset?.Name))
        {
            return atlasPage.TexturePageAsset.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(atlasPage.Name)
            ? atlasPage.TexturePageAsset?.AssetId.Value ?? string.Empty
            : atlasPage.Name.Trim();
    }

    private bool TryCreateQuery(
        SpineAssetGroupDocumentTabViewModel groupTab,
        out LoadSpineAssetGroupDetailsQuery query)
    {
        if (_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) &&
            workspaceEntry is not null &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceKey) &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceDataRootPath) &&
            workspaceEntry.WorkspaceSummary is not null)
        {
            query = new LoadSpineAssetGroupDetailsQuery(
                workspaceEntry.WorkspaceSummary.WorkspaceId,
                workspaceEntry.WorkspaceKey,
                workspaceEntry.WorkspaceDataRootPath,
                groupTab.BundleId,
                groupTab.GroupId,
                CreatePreviewSourceOverrides(groupTab),
                SpineAssetGroupDetailsLoadMode.InteractivePreview);
            return true;
        }

        var activeWorkspaceNavigationRequest = _workspaceShell.ActiveWorkspaceNavigationRequest;
        var workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (activeWorkspaceNavigationRequest is null ||
            workspaceSummary is null ||
            string.IsNullOrWhiteSpace(activeWorkspaceNavigationRequest.WorkspaceDataRootPath))
        {
            query = null!;
            return false;
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            activeWorkspaceNavigationRequest.SourcePath,
            activeWorkspaceNavigationRequest.WorkspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            query = null!;
            return false;
        }

        query = new LoadSpineAssetGroupDetailsQuery(
            workspaceSummary.WorkspaceId,
            workspaceKey,
            activeWorkspaceNavigationRequest.WorkspaceDataRootPath,
            groupTab.BundleId,
            groupTab.GroupId,
            CreatePreviewSourceOverrides(groupTab),
            SpineAssetGroupDetailsLoadMode.InteractivePreview);
        return true;
    }

    private static IReadOnlyList<AssetPreviewSourceOverrideDto>? CreatePreviewSourceOverrides(
        SpineAssetGroupDocumentTabViewModel groupTab)
    {
        var changedParts = groupTab.SpineEditDraft?.ChangedParts;
        if (changedParts is not { Count: > 0 })
        {
            return null;
        }

        return changedParts
            .Where(static part => !string.IsNullOrWhiteSpace(part.SelectedSourceFilePath))
            .Select(static part => new AssetPreviewSourceOverrideDto(
                part.BundleId,
                new AssetId(part.AssetId),
                part.SelectedSourceFilePath!))
            .ToArray();
    }

    private static AssetPreviewSourceOverrideDto? ResolvePreviewSourceOverride(
        LoadSpineAssetGroupDetailsQuery detailsQuery,
        BundleId bundleId,
        AssetId assetId) =>
        detailsQuery.PreviewSourceOverrides?.FirstOrDefault(candidate =>
            candidate.BundleId.Equals(bundleId) &&
            candidate.AssetId.Equals(assetId) &&
            !string.IsNullOrWhiteSpace(candidate.SourceFilePath));

    private static bool HasDiagnosticDetails(SpineAssetGroupDetailsDto details) =>
        details.Diagnostics.Any(static diagnostic =>
            diagnostic.Kind is
                SpineAssetGroupDiagnosticKind.AnimationPreviewRuntimeUnsupported or
                SpineAssetGroupDiagnosticKind.AnimationPreviewUnsupportedFeature or
                SpineAssetGroupDiagnosticKind.SkeletonPreviewUnavailable or
                SpineAssetGroupDiagnosticKind.SkeletonJsonParseFailed or
                SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable or
                SpineAssetGroupDiagnosticKind.AtlasParseFailed);
}
