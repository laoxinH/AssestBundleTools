namespace AssestBundleTools.BundleCodec.Arknights;

public static class ArknightsBundleCodec
{
    public const string FormatId = "arknights.unityfs.lz4ak";

    public static ArknightsBundleProbeResult Probe(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return ArknightsBundleProbeResult.NoMatch("Input file does not exist.");
        }

        try
        {
            using var stream = OpenInput(inputPath);
            var layout = UnityFsBundleReader.ReadLayout(stream);
            var evidence = new List<string>();
            if (layout.BlocksInfoUsedLz4Ak)
            {
                evidence.Add("unityfs.blocks-info.lz4ak");
            }

            if (layout.Blocks.Any(static block => Lz4AkBlockCodec.IsLz4AkType(block.CompressionType)))
            {
                evidence.Add("unityfs.data-block.type-4-or-5");
            }

            if (evidence.Count == 0 && HasDisguisedLz4AkBlock(stream, layout))
            {
                evidence.Add("unityfs.data-block.lz4ak-under-standard-flag");
            }

            return evidence.Count == 0
                ? ArknightsBundleProbeResult.NoMatch("UnityFS bundle does not contain LZ4AK blocks.")
                : new ArknightsBundleProbeResult(
                    IsMatch: true,
                    Confidence: evidence.Contains("unityfs.data-block.type-4-or-5", StringComparer.Ordinal) ? 100 : 95,
                    FormatId: FormatId,
                    Evidence: evidence,
                    Message: null);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            return ArknightsBundleProbeResult.NoMatch(exception.Message);
        }
    }

    public static void Decrypt(string inputPath, string outputPath)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        var layout = UnityFsBundleReader.ReadLayout(input);
        UnityFsBundleWriter.WriteStandardBundle(input, layout, outputPath);
    }

    public static void Encrypt(string inputPath, string outputPath)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        var layout = UnityFsBundleReader.ReadLayout(input);
        UnityFsBundleWriter.WriteLz4AkBundle(input, layout, outputPath);
    }

    private static bool HasDisguisedLz4AkBlock(Stream stream, UnityFsLayout layout)
    {
        var blockOffset = layout.DataOffset;
        foreach (var block in layout.Blocks)
        {
            if (block.CompressionType is UnityFsCompressionType.Lz4 or UnityFsCompressionType.Lz4Hc)
            {
                var compressedData = UnityFsBundleReader.ReadBlock(stream, blockOffset, block.CompressedSize);
                if (!Lz4AkBlockCodec.CanDecodeStandard(compressedData, block.UncompressedSize) &&
                    Lz4AkBlockCodec.CanDecodeLz4Ak(compressedData, block.UncompressedSize))
                {
                    return true;
                }
            }

            blockOffset = checked(blockOffset + block.CompressedSize);
        }

        return false;
    }

    private static FileStream OpenInput(string path) =>
        new(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);

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

public sealed record ArknightsBundleProbeResult(
    bool IsMatch,
    int Confidence,
    string? FormatId,
    IReadOnlyList<string> Evidence,
    string? Message)
{
    public static ArknightsBundleProbeResult NoMatch(string? message) =>
        new(false, 0, null, Array.Empty<string>(), message);
}
