using System.IO;
using System.Text;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 按片读取外部替换文件中的规范化文本，避免大文本预览回落到一次性全量载入。
/// </summary>
internal static class FileSystemReplacementTextChunkReader
{
    private const string TextContentType = "text/plain; charset=utf-8";

    public static Result<ReplacementTextChunkDto> Load(
        string sourceFilePath,
        int offset,
        int length,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ReplacementTextChunkDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Result<ReplacementTextChunkDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement source path is empty."));
        }

        if (offset < 0 || length <= 0)
        {
            return Result<ReplacementTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid());
        }

        var normalizedPath = sourceFilePath.Trim();
        try
        {
            if (!File.Exists(normalizedPath))
            {
                return Result<ReplacementTextChunkDto>.Failure(
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
                return Result<ReplacementTextChunkDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, "Replacement text source is binary."));
            }

            stream.Position = encodingInfo.PreambleLength;
            using var reader = new StreamReader(
                stream,
                encodingInfo.Encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: false);

            var skippedLength = SkipCharacters(reader, offset, cancellationToken);
            var chunkBuilder = new StringBuilder(Math.Min(length, 4096));
            var chunkLength = ReadChunk(reader, length, chunkBuilder, cancellationToken);
            var remainingLength = CountRemainingCharacters(reader, cancellationToken);
            var totalLength = skippedLength + chunkLength + remainingLength;

            return Result<ReplacementTextChunkDto>.Success(new ReplacementTextChunkDto(
                normalizedPath,
                TextContentType,
                chunkBuilder.ToString(),
                skippedLength,
                chunkLength,
                totalLength,
                fileInfo.Length));
        }
        catch (OperationCanceledException)
        {
            return Result<ReplacementTextChunkDto>.Cancelled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or DecoderFallbackException or InvalidDataException)
        {
            return Result<ReplacementTextChunkDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, ex.GetType().Name));
        }
    }

    public static Result<ReplacementTextLineRangeDto> LoadLines(
        string sourceFilePath,
        int startLineIndex,
        int lineCount,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ReplacementTextLineRangeDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return Result<ReplacementTextLineRangeDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath, "Replacement source path is empty."));
        }

        if (startLineIndex < 0 || lineCount <= 0)
        {
            return Result<ReplacementTextLineRangeDto>.Failure(
                ApplicationErrors.AssetPreviewTextChunkRangeInvalid());
        }

        var normalizedPath = sourceFilePath.Trim();
        try
        {
            if (!File.Exists(normalizedPath))
            {
                return Result<ReplacementTextLineRangeDto>.Failure(
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
                return Result<ReplacementTextLineRangeDto>.Failure(
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
            var lines = ReadLines(lineReader, lineCount, cancellationToken);
            var remainingLineCount = CountRemainingLines(lineReader, cancellationToken);
            var totalLineCount = skippedLineCount + lines.Count + remainingLineCount;

            return Result<ReplacementTextLineRangeDto>.Success(new ReplacementTextLineRangeDto(
                normalizedPath,
                TextContentType,
                lines,
                skippedLineCount,
                lines.Count,
                totalLineCount,
                fileInfo.Length));
        }
        catch (OperationCanceledException)
        {
            return Result<ReplacementTextLineRangeDto>.Cancelled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or DecoderFallbackException or InvalidDataException)
        {
            return Result<ReplacementTextLineRangeDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(normalizedPath, ex.GetType().Name));
        }
    }

    private static int SkipCharacters(
        TextReader reader,
        int count,
        CancellationToken cancellationToken)
    {
        var skipped = 0;
        while (skipped < count && TryReadNormalizedCharacter(reader, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            skipped++;
        }

        return skipped;
    }

    private static int ReadChunk(
        TextReader reader,
        int length,
        StringBuilder chunkBuilder,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < length && TryReadNormalizedCharacter(reader, out var character))
        {
            cancellationToken.ThrowIfCancellationRequested();
            chunkBuilder.Append(character);
            read++;
        }

        return read;
    }

    private static int CountRemainingCharacters(
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var remaining = 0;
        while (TryReadNormalizedCharacter(reader, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            remaining++;
        }

        return remaining;
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

    private static IReadOnlyList<string> ReadLines(
        NormalizedLineReader reader,
        int lineCount,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>(Math.Min(lineCount, 256));
        while (lines.Count < lineCount && reader.TryReadLine(out var line))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.Add(line);
        }

        return lines;
    }

    private static int CountRemainingLines(
        NormalizedLineReader reader,
        CancellationToken cancellationToken)
    {
        var remaining = 0;
        while (reader.TryReadLine(out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            remaining++;
        }

        return remaining;
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
}
