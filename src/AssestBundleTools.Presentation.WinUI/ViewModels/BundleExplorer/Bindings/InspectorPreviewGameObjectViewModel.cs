using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewGameObjectViewModel : ObservableObject
{
    private AssetPreviewGameObjectDto? _gameObject;
    private string _assetNameText = string.Empty;
    private string _stateText = string.Empty;
    private string _componentSummaryText = string.Empty;
    private string _transformSummaryText = string.Empty;
    private string _hierarchySummaryText = string.Empty;
    private IReadOnlyList<GameObjectPreviewSummaryCardViewModel> _summaryCards = Array.Empty<GameObjectPreviewSummaryCardViewModel>();

    public AssetPreviewGameObjectDto? GameObject
    {
        get => _gameObject;
        private set
        {
            if (SetProperty(ref _gameObject, value))
            {
                OnPropertyChanged(nameof(HasGameObject));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string ComponentSummaryText
    {
        get => _componentSummaryText;
        private set => SetProperty(ref _componentSummaryText, value);
    }

    public string TransformSummaryText
    {
        get => _transformSummaryText;
        private set => SetProperty(ref _transformSummaryText, value);
    }

    public string HierarchySummaryText
    {
        get => _hierarchySummaryText;
        private set
        {
            if (SetProperty(ref _hierarchySummaryText, value))
            {
                OnPropertyChanged(nameof(HierarchySummaryVisibility));
            }
        }
    }

    public IReadOnlyList<GameObjectPreviewSummaryCardViewModel> SummaryCards
    {
        get => _summaryCards;
        private set
        {
            if (SetProperty(ref _summaryCards, value))
            {
                OnPropertyChanged(nameof(SummaryCardsVisibility));
            }
        }
    }

    public bool HasGameObject => GameObject is not null;

    public Visibility Visibility => HasGameObject ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HierarchySummaryVisibility =>
        string.IsNullOrWhiteSpace(HierarchySummaryText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SummaryCardsVisibility =>
        SummaryCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewGameObjectDto gameObject,
        GameObjectPreviewDisplayState displayState)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        ArgumentNullException.ThrowIfNull(displayState);

        GameObject = gameObject;
        AssetNameText = displayState.AssetNameText;
        StateText = displayState.StateText;
        ComponentSummaryText = displayState.ComponentSummaryText;
        TransformSummaryText = displayState.TransformSummaryText;
        HierarchySummaryText = displayState.HierarchySummaryText;
        SummaryCards = displayState.SummaryCards
            .Select(static card => new GameObjectPreviewSummaryCardViewModel(card))
            .ToArray();
    }

    internal void Clear()
    {
        GameObject = null;
        AssetNameText = string.Empty;
        StateText = string.Empty;
        ComponentSummaryText = string.Empty;
        TransformSummaryText = string.Empty;
        HierarchySummaryText = string.Empty;
        SummaryCards = Array.Empty<GameObjectPreviewSummaryCardViewModel>();
    }
}

public sealed class GameObjectPreviewSummaryCardViewModel
{
    public GameObjectPreviewSummaryCardViewModel(GameObjectPreviewSummaryCardDisplayState card)
    {
        TitleText = string.IsNullOrWhiteSpace(card.TitleText) ? string.Empty : card.TitleText.Trim();
        ValueText = string.IsNullOrWhiteSpace(card.ValueText) ? string.Empty : card.ValueText.Trim();
        DetailText = string.IsNullOrWhiteSpace(card.DetailText) ? string.Empty : card.DetailText.Trim();
    }

    public string TitleText { get; }

    public string ValueText { get; }

    public string DetailText { get; }

    public Visibility DetailVisibility =>
        string.IsNullOrWhiteSpace(DetailText) ? Visibility.Collapsed : Visibility.Visible;
}
