using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 独立 Spine 资源组预览窗口，复用资源详情检查器模板承载组文档。
/// </summary>
public sealed partial class SpineAssetGroupEditorWindow : Microsoft.UI.Xaml.Window
{
    private readonly Func<ResolvedAssetNavigationTarget, Task> _openResolvedAssetAsync;
    private bool _isInitialized;

    public SpineAssetGroupEditorWindow(
        SpineAssetGroupEditorWindowViewModel viewModel,
        Func<ResolvedAssetNavigationTarget, Task> openResolvedAssetAsync)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _openResolvedAssetAsync = openResolvedAssetAsync ?? throw new ArgumentNullException(nameof(openResolvedAssetAsync));
        InitializeComponent();
        UpdateWindowTitle();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.EmptyGroupRequested += OnEmptyGroupRequested;
        Closed += OnClosed;
    }

    public SpineAssetGroupEditorWindowViewModel ViewModel { get; }

    private async void OnContentRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ViewModel.ActivateTabAsync(ViewModel.ActiveTab);
    }

    private async void OnSpineGroupTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpineGroupTabView.SelectedItem is SpineAssetGroupDocumentTabViewModel tab)
        {
            await ViewModel.ActivateTabAsync(tab);
        }
    }

    private async void OnSpineGroupTabViewTabCloseRequested(
        TabView sender,
        TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is SpineAssetGroupDocumentTabViewModel tab)
        {
            await ViewModel.CloseTabAsync(tab);
            return;
        }

        if (args.Tab is FrameworkElement { DataContext: SpineAssetGroupDocumentTabViewModel dataContextTab })
        {
            await ViewModel.CloseTabAsync(dataContextTab);
        }
    }

    private async void OnResolvedAssetOpenRequested(
        object sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        await _openResolvedAssetAsync(e.Target);
    }

    private async void OnSpineAtlasPageSelectionRequested(
        object sender,
        SpineAtlasPageSelectionRequestedEventArgs e)
    {
        await ViewModel.SelectAtlasPageAsync(e.Option);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpineAssetGroupEditorWindowViewModel.WindowTitle))
        {
            UpdateWindowTitle();
        }

        Bindings.Update();
    }

    private void OnEmptyGroupRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.EmptyGroupRequested -= OnEmptyGroupRequested;
        Closed -= OnClosed;
    }

    private void UpdateWindowTitle()
    {
        Title = ViewModel.WindowTitle;
    }
}
