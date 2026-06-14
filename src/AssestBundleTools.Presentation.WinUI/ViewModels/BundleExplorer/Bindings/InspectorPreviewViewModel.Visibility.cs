using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewViewModel
{
    public Visibility PreviewLoadingVisibility => _hasSelection && IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewImageVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Image &&
        Image.ImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewTextVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Text &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewAudioVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Audio &&
        PreviewAudioSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMeshVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Mesh &&
        Mesh.HasGeometry
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMaterialVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Material &&
        Material.HasMaterial
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewAnimationClipVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.AnimationClip &&
        AnimationClip.HasAnimationClip
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewFontVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Font &&
        Font.HasFont
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewGameObjectVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.GameObject &&
        GameObject.HasGameObject
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMonoBehaviourVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.MonoBehaviour &&
        MonoBehaviour.HasMonoBehaviour
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewAnimatorControllerVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.AnimatorController &&
        AnimatorController.HasAnimatorController
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewSpineAnimationVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.SpineAnimation &&
        SpineAnimation.HasSpineAnimation
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewSummaryVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Summary &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMessageVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.None &&
        !string.IsNullOrWhiteSpace(PreviewStatusText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool CanOpenPreviewLocation => !string.IsNullOrWhiteSpace(PreviewPayloadFilePath);

    public Visibility PreviewOpenLocationVisibility =>
        CanOpenPreviewLocation
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewStateBadgeVisibility =>
        _hasSelection &&
        _isCurrentAssetModified &&
        !string.IsNullOrWhiteSpace(PreviewStateBadgeText)
            ? Visibility.Visible
            : Visibility.Collapsed;
}
