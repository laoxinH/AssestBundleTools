using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewMonoBehaviourViewModel : ObservableObject
{
    private AssetPreviewMonoBehaviourDto? _monoBehaviour;
    private string _assetNameText = string.Empty;
    private string _scriptText = string.Empty;
    private string _stateText = string.Empty;
    private string _fieldSummaryText = string.Empty;
    private string _referenceSummaryText = string.Empty;
    private IReadOnlyList<MonoBehaviourPreviewSummaryCardViewModel> _summaryCards = Array.Empty<MonoBehaviourPreviewSummaryCardViewModel>();

    public AssetPreviewMonoBehaviourDto? MonoBehaviour
    {
        get => _monoBehaviour;
        private set
        {
            if (SetProperty(ref _monoBehaviour, value))
            {
                OnPropertyChanged(nameof(HasMonoBehaviour));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string ScriptText
    {
        get => _scriptText;
        private set => SetProperty(ref _scriptText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string FieldSummaryText
    {
        get => _fieldSummaryText;
        private set => SetProperty(ref _fieldSummaryText, value);
    }

    public string ReferenceSummaryText
    {
        get => _referenceSummaryText;
        private set => SetProperty(ref _referenceSummaryText, value);
    }

    public IReadOnlyList<MonoBehaviourPreviewSummaryCardViewModel> SummaryCards
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

    public bool HasMonoBehaviour => MonoBehaviour is not null;

    public Visibility Visibility => HasMonoBehaviour ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SummaryCardsVisibility =>
        SummaryCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        MonoBehaviourPreviewDisplayState displayState)
    {
        ArgumentNullException.ThrowIfNull(monoBehaviour);
        ArgumentNullException.ThrowIfNull(displayState);

        MonoBehaviour = monoBehaviour;
        AssetNameText = displayState.AssetNameText;
        ScriptText = displayState.ScriptText;
        StateText = displayState.StateText;
        FieldSummaryText = displayState.FieldSummaryText;
        ReferenceSummaryText = displayState.ReferenceSummaryText;
        SummaryCards = displayState.SummaryCards
            .Select(static card => new MonoBehaviourPreviewSummaryCardViewModel(card))
            .ToArray();
    }

    internal void Clear()
    {
        MonoBehaviour = null;
        AssetNameText = string.Empty;
        ScriptText = string.Empty;
        StateText = string.Empty;
        FieldSummaryText = string.Empty;
        ReferenceSummaryText = string.Empty;
        SummaryCards = Array.Empty<MonoBehaviourPreviewSummaryCardViewModel>();
    }
}

public sealed class MonoBehaviourPreviewSummaryCardViewModel
{
    public MonoBehaviourPreviewSummaryCardViewModel(MonoBehaviourPreviewSummaryCardDisplayState card)
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
