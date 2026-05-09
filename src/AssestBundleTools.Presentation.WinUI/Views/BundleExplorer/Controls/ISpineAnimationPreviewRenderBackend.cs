using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal interface ISpineAnimationPreviewRenderBackend
{
    void SetCurrentFrame(SpineAnimationFrameDto? frame);

    void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames);

    void LoadTexturePages(IReadOnlyDictionary<string, byte[]> texturePageContents);

    void ClearTexturePages();

    void ResetFrameCache();

    void ReleaseResources();

    void Invalidate();
}
