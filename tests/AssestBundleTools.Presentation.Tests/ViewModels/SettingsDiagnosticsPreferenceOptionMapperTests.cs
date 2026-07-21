using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SettingsDiagnosticsPreferenceOptionMapperTests
{
    [Theory]
    [InlineData(AppDisplayLanguage.SystemDefault, 0)]
    [InlineData(AppDisplayLanguage.SimplifiedChinese, 1)]
    [InlineData(AppDisplayLanguage.English, 2)]
    public void DisplayLanguage_ShouldRoundTripThroughSelectedIndex(
        AppDisplayLanguage language,
        int selectedIndex)
    {
        Assert.Equal(
            selectedIndex,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertDisplayLanguageToSelectedIndex(language));
        Assert.Equal(
            language,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToDisplayLanguage(selectedIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void DisplayLanguage_ShouldCoerceUnknownSelectionToSystemDefault(int selectedIndex)
    {
        Assert.Equal(0, SettingsDiagnosticsPreferenceOptionMapper.CoerceDisplayLanguageSelectedIndex(selectedIndex));
        Assert.Equal(
            AppDisplayLanguage.SystemDefault,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToDisplayLanguage(selectedIndex));
    }

    [Theory]
    [InlineData(AppThemePreference.SystemDefault, 0)]
    [InlineData(AppThemePreference.Light, 1)]
    [InlineData(AppThemePreference.Dark, 2)]
    public void Theme_ShouldRoundTripThroughSelectedIndex(AppThemePreference theme, int selectedIndex)
    {
        Assert.Equal(selectedIndex, SettingsDiagnosticsPreferenceOptionMapper.ConvertThemeToSelectedIndex(theme));
        Assert.Equal(theme, SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToTheme(selectedIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Theme_ShouldCoerceUnknownSelectionToSystemDefault(int selectedIndex)
    {
        Assert.Equal(0, SettingsDiagnosticsPreferenceOptionMapper.CoerceThemeSelectedIndex(selectedIndex));
        Assert.Equal(
            AppThemePreference.SystemDefault,
            SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToTheme(selectedIndex));
    }

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
