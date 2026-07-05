using System.Globalization;
using System.Text;
using AssestBundleTools.Application.Assets.Queries;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class TextAssetEditDraftViewModel
{
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

    public async Task LoadFullEditAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadFullEdit)
        {
            return;
        }

        await LoadEditableTextAsync(cancellationToken);
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
                        Reference,
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
                    UpdateReadOnlyDocumentText();
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
            UpdateReadOnlyDocumentText();
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
                return;
            }

            EditableText = text;
            _lastSavedText = EditableText;
            MarkFullEditLoaded();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> LoadFullEditableTextAsync(CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var offset = 0;
        while (offset < MonacoEditableTextLength)
        {
            var result = await _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.LoadPreviewTextChunkAsync(
                    new LoadPreviewTextChunkQuery(
                        _workspaceDataRootPath,
                        Reference,
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

    private void UpdateReadOnlyDocumentText()
    {
        if (IsEditable)
        {
            return;
        }

        _documentText = string.Join(Environment.NewLine, Lines.Select(static line => line.Text));
        OnPropertyChanged(nameof(DocumentText));
    }
}
