using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal static class JsonAssetEditDraftTextReadSupport
{
    public static TextAssetEditDraftPatchStatisticsDto? CalculateStatistics(
        TextReader reader,
        int maxTextLength,
        out AssetEditDraftTextPatchResultDto? failure)
    {
        failure = null;
        var length = 0;
        var byteLength = 0L;
        var lineBreaks = 0;
        var hasAnyCharacter = false;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var hashBuffer = new StringBuilder(4096);

        while (TryReadNormalizedCharacter(reader, out var character))
        {
            byteLength += AppendHashCharacter(hash, hashBuffer, character);
            length++;
            hasAnyCharacter = true;
            if (character == '\n')
            {
                lineBreaks++;
            }

            if (length > maxTextLength)
            {
                failure = AssetEditDraftTextPatchResultDto.LengthExceeded();
                return null;
            }
        }

        byteLength += FlushHashBuffer(hash, hashBuffer);
        return new TextAssetEditDraftPatchStatisticsDto(
            length,
            hasAnyCharacter ? lineBreaks + 1 : 0,
            byteLength,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static TextAssetEditDraftPatchBaselineDto ComputeBaseline(TextReader reader)
    {
        var length = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var hashBuffer = new StringBuilder(4096);
        while (TryReadNormalizedCharacter(reader, out var character))
        {
            AppendHashCharacter(hash, hashBuffer, character);
            length++;
        }

        FlushHashBuffer(hash, hashBuffer);
        return new TextAssetEditDraftPatchBaselineDto(
            length,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static int SkipCharacters(TextReader reader, int count)
    {
        var skipped = 0;
        while (skipped < count && TryReadNormalizedCharacter(reader, out _))
        {
            skipped++;
        }

        return skipped;
    }

    public static string ReadCharacters(TextReader reader, int count, out int readLength)
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

    public static int SkipLines(NormalizedLineReader reader, int lineCount)
    {
        var skipped = 0;
        while (skipped < lineCount && reader.TryReadLine(out _))
        {
            skipped++;
        }

        return skipped;
    }

    public static IReadOnlyList<string> ReadLines(NormalizedLineReader reader, int lineCount)
    {
        var lines = new List<string>(Math.Min(lineCount, 512));
        while (lines.Count < lineCount && reader.TryReadLine(out var line))
        {
            lines.Add(line);
        }

        return lines;
    }

    public static TextSearchReadResult SearchLines(
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

    public static bool TryReadNormalizedCharacter(TextReader reader, out char character)
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

    private static AssetPreviewTextSearchMatchDto CreateSearchMatch(
        int lineIndex,
        int columnIndex,
        string line)
    {
        const int maxLineTextLength = 1024;
        var isTruncated = line.Length > maxLineTextLength;
        return new AssetPreviewTextSearchMatchDto(
            lineIndex,
            columnIndex,
            isTruncated ? line[..maxLineTextLength] : line,
            isTruncated);
    }

    private static int AppendHashCharacter(
        IncrementalHash hash,
        StringBuilder buffer,
        char character)
    {
        buffer.Append(character);
        if (buffer.Length < 4096 ||
            char.IsHighSurrogate(buffer[^1]))
        {
            return 0;
        }

        return FlushHashBuffer(hash, buffer);
    }

    private static int FlushHashBuffer(
        IncrementalHash hash,
        StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(buffer.ToString());
        hash.AppendData(bytes);
        buffer.Clear();
        return bytes.Length;
    }
}

internal sealed class NormalizedLineReader
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
        while (JsonAssetEditDraftTextReadSupport.TryReadNormalizedCharacter(_reader, out var character))
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

internal readonly record struct TextSearchReadResult(
    IReadOnlyList<AssetPreviewTextSearchMatchDto> Matches,
    int ScannedLineCount,
    bool HasMore);
