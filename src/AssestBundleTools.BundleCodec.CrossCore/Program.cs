using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AssestBundleTools.BundleCodec.CrossCore;

return await CrossCoreBundleCodecProgram.RunAsync(args);

public static class CrossCoreBundleCodecProgram
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    internal const int ResultSchemaVersion = 1;

    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        var options = CodecOptions.Parse(args);
        if (!options.IsValid)
        {
            await WriteTransformResultAsync(
                options.ResultPath,
                CrossCoreCodecTransformPayload.Failed(options.ValidationMessage));
            return FailureExitCode;
        }

        try
        {
            return options.Command switch
            {
                CodecCommand.Probe => await ProbeAsync(options),
                CodecCommand.Decrypt => await DecryptAsync(options),
                CodecCommand.Encrypt => await EncryptAsync(options),
                _ => FailureExitCode
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await WriteTransformResultAsync(
                options.ResultPath,
                CrossCoreCodecTransformPayload.Failed(exception.Message));
            Console.Error.WriteLine(exception.Message);
            return FailureExitCode;
        }
    }

    private static async Task<int> ProbeAsync(CodecOptions options)
    {
        var result = CrossCoreBundleCodec.Probe(options.InputPath);
        await WriteJsonAsync(
            options.ResultPath,
            new CrossCoreCodecProbePayload(
                ResultSchemaVersion,
                result.IsMatch,
                result.Confidence,
                result.FormatId,
                result.Evidence,
                result.Message),
            CrossCoreCodecJsonContext.Default.CrossCoreCodecProbePayload);
        return SuccessExitCode;
    }

    private static async Task<int> DecryptAsync(CodecOptions options)
    {
        var metadata = CrossCoreBundleCodec.Decrypt(options.InputPath, options.OutputPath);
        var outputPath = Path.GetFullPath(options.OutputPath);
        await WriteTransformResultAsync(
            options.ResultPath,
            CrossCoreCodecTransformPayload.Succeeded(outputPath, metadata.CodecParameters));
        return SuccessExitCode;
    }

    private static async Task<int> EncryptAsync(CodecOptions options)
    {
        CrossCoreBundleCodec.Encrypt(
            options.InputPath,
            options.OutputPath,
            options.OuterPrefixBase64);
        var outputPath = Path.GetFullPath(options.OutputPath);
        await WriteTransformResultAsync(
            options.ResultPath,
            CrossCoreCodecTransformPayload.Succeeded(outputPath));
        return SuccessExitCode;
    }

    private static Task WriteTransformResultAsync(string? resultPath, CrossCoreCodecTransformPayload result) =>
        WriteJsonAsync(
            resultPath,
            result,
            CrossCoreCodecJsonContext.Default.CrossCoreCodecTransformPayload);

    private static async Task WriteJsonAsync<T>(
        string? resultPath,
        T payload,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        if (string.IsNullOrWhiteSpace(resultPath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(resultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedPath) ?? Environment.CurrentDirectory);
        await using var stream = new FileStream(
            normalizedPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, payload, jsonTypeInfo);
    }

    private enum CodecCommand
    {
        None = 0,
        Probe = 1,
        Decrypt = 2,
        Encrypt = 3
    }

    private sealed record CodecOptions(
        CodecCommand Command,
        string InputPath,
        string OutputPath,
        string ResultPath,
        string? OuterPrefixBase64,
        string? ValidationMessage)
    {
        public bool IsValid => string.IsNullOrWhiteSpace(ValidationMessage);

        public static CodecOptions Parse(IReadOnlyList<string> args)
        {
            var command = args.Count == 0
                ? CodecCommand.None
                : args[0].Trim().ToLowerInvariant() switch
                {
                    "probe" => CodecCommand.Probe,
                    "decrypt" => CodecCommand.Decrypt,
                    "encrypt" => CodecCommand.Encrypt,
                    _ => CodecCommand.None
                };
            var values = ParseNamedValues(args.Skip(1));
            var inputPath = GetValue(values, "input");
            var outputPath = GetValue(values, "output");
            var resultPath = GetValue(values, "result");
            var outerPrefixBase64 = GetValue(values, "outer-prefix-base64");
            var validationMessage = command == CodecCommand.None
                ? "Command must be probe, decrypt, or encrypt."
                : string.IsNullOrWhiteSpace(inputPath)
                    ? "Missing --input."
                    : string.IsNullOrWhiteSpace(resultPath)
                        ? "Missing --result."
                        : command != CodecCommand.Probe && string.IsNullOrWhiteSpace(outputPath)
                            ? "Missing --output."
                            : null;

            return new CodecOptions(
                command,
                inputPath ?? string.Empty,
                outputPath ?? string.Empty,
                resultPath ?? string.Empty,
                outerPrefixBase64,
                validationMessage);
        }

        private static IReadOnlyDictionary<string, string> ParseNamedValues(IEnumerable<string> args)
        {
            var items = args.ToArray();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < items.Length; index++)
            {
                var key = items[index];
                if (!key.StartsWith("--", StringComparison.Ordinal) || index == items.Length - 1)
                {
                    continue;
                }

                values[key[2..]] = items[++index];
            }

            return values;
        }

        private static string? GetValue(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
    }

}

internal sealed record CrossCoreCodecProbePayload(
    int SchemaVersion,
    bool IsMatch,
    int Confidence,
    string? FormatId,
    IReadOnlyList<string> Evidence,
    string? Message);

internal sealed record CrossCoreCodecTransformPayload(
    int SchemaVersion,
    bool IsSuccess,
    string? OutputPath,
    string? Message,
    IReadOnlyDictionary<string, string>? CodecParameters = null)
{
    public static CrossCoreCodecTransformPayload Succeeded(
        string outputPath,
        IReadOnlyDictionary<string, string>? codecParameters = null) =>
        new(CrossCoreBundleCodecProgram.ResultSchemaVersion, true, outputPath, null, codecParameters);

    public static CrossCoreCodecTransformPayload Failed(string? message) =>
        new(CrossCoreBundleCodecProgram.ResultSchemaVersion, false, null, message);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(CrossCoreCodecProbePayload))]
[JsonSerializable(typeof(CrossCoreCodecTransformPayload))]
internal sealed partial class CrossCoreCodecJsonContext : JsonSerializerContext;
