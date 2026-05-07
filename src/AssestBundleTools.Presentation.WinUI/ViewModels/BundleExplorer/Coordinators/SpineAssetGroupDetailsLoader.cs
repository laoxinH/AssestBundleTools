using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 通过 Application 预览管线加载 Spine 资源组详情，避免 UI 直接读取 atlas 文本。
/// </summary>
internal sealed class SpineAssetGroupDetailsLoader : ISpineAssetGroupDetailsLoader
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;

    public SpineAssetGroupDetailsLoader(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceContextService workspaceContextService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBackgroundTaskRunner backgroundTaskRunner)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
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
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.LoadSpineAssetGroupDetailsAsync(query, token),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return null;
            }

            var texturePreview = await TryLoadTexturePreviewSafelyAsync(
                query,
                result.Value,
                groupTab.SelectedAtlasPageOption,
                cancellationToken);
            var animationTextures = await TryLoadAnimationTexturePreviewsSafelyAsync(
                query,
                result.Value,
                cancellationToken);
            return new SpineAssetGroupDetailsLoadResult(result.Value, texturePreview, animationTextures);
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

    private async Task<SpineAssetGroupTexturePreviewState?> TryLoadTexturePreviewSafelyAsync(
        LoadSpineAssetGroupDetailsQuery detailsQuery,
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageOptionViewModel? preferredAtlasPageOption,
        CancellationToken cancellationToken)
    {
        try
        {
            var atlasPage = ResolveTexturePreviewAtlasPage(details, preferredAtlasPageOption);
            return atlasPage is null
                ? null
                : await TryLoadTexturePreviewAsync(detailsQuery, atlasPage, cancellationToken);
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
        var requiredPageNames = details.AnimationPreview.Attachments
            .Select(static attachment => attachment.PageName)
            .Where(static pageName => !string.IsNullOrWhiteSpace(pageName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
                    texturePreviews[pageName] = new SpineAnimationPreviewTextureState(pageName, texturePreview);
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
            texturePageAsset.AssetId);
        var previewResult = await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetPreviewAsync(previewQuery, token),
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

    private static SpineAtlasPageDto? ResolveTexturePreviewAtlasPage(
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageOptionViewModel? preferredAtlasPageOption)
    {
        if (preferredAtlasPageOption is not null &&
            preferredAtlasPageOption.Index >= 0 &&
            preferredAtlasPageOption.Index < details.AtlasPages.Count &&
            string.Equals(
                details.AtlasPages[preferredAtlasPageOption.Index].Name,
                preferredAtlasPageOption.AtlasPage.Name,
                StringComparison.Ordinal))
        {
            return details.AtlasPages[preferredAtlasPageOption.Index];
        }

        if (preferredAtlasPageOption is not null)
        {
            var preferredByName = details.AtlasPages.FirstOrDefault(page =>
                string.Equals(page.Name, preferredAtlasPageOption.AtlasPage.Name, StringComparison.Ordinal));
            if (preferredByName is not null)
            {
                return preferredByName;
            }
        }

        return details.AtlasPages.FirstOrDefault(static page => page.TexturePageAsset is not null);
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
                groupTab.GroupId);
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
            groupTab.GroupId);
        return true;
    }
}
