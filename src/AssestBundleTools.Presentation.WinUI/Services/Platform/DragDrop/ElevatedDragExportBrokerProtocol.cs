namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class ElevatedDragExportBrokerProtocol
{
    private const string BrokerMarker = "--abt-elevated-drag-export-broker";
    public const string ReadyFileName = "broker.ready";
    public const string CommandFileName = "command.ready";
    public const string ProcessingFileName = "command.processing";
    public const string PayloadFileName = "payload.bin";
    public const string FailureFileName = "payload.failed";
    public const string CompletionFileName = "drag.completed";

    public static string RootPath =>
        Path.Combine(Path.GetTempPath(), "AssestBundleTools", "elevated-drag-export-broker");

    public static IReadOnlyList<string> CreateArguments(string sessionPath, int parentProcessId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionPath);
        return [BrokerMarker, sessionPath, parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    public static bool TryParseCurrentProcess(out ElevatedDragExportBrokerLaunch launch)
    {
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var markerIndex = Array.FindIndex(
            arguments,
            argument => string.Equals(argument, BrokerMarker, StringComparison.Ordinal));
        if (markerIndex < 0 || markerIndex + 2 >= arguments.Length ||
            !int.TryParse(arguments[markerIndex + 2], out var parentProcessId) ||
            parentProcessId <= 0 ||
            !TryNormalizeSessionPath(arguments[markerIndex + 1], out var sessionPath))
        {
            launch = default;
            return false;
        }

        launch = new ElevatedDragExportBrokerLaunch(sessionPath, parentProcessId);
        return true;
    }

    public static void WriteCommand(
        string requestPath,
        ElevatedDragExportDescriptor descriptor)
    {
        var commandPath = Path.Combine(requestPath, CommandFileName);
        var temporaryPath = string.Concat(commandPath, ".tmp");
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(Math.Clamp(descriptor.ItemCount, 1, 1024));
            writer.Write(SanitizeDisplayName(descriptor.DisplayName));
        }

        File.Move(temporaryPath, commandPath, overwrite: true);
    }

    public static ElevatedDragExportBrokerCommand? ReadCommand(string commandPath)
    {
        using var stream = new FileStream(commandPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);
        var itemCount = reader.ReadInt32();
        var displayName = SanitizeDisplayName(reader.ReadString());
        return itemCount is > 0 and <= 1024 && displayName.Length > 0
            ? new ElevatedDragExportBrokerCommand(displayName, itemCount)
            : null;
    }

    public static Task WritePayloadAsync(
        string requestPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payloadPath = Path.Combine(requestPath, PayloadFileName);
        var temporaryPath = string.Concat(payloadPath, ".tmp");
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(paths.Count);
            foreach (var path in paths)
            {
                writer.Write(path);
            }
        }

        File.Move(temporaryPath, payloadPath, overwrite: true);
        return Task.CompletedTask;
    }

    public static ElevatedDragExportBrokerPayload? ReadPayload(string requestPath)
    {
        var payloadPath = Path.Combine(requestPath, PayloadFileName);
        using var stream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);
        var count = reader.ReadInt32();
        if (count is <= 0 or > 1024)
        {
            return null;
        }

        var paths = new string[count];
        for (var index = 0; index < count; index++)
        {
            paths[index] = reader.ReadString();
        }

        return new ElevatedDragExportBrokerPayload(paths);
    }

    public static void WriteCompletion(string requestPath, NativeOleDragResult result)
    {
        var completionPath = Path.Combine(requestPath, CompletionFileName);
        var temporaryPath = string.Concat(completionPath, ".tmp");
        File.WriteAllText(
            temporaryPath,
            string.Concat(
                result.HResult.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Environment.NewLine,
                result.Effect.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Environment.NewLine,
                SanitizeDiagnosticDetail(result.DiagnosticDetail)));
        File.Move(temporaryPath, completionPath, overwrite: true);
    }

    public static ElevatedDragExportBrokerCompletion? ReadCompletion(string requestPath)
    {
        var lines = File.ReadAllLines(Path.Combine(requestPath, CompletionFileName));
        return lines.Length >= 2 &&
               int.TryParse(lines[0], System.Globalization.CultureInfo.InvariantCulture, out var hResult) &&
               uint.TryParse(lines[1], System.Globalization.CultureInfo.InvariantCulture, out var effect)
            ? new ElevatedDragExportBrokerCompletion(
                hResult,
                effect,
                lines.Length >= 3 ? lines[2] : string.Empty)
            : null;
    }

    private static string SanitizeDiagnosticDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : detail.Replace('\r', ' ').Replace('\n', ' ');

    private static string SanitizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        var normalized = displayName.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    private static bool TryNormalizeSessionPath(string candidatePath, out string sessionPath)
    {
        sessionPath = string.Empty;
        try
        {
            var normalizedRoot = string.Concat(
                Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.DirectorySeparatorChar);
            var normalizedCandidate = Path.GetFullPath(candidatePath);
            if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            sessionPath = normalizedCandidate;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal readonly record struct ElevatedDragExportBrokerLaunch(
    string SessionPath,
    int ParentProcessId);

internal sealed record ElevatedDragExportBrokerCommand(
    string DisplayName,
    int ItemCount);

internal sealed record ElevatedDragExportBrokerPayload(string[] Paths);

internal sealed record ElevatedDragExportBrokerCompletion(
    int HResult,
    uint Effect,
    string DiagnosticDetail);
