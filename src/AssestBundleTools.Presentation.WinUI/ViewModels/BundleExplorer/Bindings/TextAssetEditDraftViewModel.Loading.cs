using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;

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

    public async Task LoadFullEditAsync(
        ITextDocumentStreamingSink? streamingSink = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanLoadFullEdit)
        {
            return;
        }

        IsFullEditLoading = true;
        try
        {
            if (streamingSink?.CanStreamDocument == true)
            {
                await LoadEditableTextStreamingAsync(streamingSink, cancellationToken);
                return;
            }

            await LoadEditableTextAsync(cancellationToken);
        }
        finally
        {
            IsFullEditLoading = false;
        }
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
            CurrentPatchBaseline = TextAssetEditDraftPatchBaseline.Create(EditableText);
            _patchState.SetSavedBaseline(CurrentPatchBaseline);
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

    private async Task LoadEditableTextStreamingAsync(
        ITextDocumentStreamingSink streamingSink,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        var hasBegunStreaming = false;
        var hasCompletedStreaming = false;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var hashBuffer = new StringBuilder(4096);
            var offset = 0;
            await streamingSink.BeginStreamingDocumentAsync(
                DocumentLanguage,
                Reference.ContentLength,
                cancellationToken);
            hasBegunStreaming = true;

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
                    await streamingSink.CancelStreamingDocumentAsync(cancellationToken);
                    return;
                }

                var chunk = result.Value;
                await streamingSink.AppendDocumentChunkAsync(chunk.Text, cancellationToken);
                AppendHashText(hash, hashBuffer, chunk.Text);
                offset = chunk.Offset + chunk.Length;
                if (!chunk.HasMore || chunk.Length == 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    FlushHashBuffer(hash, hashBuffer);
                    var baseline = new TextAssetEditDraftPatchBaselineDto(
                        chunk.TotalLength,
                        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
                    await streamingSink.EndStreamingDocumentAsync(
                        baseline,
                        readOnly: false,
                        cancellationToken);
                    hasCompletedStreaming = true;
                    MarkStreamingEditLoaded(baseline);
                    return;
                }
            }

            await streamingSink.CancelStreamingDocumentAsync(cancellationToken);
        }
        finally
        {
            if (hasBegunStreaming && !hasCompletedStreaming)
            {
                await streamingSink.CancelStreamingDocumentAsync(cancellationToken);
            }

            IsBusy = false;
        }
    }

    private static void AppendHashText(
        IncrementalHash hash,
        StringBuilder buffer,
        string text)
    {
        foreach (var character in NormalizeTextContent(text))
        {
            buffer.Append(character);
            if (buffer.Length >= 4096 && !char.IsHighSurrogate(buffer[^1]))
            {
                FlushHashBuffer(hash, buffer);
            }
        }
    }

    private static int FlushHashBuffer(
        IncrementalHash hash,
        StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        var byteCount = Encoding.UTF8.GetByteCount(buffer.ToString());
        hash.AppendData(Encoding.UTF8.GetBytes(buffer.ToString()));
        buffer.Clear();
        return byteCount;
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

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
