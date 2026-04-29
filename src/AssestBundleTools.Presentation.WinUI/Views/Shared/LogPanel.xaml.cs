using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.Views.Shared;

/// <summary>
/// 可复用的详细日志面板视图。
/// </summary>
public sealed partial class LogPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(LogPanelViewModel),
            typeof(LogPanel),
            new PropertyMetadata(null));

    public LogPanel()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public LogPanelViewModel? ViewModel
    {
        get => (LogPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Clear();
    }

    private void ApplyLocalizedText()
    {
        AllFilterButton.Content = UiText.Get(ResourceKeys.LogPanelAllFilterButtonContent);
        InfoFilterButton.Content = UiText.Get(ResourceKeys.LogPanelInfoFilterButtonContent);
        WarningFilterButton.Content = UiText.Get(ResourceKeys.LogPanelWarningFilterButtonContent);
        ErrorFilterButton.Content = UiText.Get(ResourceKeys.LogPanelErrorFilterButtonContent);
        ClearButton.Content = UiText.Get(ResourceKeys.LogPanelClearButtonContent);
    }
}
