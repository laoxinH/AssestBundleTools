namespace AssestBundleTools.BundleCodec.CrossCore;

public static class CrossCoreBundleCodec
{
    public const string FormatId = "crosscore.unityfs.duplicated-header-v1";
    public const int DuplicatedPrefixLength = 83;

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
            return HasDuplicatedPrefix(input)
                ? new CrossCoreBundleProbeResult(
                    IsMatch: true,
                    Confidence: 100,
                    FormatId: FormatId,
                    Evidence:
                    [
                        "unityfs.duplicated-prefix",
                        "unityfs.second-header.offset-83"
                    ],
                    Message: null)
                : CrossCoreBundleProbeResult.NoMatch("UnityFS duplicated header was not found.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CrossCoreBundleProbeResult.NoMatch(exception.Message);
        }
    }

    public static void Decrypt(string inputPath, string outputPath)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        if (!HasDuplicatedPrefix(input))
        {
            throw new InvalidDataException("Input is not a CrossCore duplicated-header v1 bundle.");
        }

        input.Position = DuplicatedPrefixLength;
        using var output = CreateOutput(outputPath);
        input.CopyTo(output);
    }

    public static void Encrypt(string inputPath, string outputPath)
    {
        ValidateTransformPaths(inputPath, outputPath);
        using var input = OpenInput(inputPath);
        if (input.Length < DuplicatedPrefixLength)
        {
            throw new InvalidDataException("Input is too small to be a standard UnityFS bundle.");
        }

        var prefix = new byte[DuplicatedPrefixLength];
        input.ReadExactly(prefix);
        if (!prefix.AsSpan().StartsWith(UnityFsSignature))
        {
            throw new InvalidDataException("Input is not a standard UnityFS bundle.");
        }

        input.Position = 0;
        if (HasDuplicatedPrefix(input))
        {
            throw new InvalidDataException("Input is already a CrossCore duplicated-header v1 bundle.");
        }

        input.Position = 0;
        using var output = CreateOutput(outputPath);
        output.Write(prefix);
        input.CopyTo(output);
    }

    private static bool HasDuplicatedPrefix(Stream input)
    {
        if (input.Length < DuplicatedPrefixLength * 2L)
        {
            return false;
        }

        var first = new byte[DuplicatedPrefixLength];
        var second = new byte[DuplicatedPrefixLength];
        input.Position = 0;
        input.ReadExactly(first);
        input.ReadExactly(second);
        return first.AsSpan().StartsWith(UnityFsSignature) &&
               second.AsSpan().StartsWith(UnityFsSignature) &&
               first.AsSpan().SequenceEqual(second);
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
