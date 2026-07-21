using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IAppThemeService
{
    AppThemePreference CurrentTheme { get; }

    void Apply(AppThemePreference theme);
}

/// <summary>
/// 将应用主题统一投影到当前会话中的所有窗口根元素。
/// </summary>
internal sealed class AppThemeService : IAppThemeService
{
    private readonly HashSet<Window> _windows = [];
    private readonly Dictionary<FrameworkElement, Window> _windowRoots = [];

    public AppThemeService(AppThemePreference initialTheme)
    {
        CurrentTheme = initialTheme;
    }

    public AppThemePreference CurrentTheme { get; private set; }

    public void Apply(AppThemePreference theme)
    {
        CurrentTheme = theme;
        foreach (var window in _windows.ToArray())
        {
            ApplyToWindow(window, theme);
        }
    }

    internal void RegisterWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows.Add(window);
        if (window.Content is FrameworkElement rootElement && !_windowRoots.ContainsKey(rootElement))
        {
            _windowRoots[rootElement] = window;
            rootElement.ActualThemeChanged += OnRootActualThemeChanged;
        }

        ApplyToWindow(window, CurrentTheme);
    }

    internal void UnregisterWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows.Remove(window);
        var rootEntry = _windowRoots.FirstOrDefault(pair => ReferenceEquals(pair.Value, window));
        if (rootEntry.Key is not null)
        {
            rootEntry.Key.ActualThemeChanged -= OnRootActualThemeChanged;
            _windowRoots.Remove(rootEntry.Key);
        }
    }

    internal static ElementTheme ToElementTheme(AppThemePreference theme) => theme switch
    {
        AppThemePreference.Light => ElementTheme.Light,
        AppThemePreference.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private static void ApplyToWindow(Window window, AppThemePreference theme)
    {
        if (window.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ToElementTheme(theme);
            ModernWindowAppearanceService.ApplyTitleBarTheme(window, rootElement.ActualTheme);
        }
    }

    private void OnRootActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_windowRoots.TryGetValue(sender, out var window))
        {
            ModernWindowAppearanceService.ApplyTitleBarTheme(window, sender.ActualTheme);
        }
    }
}
