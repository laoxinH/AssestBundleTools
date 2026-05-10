using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal readonly record struct AssetEditorWindowLayoutSnapshot(
    int X,
    int Y,
    int Width,
    int Height)
{
    public AssetEditorWindowLayoutSnapshot Offset(int deltaX, int deltaY) =>
        this with
        {
            X = X + deltaX,
            Y = Y + deltaY,
        };
}

internal sealed class AssetEditorWindowStateStore
{
    internal const int DefaultX = 120;
    internal const int DefaultY = 90;
    internal const int DefaultWidth = 1320;
    internal const int DefaultHeight = 820;
    internal const int MinimumRememberedWidth = 760;
    internal const int MinimumRememberedHeight = 520;
    internal const int CascadeOffset = 32;
    private const int MaxCascadeSteps = 8;

    private AssetEditorWindowLayoutSnapshot? _lastKnownLayout;
    private readonly Dictionary<int, AssetEditorWindowStateSnapshot> _openWindowStates = [];
    private int _nextCascadeStep;
    private int _nextWindowStateId = 1;

    public AssetEditorWindowLayoutSnapshot CreateNextWindowLayout()
    {
        var baseLayout = _lastKnownLayout ?? CreateDefaultLayout();
        var cascadeStep = _nextCascadeStep;
        _nextCascadeStep = (_nextCascadeStep + 1) % MaxCascadeSteps;

        return baseLayout.Offset(
            CascadeOffset * cascadeStep,
            CascadeOffset * cascadeStep);
    }

    public void RememberWindowLayout(AssetEditorWindowLayoutSnapshot layout)
    {
        if (!CanRemember(layout))
        {
            return;
        }

        _lastKnownLayout = layout;
        _nextCascadeStep = 1;
    }

    public int RegisterWindow(
        AssetEditorWindowLayoutSnapshot layout,
        AssetEditorWindowGroupSnapshot group)
    {
        var windowStateId = _nextWindowStateId++;
        _openWindowStates[windowStateId] = new AssetEditorWindowStateSnapshot(
            windowStateId,
            layout,
            group);
        RememberWindowLayout(layout);
        return windowStateId;
    }

    public void UpdateWindowLayout(int windowStateId, AssetEditorWindowLayoutSnapshot layout)
    {
        if (!_openWindowStates.TryGetValue(windowStateId, out var state))
        {
            return;
        }

        _openWindowStates[windowStateId] = state with { Layout = layout };
        RememberWindowLayout(layout);
    }

    public void UpdateWindowGroup(int windowStateId, AssetEditorWindowGroupSnapshot group)
    {
        if (!_openWindowStates.TryGetValue(windowStateId, out var state))
        {
            return;
        }

        _openWindowStates[windowStateId] = state with { Group = group };
    }

    public void RemoveWindow(int windowStateId)
    {
        _openWindowStates.Remove(windowStateId);
    }

    public IReadOnlyList<AssetEditorWindowStateSnapshot> CaptureOpenWindows() =>
        _openWindowStates.Values
            .OrderBy(state => state.WindowStateId)
            .ToArray();

    public IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CaptureOpenWindowMetadata() =>
        CaptureOpenWindows()
            .Where(static state => state.Group.DocumentTabs.Count > 0)
            .Select(CreateWindowMetadata)
            .ToArray();

    public void Clear()
    {
        _lastKnownLayout = null;
        _openWindowStates.Clear();
        _nextCascadeStep = 0;
        _nextWindowStateId = 1;
    }

    private static AssetEditorWindowLayoutSnapshot CreateDefaultLayout() =>
        new(DefaultX, DefaultY, DefaultWidth, DefaultHeight);

    private static bool CanRemember(AssetEditorWindowLayoutSnapshot layout) =>
        layout.Width >= MinimumRememberedWidth &&
        layout.Height >= MinimumRememberedHeight;

    private static WorkspaceAssetEditorWindowStateDto CreateWindowMetadata(
        AssetEditorWindowStateSnapshot state)
    {
        return new WorkspaceAssetEditorWindowStateDto(
            WindowStateId: state.WindowStateId,
            Layout: new WorkspaceAssetEditorWindowLayoutDto(
                X: state.Layout.X,
                Y: state.Layout.Y,
                Width: state.Layout.Width,
                Height: state.Layout.Height),
            OpenAssetTabs: state.Group.AssetTabs
                .Select(CreateAssetTabMetadata)
                .ToArray(),
            ActiveBundleId: state.Group.ActiveBundleId,
            ActiveAssetId: state.Group.ActiveAssetId,
            OpenDocumentTabs: state.Group.DocumentTabs
                .Select(CreateDocumentTabMetadata)
                .ToArray(),
            ActiveDocumentKind: state.Group.ActiveDocumentKind,
            ActiveSpineGroupId: state.Group.ActiveSpineGroupId);
    }

