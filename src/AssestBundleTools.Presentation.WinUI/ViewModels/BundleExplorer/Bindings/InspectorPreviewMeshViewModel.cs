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
    private bool _isTextureOverlaySelected = true;
    private bool _isMeshGridSelected = true;
    private double _rotationX = DefaultRotationX;
    private double _rotationY = DefaultRotationY;
    private double _zoom = DefaultZoom;

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
        private set
        {
            if (SetProperty(ref _displayMode, value))
            {
                OnPropertyChanged(nameof(IsWireframeSelected));
                OnPropertyChanged(nameof(IsUvSelected));
                OnPropertyChanged(nameof(DisplayModeGlyph));
                OnPropertyChanged(nameof(BadgeText));
                OnPropertyChanged(nameof(MeshGridSwitchVisibility));
                OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
                OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            }
        }
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

    public bool IsTextureOverlaySelected
    {
        get => _isTextureOverlaySelected;
        private set
        {
            if (SetProperty(ref _isTextureOverlaySelected, value))
            {
                OnPropertyChanged(nameof(TextureOverlayGlyph));
                OnPropertyChanged(nameof(MeshGridSwitchVisibility));
                OnPropertyChanged(nameof(ShouldDrawTextureOverlay));
                OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            }
        }
    }

    public bool IsMeshGridSelected
    {
        get => _isMeshGridSelected;
        private set
        {
            if (SetProperty(ref _isMeshGridSelected, value))
            {
                OnPropertyChanged(nameof(MeshGridGlyph));
                OnPropertyChanged(nameof(ShouldDrawMeshGrid));
            }
        }
    }

    public double RotationX
    {
        get => _rotationX;
        private set => SetProperty(ref _rotationX, value);
    }

    public double RotationY
    {
        get => _rotationY;
        private set => SetProperty(ref _rotationY, value);
    }

    public double Zoom
    {
        get => _zoom;
        private set => SetProperty(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom));
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

    public string TextureOverlayGlyph => IsTextureOverlaySelected
        ? TextureOverlaySelectedGlyphValue
        : TextureOverlayHiddenGlyphValue;

    public string MeshGridGlyph => IsMeshGridSelected
        ? MeshGridSelectedGlyphValue
        : MeshGridHiddenGlyphValue;

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

    internal void Apply(
        AssetPreviewMeshGeometryDto geometry,
        MeshPreviewDisplayState? displayState)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        Geometry = geometry;
        _wireframeBadgeText = displayState?.BadgeText ?? string.Empty;
        _uvBadgeText = displayState?.UvBadgeText ?? string.Empty;
        TextureAssociationText = displayState?.TextureAssociationText ?? string.Empty;
        TextureOverlayContent = null;
        IsTextureOverlaySelected = true;
        IsMeshGridSelected = true;
        OnPropertyChanged(nameof(BadgeText));
        DisplayMode = InspectorPreviewMeshDisplayMode.Wireframe;
        ResetView();
    }

    internal void ApplyTextureOverlay(byte[]? textureOverlayContent)
    {
        TextureOverlayContent = textureOverlayContent is { Length: > 0 }
            ? textureOverlayContent.ToArray()
            : null;
        IsTextureOverlaySelected = HasTextureOverlay;
    }

    internal void Clear()
    {
        Geometry = null;
        TextureOverlayContent = null;
        _wireframeBadgeText = string.Empty;
        _uvBadgeText = string.Empty;
        TextureAssociationText = string.Empty;
        IsTextureOverlaySelected = true;
        IsMeshGridSelected = true;
        DisplayMode = InspectorPreviewMeshDisplayMode.Wireframe;
        OnPropertyChanged(nameof(BadgeText));
        ResetView();
    }

    internal void ToggleDisplayMode()
    {
        DisplayMode = IsUvSelected || !HasUv
            ? InspectorPreviewMeshDisplayMode.Wireframe
            : InspectorPreviewMeshDisplayMode.Uv;
    }

    internal void ToggleTextureOverlay()
    {
        if (!HasTextureOverlay)
        {
            return;
        }

        IsTextureOverlaySelected = !IsTextureOverlaySelected;
    }

    internal void ToggleMeshGrid()
    {
        if (!ShouldDrawTextureOverlay || (!IsUvSelected && !CanDrawTexturedMesh))
        {
            return;
        }

        IsMeshGridSelected = !IsMeshGridSelected;
    }

    internal void Rotate(double deltaX, double deltaY)
    {
        if (!IsWireframeSelected)
        {
            return;
        }

        RotationY += deltaX * 0.45d;
        RotationX = Math.Clamp(RotationX + (deltaY * 0.45d), -89d, 89d);
    }

    internal void ZoomBy(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
        {
            return;
        }

        Zoom *= scale;
    }

    internal void ResetView()
    {
        RotationX = DefaultRotationX;
        RotationY = DefaultRotationY;
        Zoom = DefaultZoom;
    }
}
