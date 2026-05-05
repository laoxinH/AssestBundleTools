using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewAnimatorControllerViewModel : ObservableObject
{
    private AssetPreviewAnimatorControllerDto? _animatorController;
    private string _assetNameText = string.Empty;
    private string _structureText = string.Empty;
    private string _stateSummaryText = string.Empty;
    private string _referenceSummaryText = string.Empty;
    private IReadOnlyList<AnimatorControllerPreviewSummaryCardViewModel> _summaryCards = Array.Empty<AnimatorControllerPreviewSummaryCardViewModel>();

    public AssetPreviewAnimatorControllerDto? AnimatorController
    {
        get => _animatorController;
        private set
        {
            if (SetProperty(ref _animatorController, value))
            {
                OnPropertyChanged(nameof(HasAnimatorController));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string StructureText
    {
        get => _structureText;
        private set => SetProperty(ref _structureText, value);
    }

    public string StateSummaryText
    {
        get => _stateSummaryText;
        private set => SetProperty(ref _stateSummaryText, value);
    }

    public string ReferenceSummaryText
    {
        get => _referenceSummaryText;
        private set => SetProperty(ref _referenceSummaryText, value);
    }

    public IReadOnlyList<AnimatorControllerPreviewSummaryCardViewModel> SummaryCards
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

    public bool HasAnimatorController => AnimatorController is not null;

    public Visibility Visibility => HasAnimatorController ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SummaryCardsVisibility =>
        SummaryCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewAnimatorControllerDto animatorController,
        AnimatorControllerPreviewDisplayState displayState)
    {
        ArgumentNullException.ThrowIfNull(animatorController);
        ArgumentNullException.ThrowIfNull(displayState);

        AnimatorController = animatorController;
        AssetNameText = displayState.AssetNameText;
        StructureText = displayState.StructureText;
        StateSummaryText = displayState.StateSummaryText;
        ReferenceSummaryText = displayState.ReferenceSummaryText;
        SummaryCards = displayState.SummaryCards
            .Select(static card => new AnimatorControllerPreviewSummaryCardViewModel(card))
            .ToArray();
    }

    internal void Clear()
    {
        AnimatorController = null;
        AssetNameText = string.Empty;
        StructureText = string.Empty;
        StateSummaryText = string.Empty;
        ReferenceSummaryText = string.Empty;
        SummaryCards = Array.Empty<AnimatorControllerPreviewSummaryCardViewModel>();
    }
}

public sealed class AnimatorControllerPreviewSummaryCardViewModel
{
    public AnimatorControllerPreviewSummaryCardViewModel(AnimatorControllerPreviewSummaryCardDisplayState card)
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
