using System.Text.Json;
using AssestBundleTools.Application.Editing.Dtos;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class TextDocumentSurfaceView
{
    private readonly Dictionary<string, TaskCompletionSource<TextDocumentSurfaceRangeEditSyncResult>> _documentRangeEditRequests = new();

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        using var document = JsonDocument.Parse(args.WebMessageAsJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var messageType = typeElement.GetString();
        switch (messageType)
        {
            case "ready":
                _isWebViewReady = true;
                ++_webViewNavigationVersion;
                FallbackSurface.Visibility = Visibility.Collapsed;
                EditorWebView.Visibility = Visibility.Visible;
                SendThemeToWebView(force: true);
                SendDocumentToWebView(force: true);
                StreamingDocumentReady?.Invoke(this, EventArgs.Empty);
                break;
            case "contentChanged":
                _hasUnsynchronizedWebViewChanges = true;
                _isSentDocumentCacheValid = false;
                DocumentChanged?.Invoke(this, EventArgs.Empty);
                break;
            case "documentText":
                ResolveDocumentTextRequest(root);
                break;
            case "documentRangeEdits":
                ResolveDocumentRangeEditRequest(root);
                break;
            case "saveRequested":
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    internal async Task<TextDocumentSurfaceRangeEditSyncResult?> GetPendingRangeEditsAsync()
    {
        if (!_hasUnsynchronizedWebViewChanges)
        {
            return new TextDocumentSurfaceRangeEditSyncResult(
                [],
                NormalizeTextContent(EditableText ?? DocumentText ?? string.Empty).Length,
                VersionId: 0);
        }

        if (!_isWebViewReady || EditorWebView.CoreWebView2 is null || IsReadOnly)
        {
            return null;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<TextDocumentSurfaceRangeEditSyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _documentRangeEditRequests[requestId] = completion;

        try
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "getPendingRangeEdits",
                requestId
            });
            EditorWebView.CoreWebView2.PostWebMessageAsJson(message);
            var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            return completed == completion.Task
                ? await completion.Task
                : null;
        }
        finally
        {
            _documentRangeEditRequests.Remove(requestId);
        }
    }

    public async Task<string?> GetCurrentDocumentTextAsync()
    {
        if (!_hasUnsynchronizedWebViewChanges)
        {
            return EditableText ?? DocumentText ?? string.Empty;
        }

        if (!_isWebViewReady || EditorWebView.CoreWebView2 is null)
        {
            return EditableText ?? DocumentText ?? string.Empty;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _documentTextRequests[requestId] = completion;

        try
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "getDocumentText",
                requestId
            });
            EditorWebView.CoreWebView2.PostWebMessageAsJson(message);
            var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            return completed == completion.Task
                ? await completion.Task
                : null;
        }
        finally
        {
            _documentTextRequests.Remove(requestId);
        }
    }

    internal void AcceptCurrentDocumentAsSaved(TextAssetEditDraftPatchBaselineDto? baseline)
    {
        _hasUnsynchronizedWebViewChanges = false;
        if (_isWebViewReady && EditorWebView.CoreWebView2 is not null)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "markSaved",
                baseline = baseline is null
                    ? null
                    : new
                    {
                        contentLength = baseline.ContentLength,
                        contentSha256 = baseline.ContentSha256
                    }
            });
            EditorWebView.CoreWebView2.PostWebMessageAsJson(message);
        }
    }

    public Task BeginStreamingDocumentAsync(
        string language,
        long expectedLength,
        CancellationToken cancellationToken = default)
    {
        var coreWebView = EditorWebView.CoreWebView2;
        if (cancellationToken.IsCancellationRequested || !CanStreamDocument || coreWebView is null)
        {
            return Task.CompletedTask;
        }

        _isStreamingDocument = true;
        _hasStreamingDocumentModel = false;
        _hasUnsynchronizedWebViewChanges = false;
        _isSentDocumentCacheValid = false;
        var message = JsonSerializer.Serialize(new
        {
            type = "beginStreamingDocument",
            language = string.IsNullOrWhiteSpace(language) ? "plaintext" : language.Trim(),
            readOnly = true,
            expectedLength = expectedLength < 0 ? 0 : expectedLength
        });
        coreWebView.PostWebMessageAsJson(message);
        return Task.CompletedTask;
    }

    public Task AppendDocumentChunkAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var coreWebView = EditorWebView.CoreWebView2;
        if (cancellationToken.IsCancellationRequested || !CanStreamDocument || !_isStreamingDocument || coreWebView is null)
        {
            return Task.CompletedTask;
        }

        var message = JsonSerializer.Serialize(new
        {
            type = "appendDocumentChunk",
            text = text ?? string.Empty
        });
        coreWebView.PostWebMessageAsJson(message);
        return Task.CompletedTask;
    }

    public Task EndStreamingDocumentAsync(
        TextAssetEditDraftPatchBaselineDto baseline,
        bool readOnly,
        CancellationToken cancellationToken = default)
    {
        var coreWebView = EditorWebView.CoreWebView2;
        if (cancellationToken.IsCancellationRequested || !CanStreamDocument || coreWebView is null)
        {
            _isStreamingDocument = false;
            return Task.CompletedTask;
        }

        var language = string.IsNullOrWhiteSpace(DocumentLanguage) ? "plaintext" : DocumentLanguage.Trim();
        var message = JsonSerializer.Serialize(new
        {
            type = "endStreamingDocument",
            readOnly,
            baseline = new
            {
                contentLength = baseline.ContentLength,
                contentSha256 = baseline.ContentSha256
            }
        });
        coreWebView.PostWebMessageAsJson(message);
        _isStreamingDocument = false;
        _hasStreamingDocumentModel = true;
        _lastSentText = DocumentText ?? string.Empty;
        _lastSentLanguage = language;
        _lastSentReadOnly = readOnly;
        _isSentDocumentCacheValid = true;
        return Task.CompletedTask;
    }

    public Task CancelStreamingDocumentAsync(CancellationToken cancellationToken = default)
    {
        _isStreamingDocument = false;
        _hasStreamingDocumentModel = false;
        SendDocumentToWebView(force: true);

        return Task.CompletedTask;
    }

    private void ResolveDocumentTextRequest(JsonElement root)
    {
        if (!TryGetRequestId(root, out var requestId) ||
            !_documentTextRequests.TryGetValue(requestId, out var completion))
        {
            return;
        }

        var text = root.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
        ApplyWebViewText(text);
        completion.TrySetResult(text);
    }

    private void ResolveDocumentRangeEditRequest(JsonElement root)
    {
        if (!TryGetRequestId(root, out var requestId) ||
            !_documentRangeEditRequests.TryGetValue(requestId, out var completion))
        {
            return;
        }

        var baseLength = root.TryGetProperty("baseLength", out var baseLengthElement)
            ? baseLengthElement.GetInt32()
            : 0;
        var versionId = root.TryGetProperty("versionId", out var versionIdElement)
            ? versionIdElement.GetInt32()
            : 0;
        var edits = ReadRangeEdits(root);
        completion.TrySetResult(new TextDocumentSurfaceRangeEditSyncResult(edits, baseLength, versionId));
    }

    private static bool TryGetRequestId(JsonElement root, out string requestId)
    {
        requestId = string.Empty;
        if (!root.TryGetProperty("requestId", out var requestIdElement))
        {
            return false;
        }

        requestId = requestIdElement.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(requestId);
    }

    private static IReadOnlyList<TextAssetEditDraftRangeEditDto> ReadRangeEdits(JsonElement root)
    {
        if (!root.TryGetProperty("edits", out var editsElement) ||
            editsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var edits = new List<TextAssetEditDraftRangeEditDto>();
        foreach (var editElement in editsElement.EnumerateArray())
        {
            if (!editElement.TryGetProperty("start", out var startElement) ||
                !editElement.TryGetProperty("length", out var lengthElement) ||
                !editElement.TryGetProperty("text", out var textElement))
            {
                continue;
            }

            var start = startElement.GetInt32();
            var length = lengthElement.GetInt32();
            var text = textElement.GetString() ?? string.Empty;
            if (start < 0 || length < 0)
            {
                continue;
            }

            edits.Add(new TextAssetEditDraftRangeEditDto(start, length, text));
        }

        return edits;
    }

    private void ApplyWebViewText(string text)
    {
        _isApplyingWebViewText = true;
        try
        {
            _hasUnsynchronizedWebViewChanges = false;
            _hasStreamingDocumentModel = false;
            _lastSentText = text;
            _lastSentLanguage = string.IsNullOrWhiteSpace(DocumentLanguage)
                ? "plaintext"
                : DocumentLanguage.Trim();
            _lastSentReadOnly = IsReadOnly;
            _isSentDocumentCacheValid = true;
            DocumentText = text;
            EditableText = text;
        }
        finally
        {
            _isApplyingWebViewText = false;
        }

        EditableTextChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnEditableTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextDocumentSurfaceView view)
        {
            return;
        }

        if (!view._isApplyingWebViewText)
        {
            view.DocumentText = (string?)e.NewValue ?? string.Empty;
        }

        view.EditableTextChanged?.Invoke(view, EventArgs.Empty);
    }

    private static void OnDocumentPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextDocumentSurfaceView view &&
            !view._isStreamingDocument &&
            !view._isApplyingWebViewText &&
            !view._hasUnsynchronizedWebViewChanges)
        {
            view.SendDocumentToWebView(force: false);
        }
    }

    public void ResetPendingDocumentChanges(bool forceRefreshDocument = false)
    {
        _hasUnsynchronizedWebViewChanges = false;
        _hasStreamingDocumentModel = false;
        if (forceRefreshDocument)
        {
            SendDocumentToWebView(force: true);
        }
    }

    private void SendDocumentToWebView(bool force)
    {
        if (!_isWebViewReady || EditorWebView.CoreWebView2 is null || _isSendingDocument)
        {
            return;
        }

        if (_hasStreamingDocumentModel && !force)
        {
            return;
        }

        if (force)
        {
            _hasStreamingDocumentModel = false;
        }

        var text = DocumentText ?? string.Empty;
        var language = string.IsNullOrWhiteSpace(DocumentLanguage)
            ? "plaintext"
            : DocumentLanguage.Trim();
        var readOnly = IsReadOnly;
        if (!force &&
            _isSentDocumentCacheValid &&
            string.Equals(text, _lastSentText, StringComparison.Ordinal) &&
            string.Equals(language, _lastSentLanguage, StringComparison.Ordinal) &&
            readOnly == _lastSentReadOnly)
        {
            return;
        }

        _isSendingDocument = true;
        try
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "setDocument",
                text,
                language,
                readOnly
            });
            EditorWebView.CoreWebView2.PostWebMessageAsJson(message);
            _lastSentText = text;
            _lastSentLanguage = language;
            _lastSentReadOnly = readOnly;
            _isSentDocumentCacheValid = true;
        }
        finally
        {
            _isSendingDocument = false;
        }
    }

    private void SendThemeToWebView(bool force)
    {
        if (!_isWebViewReady || EditorWebView.CoreWebView2 is null)
        {
            return;
        }

        var theme = ActualTheme == ElementTheme.Dark ? "dark" : "light";
        if (!force && string.Equals(theme, _lastSentTheme, StringComparison.Ordinal))
        {
            return;
        }

        var message = JsonSerializer.Serialize(new
        {
            type = "setTheme",
            theme
        });
        EditorWebView.CoreWebView2.PostWebMessageAsJson(message);
        _lastSentTheme = theme;
    }

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}

internal sealed record TextDocumentSurfaceRangeEditSyncResult(
    IReadOnlyList<TextAssetEditDraftRangeEditDto> Edits,
    int BaseLength,
    int VersionId);
