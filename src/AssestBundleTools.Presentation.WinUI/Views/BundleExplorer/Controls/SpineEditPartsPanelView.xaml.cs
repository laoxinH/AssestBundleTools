using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class SpineEditPartsPanelView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SpineAssetGroupEditDraftViewModel),
            typeof(SpineEditPartsPanelView),
            new PropertyMetadata(null));

    public SpineEditPartsPanelView()
    {
        InitializeComponent();
    }

    public SpineAssetGroupEditDraftViewModel? ViewModel
    {
        get => (SpineAssetGroupEditDraftViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<SpineEditPartFileSelectionRequestedEventArgs>? PartFileSelectionRequested;

    public event EventHandler? BatchMatchRequested;

    private void OnSelectPartFileButtonClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SpineAssetGroupEditPartViewModel part)
        {
            PartFileSelectionRequested?.Invoke(this, new SpineEditPartFileSelectionRequestedEventArgs(part));
        }
    }

    private void OnBatchMatchButtonClick(object sender, RoutedEventArgs e)
    {
        BatchMatchRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SpineEditPartFileSelectionRequestedEventArgs : EventArgs
{
    public SpineEditPartFileSelectionRequestedEventArgs(SpineAssetGroupEditPartViewModel part)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
    }

    public SpineAssetGroupEditPartViewModel Part { get; }
}
