using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

/// <summary>
/// 右侧详情检查器的可复用视图。
/// </summary>
public sealed partial class InspectorPanelView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorPanelView),
            new PropertyMetadata(null));

    public InspectorPanelView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler? ExtractRequested;

    public event EventHandler? SelectFileReplacementSourceRequested;

    public event EventHandler? SelectReplacementSourceRequested;

    public event EventHandler? ApplyReplacementRequested;

    public event EventHandler? UndoReplacementRequested;

    private void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        ExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectFileReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectFileReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplyReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        ApplyReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        UndoReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLocalizedText()
    {
        OverviewTab.Header = UiText.Get("InspectorPanelView.OverviewTab.Header");
        PreviewTab.Header = UiText.Get("InspectorPanelView.PreviewTab.Header");
        DependenciesTab.Header = UiText.Get("InspectorPanelView.DependenciesTab.Header");
        ReplacementTab.Header = UiText.Get("InspectorPanelView.ReplacementTab.Header");
        ChangesTab.Header = UiText.Get("InspectorPanelView.ChangesTab.Header");

        EmptyStateTitleText.Text = UiText.Get("InspectorPanelView.EmptyStateTitleText.Text");
        AssetNameLabelText.Text = UiText.Get("InspectorPanelView.AssetNameLabelText.Text");
        AssetTypeLabelText.Text = UiText.Get("InspectorPanelView.AssetTypeLabelText.Text");
        BundleLabelText.Text = UiText.Get("InspectorPanelView.BundleLabelText.Text");
        AssetPathLabelText.Text = UiText.Get("InspectorPanelView.AssetPathLabelText.Text");
        ContainerPathLabelText.Text = UiText.Get("InspectorPanelView.ContainerPathLabelText.Text");
        AssetSizeLabelText.Text = UiText.Get("InspectorPanelView.AssetSizeLabelText.Text");
        PathIdLabelText.Text = UiText.Get("InspectorPanelView.PathIdLabelText.Text");
        PreviewInfoSectionTitleText.Text = UiText.Get("InspectorPanelView.PreviewInfoLabelText.Text");
        PreviewDiagnosticTitleText.Text = UiText.Get("InspectorPanelView.PreviewDiagnosticTitleText.Text");
        PreviewCacheStatusLabelText.Text = UiText.Get("InspectorPanelView.PreviewCacheStatusLabelText.Text");
        PreviewFallbackReasonLabelText.Text = UiText.Get("InspectorPanelView.PreviewFallbackReasonLabelText.Text");
        PreviewDiagnosticDetailLabelText.Text = UiText.Get("InspectorPanelView.PreviewDiagnosticDetailLabelText.Text");
        AssetDependenciesTitleText.Text = UiText.Get("InspectorPanelView.AssetDependenciesTitleText.Text");
        ReferencedByTitleText.Text = UiText.Get("InspectorPanelView.ReferencedByTitleText.Text");
        DependencyClosureTitleText.Text = UiText.Get("InspectorPanelView.DependencyClosureTitleText.Text");
        BundleExternalDependenciesTitleText.Text = UiText.Get("InspectorPanelView.BundleExternalDependenciesTitleText.Text");
        DependenciesEmptyStateText.Text = UiText.Get("InspectorPanelView.DependenciesEmptyStateText.Text");
        ReplacementTargetTitleText.Text = UiText.Get("InspectorPanelView.ReplacementTargetTitleText.Text");
        ReplacementSourceTitleText.Text = UiText.Get("InspectorPanelView.ReplacementSourceTitleText.Text");
        ReplacementCompatibilityTitleText.Text = UiText.Get("InspectorPanelView.ReplacementCompatibilityTitleText.Text");
        ExtractCapabilityLabelText.Text = UiText.Get("InspectorPanelView.ExtractCapabilityLabelText.Text");
        FileReplacementCapabilityLabelText.Text = UiText.Get("InspectorPanelView.FileReplacementCapabilityLabelText.Text");
        WorkspaceReplacementCapabilityLabelText.Text = UiText.Get("InspectorPanelView.WorkspaceReplacementCapabilityLabelText.Text");
        ChangeSummaryTitleText.Text = UiText.Get("InspectorPanelView.ChangeSummaryTitleText.Text");

        ExtractButton.Content = UiText.Get("InspectorPanelView.ExtractButton.Content");
        SelectFileReplacementButton.Content = UiText.Get("InspectorPanelView.SelectFileReplacementButton.Content");
        SelectReplacementButton.Content = UiText.Get("InspectorPanelView.SelectReplacementButton.Content");
        ApplyReplacementButton.Content = UiText.Get("InspectorPanelView.ApplyReplacementButton.Content");
        UndoReplacementButton.Content = UiText.Get("InspectorPanelView.UndoReplacementButton.Content");
    }
}