    private static WorkspaceAssetEditorWindowDocumentTabDto CreateDocumentTabMetadata(
        AssetEditorWindowDocumentTabSnapshot documentTab)
    {
        return documentTab.AssetTab is { } assetTab
            ? new WorkspaceAssetEditorWindowDocumentTabDto(
                WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                AssetTab: CreateAssetTabMetadata(assetTab))
            : new WorkspaceAssetEditorWindowDocumentTabDto(
                WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                SpineGroupTab: documentTab.SpineGroupTab is { } spineGroupTab
                    ? CreateSpineGroupTabMetadata(spineGroupTab)
                    : null);
    }

    private static WorkspaceAssetEditorWindowAssetTabDto CreateAssetTabMetadata(
        AssetEditorWindowAssetTabSnapshot assetTab)
    {
        return new WorkspaceAssetEditorWindowAssetTabDto(
            BundleId: assetTab.BundleId,
            AssetId: assetTab.AssetId,
            AssetName: assetTab.DisplayName,
            SelectedInspectorSection: assetTab.SelectedInspectorSection,
            PreviewImageBackgroundMode: assetTab.PreviewImageBackgroundMode,
            SpritePreviewDisplayMode: assetTab.SpritePreviewDisplayMode,
            MeshPreviewDisplayMode: assetTab.MeshPreviewDisplayMode,
            MeshPreviewTextureOverlaySelected: assetTab.MeshPreviewTextureOverlaySelected,
            MeshPreviewGridSelected: assetTab.MeshPreviewGridSelected,
            MeshPreviewZoom: assetTab.MeshPreviewZoom,
            MeshPreviewRotationX: assetTab.MeshPreviewRotationX,
            MeshPreviewRotationY: assetTab.MeshPreviewRotationY,
            PreviewImageZoom: assetTab.PreviewImageZoom,
            PreviewImagePanX: assetTab.PreviewImagePanX,
            PreviewImagePanY: assetTab.PreviewImagePanY,
            EditDraftReference: assetTab.EditDraftReference,
            InspectorScrollState: assetTab.InspectorScrollState);
    }

    private static WorkspaceAssetEditorWindowSpineGroupTabDto CreateSpineGroupTabMetadata(
        AssetEditorWindowSpineGroupTabSnapshot spineGroupTab)
    {
        return new WorkspaceAssetEditorWindowSpineGroupTabDto(
            BundleId: spineGroupTab.BundleId,
            GroupId: spineGroupTab.GroupId,
            DisplayName: spineGroupTab.DisplayName,
            GroupKey: spineGroupTab.GroupKey,
            PreviewState: spineGroupTab.PreviewState,
            InspectorScrollState: spineGroupTab.InspectorScrollState);
    }
}

internal sealed record AssetEditorWindowStateSnapshot(
    int WindowStateId,
    AssetEditorWindowLayoutSnapshot Layout,
    AssetEditorWindowGroupSnapshot Group);

internal sealed record AssetEditorWindowGroupSnapshot(
    IReadOnlyList<AssetEditorWindowDocumentTabSnapshot> DocumentTabs,
    IReadOnlyList<AssetEditorWindowAssetTabSnapshot> AssetTabs,
    string? ActiveDocumentKind,
    string? ActiveBundleId,
    string? ActiveAssetId,
    string? ActiveSpineGroupId)
{
    public static AssetEditorWindowGroupSnapshot FromDocumentGroup(AssetDocumentGroupViewModel group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new AssetEditorWindowGroupSnapshot(
            group.DocumentTabs
                .Select(AssetEditorWindowDocumentTabSnapshot.FromDocumentTab)
                .ToArray(),
            group.AssetTabs
                .Select(AssetEditorWindowAssetTabSnapshot.FromAssetTab)
                .ToArray(),
            group.ActiveDocumentTab?.IsSpineGroup == true
                ? WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind
                : WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
            group.ActiveDocumentTab?.BundleId.Value,
            group.ActiveAssetTab?.AssetId.Value,
            group.ActiveSpineGroupTab?.GroupId);
    }
}

