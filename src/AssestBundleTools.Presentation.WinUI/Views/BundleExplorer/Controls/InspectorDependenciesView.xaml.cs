using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorDependenciesView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorDependenciesView),
            new PropertyMetadata(null));

    public InspectorDependenciesView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    public event EventHandler<ScrollViewerViewChangedEventArgs>? SectionViewChanged;

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ScrollViewer SectionScrollViewer => DependenciesSectionScrollViewer;

    private void ApplyLocalizedText()
    {
        AssetDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorAssetDependenciesTitle);
        ReferencedByTitleText.Text = UiText.Get(ResourceKeys.InspectorReferencedByTitle);
        DependencyClosureTitleText.Text = UiText.Get(ResourceKeys.InspectorDependencyClosureTitle);
        BundleExternalDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorBundleExternalDependenciesTitle);
        DependenciesEmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorDependenciesEmptyStateTitle);
        DependenciesEmptyStateText.Text = UiText.Get(ResourceKeys.InspectorDependenciesEmptyStateText);
    }

    private void OnResolvedAssetOpenButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ResolvedAssetNavigationTarget target })
        {
            ResolvedAssetOpenRequested?.Invoke(this, new ResolvedAssetOpenRequestedEventArgs(target));
        }
    }

    private void OnDependenciesSectionScrollViewerViewChanged(
        object sender,
        ScrollViewerViewChangedEventArgs e) =>
        SectionViewChanged?.Invoke(sender, e);
}
