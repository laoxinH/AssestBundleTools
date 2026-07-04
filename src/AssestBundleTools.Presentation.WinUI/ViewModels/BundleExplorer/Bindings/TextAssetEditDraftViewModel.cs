using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 文本编辑器第一阶段的 draft-backed 只读文本工作面。
/// </summary>
public sealed class TextAssetEditDraftViewModel : ObservableObject
{
    private const int InitialLineCount = 200;
    private const int SearchMatchCount = 50;

    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly string _workspaceDataRootPath;
    private readonly AssetTextEditDraftDto _draft;
    private readonly string _lineStatusFormat;
    private string _statusText = string.Empty;
    private string _searchText = string.Empty;
    private bool _isBusy;

    public TextAssetEditDraftViewModel(
        AssetTextEditDraftDto draft,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        string workspaceDataRootPath,
        string titleText,
        string readOnlyHintText,
        string lineStatusFormat)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _workspaceDataRootPath = workspaceDataRootPath ?? throw new ArgumentNullException(nameof(workspaceDataRootPath));
        TitleText = titleText;
        ReadOnlyHintText = readOnlyHintText;
        _lineStatusFormat = string.IsNullOrWhiteSpace(lineStatusFormat) ? "{0} / {1}" : lineStatusFormat;
    }

    public string DraftId => _draft.DraftId;

    public string DraftMode => _draft.DraftMode;

    public bool CanApply => _draft.CanApply;

    public AssetPreviewFullContentReferenceDto Reference => _draft.Reference;

    public string TitleText { get; }

    public string ReadOnlyHintText { get; }

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
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public bool CanSearch => !IsBusy && !string.IsNullOrWhiteSpace(SearchText);

    public ObservableCollection<TextAssetEditLineViewModel> Lines { get; } = new();

    public ObservableCollection<TextAssetEditSearchMatchViewModel> SearchMatches { get; } = new();

    public async Task LoadInitialLinesAsync(CancellationToken cancellationToken = default)
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
                        StartLineIndex: 0,
                        LineCount: InitialLineCount),
                    token),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                StatusText = string.Empty;
                return;
            }

            Lines.Clear();
            var lineIndex = result.Value.StartLineIndex;
            foreach (var line in result.Value.Lines)
            {
                Lines.Add(new TextAssetEditLineViewModel(
                    (lineIndex + 1).ToString(CultureInfo.CurrentCulture),
                    line));
                lineIndex++;
            }

            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                _lineStatusFormat,
                result.Value.LineCount,
                result.Value.TotalLineCount);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch)
        {
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
                return;
            }

            SearchMatches.Clear();
            foreach (var match in result.Value.Matches)
            {
                SearchMatches.Add(new TextAssetEditSearchMatchViewModel(
                    string.Create(
                        CultureInfo.CurrentCulture,
                        $"{match.LineIndex + 1}:{match.ColumnIndex + 1}"),
                    match.LineText));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
