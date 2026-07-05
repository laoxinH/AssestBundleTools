using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml.Controls;
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
        await ApplyTextEditFromSurfaceAsync();
    }

    private async void OnTextEditSaveRequested(object sender, EventArgs e)
    {
        await ApplyTextEditFromSurfaceAsync();
    }

    private async void OnExitTextEditButtonClick(object sender, RoutedEventArgs e)
    {
        await TryExitTextEditModeAsync();
    }

    private async void OnInspectorEditRequested(object sender, EventArgs e)
    {
        await ViewModel.RequestTextEditModeForActiveAssetAsync();
    }

    private async Task<bool> TryExitTextEditModeAsync()
    {
        var draft = ViewModel.DocumentGroup.ActiveAssetTab?.TextEditDraft;
        if (draft is null)
        {
            return true;
        }

        if (!draft.HasUnsavedChanges)
        {
            return await ViewModel.ExitTextEditModeForActiveAssetAsync();
        }

        if (ContentRoot.XamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = ContentRoot.XamlRoot,
            Title = UiText.Get(ResourceKeys.TextAssetEditorUnsavedDialogTitle),
            Content = UiText.Get(ResourceKeys.TextAssetEditorUnsavedDialogDescription),
            PrimaryButtonText = UiText.Get(ResourceKeys.TextAssetEditorApplyButton),
            SecondaryButtonText = UiText.Get(ResourceKeys.CommonDiscardButton),
            CloseButtonText = UiText.Get(ResourceKeys.CommonCancelButton),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return await ApplyTextEditFromSurfaceAsync(exitWhenClean: true);
        }

        if (result == ContentDialogResult.Secondary)
        {
            return await ViewModel.ExitTextEditModeForActiveAssetAsync();
        }

        return false;
    }

    private async Task<bool> ApplyTextEditFromSurfaceAsync(bool exitWhenClean = false)
    {
        if (!await TextEditDraftView.SynchronizeEditorSaveAsync())
        {
            return false;
        }

        var draft = ViewModel.DocumentGroup.ActiveAssetTab?.TextEditDraft;
        if (draft?.CanSave == true)
        {
            var applied = await ViewModel.ApplyTextEditAsync();
            if (!draft.HasUnsavedChanges)
            {
                TextEditDraftView.AcceptSynchronizedEditorText();
            }

            return applied;
        }

        return exitWhenClean
            ? await ViewModel.ExitTextEditModeForActiveAssetAsync()
            : false;
    }
}
