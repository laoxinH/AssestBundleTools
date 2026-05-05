using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorInfoCardView : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(
            nameof(Card),
            typeof(InspectorInfoCardViewModel),
            typeof(InspectorInfoCardView),
            new PropertyMetadata(null));

    public InspectorInfoCardView()
    {
        InitializeComponent();
    }

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    public InspectorInfoCardViewModel? Card
    {
        get => (InspectorInfoCardViewModel?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
    {
        Card?.ToggleExpanded();
    }

    private void OnCopyButtonClick(object sender, RoutedEventArgs e)
    {
        if (Card is not { CopyPayloadText.Length: > 0 } card ||
            string.IsNullOrWhiteSpace(card.CopyPayloadText))
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(card.CopyPayloadText);
        Clipboard.SetContent(dataPackage);
    }

    private void OnResolvedAssetOpenButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ResolvedAssetNavigationTarget target })
        {
            ResolvedAssetOpenRequested?.Invoke(this, new ResolvedAssetOpenRequestedEventArgs(target));
        }
    }
}
