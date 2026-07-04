using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 在外部替换文件中执行规范化文本搜索，避免完整大文本回到 UI 层处理。
/// </summary>
internal static class FileSystemReplacementTextSearchReader
{
    private const string TextContentType = "text/plain; charset=utf-8";

    public static Result<ReplacementTextSearchResultDto> Search(
        string sourceFilePath,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ReplacementTextSearchResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Result<ReplacementTextSearchResultDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement source path is empty."));
        }

        if (string.IsNullOrEmpty(searchText) || startLineIndex < 0 || maxMatchCount <= 0)
        {
            return Result<ReplacementTextSearchResultDto>.Failure(
                ApplicationErrors.AssetPreviewTextSearchQueryInvalid());
        }

        var normalizedPath = sourceFilePath.Trim();
        try
        {
            if (!File.Exists(normalizedPath))
            {
                return Result<ReplacementTextSearchResultDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, "Replacement source file was not found."));
            }

            var fileInfo = new FileInfo(normalizedPath);
            using var stream = new FileStream(
                normalizedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: false);
            var encodingInfo = DetectEncoding(stream);
            if (ShouldKeepSkelBinary(normalizedPath, stream, encodingInfo))
            {
                return Result<ReplacementTextSearchResultDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, "Replacement text source is binary."));
            }

            stream.Position = encodingInfo.PreambleLength;
            using var reader = new StreamReader(
                stream,
                encodingInfo.Encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: false);
            var lineReader = new NormalizedLineReader(reader);

            var skippedLineCount = SkipLines(lineReader, startLineIndex, cancellationToken);
            var result = SearchLines(
                lineReader,
                searchText,
                skippedLineCount,
                maxMatchCount,
                matchCase,
                cancellationToken);

            return Result<ReplacementTextSearchResultDto>.Success(new ReplacementTextSearchResultDto(
                normalizedPath,
                TextContentType,
                result.Matches,
                skippedLineCount,
                skippedLineCount + result.ScannedLineCount,
                result.HasMore,
                fileInfo.Length));
        }
        catch (OperationCanceledException)
        {
            return Result<ReplacementTextSearchResultDto>.Cancelled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or DecoderFallbackException or InvalidDataException)
        {
            return Result<ReplacementTextSearchResultDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, ex.GetType().Name));
        }
    }

    private static int SkipLines(
        NormalizedLineReader reader,
        int lineCount,
        CancellationToken cancellationToken)
    {
        var skipped = 0;
        while (skipped < lineCount && reader.TryReadLine(out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            skipped++;
        }

        return skipped;
    }

    private static TextSearchReadResult SearchLines(
        NormalizedLineReader reader,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase,
        CancellationToken cancellationToken)
    {
        var matches = new List<AssetPreviewTextSearchMatchDto>(Math.Min(maxMatchCount, 64));
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var lineIndex = startLineIndex;
        var scannedLineCount = 0;

        while (reader.TryReadLine(out var line))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scannedLineCount++;
            var columnIndex = line.IndexOf(searchText, comparison);
            while (columnIndex >= 0)
            {
                if (matches.Count >= maxMatchCount)
                {
                    return new TextSearchReadResult(matches, scannedLineCount, HasMore: true);
                }

                matches.Add(CreateSearchMatch(lineIndex, columnIndex, line));
                columnIndex = line.IndexOf(searchText, columnIndex + Math.Max(1, searchText.Length), comparison);
            }

            lineIndex++;
        }

        return new TextSearchReadResult(matches, scannedLineCount, HasMore: false);
    }

    private static AssetPreviewTextSearchMatchDto CreateSearchMatch(
        int lineIndex,
        int columnIndex,
        string line)
    {
        const int maxLineTextLength = 1024;
        var isTruncated = line.Length > maxLineTextLength;
        var lineText = isTruncated ? line[..maxLineTextLength] : line;
        return new AssetPreviewTextSearchMatchDto(lineIndex, columnIndex, lineText, isTruncated);
    }

    private static bool ShouldKeepSkelBinary(
        string sourceFilePath,
        Stream stream,
        TextEncodingInfo encodingInfo)
    {
        if (!Path.GetExtension(sourceFilePath).Equals(".skel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (stream.Length <= encodingInfo.PreambleLength)
        {
            return false;
        }

        stream.Position = encodingInfo.PreambleLength;
        using var reader = new StreamReader(
            stream,
            encodingInfo.Encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
        while (TryReadNormalizedCharacter(reader, out var character))
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character != '{';
        }

        return true;
    }

    private static bool TryReadNormalizedCharacter(
        TextReader reader,
        out char character)
    {
        var value = reader.Read();
        if (value < 0)
        {
            character = default;
            return false;
        }

        character = (char)value;
        if (character == '\uFEFF')
        {
            return TryReadNormalizedCharacter(reader, out character);
        }

        if (character == '\r')
        {
            if (reader.Peek() == '\n')
            {
                _ = reader.Read();
            }

            character = '\n';
            return true;
        }

        if (character == '\uFFFD' ||
            (char.IsControl(character) && character is not ('\t' or '\n')))
        {
            throw new InvalidDataException("Replacement source contains unreadable text characters.");
        }

        return true;
    }

    private static TextEncodingInfo DetectEncoding(Stream stream)
    {
        Span<byte> header = stackalloc byte[3];
        stream.Position = 0;
        var bytesRead = stream.Read(header);

        if (bytesRead >= 2 &&
            header[0] == 0xFF &&
            header[1] == 0xFE)
        {
            return new TextEncodingInfo(
                new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true),
                PreambleLength: 2);
        }

        if (bytesRead >= 2 &&
            header[0] == 0xFE &&
            header[1] == 0xFF)
        {
            return new TextEncodingInfo(
                new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true),
                PreambleLength: 2);
        }

        if (bytesRead >= 3 &&
            header[0] == 0xEF &&
            header[1] == 0xBB &&
            header[2] == 0xBF)
        {
            return new TextEncodingInfo(
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                PreambleLength: 3);
        }

        return new TextEncodingInfo(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            PreambleLength: 0);
    }

    private readonly record struct TextEncodingInfo(
        Encoding Encoding,
        int PreambleLength);

    private sealed class NormalizedLineReader
    {
        private readonly TextReader _reader;
        private bool _hasPendingFinalEmptyLine;

        public NormalizedLineReader(TextReader reader)
        {
            _reader = reader;
        }

        public bool TryReadLine(out string line)
        {
            if (_hasPendingFinalEmptyLine)
            {
                _hasPendingFinalEmptyLine = false;
                line = string.Empty;
                return true;
            }

            var builder = new StringBuilder();
            while (TryReadNormalizedCharacter(_reader, out var character))
            {
                if (character == '\n')
                {
                    _hasPendingFinalEmptyLine = _reader.Peek() < 0;
                    line = builder.ToString();
                    return true;
                }

                builder.Append(character);
            }

            if (builder.Length == 0)
            {
                line = string.Empty;
                return false;
            }

            line = builder.ToString();
            return true;
        }
    }

    private readonly record struct TextSearchReadResult(
        IReadOnlyList<AssetPreviewTextSearchMatchDto> Matches,
        int ScannedLineCount,
        bool HasMore);
}
