using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

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
        InformationLevelText = UiText.Get(ResourceKeys.LogPanelInfoFilterButtonContent);
        WarningLevelText = UiText.Get(ResourceKeys.LogPanelWarningFilterButtonContent);
        ErrorLevelText = UiText.Get(ResourceKeys.LogPanelErrorFilterButtonContent);
        CopyEntryToolTipText = UiText.Get(ResourceKeys.LogPanelCopyEntryButtonToolTip);

        InitializeComponent();
        ApplyLocalizedText();
    }

    public string InformationLevelText { get; }

    public string WarningLevelText { get; }

    public string ErrorLevelText { get; }

    public string CopyEntryToolTipText { get; }

    public LogPanelViewModel? ViewModel
    {
        get => (LogPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnAllFilterButtonChecked(object sender, RoutedEventArgs e) =>
        ViewModel?.SetFilter(LogPanelFilter.All);

    private void OnInfoFilterButtonChecked(object sender, RoutedEventArgs e) =>
        ViewModel?.SetFilter(LogPanelFilter.Information);

    private void OnWarningFilterButtonChecked(object sender, RoutedEventArgs e) =>
        ViewModel?.SetFilter(LogPanelFilter.Warning);

    private void OnErrorFilterButtonChecked(object sender, RoutedEventArgs e) =>
        ViewModel?.SetFilter(LogPanelFilter.Error);

    private void OnCopyEntryButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string copyText })
        {
            SetClipboardText(copyText);
        }
    }

    private void OnCopyAllButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            SetClipboardText(ViewModel.BuildCopyAllText());
        }
    }

    private void OnClearButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel?.Clear();

    private void ApplyLocalizedText()
    {
        AllFilterButton.Content = UiText.Get(ResourceKeys.LogPanelAllFilterButtonContent);
        InfoFilterButton.Content = InformationLevelText;
        WarningFilterButton.Content = WarningLevelText;
        ErrorFilterButton.Content = ErrorLevelText;

        var copyAllText = UiText.Get(ResourceKeys.LogPanelCopyAllButtonContent);
        CopyAllButtonText.Text = copyAllText;
        AutomationProperties.SetName(CopyAllButton, copyAllText);
        ToolTipService.SetToolTip(CopyAllButton, copyAllText);

        var clearText = UiText.Get(ResourceKeys.LogPanelClearButtonContent);
        ClearButtonText.Text = clearText;
        AutomationProperties.SetName(ClearButton, clearText);
        ToolTipService.SetToolTip(ClearButton, clearText);
    }

    private static void SetClipboardText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }
}
