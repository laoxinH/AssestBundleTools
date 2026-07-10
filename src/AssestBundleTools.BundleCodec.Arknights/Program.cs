using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AssestBundleTools.BundleCodec.Arknights;

return await ArknightsBundleCodecProgram.RunAsync(args);

public static class ArknightsBundleCodecProgram
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
                ArknightsCodecTransformPayload.Failed(options.ValidationMessage));
            return FailureExitCode;
        }

        try
        {
            return options.Command switch
            {
                CodecCommand.Probe => await ProbeAsync(options),
                CodecCommand.Decrypt => await TransformAsync(options, ArknightsBundleCodec.Decrypt),
                CodecCommand.Encrypt => await TransformAsync(options, ArknightsBundleCodec.Encrypt),
                _ => FailureExitCode
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await WriteTransformResultAsync(
                options.ResultPath,
                ArknightsCodecTransformPayload.Failed(exception.Message));
            Console.Error.WriteLine(exception.Message);
            return FailureExitCode;
        }
    }

    private static async Task<int> ProbeAsync(CodecOptions options)
    {
        var result = ArknightsBundleCodec.Probe(options.InputPath);
        await WriteJsonAsync(
            options.ResultPath,
            new ArknightsCodecProbePayload(
                ResultSchemaVersion,
                result.IsMatch,
                result.Confidence,
                result.FormatId,
                result.Evidence,
                result.Message),
            ArknightsCodecJsonContext.Default.ArknightsCodecProbePayload);
        return SuccessExitCode;
    }

    private static async Task<int> TransformAsync(
        CodecOptions options,
        Action<string, string> transform)
    {
        transform(options.InputPath, options.OutputPath);
        var outputPath = Path.GetFullPath(options.OutputPath);
        await WriteTransformResultAsync(
            options.ResultPath,
            ArknightsCodecTransformPayload.Succeeded(outputPath));
        return SuccessExitCode;
    }

    private static Task WriteTransformResultAsync(string? resultPath, ArknightsCodecTransformPayload result) =>
        WriteJsonAsync(
            resultPath,
            result,
            ArknightsCodecJsonContext.Default.ArknightsCodecTransformPayload);

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

internal sealed record ArknightsCodecProbePayload(
    int SchemaVersion,
    bool IsMatch,
    int Confidence,
    string? FormatId,
    IReadOnlyList<string> Evidence,
    string? Message);

internal sealed record ArknightsCodecTransformPayload(
    int SchemaVersion,
    bool IsSuccess,
    string? OutputPath,
    string? Message)
{
    public static ArknightsCodecTransformPayload Succeeded(string outputPath) =>
        new(ArknightsBundleCodecProgram.ResultSchemaVersion, true, outputPath, null);

    public static ArknightsCodecTransformPayload Failed(string? message) =>
        new(ArknightsBundleCodecProgram.ResultSchemaVersion, false, null, message);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(ArknightsCodecProbePayload))]
[JsonSerializable(typeof(ArknightsCodecTransformPayload))]
internal sealed partial class ArknightsCodecJsonContext : JsonSerializerContext;
