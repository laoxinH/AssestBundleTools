using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示右侧详情检查器当前的展示状态。
/// </summary>
public sealed partial class InspectorPanelViewModel : ObservableObject
{
    private string _emptyStateText = string.Empty;
    private string _assetName = string.Empty;
    private string _bundleDisplayName = string.Empty;
    private string _assetTypeText = string.Empty;
    private string _containerPath = string.Empty;
    private string _serializedFileName = string.Empty;
    private string _sizeText = string.Empty;
    private string _serializedObjectSizeText = string.Empty;
    private string _pathIdText = string.Empty;
    private string _extractCapabilityText = string.Empty;
    private string _fileReplacementCapabilityText = string.Empty;
    private string _workspaceReplacementCapabilityText = string.Empty;
    private string _replacementSourceText = string.Empty;
    private string _replacementStatusText = string.Empty;
    private string _changeSummaryText = string.Empty;
    private string _modifyButtonText = string.Empty;
    private string _modifyButtonIconGlyph = "\uEF5D";
    private string _editButtonText = string.Empty;
    private string _editButtonIconGlyph = "\uE837";
    private bool _canExtract;
    private bool _canSelectFileReplacementSource;
    private bool _canSelectReplacementSource;
    private bool _canApplyReplacement;
    private bool _canUndoReplacement;
    private bool _canRedoReplacement;
    private bool _canModify;
    private bool _canEditText;
    private bool _hasSelection;
    private readonly InspectorInformationCardPresenter _informationCardPresenter;

    public InspectorPanelViewModel(InspectorInformationTextSet informationTextSet)
    {
        _informationCardPresenter = new InspectorInformationCardPresenter(informationTextSet);
        Preview = new InspectorPreviewViewModel();
        SpineAtlasPageSelector = new SpineAtlasPageSelectorViewModel();
    }

    public InspectorPreviewViewModel Preview { get; }

    public SpineAtlasPageSelectorViewModel SpineAtlasPageSelector { get; }

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public string AssetName
    {
        get => _assetName;
        set => SetProperty(ref _assetName, value);
    }

    public string BundleDisplayName
    {
        get => _bundleDisplayName;
        set => SetProperty(ref _bundleDisplayName, value);
    }

    public string AssetTypeText
    {
        get => _assetTypeText;
        set => SetProperty(ref _assetTypeText, value);
    }

    public string ContainerPath
    {
        get => _containerPath;
        set => SetProperty(ref _containerPath, value);
    }

