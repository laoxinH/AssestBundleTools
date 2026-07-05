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

    public event EventHandler? SaveRequested;

    public TextAssetEditDraftViewModel? ViewModel
    {
        get => (TextAssetEditDraftViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextAssetEditDraftView view)
        {
            view.TextEditorSurface.ResetPendingDocumentChanges();
            view.Bindings.Update();
        }
    }

    private async void OnSearchButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            if (await SynchronizeEditorTextAsync())
            {
                await ViewModel.SearchAsync();
            }
        }
    }

    private async void OnLoadMoreLinesRequested(object? sender, EventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadMoreLinesAsync();
        }
    }

    private void OnSaveRequested(object? sender, EventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        ViewModel?.MarkEditorDirty();
    }

    public async Task<bool> SynchronizeEditorTextAsync()
    {
        if (ViewModel is null)
        {
            return true;
        }

        return await SynchronizeFullEditorTextAsync();
    }

    public async Task<bool> SynchronizeEditorSaveAsync()
    {
        if (ViewModel is null)
        {
            return true;
        }

        var rangeEditSync = await TextEditorSurface.GetPendingRangeEditsAsync();
        if (rangeEditSync is { Edits.Count: > 0 } &&
            ViewModel.CanUseEditorRangeEditBase(rangeEditSync.BaseLength))
        {
            ViewModel.SetEditorRangeEditsFromSurface(rangeEditSync.Edits, ViewModel.CreateCurrentPatchBaseline());
            return true;
        }

        var text = await TextEditorSurface.GetCurrentDocumentTextAsync();
        if (text is null)
        {
            return false;
        }

        ViewModel.SetEditorTextFromSurface(text);
        return true;
    }

    private async Task<bool> SynchronizeFullEditorTextAsync()
    {
        if (ViewModel is null)
        {
            return true;
        }

        var text = await TextEditorSurface.GetCurrentDocumentTextAsync();
        if (text is null)
        {
            return false;
        }

        ViewModel.SetEditorTextFromSurface(text);
        return true;
    }

    public void AcceptSynchronizedEditorText()
    {
        TextEditorSurface.AcceptCurrentDocumentAsSaved();
    }

    private void ApplyLocalizedText()
    {
        SearchBox.PlaceholderText = UiText.Get(ResourceKeys.TextAssetEditorSearchPlaceholder);
        ToolTipService.SetToolTip(SearchButton, UiText.Get(ResourceKeys.TextAssetEditorSearchButtonToolTip));
    }
}
