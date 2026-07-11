using System.Buffers.Binary;

namespace AssestBundleTools.BundleCodec.CrossCore;

public static class CrossCoreBundleCodec
{
    public const string DuplicatedHeaderFormatId = "crosscore.unityfs.duplicated-header-v1";
    public const string PrefixedHeaderFormatId = "crosscore.unityfs.prefixed-header-v2";
    public const string OuterPrefixBase64Parameter = "crosscore.outerPrefixBase64";
    public const int DuplicatedPrefixLength = 83;
    private const int MaximumOuterPrefixLength = 64 * 1024;

    private static ReadOnlySpan<byte> UnityFsSignature => "UnityFS\0"u8;

    public static CrossCoreBundleProbeResult Probe(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return CrossCoreBundleProbeResult.NoMatch("Input file does not exist.");
        }

        try
        {
            using var input = OpenInput(inputPath);
            return TryInspectPrefixedBundle(input, out var inspection)
                ? new CrossCoreBundleProbeResult(
                    IsMatch: true,
                    Confidence: 100,
                    FormatId: inspection.FormatId,
                    Evidence:
                    [
                        inspection.IsDuplicatedPrefix
                            ? "unityfs.duplicated-prefix"
                            : "unityfs.prefixed-outer-header",
                        $"unityfs.inner-header.offset-{inspection.InnerBundleOffset}",
                        "unityfs.second-header.declared-size-valid"
                    ],
                    Message: null)
                : CrossCoreBundleProbeResult.NoMatch("CrossCore UnityFS prefixed header was not found.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CrossCoreBundleProbeResult.NoMatch(exception.Message);
        }
    }

    public static CrossCoreBundleDecryptMetadata Decrypt(string inputPath, string outputPath)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        if (!TryInspectPrefixedBundle(input, out var inspection))
        {
            throw new InvalidDataException("Input is not a supported CrossCore prefixed-header bundle.");
        }

