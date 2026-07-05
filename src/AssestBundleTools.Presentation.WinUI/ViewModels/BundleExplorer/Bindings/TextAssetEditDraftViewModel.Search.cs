using System.Globalization;
using AssestBundleTools.Application.Assets.Queries;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class TextAssetEditDraftViewModel
{
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
                        Reference,
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
}
