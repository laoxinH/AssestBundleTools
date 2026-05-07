using System.Collections.ObjectModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

internal sealed class AssetPaneAssetTypeFilterState
{
    public ObservableCollection<AssetTypeOptionViewModel> Options { get; } = new();

    public bool IsReplacingOptions { get; private set; }

    public AssetTypeOptionViewModel? SelectedOption { get; private set; }

    public string? SelectedFilterKey { get; private set; }

    public AssetType? SelectedAssetType =>
        AssetTypeOptionViewModel.TryParseFilterKey(
            SelectedFilterKey,
            out var assetType,
            out _)
            ? assetType
            : null;

    public string? SelectedAssetTypeDisplayNameFilter =>
        AssetTypeOptionViewModel.TryParseFilterKey(
            SelectedFilterKey,
            out _,
            out var assetTypeDisplayNameFilter)
            ? assetTypeDisplayNameFilter
            : null;

    public void ReplaceOptions(IEnumerable<AssetTypeOptionViewModel> options)
    {
        var selectedFilterKey = SelectedFilterKey;
        var materializedOptions = options.ToArray();
        if (AreEquivalent(materializedOptions))
        {
            SetSelectedOption(
                SelectOption(selectedFilterKey) ?? Options.FirstOrDefault(),
                syncFilterKeyFromOption: true);
            return;
        }

        IsReplacingOptions = true;
        try
        {
            Options.Clear();
            foreach (var option in materializedOptions)
            {
                Options.Add(option);
            }

            var selectedOption = SelectOption(selectedFilterKey) ??
                                 EnsureRestoredOption(selectedFilterKey) ??
                                 Options.FirstOrDefault();
            SetSelectedOption(selectedOption, syncFilterKeyFromOption: false);
            SetSelectedFilterKey(selectedOption?.FilterKey);
        }
        finally
        {
            IsReplacingOptions = false;
        }
    }

    public void SetSelectedOption(
        AssetTypeOptionViewModel? option,
        bool syncFilterKeyFromOption)
    {
        SelectedOption = option;
        if (syncFilterKeyFromOption && !IsReplacingOptions)
        {
            SetSelectedFilterKey(option?.FilterKey);
        }
    }

    public bool SetSelectedFilterKey(string? filterKey)
    {
        var normalizedFilterKey = NormalizeFilterKey(filterKey);
        if (string.Equals(SelectedFilterKey, normalizedFilterKey, StringComparison.Ordinal))
        {
            return false;
        }

        SelectedFilterKey = normalizedFilterKey;
        return true;
    }

    public AssetTypeOptionViewModel? SelectOption(string? filterKey)
    {
        return string.IsNullOrWhiteSpace(filterKey)
            ? Options.FirstOrDefault(option => option.AssetType is null)
            : Options.FirstOrDefault(option => option.MatchesFilterKey(filterKey));
    }

    public AssetTypeOptionViewModel? EnsureRestoredOption(string? filterKey)
    {
        var restoredOption = AssetTypeOptionViewModel.CreateFromFilterKey(filterKey);
        if (restoredOption.AssetType is null)
        {
            return null;
        }

        Options.Add(restoredOption);
        return restoredOption;
    }

    private bool AreEquivalent(IReadOnlyList<AssetTypeOptionViewModel> options)
    {
        if (Options.Count != options.Count)
        {
            return false;
        }

        for (var index = 0; index < options.Count; index++)
        {
            var current = Options[index];
            var next = options[index];
            if (!string.Equals(current.FilterKey, next.FilterKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.DisplayName, next.DisplayName, StringComparison.Ordinal) ||
                current.Count != next.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeFilterKey(string? filterKey) =>
        string.IsNullOrWhiteSpace(filterKey)
            ? null
            : filterKey.Trim();
}
