using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

public sealed partial class WorkspaceSetupDialogContent
{
    private void PopulateDecryptorOptions()
    {
        DecryptorComboBox.Items.Clear();

        // “自动识别”不是注册表里的真实解密器实现，而是工作区导入阶段的一种解析策略。
        // 这里把它作为首选项显式展示出来，避免用户把“未选择”误解成“默认直通”。
        DecryptorComboBox.Items.Add(CreateDecryptorComboBoxItem(
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            AssetBundleDecryptorDisplayText.GetDisplayName(
                _localizationService,
                _assetBundleDecryptorRegistry,
                AssetBundleDecryptorIds.BuiltInAutoDetect),
            AssetBundleDecryptorDisplayText.GetDescription(
                _localizationService,
                _assetBundleDecryptorRegistry,
                AssetBundleDecryptorIds.BuiltInAutoDetect)));

        foreach (var decryptor in _assetBundleDecryptorRegistry.GetAll()
                     .OrderByDescending(static decryptor => decryptor.Descriptor.IsFallback)
                     .ThenBy(static decryptor => decryptor.Descriptor.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            DecryptorComboBox.Items.Add(CreateDecryptorComboBoxItem(
                decryptor.Descriptor.Id,
                AssetBundleDecryptorDisplayText.GetDisplayName(_localizationService, decryptor.Descriptor),
                AssetBundleDecryptorDisplayText.GetDescription(_localizationService, decryptor.Descriptor)));
        }
    }

    private void SelectAssetBundleDecryptor(string? decryptorId, bool allowFallback = true)
    {
        ComboBoxItem? matchedItem = null;

        if (!string.IsNullOrWhiteSpace(decryptorId))
        {
            matchedItem = FindDecryptorComboBoxItem(decryptorId);
            matchedItem ??= EnsureDecryptorComboBoxItem(
                decryptorId.Trim(),
                decryptorId.Trim(),
                decryptorId.Trim());
        }
        else
        {
            matchedItem = FindAutoDetectDecryptorComboBoxItem();
            if (matchedItem is null && allowFallback)
            {
                matchedItem = FindFallbackDecryptorComboBoxItem()
                              ?? DecryptorComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
            }
        }

        if (ReferenceEquals(DecryptorComboBox.SelectedItem, matchedItem))
        {
            RefreshSelectedDecryptorPresentation();
            return;
        }

        DecryptorComboBox.SelectedItem = matchedItem;
        RefreshSelectedDecryptorPresentation();
    }

    private void RefreshSelectedDecryptorPresentation()
    {
        var selectedItem = DecryptorComboBox.SelectedItem as ComboBoxItem;
        var selectedOption = selectedItem?.Tag as WorkspaceSetupDecryptorOption;
        if (selectedOption is null)
        {
            DecryptorDescriptionTextBlock.Text = UiText.Get(
                _dialogMode == WorkspaceDialogMode.New
                    ? ResourceKeys.WorkspaceDialogSetupDecryptorHint
                    : ResourceKeys.WorkspaceDialogSetupOpenDecryptorHint);
            return;
        }

        DecryptorDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(selectedOption.Description)
            ? selectedItem?.Content?.ToString() ?? string.Empty
            : selectedOption.Description;
    }

    private string? GetSelectedDecryptorId()
    {
        return (DecryptorComboBox.SelectedItem as ComboBoxItem)?.Tag is WorkspaceSetupDecryptorOption option
            ? option.Id
            : null;
    }

    private ComboBoxItem? FindFallbackDecryptorComboBoxItem()
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(static item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                           string.Equals(option.Id, AssetBundleDecryptorIds.BuiltInPassThrough, StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem? FindAutoDetectDecryptorComboBoxItem()
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(static item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                           string.Equals(option.Id, AssetBundleDecryptorIds.BuiltInAutoDetect, StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem? FindDecryptorComboBoxItem(string decryptorId)
    {
        return DecryptorComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is WorkspaceSetupDecryptorOption option &&
                                    string.Equals(option.Id, decryptorId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private ComboBoxItem EnsureDecryptorComboBoxItem(
        string decryptorId,
        string displayName,
        string? description)
    {
        var existingItem = FindDecryptorComboBoxItem(decryptorId);
        if (existingItem is not null)
        {
            return existingItem;
        }

        var createdItem = CreateDecryptorComboBoxItem(decryptorId, displayName, description);
        DecryptorComboBox.Items.Add(createdItem);
        return createdItem;
    }

    private static ComboBoxItem CreateDecryptorComboBoxItem(
        string decryptorId,
        string displayName,
        string? description)
    {
        return new ComboBoxItem
        {
            Content = displayName,
            Tag = new WorkspaceSetupDecryptorOption(decryptorId, description)
        };
    }
}

file sealed record WorkspaceSetupDecryptorOption(string Id, string? Description);
