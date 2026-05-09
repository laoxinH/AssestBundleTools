namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel
{
    private static IReadOnlyDictionary<string, SpineAnimationTexturePageContent> CreateTexturePageContents(
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> texturePreviews) =>
        texturePreviews
            .Where(static pair => pair.Value.BinaryContent is { Length: > 0 })
            .ToDictionary(
                static pair => pair.Key,
                static pair => new SpineAnimationTexturePageContent(
                    pair.Key,
                    pair.Value.BinaryContent!,
                    pair.Value.AlphaBinaryContent),
                StringComparer.Ordinal);
}
