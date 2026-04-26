using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        AllFilterButton.Content = UiText.Get("LogPanel.AllFilterButton.Content");
        InfoFilterButton.Content = UiText.Get("LogPanel.InfoFilterButton.Content");
        WarningFilterButton.Content = UiText.Get("LogPanel.WarningFilterButton.Content");
        ErrorFilterButton.Content = UiText.Get("LogPanel.ErrorFilterButton.Content");
        ClearButton.Content = UiText.Get("LogPanel.ClearButton.Content");
    }
}
