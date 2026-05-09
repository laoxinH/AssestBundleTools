using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel
{
    private int ResolveFrameIndex(IReadOnlyList<SpineAnimationFrameDto> frames, double time)
    {
        if (frames.Count <= 1)
        {
            return 0;
        }

        if (IsCachedFrameValid(frames, time))
        {
            return _cachedFrameIndex;
        }

        var low = 0;
        var high = frames.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (frames[mid].Time <= time)
            {
                low = mid + 1;
                continue;
            }

            high = mid - 1;
        }

        return Math.Clamp(high, 0, frames.Count - 1);
    }

    private bool IsCachedFrameValid(IReadOnlyList<SpineAnimationFrameDto> frames, double time)
    {
        if (_cachedFrameIndex < 0 || _cachedFrameIndex >= frames.Count)
        {
            return false;
        }

        var currentTime = frames[_cachedFrameIndex].Time;
        var nextTime = _cachedFrameIndex + 1 < frames.Count
            ? frames[_cachedFrameIndex + 1].Time
            : double.PositiveInfinity;
        return time >= currentTime && time < nextTime;
    }

    private SpineAnimationSkinPreviewDto? SelectedSkinPreview =>
        Preview?.SkinPreviews.FirstOrDefault(skin =>
            string.Equals(skin.Name, SelectedSkinName, StringComparison.Ordinal));

    private IReadOnlyList<SpineAnimationClipDto> ActiveAnimations =>
        SelectedSkinPreview?.Animations ??
        Preview?.Animations ??
        Array.Empty<SpineAnimationClipDto>();

    private SpineAnimationFrameDto? ActiveSetupFrame =>
        SelectedSkinPreview?.SetupFrame ?? Preview?.SetupFrame;

    private void RefreshAnimationNamesForActiveSkin()
    {
        AnimationNames.Clear();
        foreach (var animation in ActiveAnimations.Where(animation =>
                     HasRuntimePlayback || animation.SampledFrames.Count > 0))
        {
            AnimationNames.Add(animation.Name);
        }

        OnPropertyChanged(nameof(HasSpineAnimation));
        OnPropertyChanged(nameof(Visibility));
    }

    private static IReadOnlyList<string> ResolveSkinNames(SpineAnimationPreviewDto preview)
    {
        var names = preview.SkinPreviews.Count > 0
            ? preview.SkinPreviews.Select(static skin => skin.Name)
            : preview.SkinNames;
        return names
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolveInitialSkinName(SpineAnimationPreviewDto preview)
    {
        var skinPreview = preview.SkinPreviews.FirstOrDefault(static skin =>
            string.Equals(skin.Name, "default", StringComparison.OrdinalIgnoreCase)) ??
            preview.SkinPreviews.FirstOrDefault(skin =>
                preview.PlaybackSource is not null && skin.Animations.Count > 0) ??
            preview.SkinPreviews.FirstOrDefault(static skin =>
                skin.Animations.Any(static animation =>
                    animation.SampledFrames.Any(static frame => frame.DrawCommands.Count > 0))) ??
            preview.SkinPreviews.FirstOrDefault();
        return skinPreview?.Name ?? preview.SkinNames.FirstOrDefault();
    }
}