        var outerPrefix = new byte[checked((int)inspection.InnerBundleOffset)];
        input.Position = 0;
        input.ReadExactly(outerPrefix);
        input.Position = inspection.InnerBundleOffset;
        using var output = CreateOutput(outputPath);
        input.CopyTo(output);
        return new CrossCoreBundleDecryptMetadata(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OuterPrefixBase64Parameter] = Convert.ToBase64String(outerPrefix)
            });
    }

    public static void Encrypt(
        string inputPath,
        string outputPath,
        string? outerPrefixBase64 = null)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        if (input.Length < DuplicatedPrefixLength)
        {
            throw new InvalidDataException("Input is too small to be a standard UnityFS bundle.");
        }

        if (!TryReadUnityFsHeader(input, offset: 0, expectedLength: input.Length))
        {
            throw new InvalidDataException("Input is not a structurally valid standard UnityFS bundle.");
        }

        input.Position = 0;
        if (TryInspectPrefixedBundle(input, out _))
        {
            throw new InvalidDataException("Input is already a CrossCore prefixed-header bundle.");
        }

        var prefix = ResolveEncryptionPrefix(input, outerPrefixBase64);
        input.Position = 0;
        using var output = CreateOutput(outputPath);
        output.Write(prefix);
        input.CopyTo(output);
    }

    private static byte[] ResolveEncryptionPrefix(Stream input, string? outerPrefixBase64)
    {
        if (!string.IsNullOrWhiteSpace(outerPrefixBase64))
        {
            byte[] prefix;
            try
            {
                prefix = Convert.FromBase64String(outerPrefixBase64.Trim());
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("CrossCore outer-prefix metadata is not valid Base64.", exception);
            }

            if (prefix.Length is < 8 or > MaximumOuterPrefixLength ||
                !prefix.AsSpan().StartsWith(UnityFsSignature))
            {
                throw new InvalidDataException("CrossCore outer-prefix metadata is invalid or exceeds the supported limit.");
            }

            return prefix;
        }

        var duplicatedPrefix = new byte[DuplicatedPrefixLength];
        input.Position = 0;
        input.ReadExactly(duplicatedPrefix);
        return duplicatedPrefix;
    }

    private static bool TryInspectPrefixedBundle(
        Stream input,
        out CrossCorePrefixedBundleInspection inspection)
    {
        inspection = default;
        if (input.Length < DuplicatedPrefixLength * 2L)
        {
            return false;
        }

        Span<byte> outerSignature = stackalloc byte[8];
        input.Position = 0;
        input.ReadExactly(outerSignature);
        if (!outerSignature.SequenceEqual(UnityFsSignature))
        {
            return false;
        }

        foreach (var candidateOffset in FindInnerUnityFsOffsets(input))
        {
            if (!TryReadUnityFsHeader(input, candidateOffset, input.Length - candidateOffset))
            {
                continue;
            }

            var isDuplicatedPrefix = candidateOffset == DuplicatedPrefixLength &&
                                     HasEqualPrefix(input, candidateOffset);
            inspection = new CrossCorePrefixedBundleInspection(
                isDuplicatedPrefix ? DuplicatedHeaderFormatId : PrefixedHeaderFormatId,
                isDuplicatedPrefix,
                candidateOffset);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<long> FindInnerUnityFsOffsets(Stream input)
    {
        var offsets = new List<long>();
        var searchLength = (int)Math.Min(
            input.Length,
            MaximumOuterPrefixLength + UnityFsSignature.Length);
        var prefix = new byte[searchLength];
        input.Position = 0;
        input.ReadExactly(prefix);

        var searchOffset = UnityFsSignature.Length;
        while (searchOffset <= prefix.Length - UnityFsSignature.Length)
        {
            var relativeOffset = prefix.AsSpan(searchOffset).IndexOf(UnityFsSignature);
            if (relativeOffset < 0)
            {
                break;
            }

            var candidateOffset = searchOffset + relativeOffset;
            offsets.Add(candidateOffset);
            searchOffset = candidateOffset + UnityFsSignature.Length;
        }

        return offsets;
    }

    private static bool HasEqualPrefix(Stream input, long innerBundleOffset)
    {
        var first = new byte[DuplicatedPrefixLength];
        var second = new byte[DuplicatedPrefixLength];
        input.Position = 0;
        input.ReadExactly(first);
        input.Position = innerBundleOffset;
        input.ReadExactly(second);
        return first.AsSpan().SequenceEqual(second);
    }

    private static bool TryReadUnityFsHeader(Stream input, long offset, long expectedLength)
    {
        try
        {
            input.Position = offset;
            Span<byte> signature = stackalloc byte[8];
            input.ReadExactly(signature);
            if (!signature.SequenceEqual(UnityFsSignature))
            {
                return false;
            }

            var formatVersion = ReadUInt32BigEndian(input);
            if (formatVersion is < 6 or > 9 ||
                !TrySkipNullTerminatedString(input, maximumLength: 128) ||
                !TrySkipNullTerminatedString(input, maximumLength: 128))
            {
                return false;
            }

            var declaredLength = ReadInt64BigEndian(input);
            var compressedBlocksInfoSize = ReadUInt32BigEndian(input);
            var uncompressedBlocksInfoSize = ReadUInt32BigEndian(input);
            _ = ReadUInt32BigEndian(input);
            var headerLength = input.Position - offset;
            return declaredLength == expectedLength &&
                   declaredLength >= headerLength &&
                   compressedBlocksInfoSize > 0 &&
                   uncompressedBlocksInfoSize > 0 &&
                   compressedBlocksInfoSize <= declaredLength - headerLength;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    private static bool TrySkipNullTerminatedString(Stream input, int maximumLength)
    {
        for (var index = 0; index < maximumLength; index++)
        {
            var value = input.ReadByte();
            if (value < 0)
            {
                return false;
            }

            if (value == 0)
            {
                return index > 0;
            }
        }

        return false;
    }

    private static uint ReadUInt32BigEndian(Stream input)
    {
        Span<byte> buffer = stackalloc byte[4];
        input.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static long ReadInt64BigEndian(Stream input)
    {
        Span<byte> buffer = stackalloc byte[8];
        input.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    private static FileStream OpenInput(string path) =>
        new(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);

    private static FileStream CreateOutput(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        return new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);
    }

    private static void ValidateTransformPaths(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input bundle does not exist.", inputPath);
        }

        if (string.Equals(
                Path.GetFullPath(inputPath),
                Path.GetFullPath(outputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Input and output paths must be different.");
        }
    }
}

internal readonly record struct CrossCorePrefixedBundleInspection(
    string FormatId,
    bool IsDuplicatedPrefix,
    long InnerBundleOffset);

public sealed record CrossCoreBundleDecryptMetadata(
    IReadOnlyDictionary<string, string> CodecParameters);

public sealed record CrossCoreBundleProbeResult(
    bool IsMatch,
    int Confidence,
    string? FormatId,
    IReadOnlyList<string> Evidence,
    string? Message)
{
    public static CrossCoreBundleProbeResult NoMatch(string? message) =>
        new(false, 0, null, Array.Empty<string>(), message);
}
