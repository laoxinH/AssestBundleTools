using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public enum InspectorPreviewMeshDisplayMode
{
    Wireframe,
    Uv
}

/// <summary>
/// 承载 Mesh 线框预览的交互状态。
/// </summary>
public sealed class InspectorPreviewMeshViewModel : ObservableObject
{
    private const double DefaultRotationX = -18d;
    private const double DefaultRotationY = 32d;
    private const double DefaultZoom = 1d;
    private const double MinZoom = 0.35d;
    private const double MaxZoom = 6d;
    private const double MinRotationX = -89d;
    private const double MaxRotationX = 89d;
    private const string WireframeDisplayModeGlyphValue = "\uE9D9";
    private const string UvDisplayModeGlyphValue = "\uE8A9";
    private const string TextureOverlaySelectedGlyphValue = "\uE91B";
    private const string TextureOverlayHiddenGlyphValue = "\uE8F4";
    private const string MeshGridSelectedGlyphValue = "\uE9D9";
    private const string MeshGridHiddenGlyphValue = "\uE8F4";

    private AssetPreviewMeshGeometryDto? _geometry;
    private byte[]? _textureOverlayContent;
    private InspectorPreviewMeshDisplayMode _displayMode = InspectorPreviewMeshDisplayMode.Wireframe;
    private string _wireframeBadgeText = string.Empty;
    private string _uvBadgeText = string.Empty;
    private string _textureAssociationText = string.Empty;
    private IReadOnlyList<ResolvedAssetReferenceViewModel> _textureAssociationRows = Array.Empty<ResolvedAssetReferenceViewModel>();
    private bool _isTextureOverlaySelected = true;
    private bool _isMeshGridSelected = true;
    private double _rotationX = DefaultRotationX;
    private double _rotationY = DefaultRotationY;
    private double _zoom = DefaultZoom;

    public event EventHandler? DisplayOptionsChanged;

