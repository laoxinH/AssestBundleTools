using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 文本编辑器第一阶段的 draft-backed 文本工作面。
/// </summary>
public sealed class TextAssetEditDraftViewModel : ObservableObject
{
    private const int LinePageSize = 200;
    private const int SearchMatchCount = 50;
    private const int MonacoEditableTextLength = AssetTextEditPolicy.InlineEditableTextLength;

    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly string _workspaceDataRootPath;
    private readonly AssetTextEditDraftDto _draft;
    private readonly string _lineStatusFormat;
    private string _statusText = string.Empty;
    private string _searchText = string.Empty;
    private string _editableText = string.Empty;
    private string _lastSavedText = string.Empty;
    private int _nextLineIndex;
    private bool _hasMoreLines;
    private bool _isBusy;
    private bool _hasPendingEditorTextChanges;

    public TextAssetEditDraftViewModel(
        AssetTextEditDraftDto draft,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        string workspaceDataRootPath,
        string titleText,
        string readOnlyHintText,
        string inlineEditHintText,
        string lineStatusFormat)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _workspaceDataRootPath = workspaceDataRootPath ?? throw new ArgumentNullException(nameof(workspaceDataRootPath));
        TitleText = titleText;
        ReadOnlyHintText = readOnlyHintText;
        InlineEditHintText = inlineEditHintText;
        _lineStatusFormat = string.IsNullOrWhiteSpace(lineStatusFormat) ? "{0} / {1}" : lineStatusFormat;
    }

    public string DraftId => _draft.DraftId;

    public string DraftMode => _draft.DraftMode;

    public bool CanApply => _draft.CanApply;

    public bool IsAppliedBaseline => _draft.IsAppliedBaseline;

    public string? SourceFingerprint => _draft.SourceFingerprint;

    public string? SourceDescription => _draft.SourceDescription;

    public bool IsInlineEditEnabled => _draft.CanEditInline;

    public Visibility ReadOnlyViewVisibility =>
        IsEditable ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditableViewVisibility =>
        IsEditable ? Visibility.Visible : Visibility.Collapsed;

    public AssetPreviewFullContentReferenceDto Reference => _draft.Reference;

    public string TitleText { get; }

    public string ReadOnlyHintText { get; }

    public string InlineEditHintText { get; }

    public string ModeHintText => IsInlineEditEnabled ? InlineEditHintText : ReadOnlyHintText;

    public string DocumentLanguage => DetectLanguage(TitleText, _draft.AssetTypeDisplayName);

    public bool IsReadOnly => !IsEditable;

    private bool IsEditable =>
        CanApply &&
        _draft.CanEditInline &&
        _draft.AssetType == AssetType.TextAsset &&
        _draft.Reference.ContentLength <= MonacoEditableTextLength;

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
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool HasUnsavedChanges =>
        IsEditable &&
        (_hasPendingEditorTextChanges ||
        !string.Equals(EditableText, _lastSavedText, StringComparison.Ordinal));

    public bool CanSave => CanApply && HasUnsavedChanges;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSearch));
                OnPropertyChanged(nameof(CanLoadMoreLines));
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
        await LoadEditableTextAsync(cancellationToken);
        NotifyDocumentEditState();
    }

    public async Task LoadInitialLinesAsync(CancellationToken cancellationToken = default)
    {
        await LoadLinePageAsync(startLineIndex: 0, reset: true, cancellationToken);
    }

    public async Task LoadMoreLinesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadMoreLines)
        {
            return;
        }

        await LoadLinePageAsync(_nextLineIndex, reset: false, cancellationToken);
    }

    private async Task LoadLinePageAsync(
        int startLineIndex,
        bool reset,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.LoadPreviewTextLinesAsync(
                    new LoadPreviewTextLinesQuery(
                        _workspaceDataRootPath,
                        _draft.Reference,
                        startLineIndex,
                        LinePageSize),
                    token),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                if (reset)
                {
                    Lines.Clear();
                    _nextLineIndex = 0;
                    StatusText = string.Empty;
                    SetHasMoreLines(false);
                }

                return;
            }

            if (reset)
            {
                Lines.Clear();
            }

            var lineIndex = result.Value.StartLineIndex;
            foreach (var line in result.Value.Lines)
            {
                Lines.Add(new TextDocumentLineViewModel(
                    (lineIndex + 1).ToString(CultureInfo.CurrentCulture),
                    line));
                lineIndex++;
            }

            _nextLineIndex = result.Value.StartLineIndex + result.Value.LineCount;
            SetHasMoreLines(result.Value.HasMore && result.Value.LineCount > 0);
            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                _lineStatusFormat,
                Lines.Count,
                result.Value.TotalLineCount);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void MarkSaved()
    {
        _lastSavedText = EditableText;
        _hasPendingEditorTextChanges = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
    }

    public TextAssetEditDraftRangeEditDto CreateFullDocumentRangeEdit() =>
        new(0, NormalizeTextContent(_lastSavedText).Length, EditableText);

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
        _hasPendingEditorTextChanges = false;
        EditableText = text ?? string.Empty;
        NotifyDocumentEditState();
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch)
        {
            return;
        }

        if (IsEditable)
        {
            SearchEditableText();
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.SearchPreviewTextAsync(
                    new SearchPreviewTextQuery(
                        _workspaceDataRootPath,
                        _draft.Reference,
                        SearchText.Trim(),
                        StartLineIndex: 0,
                        MaxMatchCount: SearchMatchCount,
                        MatchCase: false),
                    token),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                SearchMatches.Clear();
                NotifySearchResultState();
                return;
            }

            SearchMatches.Clear();
            foreach (var match in result.Value.Matches)
            {
                SearchMatches.Add(new TextDocumentSearchMatchViewModel(
                    string.Create(
                        CultureInfo.CurrentCulture,
                        $"{match.LineIndex + 1}:{match.ColumnIndex + 1}"),
                    match.LineText));
            }

            NotifySearchResultState();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadEditableTextAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var text = await LoadFullEditableTextAsync(cancellationToken);
            if (text is null)
            {
                EditableText = string.Empty;
                MarkSaved();
                return;
            }

            EditableText = text;
            _lastSavedText = EditableText;
            NotifyDocumentEditState();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> LoadFullEditableTextAsync(CancellationToken cancellationToken)
    {
        var builder = new System.Text.StringBuilder();
        var offset = 0;
        while (offset < MonacoEditableTextLength)
        {
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.LoadPreviewTextChunkAsync(
                    new LoadPreviewTextChunkQuery(
                        _workspaceDataRootPath,
                        _draft.Reference,
                        offset,
                        LoadPreviewTextChunkHandler.MaxChunkLength),
                    token),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return null;
            }

            var chunk = result.Value;
            builder.Append(chunk.Text);
            offset = chunk.Offset + chunk.Length;
            if (!chunk.HasMore || chunk.Length == 0)
            {
                return builder.ToString();
            }
        }

        return builder.ToString();
    }

    private void SearchEditableText()
    {
        var searchText = SearchText.Trim();
        if (searchText.Length == 0)
        {
            SearchMatches.Clear();
            NotifySearchResultState();
            return;
        }

        SearchMatches.Clear();
        var lines = EditableText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        for (var lineIndex = 0; lineIndex < lines.Length && SearchMatches.Count < SearchMatchCount; lineIndex++)
        {
            var line = lines[lineIndex];
            var columnIndex = line.IndexOf(searchText, comparison);
            if (columnIndex < 0)
            {
                continue;
            }

            SearchMatches.Add(new TextDocumentSearchMatchViewModel(
                string.Create(
                    CultureInfo.CurrentCulture,
                    $"{lineIndex + 1}:{columnIndex + 1}"),
                line));
        }

        NotifySearchResultState();
    }

    private void NotifySearchResultState()
    {
        OnPropertyChanged(nameof(SearchResultsVisibility));
        OnPropertyChanged(nameof(SearchResultsColumnWidth));
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

    private void NotifyDocumentEditState()
    {
        OnPropertyChanged(nameof(ReadOnlyViewVisibility));
        OnPropertyChanged(nameof(EditableViewVisibility));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(DocumentLanguage));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanLoadMoreLines));
        OnPropertyChanged(nameof(LoadMoreLinesButtonVisibility));
    }

    private static string DetectLanguage(string titleText, string? assetTypeDisplayName)
    {
        var extension = Path.GetExtension(titleText);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".skel", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (extension.Equals(".atlas", StringComparison.OrdinalIgnoreCase))
        {
            return "atlas";
        }

        if (string.Equals(assetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase))
        {
            return "shader";
        }

        return "plaintext";
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
