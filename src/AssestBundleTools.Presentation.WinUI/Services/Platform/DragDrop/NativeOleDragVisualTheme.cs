using Microsoft.Win32;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal readonly record struct NativeOleDragVisualTheme(
    string Name,
    uint CardSurface,
    uint CardBorder,
    uint PrimaryText,
    uint IconSurface,
    uint IconGlyph,
    uint BadgeSurface,
    uint BadgeText,
    uint Shadow)
{
    public static NativeOleDragVisualTheme Light { get; } = new(
        "Light",
        CardSurface: 0x00F9F9F9,
        CardBorder: 0x00DADADA,
        PrimaryText: 0x001B1B1B,
        IconSurface: 0x00D80B32,
        IconGlyph: 0x00FFFFFF,
        BadgeSurface: 0x00E9E9E9,
        BadgeText: 0x002B2B2B,
        Shadow: 0x00000000);

    public static NativeOleDragVisualTheme Dark { get; } = new(
        "Dark",
        CardSurface: 0x002C2C2C,
        CardBorder: 0x00474747,
        PrimaryText: 0x00F5F5F5,
        IconSurface: 0x00E13A59,
        IconGlyph: 0x00FFFFFF,
        BadgeSurface: 0x00414141,
        BadgeText: 0x00F5F5F5,
        Shadow: 0x00000000);
}

internal static class NativeOleDragVisualThemeResolver
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    public static NativeOleDragVisualTheme Resolve()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            return key?.GetValue(AppsUseLightThemeValueName) is int value && value == 0
                ? NativeOleDragVisualTheme.Dark
                : NativeOleDragVisualTheme.Light;
        }
        catch
        {
            return NativeOleDragVisualTheme.Light;
        }
    }
}