    public string SerializedFileName
    {
        get => _serializedFileName;
        set => SetProperty(ref _serializedFileName, value);
    }

    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value);
    }

    public string SerializedObjectSizeText
    {
        get => _serializedObjectSizeText;
        set => SetProperty(ref _serializedObjectSizeText, value);
    }

    public string PathIdText
    {
        get => _pathIdText;
        set => SetProperty(ref _pathIdText, value);
    }

    /// <summary>
    /// 用于右侧“替换 / 详情”区展示当前资源的可提取能力。
    /// 这里使用已经本地化后的展示文本，避免把文案拼装逻辑放回 XAML。
    /// </summary>
    public string ExtractCapabilityText
    {
        get => _extractCapabilityText;
        set => SetProperty(ref _extractCapabilityText, value);
    }

    public string FileReplacementCapabilityText
    {
        get => _fileReplacementCapabilityText;
        set => SetProperty(ref _fileReplacementCapabilityText, value);
    }

    public string WorkspaceReplacementCapabilityText
    {
        get => _workspaceReplacementCapabilityText;
        set => SetProperty(ref _workspaceReplacementCapabilityText, value);
    }

    public string ReplacementSourceText
    {
        get => _replacementSourceText;
        set => SetProperty(ref _replacementSourceText, value);
    }

    public string ReplacementStatusText
    {
        get => _replacementStatusText;
        set => SetProperty(ref _replacementStatusText, value);
    }

    public string ChangeSummaryText
    {
        get => _changeSummaryText;
        set => SetProperty(ref _changeSummaryText, value);
    }

    public string ModifyButtonText
    {
        get => _modifyButtonText;
        set => SetProperty(ref _modifyButtonText, value);
    }

    public string ModifyButtonIconGlyph
    {
        get => _modifyButtonIconGlyph;
        set => SetProperty(ref _modifyButtonIconGlyph, value);
    }

    public string EditButtonText
    {
        get => _editButtonText;
        set
        {
            if (SetProperty(ref _editButtonText, value))
            {
                OnPropertyChanged(nameof(EditButtonVisibility));
            }
        }
    }

    public string EditButtonIconGlyph
    {
        get => _editButtonIconGlyph;
        set => SetProperty(ref _editButtonIconGlyph, value);
    }

    public bool CanExtract
    {
        get => _canExtract;
        set => SetProperty(ref _canExtract, value);
    }

    public bool CanSelectFileReplacementSource
    {
        get => _canSelectFileReplacementSource;
        set => SetProperty(ref _canSelectFileReplacementSource, value);
    }

    public bool CanSelectReplacementSource
    {
        get => _canSelectReplacementSource;
        set => SetProperty(ref _canSelectReplacementSource, value);
    }

    public bool CanApplyReplacement
    {
        get => _canApplyReplacement;
        set => SetProperty(ref _canApplyReplacement, value);
    }

    public bool CanUndoReplacement
    {
        get => _canUndoReplacement;
        set => SetProperty(ref _canUndoReplacement, value);
    }

    public bool CanRedoReplacement
    {
        get => _canRedoReplacement;
        set => SetProperty(ref _canRedoReplacement, value);
    }

    public bool CanModify
    {
        get => _canModify;
        set => SetProperty(ref _canModify, value);
    }

    public bool CanEditText
    {
        get => _canEditText;
        set => SetProperty(ref _canEditText, value);
    }

    public Visibility EditButtonVisibility =>
        string.IsNullOrWhiteSpace(EditButtonText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public ObservableCollection<AssetDependencyViewModel> AssetDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> ReferencedByDependencies { get; } = new();

    public ObservableCollection<AssetDependencyViewModel> DependencyClosureItems { get; } = new();

    public ObservableCollection<BundleExternalDependencyViewModel> BundleExternalDependencies { get; } = new();

    public ObservableCollection<InspectorInfoCardViewModel> InformationCards { get; } = new();

    public ObservableCollection<InspectorChangeRecordViewModel> ChangeRecords { get; } = new();

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                Preview.UpdateHasSelection(value);
                OnPropertyChanged(nameof(DetailsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public Visibility DetailsVisibility => HasSelection ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => HasSelection ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AssetDependenciesVisibility => AssetDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReferencedByDependenciesVisibility => ReferencedByDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependencyClosureVisibility => DependencyClosureItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BundleExternalDependenciesVisibility => BundleExternalDependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DependenciesEmptyStateVisibility =>
        AssetDependencies.Count == 0 &&
        ReferencedByDependencies.Count == 0 &&
        DependencyClosureItems.Count == 0 &&
        BundleExternalDependencies.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ChangeRecordsListVisibility =>
        ChangeRecords.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChangeRecordsEmptyStateVisibility =>
        ChangeRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public void Clear(string emptyStateText)
    {
        EmptyStateText = emptyStateText;
        AssetName = string.Empty;
        BundleDisplayName = string.Empty;
        AssetTypeText = string.Empty;
        ContainerPath = string.Empty;
        SerializedFileName = string.Empty;
        SizeText = string.Empty;
        SerializedObjectSizeText = string.Empty;
        PathIdText = string.Empty;
        ExtractCapabilityText = string.Empty;
        FileReplacementCapabilityText = string.Empty;
        WorkspaceReplacementCapabilityText = string.Empty;
        ReplacementSourceText = string.Empty;
        ReplacementStatusText = string.Empty;
        ChangeSummaryText = string.Empty;
        ModifyButtonText = string.Empty;
        ModifyButtonIconGlyph = "\uEF5D";
        EditButtonText = string.Empty;
        EditButtonIconGlyph = "\uE837";
        CanExtract = false;
        CanSelectFileReplacementSource = false;
        CanSelectReplacementSource = false;
        CanApplyReplacement = false;
        CanUndoReplacement = false;
        CanRedoReplacement = false;
        CanModify = false;
        CanEditText = false;
        IsReplacementSectionAvailable = false;
        AssetDependencies.Clear();
        ReferencedByDependencies.Clear();
        DependencyClosureItems.Clear();
        BundleExternalDependencies.Clear();
        ChangeRecords.Clear();
        SpineAtlasPageSelector.Clear();
        Preview.ResetState();
        ResetSectionScrollPositions();
        NotifyDependencyStateChanged();
        NotifyChangeRecordStateChanged();
        HasSelection = false;
        RebuildInformationCards();
    }

    public void Apply(
        string assetName,
        string bundleDisplayName,
        string assetTypeText,
        string containerPath,
        string serializedFileName,
        string sizeText,
        string pathIdText,
        string extractCapabilityText,
        string fileReplacementCapabilityText,
        string workspaceReplacementCapabilityText,
        string replacementSourceText,
        string replacementStatusText,
        string changeSummaryText,
        bool canExtract,
        bool canSelectFileReplacementSource,
        bool canSelectReplacementSource,
        bool canApplyReplacement,
        bool canUndoReplacement,
        IEnumerable<AssetDependencyViewModel> assetDependencies,
        IEnumerable<AssetDependencyViewModel> referencedByDependencies,
        IEnumerable<AssetDependencyViewModel> dependencyClosureItems,
        IEnumerable<BundleExternalDependencyViewModel> bundleExternalDependencies,
        bool canRedoReplacement = false,
        IEnumerable<InspectorChangeRecordViewModel>? changeRecords = null,
        bool? canModify = null,
        string? modifyButtonText = null,
        string? modifyButtonIconGlyph = null,
        bool canEditText = false,
        string? editButtonText = null,
        string? editButtonIconGlyph = null,
        string? serializedObjectSizeText = null)
    {
        AssetName = assetName;
        BundleDisplayName = bundleDisplayName;
        AssetTypeText = assetTypeText;
        ContainerPath = NormalizePathValue(containerPath);
        SerializedFileName = NormalizeSerializedFileName(serializedFileName);
        SizeText = sizeText;
        SerializedObjectSizeText = serializedObjectSizeText ?? string.Empty;
        PathIdText = pathIdText;
        ExtractCapabilityText = extractCapabilityText;
        FileReplacementCapabilityText = fileReplacementCapabilityText;
        WorkspaceReplacementCapabilityText = workspaceReplacementCapabilityText;
        ReplacementSourceText = replacementSourceText;
        ReplacementStatusText = replacementStatusText;
        ChangeSummaryText = changeSummaryText;
        ModifyButtonText = modifyButtonText ?? string.Empty;
        ModifyButtonIconGlyph = modifyButtonIconGlyph ?? "\uEF5D";
        EditButtonText = editButtonText ?? string.Empty;
        EditButtonIconGlyph = editButtonIconGlyph ?? "\uE837";
        CanExtract = canExtract;
        CanSelectFileReplacementSource = canSelectFileReplacementSource;
        CanSelectReplacementSource = canSelectReplacementSource;
        CanApplyReplacement = canApplyReplacement;
        CanUndoReplacement = canUndoReplacement;
        CanRedoReplacement = canRedoReplacement;
        CanModify = canModify ?? canSelectFileReplacementSource;
        CanEditText = canEditText;

        ReplaceCollection(AssetDependencies, assetDependencies);
        ReplaceCollection(ReferencedByDependencies, referencedByDependencies);
        ReplaceCollection(DependencyClosureItems, dependencyClosureItems);
        ReplaceCollection(BundleExternalDependencies, bundleExternalDependencies);
        ReplaceCollection(ChangeRecords, changeRecords ?? Array.Empty<InspectorChangeRecordViewModel>());
        SpineAtlasPageSelector.Clear();

        NotifyDependencyStateChanged();
        NotifyChangeRecordStateChanged();
        HasSelection = true;
        RebuildInformationCards();
    }

    internal void ApplyOperationProjection(
        string changeSummaryText,
        bool canExtract,
        bool canModify,
        IEnumerable<InspectorChangeRecordViewModel>? changeRecords = null)
    {
        ChangeSummaryText = changeSummaryText;
        CanExtract = canExtract;
        CanModify = canModify;
        ReplaceCollection(ChangeRecords, changeRecords ?? Array.Empty<InspectorChangeRecordViewModel>());
        NotifyChangeRecordStateChanged();
    }

    private void RebuildInformationCards()
    {
        _informationCardPresenter.RebuildInformationCards(
            InformationCards,
            HasSelection,
            AssetName,
            AssetTypeText,
            BundleDisplayName,
            SizeText,
            SerializedObjectSizeText,
            ContainerPath,
            SerializedFileName,
            PathIdText,
            Preview);
    }

    private void NotifyDependencyStateChanged()
    {
        OnPropertyChanged(nameof(AssetDependenciesVisibility));
        OnPropertyChanged(nameof(ReferencedByDependenciesVisibility));
        OnPropertyChanged(nameof(DependencyClosureVisibility));
        OnPropertyChanged(nameof(BundleExternalDependenciesVisibility));
        OnPropertyChanged(nameof(DependenciesEmptyStateVisibility));
    }

    private void NotifyChangeRecordStateChanged()
    {
        OnPropertyChanged(nameof(ChangeRecordsListVisibility));
        OnPropertyChanged(nameof(ChangeRecordsEmptyStateVisibility));
    }

    private static string NormalizePathValue(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Replace('\\', '/');
    }

    private static string NormalizeSerializedFileName(string serializedFileName)
    {
        return string.IsNullOrWhiteSpace(serializedFileName)
            ? string.Empty
            : serializedFileName.Trim().Replace('\\', '/');
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
