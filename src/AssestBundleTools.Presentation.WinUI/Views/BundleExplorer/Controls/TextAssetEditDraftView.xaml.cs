using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class TextAssetEditDraftView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TextAssetEditDraftViewModel),
            typeof(TextAssetEditDraftView),
            new PropertyMetadata(null, OnViewModelChanged));

    public TextAssetEditDraftView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public TextAssetEditDraftViewModel? ViewModel
    {
        get => (TextAssetEditDraftViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextAssetEditDraftView view)
        {
            view.Bindings.Update();
        }
    }

    private async void OnSearchButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SearchAsync();
        }
    }

    private async void OnLoadMoreLinesRequested(object? sender, EventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadMoreLinesAsync();
        }
    }

    private void ApplyLocalizedText()
    {
        SearchBox.PlaceholderText = UiText.Get(ResourceKeys.TextAssetEditorSearchPlaceholder);
        ToolTipService.SetToolTip(SearchButton, UiText.Get(ResourceKeys.TextAssetEditorSearchButtonToolTip));
    }
}
