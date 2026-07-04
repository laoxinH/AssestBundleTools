using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class AssetEditorWindow
{
    private async void OnEditTextButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RequestTextEditModeForActiveAssetAsync();
    }

    private async void OnSaveTextEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyTextEditAsync();
    }

    private async void OnExitTextEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExitTextEditModeForActiveAssetAsync();
    }

    private async void OnInspectorEditRequested(object sender, EventArgs e)
    {
        await ViewModel.RequestTextEditModeForActiveAssetAsync();
    }
}
