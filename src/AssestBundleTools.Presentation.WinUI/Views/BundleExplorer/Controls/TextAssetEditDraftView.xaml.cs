using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AssestBundleTools.Application.Editing.Dtos;
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

    private bool _hasRequestedAutomaticFullEdit;
    private TextAssetEditDraftViewModel? _observedViewModel;

    public TextAssetEditDraftView()
    {
        InitializeComponent();
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
            view.UnobserveViewModel();
            view._hasRequestedAutomaticFullEdit = false;
            view.ObserveViewModel(e.NewValue as TextAssetEditDraftViewModel);
            view.Bindings.Update();
            view.TextEditorSurface.ResetPendingDocumentChanges(forceRefreshDocument: true);
            _ = view.TryLoadFullEditWithStreamingAsync();
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

    private async void OnLoadFullEditButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.LoadFullEditAsync(TextEditorSurface);
        Bindings.Update();
    }

    private async void OnStreamingDocumentReady(object? sender, EventArgs e)
    {
        await TryLoadFullEditWithStreamingAsync();
    }

    private async void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TextAssetEditDraftViewModel.CanLoadFullEdit))
        {
            await TryLoadFullEditWithStreamingAsync();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(_observedViewModel, ViewModel))
        {
            UnobserveViewModel();
            ObserveViewModel(ViewModel);
        }

        await TryLoadFullEditWithStreamingAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnobserveViewModel();
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

    private void ObserveViewModel(TextAssetEditDraftViewModel? viewModel)
    {
        _observedViewModel = viewModel;
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        }
    }

    private void UnobserveViewModel()
    {
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
            _observedViewModel = null;
        }
    }

    private async Task TryLoadFullEditWithStreamingAsync()
    {
        if (ViewModel is null ||
            _hasRequestedAutomaticFullEdit ||
            !TextEditorSurface.CanStreamDocument ||
            !ViewModel.CanLoadFullEdit)
        {
            return;
        }

        _hasRequestedAutomaticFullEdit = true;
        await ViewModel.LoadFullEditAsync(TextEditorSurface);
        Bindings.Update();
        if (ViewModel.CanLoadFullEdit)
        {
            _hasRequestedAutomaticFullEdit = false;
        }
    }

    public async Task<bool> SynchronizeEditorTextAsync()
    {
        if (ViewModel is null)
        {
            return true;
        }

        if (ViewModel.IsReadOnly)
        {
            return true;
        }

        if (ViewModel.UsesStreamingDocumentModel)
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

        if (ViewModel.UsesStreamingDocumentModel && !ViewModel.HasUnsavedChanges)
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

    public void AcceptSynchronizedEditorText(TextAssetEditDraftPatchBaselineDto? baseline = null)
    {
        TextEditorSurface.AcceptCurrentDocumentAsSaved(baseline);
    }

    private void ApplyLocalizedText()
    {
        SearchBox.PlaceholderText = UiText.Get(ResourceKeys.TextAssetEditorSearchPlaceholder);
        LoadFullEditButtonText.Text = UiText.Get(ResourceKeys.TextAssetEditorLoadFullEditButton);
        ToolTipService.SetToolTip(LoadFullEditButton, UiText.Get(ResourceKeys.TextAssetEditorLoadFullEditButtonToolTip));
        ToolTipService.SetToolTip(SearchButton, UiText.Get(ResourceKeys.TextAssetEditorSearchButtonToolTip));
    }
}
