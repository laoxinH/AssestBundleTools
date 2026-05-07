using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载 Spine atlas page 选择器的轻量展示状态。
/// </summary>
public sealed class SpineAtlasPageSelectorViewModel : ObservableObject
{
    private SpineAtlasPageOptionViewModel? _selectedOption;

    public ObservableCollection<SpineAtlasPageOptionViewModel> Options { get; } = new();

    public SpineAtlasPageOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        private set => SetProperty(ref _selectedOption, value);
    }

    public Visibility Visibility => Options.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public void Apply(
        IEnumerable<SpineAtlasPageOptionViewModel> options,
        SpineAtlasPageOptionViewModel? selectedOption)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options.Clear();
        foreach (var option in options)
        {
            Options.Add(option);
        }

        SelectedOption = selectedOption is not null && Options.Contains(selectedOption)
            ? selectedOption
            : Options.FirstOrDefault();
        OnPropertyChanged(nameof(Visibility));
    }

    public void Clear()
    {
        Options.Clear();
        SelectedOption = null;
        OnPropertyChanged(nameof(Visibility));
    }
}
