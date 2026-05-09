using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal interface ISpineAnimationPreviewRenderBackend
{
    void SetCurrentFrame(SpineAnimationFrameDto? frame);

    void SetViewport(SpineAnimationPreviewViewport viewport);

    void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames);

    void LoadTexturePages(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents);

    void ClearTexturePages();

    void ResetFrameCache();

    void ReleaseResources();

    void Invalidate();
}
