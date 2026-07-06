using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewViewModel
{
    internal void ApplyTextPreview(
        string textContent,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null,
        AssetPreviewFullContentReferenceDto? reference = null,
        string? workspaceDataRootPath = null,
        bool hasMoreLines = false)
    {
        ResetState();
        _previewKind = AssetPreviewKind.Text;
        _previewInfoRows = CloneRows(previewInfoRows);
        _previewTextReference = reference;
        _previewTextWorkspaceDataRootPath = NormalizePreviewPayloadFilePath(workspaceDataRootPath);
        _hasMorePreviewTextLines = reference is not null && hasMoreLines;
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewText = textContent;
        PreviewTextLanguage = DetectTextLanguage(previewPayloadFilePath, textContent);
        ApplyPreviewTextLines(textContent);
        NotifyPreviewStateChanged();
    }

    internal LoadPreviewTextLinesQuery? CreateLoadMorePreviewTextLinesQuery(int lineCount)
    {
        if (_previewTextReference is null ||
            string.IsNullOrWhiteSpace(_previewTextWorkspaceDataRootPath) ||
            lineCount <= 0)
        {
            return null;
        }

        return new LoadPreviewTextLinesQuery(
            _previewTextWorkspaceDataRootPath,
            _previewTextReference,
            PreviewTextLines.Count,
            lineCount);
    }

    internal void SetPreviewTextLoadingMore(bool value)
    {
        if (_isPreviewTextLoadingMore == value)
        {
            return;
        }

        _isPreviewTextLoadingMore = value;
        NotifyPreviewTextLoadingMoreStateChanged();
    }

    internal void AppendPreviewTextLines(IReadOnlyList<string> lines, bool hasMoreLines)
    {
        if (lines is { Count: > 0 })
        {
            foreach (var line in lines)
            {
                PreviewTextLines.Add(new TextDocumentLineViewModel(
                    (PreviewTextLines.Count + 1).ToString(CultureInfo.CurrentCulture),
                    line ?? string.Empty));
            }

            PreviewText = string.Join('\n', PreviewTextLines.Select(static line => line.Text));
        }

        _hasMorePreviewTextLines = hasMoreLines;
        NotifyPreviewTextLoadingMoreStateChanged();
    }

    private void ApplyPreviewTextLines(string? textContent)
    {
        PreviewTextLines.Clear();
        if (string.IsNullOrEmpty(textContent))
        {
            return;
        }

        var normalizedText = textContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lineIndex = 0;
        foreach (var line in normalizedText.Split('\n'))
        {
            PreviewTextLines.Add(new TextDocumentLineViewModel(
                (lineIndex + 1).ToString(CultureInfo.CurrentCulture),
                line));
            lineIndex++;
        }
    }

    private void NotifyPreviewTextLoadingMoreStateChanged()
    {
        OnPropertyChanged(nameof(CanLoadMorePreviewTextLines));
        OnPropertyChanged(nameof(LoadMorePreviewTextLinesButtonVisibility));
    }

    private static string DetectTextLanguage(string? previewPayloadFilePath, string? textContent)
    {
        var extension = string.IsNullOrWhiteSpace(previewPayloadFilePath)
            ? string.Empty
            : Path.GetExtension(previewPayloadFilePath.Trim());
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".skel", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (extension.Equals(".atlas", StringComparison.OrdinalIgnoreCase))
        {
            return "atlas";
        }

        var trimmed = textContent?.TrimStart();
        if (!string.IsNullOrEmpty(trimmed) &&
            (trimmed[0] == '{' || trimmed[0] == '['))
        {
            return "json";
        }

        return "plaintext";
    }
}