internal sealed record AssetEditorWindowDocumentTabSnapshot(
    string Kind,
    AssetEditorWindowAssetTabSnapshot? AssetTab = null,
    AssetEditorWindowSpineGroupTabSnapshot? SpineGroupTab = null)
{
    public static AssetEditorWindowDocumentTabSnapshot FromDocumentTab(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        if (documentTab.AssetTab is { } assetTab)
        {
            return new AssetEditorWindowDocumentTabSnapshot(
                WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                AssetTab: AssetEditorWindowAssetTabSnapshot.FromAssetTab(assetTab));
        }

        if (documentTab.SpineGroupTab is { } spineGroupTab)
        {
            return new AssetEditorWindowDocumentTabSnapshot(
                WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                SpineGroupTab: AssetEditorWindowSpineGroupTabSnapshot.FromSpineGroupTab(spineGroupTab));
        }

        throw new InvalidOperationException("Document tab snapshot requires a backing document.");
    }
}

internal sealed record AssetEditorWindowAssetTabSnapshot(
    string BundleId,
    string AssetId,
    string DisplayName,
    string? SelectedInspectorSection = null,
    string? PreviewImageBackgroundMode = null,
    string? SpritePreviewDisplayMode = null,
    string? MeshPreviewDisplayMode = null,
    bool? MeshPreviewTextureOverlaySelected = null,
    bool? MeshPreviewGridSelected = null,
    double? MeshPreviewZoom = null,
    double? MeshPreviewRotationX = null,
    double? MeshPreviewRotationY = null,
    double? PreviewImageZoom = null,
    double? PreviewImagePanX = null,
    double? PreviewImagePanY = null,
    WorkspaceAssetEditDraftReferenceDto? EditDraftReference = null,
    WorkspaceInspectorScrollStateDto? InspectorScrollState = null)
{
    public static AssetEditorWindowAssetTabSnapshot FromAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        var imageDisplayOptions = assetTab.Inspector.Preview.Image.DisplayOptions;
        var imageViewState = assetTab.Inspector.Preview.Image;
        var meshDisplayOptions = assetTab.Inspector.Preview.Mesh;
        return new AssetEditorWindowAssetTabSnapshot(
            assetTab.BundleId.Value,
            assetTab.AssetId.Value,
            assetTab.DisplayName,
            SelectedInspectorSection: assetTab.Inspector.SelectedSectionKey,
            PreviewImageBackgroundMode: imageDisplayOptions.BackgroundModePersistenceKey,
            SpritePreviewDisplayMode: imageDisplayOptions.SpriteDisplayModePersistenceKey,
            MeshPreviewDisplayMode: meshDisplayOptions.DisplayModePersistenceKey,
            MeshPreviewTextureOverlaySelected: meshDisplayOptions.IsTextureOverlaySelected,
            MeshPreviewGridSelected: meshDisplayOptions.IsMeshGridSelected,
            MeshPreviewZoom: meshDisplayOptions.Zoom,
            MeshPreviewRotationX: meshDisplayOptions.RotationX,
            MeshPreviewRotationY: meshDisplayOptions.RotationY,
            PreviewImageZoom: imageViewState.Zoom,
            PreviewImagePanX: imageViewState.PanX,
            PreviewImagePanY: imageViewState.PanY,
            EditDraftReference: AssetDocumentEditDraftReferenceMapper.ToDto(assetTab.EditDraftReference),
            InspectorScrollState: assetTab.Inspector.CaptureInspectorScrollState());
    }
}

internal sealed record AssetEditorWindowSpineGroupTabSnapshot(
    string BundleId,
    string GroupId,
    string DisplayName,
    string? GroupKey,
    WorkspaceSpinePreviewStateDto? PreviewState = null,
    WorkspaceInspectorScrollStateDto? InspectorScrollState = null)
{
    public static AssetEditorWindowSpineGroupTabSnapshot FromSpineGroupTab(
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        return new AssetEditorWindowSpineGroupTabSnapshot(
            spineGroupTab.BundleId.Value,
            spineGroupTab.GroupId,
            spineGroupTab.DisplayName,
            spineGroupTab.Item.GroupKey,
            spineGroupTab.CapturePreviewState(),
            spineGroupTab.Inspector.CaptureInspectorScrollState());
    }
}
