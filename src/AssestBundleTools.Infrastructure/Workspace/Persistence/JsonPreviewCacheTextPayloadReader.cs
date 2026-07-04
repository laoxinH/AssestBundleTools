using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 从预览缓存文本 payload 文件中按需读取规范化文本片段。
/// </summary>
internal static class JsonPreviewCacheTextPayloadReader
{
    public static PreviewCacheTextChunkDto? TryReadChunk(
        PreviewCacheDescriptorDto descriptor,
        string payloadFilePath,
        long payloadLength,
        int offset,
        int length)
    {
        try
        {
            using var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(payloadFilePath);
            using var reader = CreateReader(stream);

            var skipped = SkipNormalizedCharacters(reader, offset);
            var text = ReadNormalizedCharacters(reader, length, out var readLength);
            var totalLength = ResolveKnownFullCharacterCount(descriptor)
                ?? skipped + readLength + CountRemainingNormalizedCharacters(reader);

            return new PreviewCacheTextChunkDto(
                descriptor,
                text,
                skipped,
                readLength,
                totalLength,
                payloadLength);
        }
        catch (IOException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static PreviewCacheTextLineRangeDto? TryReadLines(
        PreviewCacheDescriptorDto descriptor,
        string payloadFilePath,
        long payloadLength,
        int startLineIndex,
        int lineCount)
    {
        try
        {
            using var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(payloadFilePath);
            using var reader = CreateReader(stream);
            var lineReader = new NormalizedLineReader(reader);

            var skippedLineCount = SkipLines(lineReader, startLineIndex);
            var lines = ReadLines(lineReader, lineCount);
            var totalLineCount = ResolveKnownFullLineCount(descriptor)
                ?? skippedLineCount + lines.Count + CountRemainingLines(lineReader);

            return new PreviewCacheTextLineRangeDto(
                descriptor,
                lines,
                skippedLineCount,
                lines.Count,
                totalLineCount,
                payloadLength);
        }
        catch (IOException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static PreviewCacheTextSearchResultDto? TrySearch(
        PreviewCacheDescriptorDto descriptor,
        string payloadFilePath,
        long payloadLength,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase)
    {
        try
        {
            using var stream = JsonWorkspaceFileStorageHelper.OpenSharedReadStream(payloadFilePath);
            using var reader = CreateReader(stream);
            var lineReader = new NormalizedLineReader(reader);

            var skippedLineCount = SkipLines(lineReader, startLineIndex);
            var result = SearchLines(
                lineReader,
                searchText,
                skippedLineCount,
                maxMatchCount,
                matchCase);

            return new PreviewCacheTextSearchResultDto(
                descriptor,
                result.Matches,
                skippedLineCount,
                skippedLineCount + result.ScannedLineCount,
                result.HasMore,
                payloadLength);
        }
        catch (IOException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static StreamReader CreateReader(Stream stream) =>
        new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: false);

    private static int? ResolveKnownFullCharacterCount(PreviewCacheDescriptorDto descriptor)
    {
        var value = descriptor.PreviewInfoItems?
            .FirstOrDefault(static item => item.Key == AssetPreviewInfoKey.TextFullCharacterCount)
            ?.Int64Value;
        return value is >= 0 and <= int.MaxValue
            ? (int)value.Value
            : null;
    }

    private static int? ResolveKnownFullLineCount(PreviewCacheDescriptorDto descriptor)
    {
        var value = descriptor.PreviewInfoItems?
            .FirstOrDefault(static item => item.Key == AssetPreviewInfoKey.TextEstimatedFullLineCount)
            ?.Int64Value;
        return value is >= 0 and <= int.MaxValue
            ? (int)value.Value
            : null;
    }

    private static int SkipNormalizedCharacters(TextReader reader, int count)
    {
        var skipped = 0;
        while (skipped < count && TryReadNormalizedCharacter(reader, out _))
        {
            skipped++;
        }

        return skipped;
    }

    private static string ReadNormalizedCharacters(
        TextReader reader,
        int count,
        out int readLength)
    {
        var builder = new StringBuilder(Math.Min(count, 4096));
        readLength = 0;

        while (readLength < count && TryReadNormalizedCharacter(reader, out var character))
        {
            builder.Append(character);
            readLength++;
        }

        return builder.ToString();
    }

    private static int CountRemainingNormalizedCharacters(TextReader reader)
    {
        var count = 0;
        while (TryReadNormalizedCharacter(reader, out _))
        {
            count++;
        }

        return count;
    }

    private static int SkipLines(NormalizedLineReader reader, int lineCount)
    {
        var skipped = 0;
        while (skipped < lineCount && reader.TryReadLine(out _))
        {
            skipped++;
        }

        return skipped;
    }

    private static IReadOnlyList<string> ReadLines(NormalizedLineReader reader, int lineCount)
    {
        var lines = new List<string>(Math.Min(lineCount, 256));
        while (lines.Count < lineCount && reader.TryReadLine(out var line))
        {
            lines.Add(line);
        }

        return lines;
    }

    private static int CountRemainingLines(NormalizedLineReader reader)
    {
        var count = 0;
        while (reader.TryReadLine(out _))
        {
            count++;
        }

        return count;
    }

    private static TextSearchReadResult SearchLines(
        NormalizedLineReader reader,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase)
    {
        var matches = new List<AssetPreviewTextSearchMatchDto>(Math.Min(maxMatchCount, 64));
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var lineIndex = startLineIndex;
        var scannedLineCount = 0;

        while (reader.TryReadLine(out var line))
        {
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

    private static bool TryReadNormalizedCharacter(TextReader reader, out char character)
    {
        var value = reader.Read();
        if (value < 0)
        {
            character = '\0';
            return false;
        }

        character = (char)value;
        if (character != '\r')
        {
            return true;
        }

        if (reader.Peek() == '\n')
        {
            reader.Read();
        }

        character = '\n';
        return true;
    }

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
