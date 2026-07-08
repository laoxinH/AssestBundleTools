using System.Globalization;
using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示右侧详情区域中的单个 Spine 资源组文档。
/// </summary>
public sealed class SpineAssetGroupDocumentTabViewModel : ObservableObject
{
    private SpineAssetGroupDto _item;
    private SpineAssetGroupDetailsDto? _details;
    private SpineAssetGroupTexturePreviewState? _texturePreview;
    private IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> _animationTextures =
        new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
    private SpineAtlasPageOptionViewModel? _selectedAtlasPageOption;
    private WorkspaceSpinePreviewStateDto? _previewState;
    private string _displayName;
    private string _toolTipText;
    private SpineAssetGroupEditDraftViewModel? _spineEditDraft;
    private string? _loadedPreviewInputSignature;

    public SpineAssetGroupDocumentTabViewModel(
        SpineAssetGroupDto item,
        InspectorInformationTextSet inspectorInformationTextSet)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _displayName = ResolveDisplayName(item);
        _toolTipText = CreateToolTipText(item);
        Inspector = new InspectorPanelViewModel(
            inspectorInformationTextSet ?? throw new ArgumentNullException(nameof(inspectorInformationTextSet)));
        Inspector.SectionScrollPositionChanged += OnInspectorSectionScrollPositionChanged;
        Inspector.Preview.SpineAnimation.PreviewStateChanged += OnSpineAnimationPreviewStateChanged;
    }

    public event EventHandler? InspectorScrollStateChanged;

    public event EventHandler? PreviewStateChanged;

    public SpineAssetGroupDto Item
    {
        get => _item;
        private set => SetProperty(ref _item, value);
    }

    public BundleId BundleId => Item.BundleId;

    public string GroupId => Item.GroupId;

    public string DocumentId => GroupId;

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        private set => SetProperty(ref _toolTipText, value);
    }

    public SpineAssetGroupDetailsDto? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    internal SpineAssetGroupTexturePreviewState? TexturePreview
    {
        get => _texturePreview;
        private set => SetProperty(ref _texturePreview, value);
    }

    internal IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> AnimationTextures
    {
        get => _animationTextures;
        private set => SetProperty(ref _animationTextures, value);
    }

    public ObservableCollection<SpineAtlasPageOptionViewModel> AtlasPageOptions { get; } = new();

    public SpineAtlasPageOptionViewModel? SelectedAtlasPageOption
    {
        get => _selectedAtlasPageOption;
        private set => SetProperty(ref _selectedAtlasPageOption, value);
    }

    internal WorkspaceSpinePreviewStateDto? PreviewState
    {
        get => _previewState;
        private set => SetProperty(ref _previewState, value);
    }

    public InspectorPanelViewModel Inspector { get; }

    public SpineAssetGroupEditDraftViewModel? SpineEditDraft
    {
        get => _spineEditDraft;
        private set
        {
            if (SetProperty(ref _spineEditDraft, value))
            {
                Inspector.SetReplacementSectionAvailability(value is not null);
                OnPropertyChanged(nameof(IsSpineEditModeActive));
                OnPropertyChanged(nameof(SpineEditPanelVisibility));
                OnPropertyChanged(nameof(SpineEditActionBarVisibility));
                OnPropertyChanged(nameof(SpineEditNormalActionBarVisibility));
                OnPropertyChanged(nameof(CanSaveSpineEdit));
                OnPropertyChanged(nameof(CanUndoSpineEdit));
                OnPropertyChanged(nameof(CanRedoSpineEdit));
                PreviewStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsSpineEditModeActive => SpineEditDraft is not null;

    public Visibility SpineEditPanelVisibility => IsSpineEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpineEditActionBarVisibility => IsSpineEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpineEditNormalActionBarVisibility => IsSpineEditModeActive ? Visibility.Collapsed : Visibility.Visible;

    public bool CanSaveSpineEdit => SpineEditDraft?.HasChanges == true;

    public bool CanUndoSpineEdit => SpineEditDraft?.CanUndo == true;

    public bool CanRedoSpineEdit => SpineEditDraft?.CanRedo == true;

    internal string? LoadedPreviewInputSignature
    {
        get => _loadedPreviewInputSignature;
        private set => _loadedPreviewInputSignature = value;
    }

    public void ApplyGroup(SpineAssetGroupDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Details = null;
        TexturePreview = null;
        AnimationTextures = new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
        LoadedPreviewInputSignature = null;
        ClearAtlasPageOptions();
        Item = item;
        DisplayName = ResolveDisplayName(item);
        ToolTipText = CreateToolTipText(item);
    }

    internal void StartSpineEditMode(SpineAssetGroupEditDraftViewModel editDraft)
    {
        ArgumentNullException.ThrowIfNull(editDraft);

        SpineEditDraft = editDraft;
    }

    internal bool ExitSpineEditMode(bool discardChanges)
    {
        var hadUnsavedChanges = SpineEditDraft?.HasChanges == true;
        SpineEditDraft = null;
        return hadUnsavedChanges;
    }

    internal void ClearDetails()
    {
        Details = null;
        TexturePreview = null;
        AnimationTextures = new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
        LoadedPreviewInputSignature = null;
        ClearAtlasPageOptions();
    }

    internal void NotifySpineEditStateChanged()
    {
        SpineEditDraft?.NotifyStateChanged();
        OnPropertyChanged(nameof(CanSaveSpineEdit));
        OnPropertyChanged(nameof(CanUndoSpineEdit));
        OnPropertyChanged(nameof(CanRedoSpineEdit));
        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplyDetails(
        SpineAssetGroupDetailsDto details,
        SpineAssetGroupTexturePreviewState? texturePreview = null,
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState>? animationTextures = null)
    {
        ArgumentNullException.ThrowIfNull(details);

        Item = details.Group;
        Details = details;
        ReplaceAtlasPageOptions(details, texturePreview?.AtlasPage);
        TexturePreview = texturePreview;
        AnimationTextures = animationTextures is { Count: > 0 }
            ? new Dictionary<string, SpineAnimationPreviewTextureState>(animationTextures, StringComparer.Ordinal)
            : new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
        DisplayName = ResolveDisplayName(details.Group);
        ToolTipText = CreateToolTipText(details.Group);
    }

    internal void MarkPreviewInputLoaded(string inputSignature)
    {
        LoadedPreviewInputSignature = inputSignature ?? throw new ArgumentNullException(nameof(inputSignature));
    }

    internal void ApplyOperationProjection(SpineAssetGroupDto group)
    {
        ArgumentNullException.ThrowIfNull(group);

        Item = group;
        if (Details is not null)
        {
            Details = Details with { Group = group };
        }

        DisplayName = ResolveDisplayName(group);
        ToolTipText = CreateToolTipText(group);
    }

    internal bool SelectAtlasPageOption(SpineAtlasPageOptionViewModel option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!AtlasPageOptions.Contains(option) ||
            ReferenceEquals(SelectedAtlasPageOption, option))
        {
            return false;
        }

        SelectedAtlasPageOption = option;
        TexturePreview = null;
        CapturePreviewState();
        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    internal void RestorePreviewState(WorkspaceSpinePreviewStateDto? previewState)
    {
        PreviewState = previewState;
        RestoreSelectedAtlasPageOption(previewState);
        Inspector.Preview.SpineAnimation.RestorePreviewState(previewState);
    }

    internal void RestoreInspectorScrollState(WorkspaceInspectorScrollStateDto? scrollState)
    {
        Inspector.RestoreInspectorScrollState(scrollState);
    }

    internal WorkspaceSpinePreviewStateDto? CapturePreviewState()
    {
        if (Inspector.Preview.SpineAnimation.Preview is null &&
            PreviewState is not null)
        {
            return PreviewState;
        }

        var animationState = Inspector.Preview.SpineAnimation.CapturePreviewState();
        var state = animationState with
        {
            SelectedAtlasPageName = NormalizeAtlasPageName(SelectedAtlasPageOption?.AtlasPage.Name),
            SelectedAtlasPageIndex = SelectedAtlasPageOption?.Index
        };
        PreviewState = state;
        return state;
    }

    internal void ApplyTexturePreview(SpineAssetGroupTexturePreviewState? texturePreview)
    {
        if (texturePreview is not null &&
            TryFindAtlasPageOption(texturePreview.AtlasPage, out var atlasPageOption))
        {
            SelectedAtlasPageOption = atlasPageOption;
        }

        TexturePreview = texturePreview;
        CapturePreviewState();
    }

    private void ReplaceAtlasPageOptions(
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageDto? preferredAtlasPage)
    {
        var previousSelectedIndex = SelectedAtlasPageOption?.Index;
        AtlasPageOptions.Clear();
        for (var index = 0; index < details.AtlasPages.Count; index++)
        {
            AtlasPageOptions.Add(new SpineAtlasPageOptionViewModel(index, details.AtlasPages[index]));
        }

        SelectedAtlasPageOption = ResolveSelectedAtlasPageOption(
            preferredAtlasPage,
            previousSelectedIndex,
            PreviewState);
    }

    private SpineAtlasPageOptionViewModel? ResolveSelectedAtlasPageOption(
        SpineAtlasPageDto? preferredAtlasPage,
        int? previousSelectedIndex,
        WorkspaceSpinePreviewStateDto? previewState)
    {
        if (TryFindAtlasPageOption(previewState, out var previewStateOption))
        {
            return previewStateOption;
        }

        if (preferredAtlasPage is not null &&
            TryFindAtlasPageOption(preferredAtlasPage, out var preferredOption))
        {
            return preferredOption;
        }

        if (previousSelectedIndex is >= 0 &&
            previousSelectedIndex.Value < AtlasPageOptions.Count)
        {
            return AtlasPageOptions[previousSelectedIndex.Value];
        }

        return AtlasPageOptions.FirstOrDefault(static option =>
                   option.AtlasPage.TexturePageAsset is not null) ??
               AtlasPageOptions.FirstOrDefault();
    }

    private bool TryFindAtlasPageOption(
        SpineAtlasPageDto atlasPage,
        out SpineAtlasPageOptionViewModel option)
    {
        option = AtlasPageOptions.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.AtlasPage, atlasPage) ||
            string.Equals(candidate.AtlasPage.Name, atlasPage.Name, StringComparison.Ordinal))!;
        return option is not null;
    }

    private bool TryFindAtlasPageOption(
        WorkspaceSpinePreviewStateDto? previewState,
        out SpineAtlasPageOptionViewModel option)
    {
        option = null!;
        if (previewState is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(previewState.SelectedAtlasPageName))
        {
            var atlasPageName = previewState.SelectedAtlasPageName.Trim();
            option = AtlasPageOptions.FirstOrDefault(candidate =>
                string.Equals(candidate.AtlasPage.Name, atlasPageName, StringComparison.Ordinal))!;
            if (option is not null)
            {
                return true;
            }
        }

        if (previewState.SelectedAtlasPageIndex is >= 0 &&
            previewState.SelectedAtlasPageIndex.Value < AtlasPageOptions.Count)
        {
            option = AtlasPageOptions[previewState.SelectedAtlasPageIndex.Value];
            return true;
        }

        return false;
    }

    private void RestoreSelectedAtlasPageOption(WorkspaceSpinePreviewStateDto? previewState)
    {
        if (AtlasPageOptions.Count == 0 ||
            !TryFindAtlasPageOption(previewState, out var option) ||
            ReferenceEquals(SelectedAtlasPageOption, option))
        {
            return;
        }

        SelectedAtlasPageOption = option;
    }

    private void OnSpineAnimationPreviewStateChanged(object? sender, EventArgs e)
    {
        CapturePreviewState();
        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnInspectorSectionScrollPositionChanged(object? sender, EventArgs e)
    {
        InspectorScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearAtlasPageOptions()
    {
        AtlasPageOptions.Clear();
        SelectedAtlasPageOption = null;
    }

    private static string ResolveDisplayName(SpineAssetGroupDto item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName)
            ? item.GroupId
            : item.DisplayName.Trim();
    }

    private static string? NormalizeAtlasPageName(string? atlasPageName) =>
        string.IsNullOrWhiteSpace(atlasPageName)
            ? null
            : atlasPageName.Trim();

    private static string CreateToolTipText(SpineAssetGroupDto item)
    {
        var groupKey = string.IsNullOrWhiteSpace(item.GroupKey)
            ? item.GroupId
            : item.GroupKey.Trim();
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} [{1}]",
            groupKey,
            item.GroupId);
    }
}
