using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 文本编辑器 draft-backed 工作面；大文本默认快速预览，按需进入全文编辑。
/// </summary>
public sealed partial class TextAssetEditDraftViewModel : ObservableObject
{
    private const int LinePageSize = 200;
    private const int SearchMatchCount = 50;
    private const int MonacoEditableTextLength = AssetTextEditPolicy.InlineEditableTextLength;

    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly string _workspaceDataRootPath;
    private readonly AssetTextEditDraftDto _draft;
    private readonly string _lineStatusFormat;
    private readonly TextAssetEditDraftPatchState _patchState = new();
    private AssetPreviewFullContentReferenceDto _reference;
    private string _statusText = string.Empty;
    private string _searchText = string.Empty;
    private string _editableText = string.Empty;
    private string _documentText = string.Empty;
    private string _lastSavedText = string.Empty;
    private int _nextLineIndex;
    private bool _hasMoreLines;
    private bool _isBusy;
    private bool _hasPendingEditorTextChanges;
    private bool _isFullEditLoaded;
    private bool _isFullEditLoading;
    private bool _isDraftMaterialized;
    private bool _usesStreamingDocumentModel;

    public TextAssetEditDraftViewModel(
        AssetTextEditDraftDto draft,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        string workspaceDataRootPath,
        string titleText,
        string readOnlyHintText,
        string inlineEditHintText,
        string deferredEditHintText,
        string lineStatusFormat)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _workspaceDataRootPath = workspaceDataRootPath ?? throw new ArgumentNullException(nameof(workspaceDataRootPath));
        _reference = draft.Reference;
        _isDraftMaterialized = draft.IsMaterialized;
        TitleText = titleText;
        ReadOnlyHintText = readOnlyHintText;
        InlineEditHintText = inlineEditHintText;
        DeferredEditHintText = deferredEditHintText;
        _lineStatusFormat = string.IsNullOrWhiteSpace(lineStatusFormat) ? "{0} / {1}" : lineStatusFormat;
    }

    public string DraftId => _draft.DraftId;

    public string DraftMode => _draft.DraftMode;

    public bool CanApply => _draft.CanApply;

    public bool IsAppliedBaseline => _draft.IsAppliedBaseline;

    public string? SourceFingerprint => _draft.SourceFingerprint;

    public string? SourceDescription => _draft.SourceDescription;

    public bool IsInlineEditEnabled => _draft.CanEditInline;

    public AssetPreviewFullContentReferenceDto Reference => _reference;

    public string TitleText { get; }

    public string ReadOnlyHintText { get; }

    public string InlineEditHintText { get; }

    public string DeferredEditHintText { get; }

    public string ModeHintText =>
        IsEditable ? InlineEditHintText :
        CanShowLoadFullEdit ? DeferredEditHintText :
        ReadOnlyHintText;

    public Visibility HeaderVisibility =>
        IsEditable || IsFullEditLoading ? Visibility.Collapsed : Visibility.Visible;

    public string DocumentLanguage => TextAssetEditLanguageResolver.DetectLanguage(
        TitleText,
        _draft.AssetTypeDisplayName,
        IsEditable ? DocumentText : _documentText);

    public string DocumentText => IsEditable ? EditableText : _documentText;

    public bool IsReadOnly => !IsEditable;

    private bool IsEditable =>
        _isFullEditLoaded &&
        CanApply &&
        _draft.CanEditInline &&
        _draft.AssetType == AssetType.TextAsset &&
        Reference.ContentLength <= MonacoEditableTextLength;

    public Visibility ReadOnlyViewVisibility =>
        IsEditable ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditableViewVisibility =>
        IsEditable ? Visibility.Visible : Visibility.Collapsed;

    private bool CanShowLoadFullEdit =>
        !_isFullEditLoaded &&
        CanApply &&
        _draft.CanEditInline &&
        _draft.AssetType == AssetType.TextAsset &&
        Reference.ContentLength <= MonacoEditableTextLength;

    public bool CanLoadFullEdit => CanShowLoadFullEdit && !IsBusy;

    public Visibility LoadFullEditButtonVisibility =>
        CanShowLoadFullEdit ? Visibility.Visible : Visibility.Collapsed;

    public bool IsFullEditLoading
    {
        get => _isFullEditLoading;
        private set
        {
            if (SetProperty(ref _isFullEditLoading, value))
            {
                OnPropertyChanged(nameof(HeaderVisibility));
                OnPropertyChanged(nameof(ModeHintText));
                OnPropertyChanged(nameof(CanLoadFullEdit));
                OnPropertyChanged(nameof(LoadFullEditButtonVisibility));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    SearchMatches.Clear();
                    NotifySearchResultState();
                }

                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public string EditableText
    {
        get => _editableText;
        set
        {
            if (SetProperty(ref _editableText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DocumentText));
                OnPropertyChanged(nameof(DocumentLanguage));
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool HasUnsavedChanges =>
        IsEditable &&
        (_patchState.HasPendingRangeEdits ||
        _hasPendingEditorTextChanges ||
        !string.Equals(EditableText, _lastSavedText, StringComparison.Ordinal));

    public bool CanSave => CanApply && HasUnsavedChanges && !IsFullEditLoading;

    public bool UsesStreamingDocumentModel => _usesStreamingDocumentModel;

    public TextAssetEditDraftPatchBaselineDto? CurrentPatchBaseline { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSearch));
                OnPropertyChanged(nameof(CanLoadMoreLines));
                OnPropertyChanged(nameof(CanLoadFullEdit));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool CanSearch => !IsBusy && !string.IsNullOrWhiteSpace(SearchText);

    public bool CanLoadMoreLines => !IsEditable && !IsBusy && _hasMoreLines;

    public Visibility LoadMoreLinesButtonVisibility =>
        !IsEditable && _hasMoreLines ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<TextDocumentLineViewModel> Lines { get; } = new();

    public ObservableCollection<TextDocumentSearchMatchViewModel> SearchMatches { get; } = new();

    public Visibility SearchResultsVisibility =>
        SearchMatches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public GridLength SearchResultsColumnWidth =>
        SearchMatches.Count > 0 ? new GridLength(280) : new GridLength(0);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadInitialLinesAsync(cancellationToken);
        if (ShouldAutoLoadFullEditWithoutStreamingSurface())
        {
            await LoadFullEditAsync(cancellationToken: cancellationToken);
        }

        NotifyDocumentEditState();
    }

    public void MarkSaved(
        AssetPreviewFullContentReferenceDto? reference = null,
        TextAssetEditDraftPatchBaselineDto? patchBaseline = null)
    {
        if (_patchState.HasPendingRangeEdits)
        {
            var updatedText = _patchState.TryApplyToSavedText(_lastSavedText);
            if (updatedText is not null)
            {
                EditableText = updatedText;
            }
        }

        if (reference is not null)
        {
            _reference = reference;
            OnPropertyChanged(nameof(Reference));
        }

        if (!_usesStreamingDocumentModel)
        {
            _lastSavedText = EditableText;
        }

        _isDraftMaterialized = true;
        CurrentPatchBaseline = patchBaseline ??
            (_usesStreamingDocumentModel
                ? CurrentPatchBaseline
                : TextAssetEditDraftPatchBaseline.Create(_lastSavedText));

        _patchState.Clear();
        if (CurrentPatchBaseline is not null)
        {
            _patchState.SetSavedBaseline(CurrentPatchBaseline);
        }

        _hasPendingEditorTextChanges = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
    }

    public IReadOnlyList<TextAssetEditDraftRangeEditDto> CreateSaveRangeEdits() =>
        _patchState.CreateSaveRangeEdits(_lastSavedText, EditableText);

    public TextAssetEditDraftPatchBaselineDto CreateSavePatchBaseline() =>
        _patchState.CreateSavePatchBaseline(_lastSavedText);

    public bool CanUseEditorRangeEditBase(int baseLength)
    {
        var baseline = CreateCurrentPatchBaseline();
        return baseline.ContentLength == baseLength;
    }

    public TextAssetEditDraftPatchBaselineDto CreateCurrentPatchBaseline() =>
        CurrentPatchBaseline ?? TextAssetEditDraftPatchBaseline.Create(_lastSavedText);

    public void MarkEditorDirty()
    {
        if (!IsEditable || _hasPendingEditorTextChanges)
        {
            return;
        }

        _hasPendingEditorTextChanges = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
    }

    public void SetEditorTextFromSurface(string text)
    {
        MarkFullEditLoaded();
        _usesStreamingDocumentModel = false;
        _patchState.Clear();
        _hasPendingEditorTextChanges = false;
        EditableText = text ?? string.Empty;
        NotifyDocumentEditState();
    }

    public void SetEditorRangeEditsFromSurface(
        IReadOnlyList<TextAssetEditDraftRangeEditDto> rangeEdits,
        TextAssetEditDraftPatchBaselineDto baseline)
    {
        MarkFullEditLoaded();
        _patchState.Set(rangeEdits ?? [], baseline);
        _hasPendingEditorTextChanges = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
    }

    public void MarkStreamingEditLoaded(TextAssetEditDraftPatchBaselineDto baseline)
    {
        MarkFullEditLoaded();
        _usesStreamingDocumentModel = true;
        _lastSavedText = string.Empty;
        EditableText = string.Empty;
        CurrentPatchBaseline = baseline;
        _patchState.SetSavedBaseline(baseline);
        _hasPendingEditorTextChanges = false;
        NotifyDocumentEditState();
    }

    private bool ShouldAutoLoadFullEditWithoutStreamingSurface() =>
        _isDraftMaterialized &&
        CanShowLoadFullEdit &&
        Reference.ContentLength <= AssetTextEditPolicy.AutoMaterializeTextLength;

    private void MarkFullEditLoaded()
    {
        if (_isFullEditLoaded)
        {
            return;
        }

        _isFullEditLoaded = true;
        NotifyDocumentEditState();
    }

    private void SetHasMoreLines(bool value)
    {
        if (_hasMoreLines == value)
        {
            return;
        }

        _hasMoreLines = value;
        OnPropertyChanged(nameof(CanLoadMoreLines));
        OnPropertyChanged(nameof(LoadMoreLinesButtonVisibility));
    }

    private void NotifySearchResultState()
    {
        OnPropertyChanged(nameof(SearchResultsVisibility));
        OnPropertyChanged(nameof(SearchResultsColumnWidth));
    }

    private void NotifyDocumentEditState()
    {
        OnPropertyChanged(nameof(ReadOnlyViewVisibility));
        OnPropertyChanged(nameof(EditableViewVisibility));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(DocumentText));
        OnPropertyChanged(nameof(DocumentLanguage));
        OnPropertyChanged(nameof(HeaderVisibility));
        OnPropertyChanged(nameof(ModeHintText));
        OnPropertyChanged(nameof(CanLoadFullEdit));
        OnPropertyChanged(nameof(LoadFullEditButtonVisibility));
        OnPropertyChanged(nameof(IsFullEditLoading));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanLoadMoreLines));
        OnPropertyChanged(nameof(LoadMoreLinesButtonVisibility));
    }
}
