using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private SpineAnimationFrameDto? _runtimeCurrentFrameAutoFitReferenceFrame;

    private static bool ShouldRefreshPrewarmFrames(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.Preview) or
            nameof(InspectorPreviewSpineAnimationViewModel.SelectedAnimationName) or
            nameof(InspectorPreviewSpineAnimationViewModel.SelectedSkinName) or
            nameof(InspectorPreviewSpineAnimationViewModel.TexturePageContents);

    private static bool ShouldResetRenderFrameCache(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.Preview) or
            nameof(InspectorPreviewSpineAnimationViewModel.SelectedAnimationName) or
            nameof(InspectorPreviewSpineAnimationViewModel.SelectedSkinName);

    private static bool ShouldCaptureRuntimeCurrentFrameAutoFitReference(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.CurrentFrame);

    internal static IReadOnlyList<SpineAnimationFrameDto> ResolveAutoFitReferenceFrames(
        InspectorPreviewSpineAnimationViewModel? viewModel,
        SpineAnimationFrameDto? runtimeCurrentFrameReference = null)
    {
        if (viewModel is null)
        {
            return Array.Empty<SpineAnimationFrameDto>();
        }

        var frames = new List<SpineAnimationFrameDto>();
        var activeSetupFrame = ResolveActiveSetupFrame(viewModel);
        if (viewModel.IsShowingSetupPose)
        {
            IncludeFrame(frames, activeSetupFrame);
            return frames.Count == 0 ? Array.Empty<SpineAnimationFrameDto>() : frames.ToArray();
        }

        if (viewModel.SelectedAnimation?.SampledFrames is { Count: > 0 } sampledFrames)
        {
            foreach (var frame in sampledFrames)
            {
                IncludeFrame(frames, frame);
            }

            return frames.Count == 0 ? Array.Empty<SpineAnimationFrameDto>() : frames.ToArray();
        }

        if (viewModel.HasRuntimePlayback)
        {
            if (runtimeCurrentFrameReference is not null &&
                !ReferenceEquals(runtimeCurrentFrameReference, activeSetupFrame))
            {
                IncludeFrame(frames, runtimeCurrentFrameReference);
            }

            return frames.Count == 0 ? Array.Empty<SpineAnimationFrameDto>() : frames.ToArray();
        }

        IncludeFrame(frames, activeSetupFrame);
        return frames.Count == 0 ? Array.Empty<SpineAnimationFrameDto>() : frames.ToArray();
    }

    private static SpineAnimationFrameDto? ResolveActiveSetupFrame(
        InspectorPreviewSpineAnimationViewModel viewModel) =>
        viewModel.Preview?.SkinPreviews.FirstOrDefault(skin =>
            string.Equals(skin.Name, viewModel.SelectedSkinName, StringComparison.Ordinal))?.SetupFrame ??
        viewModel.Preview?.SetupFrame;

    private static void IncludeFrame(List<SpineAnimationFrameDto> frames, SpineAnimationFrameDto? frame)
    {
        if (frame is null || frames.Any(existing => ReferenceEquals(existing, frame)))
        {
            return;
        }

        frames.Add(frame);
    }

    private void ResetAutoFitReferenceCapture()
    {
        _runtimeCurrentFrameAutoFitReferenceFrame = null;
    }

    private void CaptureRuntimeCurrentFrameAutoFitReferenceIfNeeded()
    {
        if (_runtimeCurrentFrameAutoFitReferenceFrame is not null ||
            !CanUseCurrentRuntimeFrameAsAutoFitReference(ViewModel))
        {
            return;
        }

        _runtimeCurrentFrameAutoFitReferenceFrame = ViewModel!.CurrentFrame;
        RefreshPrewarmFrames();
    }

    private static bool CanUseCurrentRuntimeFrameAsAutoFitReference(
        InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        if (viewModel is not { HasRuntimePlayback: true, IsShowingSetupPose: false } ||
            viewModel.SelectedAnimation?.SampledFrames.Count > 0 ||
            viewModel.CurrentFrame is null)
        {
            return false;
        }

        return !ReferenceEquals(viewModel.CurrentFrame, ResolveActiveSetupFrame(viewModel));
    }
}
