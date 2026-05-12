using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class WinUIFilePickerServiceTests
{
    [Fact]
    public void ResolveExtractionFileTypeChoices_ShouldUseSimpleExtensions_ForMonoBehaviourPicker()
    {
        var choices = WinUIFilePickerService.ResolveExtractionFileTypeChoices(
            AssetType.Unknown,
            "MonoBehaviour",
            "HeroController");

        Assert.Collection(
            choices,
            choice =>
            {
                Assert.Equal(ResourceKeys.FilePickerExtractionTypeMonoBehaviourTypeTreeJson, choice.ResourceKey);
                Assert.Equal([".json"], choice.Extensions);
            },
            choice =>
            {
                Assert.Equal(ResourceKeys.FilePickerExtractionTypeYaml, choice.ResourceKey);
                Assert.Equal([".yaml", ".yml"], choice.Extensions);
            },
            choice =>
            {
                Assert.Equal(ResourceKeys.FilePickerExtractionTypeMonoBehaviourFields, choice.ResourceKey);
                Assert.Equal([".txt"], choice.Extensions);
            });

        var extensions = choices.SelectMany(static choice => choice.Extensions).ToArray();
        Assert.All(extensions, static extension => Assert.Equal(1, extension.Count(static ch => ch == '.')));
        Assert.DoesNotContain(".typetree.json", extensions);
        Assert.DoesNotContain(".mono-behaviour.json", extensions);
        Assert.DoesNotContain(".summary.json", extensions);
    }

    [Fact]
    public void ResolvePickerSuggestedFileName_ShouldUseTypeTreeStem_ForMonoBehaviourDefault()
    {
        var suggestedName = WinUIFilePickerService.ResolvePickerSuggestedFileName(
            "MonoBehaviour",
            "HeroController");

        Assert.Equal("HeroController.typetree", suggestedName);
    }

    [Fact]
    public void ResolvePickerSuggestedFileName_ShouldPreserveExistingMonoBehaviourFormatStem()
    {
        var suggestedName = WinUIFilePickerService.ResolvePickerSuggestedFileName(
            "MonoBehaviour",
            "HeroController.summary.json");

        Assert.Equal("HeroController.summary", suggestedName);
    }

    [Fact]
    public void ResolveDefaultSelectionPathForFolderPath_ShouldUseCurrentFolderPath()
    {
        var selectionPath = NativeFolderPicker.ResolveDefaultSelectionPathForFolderPath(
            @"C:\Users\thixi\Downloads\Telegram Desktop\导出spine\批量测试\");

        Assert.Equal(@"C:\Users\thixi\Downloads\Telegram Desktop\导出spine\批量测试", selectionPath);
    }
}
