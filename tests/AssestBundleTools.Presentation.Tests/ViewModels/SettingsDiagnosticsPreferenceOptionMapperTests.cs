using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SettingsDiagnosticsPreferenceOptionMapperTests
{
    [Theory]
    [InlineData(AssetEditorCloseBehavior.ReturnToMainWindow, 0)]
    [InlineData(AssetEditorCloseBehavior.DirectClose, 1)]
    public void AssetEditorCloseBehavior_ShouldRoundTripThroughSelectedIndex(
        AssetEditorCloseBehavior behavior,
        int selectedIndex)
    {
        Assert.Equal(
            selectedIndex,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertAssetEditorCloseBehaviorToSelectedIndex(behavior));
        Assert.Equal(
            behavior,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToAssetEditorCloseBehavior(selectedIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void AssetEditorCloseBehavior_ShouldCoerceUnknownSelectionToReturnToMainWindow(int selectedIndex)
    {
        Assert.Equal(0, SettingsDiagnosticsPreferenceOptionMapper.CoerceAssetEditorCloseBehaviorSelectedIndex(selectedIndex));
        Assert.Equal(
            AssetEditorCloseBehavior.ReturnToMainWindow,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToAssetEditorCloseBehavior(selectedIndex));
    }
}