    public AssetPreviewMeshGeometryDto? Geometry
    {
        get => _geometry;
        private set
        {
            if (SetProperty(ref _geometry, value))
            {
                OnPropertyChanged(nameof(Visibility));
                OnPropertyChanged(nameof(ControlsVisibility));
                OnPropertyChanged(nameof(HasGeometry));
                OnPropertyChanged(nameof(HasUv));
                OnPropertyChanged(nameof(CanDrawTexturedMesh));
                OnPropertyChanged(nameof(IsWireframeSelected));
                OnPropertyChanged(nameof(IsUvSelected));
                OnPropertyChanged(nameof(DisplayModeGlyph));
                OnPropertyChanged(nameof(DisplayModeSwitchVisibility));
                OnPropertyChanged(nameof(TextureOverlaySwitchVisibility));
                OnPropertyChanged(nameof(MeshGridSwitchVisibility));
                OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
                OnPropertyChanged(nameof(ShouldDrawMeshGrid));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    public byte[]? TextureOverlayContent
    {
        get => _textureOverlayContent;
        private set
        {
            if (SetProperty(ref _textureOverlayContent, value))
            {
                OnPropertyChanged(nameof(HasTextureOverlay));
                OnPropertyChanged(nameof(TextureOverlaySwitchVisibility));
                OnPropertyChanged(nameof(MeshGridSwitchVisibility));
                OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
                OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            }
        }
    }

    public InspectorPreviewMeshDisplayMode DisplayMode
    {
        get => _displayMode;
        private set => SetDisplayMode(value);
    }

    public string BadgeText => IsUvSelected && !string.IsNullOrWhiteSpace(_uvBadgeText)
        ? _uvBadgeText
        : _wireframeBadgeText;

    public string TextureAssociationText
    {
        get => _textureAssociationText;
        private set
        {
            if (SetProperty(ref _textureAssociationText, value))
            {
                OnPropertyChanged(nameof(TextureAssociationVisibility));
            }
        }
    }

    public IReadOnlyList<ResolvedAssetReferenceViewModel> TextureAssociationRows
    {
        get => _textureAssociationRows;
        private set
        {
            if (SetProperty(ref _textureAssociationRows, value))
            {
                OnPropertyChanged(nameof(TextureAssociationRowsVisibility));
            }
        }
    }

    public bool IsTextureOverlaySelected
    {
        get => _isTextureOverlaySelected;
        private set => SetTextureOverlaySelected(value);
    }

    public bool IsMeshGridSelected
    {
        get => _isMeshGridSelected;
        private set => SetMeshGridSelected(value);
    }

    public double RotationX
    {
        get => _rotationX;
        private set => SetRotationX(value);
    }

    public double RotationY
    {
        get => _rotationY;
        private set => SetRotationY(value);
    }

    public double Zoom
    {
        get => _zoom;
        private set => SetZoom(value);
    }

    public bool HasGeometry => Geometry is not null;

    public bool HasUv => Geometry?.Uv0 is { Count: > 0 };

    public bool HasTextureOverlay => TextureOverlayContent is { Length: > 0 };

    public bool CanDrawTexturedMesh =>
        HasUv &&
        Geometry?.Triangles is { Count: > 0 };

    public bool IsWireframeSelected => DisplayMode == InspectorPreviewMeshDisplayMode.Wireframe;

    public bool IsUvSelected => DisplayMode == InspectorPreviewMeshDisplayMode.Uv && HasUv;

    public bool ShouldDrawTextureOverlay =>
        IsTextureOverlaySelected &&
        HasTextureOverlay &&
        (IsUvSelected || CanDrawTexturedMesh);

    public bool ShouldDrawMeshGrid =>
        IsMeshGridSelected ||
        !ShouldDrawTextureOverlay;

    public string DisplayModeGlyph => IsUvSelected
        ? UvDisplayModeGlyphValue
        : WireframeDisplayModeGlyphValue;

    public string DisplayModePersistenceKey => DisplayMode.ToString();

    public string TextureOverlayGlyph => IsTextureOverlaySelected
        ? TextureOverlaySelectedGlyphValue
        : TextureOverlayHiddenGlyphValue;

    public string MeshGridGlyph => IsMeshGridSelected
        ? MeshGridSelectedGlyphValue
        : MeshGridHiddenGlyphValue;

    public string RotationStatusText => FormattableString.Invariant(
        $"X {Math.Round(RotationX):+0;-0;0}\u00B0  Y {Math.Round(NormalizeRotationY(RotationY)):+0;-0;0}\u00B0");

    public Visibility Visibility => HasGeometry ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ControlsVisibility => HasGeometry ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DisplayModeSwitchVisibility =>
        HasGeometry && HasUv ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TextureOverlaySwitchVisibility =>
        HasGeometry && HasUv && HasTextureOverlay ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MeshGridSwitchVisibility =>
        HasGeometry &&
        ShouldDrawTextureOverlay &&
        (IsUvSelected || CanDrawTexturedMesh)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility TextureAssociationVisibility =>
        string.IsNullOrWhiteSpace(TextureAssociationText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TextureAssociationRowsVisibility =>
        TextureAssociationRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    internal void Apply(
        AssetPreviewMeshGeometryDto geometry,
        MeshPreviewDisplayState? displayState)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        Geometry = geometry;
        _wireframeBadgeText = displayState?.BadgeText ?? string.Empty;
        _uvBadgeText = displayState?.UvBadgeText ?? string.Empty;
        TextureAssociationText = displayState?.TextureAssociationText ?? string.Empty;
        TextureAssociationRows = displayState?.TextureAssociationReferences?
            .Select(static reference => new ResolvedAssetReferenceViewModel(reference))
            .ToArray() ?? Array.Empty<ResolvedAssetReferenceViewModel>();
        TextureOverlayContent = null;
        OnPropertyChanged(nameof(BadgeText));
        if (!HasUv && DisplayMode == InspectorPreviewMeshDisplayMode.Uv)
        {
            SetDisplayMode(InspectorPreviewMeshDisplayMode.Wireframe, notifyDisplayOptionsChanged: false);
        }
    }

    internal void ApplyTextureOverlay(byte[]? textureOverlayContent)
    {
        TextureOverlayContent = textureOverlayContent is { Length: > 0 }
            ? textureOverlayContent.ToArray()
            : null;
    }

    internal void Clear()
    {
        Geometry = null;
        TextureOverlayContent = null;
        _wireframeBadgeText = string.Empty;
        _uvBadgeText = string.Empty;
        TextureAssociationText = string.Empty;
        TextureAssociationRows = Array.Empty<ResolvedAssetReferenceViewModel>();
        OnPropertyChanged(nameof(BadgeText));
    }

    internal void ToggleDisplayMode()
    {
        SetDisplayMode(
            IsUvSelected || !HasUv
                ? InspectorPreviewMeshDisplayMode.Wireframe
                : InspectorPreviewMeshDisplayMode.Uv);
    }

    internal void RestoreDisplayOptions(
        string? displayMode,
        bool? textureOverlaySelected,
        bool? meshGridSelected,
        double? zoom,
        double? rotationX,
        double? rotationY)
    {
        SetDisplayMode(ParseDisplayMode(displayMode), notifyDisplayOptionsChanged: false);
        SetTextureOverlaySelected(textureOverlaySelected ?? true, notifyDisplayOptionsChanged: false);
        SetMeshGridSelected(meshGridSelected ?? true, notifyDisplayOptionsChanged: false);
        SetZoom(zoom ?? DefaultZoom, notifyDisplayOptionsChanged: false);
        SetRotationX(rotationX ?? DefaultRotationX, notifyDisplayOptionsChanged: false);
        SetRotationY(rotationY ?? DefaultRotationY, notifyDisplayOptionsChanged: false);
    }

    internal void ToggleTextureOverlay()
    {
        if (!HasTextureOverlay)
        {
            return;
        }

        SetTextureOverlaySelected(!IsTextureOverlaySelected);
    }

    internal void ToggleMeshGrid()
    {
        if (!ShouldDrawTextureOverlay || (!IsUvSelected && !CanDrawTexturedMesh))
        {
            return;
        }

        SetMeshGridSelected(!IsMeshGridSelected);
    }

    internal void Rotate(double deltaX, double deltaY)
    {
        if (!IsWireframeSelected)
        {
            return;
        }

        var hasChanged = SetRotationY(RotationY + (deltaX * 0.45d), notifyDisplayOptionsChanged: false);
        hasChanged = SetRotationX(RotationX + (deltaY * 0.45d), notifyDisplayOptionsChanged: false) || hasChanged;
        if (hasChanged)
        {
            DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void ZoomBy(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
        {
            return;
        }

        SetZoom(Zoom * scale);
    }

    internal void ResetView()
    {
        SetRotationX(DefaultRotationX);
        SetRotationY(DefaultRotationY);
        SetZoom(DefaultZoom);
    }

    private void SetDisplayMode(
        InspectorPreviewMeshDisplayMode mode,
        bool notifyDisplayOptionsChanged = true)
    {
        if (SetProperty(ref _displayMode, mode, nameof(DisplayMode)))
        {
            OnPropertyChanged(nameof(IsWireframeSelected));
            OnPropertyChanged(nameof(IsUvSelected));
            OnPropertyChanged(nameof(DisplayModeGlyph));
            OnPropertyChanged(nameof(DisplayModePersistenceKey));
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(MeshGridSwitchVisibility));
            OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
            OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            if (notifyDisplayOptionsChanged)
            {
                DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void SetTextureOverlaySelected(
        bool isSelected,
        bool notifyDisplayOptionsChanged = true)
    {
        if (SetProperty(ref _isTextureOverlaySelected, isSelected, nameof(IsTextureOverlaySelected)))
        {
            OnPropertyChanged(nameof(TextureOverlayGlyph));
            OnPropertyChanged(nameof(MeshGridSwitchVisibility));
            OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
            OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            if (notifyDisplayOptionsChanged)
            {
                DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void SetMeshGridSelected(
        bool isSelected,
        bool notifyDisplayOptionsChanged = true)
    {
        if (SetProperty(ref _isMeshGridSelected, isSelected, nameof(IsMeshGridSelected)))
        {
            OnPropertyChanged(nameof(MeshGridGlyph));
            OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            if (notifyDisplayOptionsChanged)
            {
                DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void SetZoom(
        double zoom,
        bool notifyDisplayOptionsChanged = true)
    {
        var normalizedZoom = double.IsNaN(zoom) || double.IsInfinity(zoom)
            ? DefaultZoom
            : Math.Clamp(zoom, MinZoom, MaxZoom);
        if (SetProperty(ref _zoom, normalizedZoom, nameof(Zoom)) && notifyDisplayOptionsChanged)
        {
            DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool SetRotationX(
        double rotationX,
        bool notifyDisplayOptionsChanged = true)
    {
        var normalizedRotationX = double.IsNaN(rotationX) || double.IsInfinity(rotationX)
            ? DefaultRotationX
            : Math.Clamp(rotationX, MinRotationX, MaxRotationX);
        if (SetProperty(ref _rotationX, normalizedRotationX, nameof(RotationX)))
        {
            OnPropertyChanged(nameof(RotationStatusText));
            if (notifyDisplayOptionsChanged)
            {
                DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        return false;
    }

    private bool SetRotationY(
        double rotationY,
        bool notifyDisplayOptionsChanged = true)
    {
        var normalizedRotationY = double.IsNaN(rotationY) || double.IsInfinity(rotationY)
            ? DefaultRotationY
            : NormalizeRotationY(rotationY);
        if (SetProperty(ref _rotationY, normalizedRotationY, nameof(RotationY)))
        {
            OnPropertyChanged(nameof(RotationStatusText));
            if (notifyDisplayOptionsChanged)
            {
                DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        return false;
    }

    private static InspectorPreviewMeshDisplayMode ParseDisplayMode(string? mode)
    {
        return Enum.TryParse<InspectorPreviewMeshDisplayMode>(
            mode,
            ignoreCase: true,
            out var parsedMode)
            ? parsedMode
            : InspectorPreviewMeshDisplayMode.Wireframe;
    }

    private static double NormalizeRotationY(double rotationY)
    {
        var normalizedRotation = rotationY % 360d;
        if (normalizedRotation > 180d)
        {
            normalizedRotation -= 360d;
        }
        else if (normalizedRotation < -180d)
        {
            normalizedRotation += 360d;
        }

        return normalizedRotation;
    }
}
