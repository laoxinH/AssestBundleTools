namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed record SpineAnimationTexturePageContent(
    string PageName,
    byte[] ColorContent,
    byte[]? AlphaContent = null)
{
    public bool HasAlphaContent => AlphaContent is { Length: > 0 };
}
