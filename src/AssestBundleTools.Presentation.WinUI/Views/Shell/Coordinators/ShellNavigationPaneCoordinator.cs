using AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.Shell.Coordinators;

/// <summary>
/// 协调 Shell 主导航栏状态与设置页内容宽度，避免 compact 层级浮层泄漏到展开态。
/// </summary>
internal sealed class ShellNavigationPaneCoordinator
{
    private readonly Frame _contentFrame;
    private readonly NavigationView _navigationView;
    private readonly NavigationViewItem _settingsNavigationItem;

    public ShellNavigationPaneCoordinator(
        NavigationView navigationView,
        NavigationViewItem settingsNavigationItem,
        Frame contentFrame)
    {
        _navigationView = navigationView ?? throw new ArgumentNullException(nameof(navigationView));
        _settingsNavigationItem = settingsNavigationItem ?? throw new ArgumentNullException(nameof(settingsNavigationItem));
        _contentFrame = contentFrame ?? throw new ArgumentNullException(nameof(contentFrame));
    }

    public void ExpandSettingsIfPaneOpen()
    {
        if (_navigationView.IsPaneOpen)
        {
            _settingsNavigationItem.IsExpanded = true;
        }
    }

    public void HandlePaneOpened()
    {
        // PaneOpening 阶段仍使用 compact 模板，此时展开层级项会遗留空的浮层外壳。
        _settingsNavigationItem.IsExpanded = true;
        ApplySettingsPageLayout(isPaneOpen: true);
    }

    public void HandlePaneClosing()
    {
        _settingsNavigationItem.IsExpanded = false;
        ApplySettingsPageLayout(isPaneOpen: false);
    }

    public void ApplyCurrentLayout()
    {
        ApplySettingsPageLayout(_navigationView.IsPaneOpen);
    }

    private void ApplySettingsPageLayout(bool isPaneOpen)
    {
        if (_contentFrame.Content is not SettingsDiagnosticsPage settingsPage)
        {
            return;
        }

        settingsPage.ApplyNavigationPaneLayout(
            isPaneOpen,
            _navigationView.OpenPaneLength - _navigationView.CompactPaneLength);
    }
}
