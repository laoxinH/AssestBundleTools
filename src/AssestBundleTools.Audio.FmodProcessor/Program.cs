using System.Diagnostics;
using System.Text.Json;

return await FmodAudioProcessorProgram.RunAsync(args);

internal static class FmodAudioProcessorProgram
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    private const int ResultSchemaVersion = 1;

    public static async Task<int> RunAsync(string[] args)
    {
        var options = ProcessorOptions.Parse(args);
        if (!options.IsValid)
        {
            await WriteResultAsync(
                options.ResultPath,
                ResultPayload.Failed("Audio.FmodProcessor.ArgumentsInvalid", options.ValidationMessage));
            return FailureExitCode;
        }

        if (!File.Exists(options.InputPath))
        {
            await WriteResultAsync(
                options.ResultPath,
                ResultPayload.Failed("Audio.FmodProcessor.InputMissing", "Input payload does not exist."));
            return FailureExitCode;
        }

        if (!File.Exists(options.FsbankPath))
        {
            await WriteResultAsync(
                options.ResultPath,
                ResultPayload.Failed("Audio.FmodProcessor.FsbankMissing", "FMOD fsbankcl executable was not found."));
            return FailureExitCode;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath)) ?? Environment.CurrentDirectory);
        var workingDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools",
            "fmod-audio",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectoryPath);

        try
        {
            var sourcePath = Path.Combine(workingDirectoryPath, SanitizeFileName(Path.GetFileName(options.InputPath)));
            File.Copy(options.InputPath, sourcePath, overwrite: true);
            var processResult = await RunFsbankAsync(options.FsbankPath, workingDirectoryPath, sourcePath);
            if (!processResult.IsSuccess)
            {
                await WriteResultAsync(
                    options.ResultPath,
                    ResultPayload.Failed(
                        "Audio.FmodProcessor.FsbankFailed",
                        processResult.Diagnostic,
                        BuildProcessDiagnostics(processResult)));
                return FailureExitCode;
            }

            var fsbPath = Directory
                .EnumerateFiles(workingDirectoryPath, "*.fsb", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(fsbPath) || !File.Exists(fsbPath))
            {
                await WriteResultAsync(
                    options.ResultPath,
                    ResultPayload.Failed(
                        "Audio.FmodProcessor.OutputMissing",
                        "FMOD fsbankcl completed without producing an FSB payload.",
                        BuildProcessDiagnostics(processResult)));
                return FailureExitCode;
            }

            File.Copy(fsbPath, options.OutputPath, overwrite: true);
            await WriteResultAsync(
                options.ResultPath,
                ResultPayload.Succeeded(options.OutputPath, BuildProcessDiagnostics(processResult)));
            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            await WriteResultAsync(
                options.ResultPath,
                ResultPayload.Failed("Audio.FmodProcessor.ExecutionFailed", exception.GetType().Name));
            return FailureExitCode;
        }
        finally
        {
            TryDeleteDirectory(workingDirectoryPath);
        }
    }

    private static async Task<ProcessExecutionResult> RunFsbankAsync(
        string fsbankPath,
        string workingDirectoryPath,
        string sourcePath)
    {
        using var process = new Process();
        process.StartInfo.FileName = Path.GetFullPath(fsbankPath);
        process.StartInfo.WorkingDirectory = workingDirectoryPath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        AddFsbankArguments(process.StartInfo.ArgumentList, sourcePath);

        process.Start();
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var standardOutput = NormalizeProcessText(await standardOutputTask);
        var standardError = NormalizeProcessText(await standardErrorTask);
        return new ProcessExecutionResult(
            process.ExitCode == 0,
            process.ExitCode,
            standardOutput,
            standardError);
    }

    private static void AddFsbankArguments(ICollection<string> arguments, string sourcePath)
    {
        arguments.Add("-format");
        arguments.Add("vorbis");
        arguments.Add("-quality");
        arguments.Add("50");
        arguments.Add("-o");
        arguments.Add("output.fsb");
        arguments.Add(sourcePath);
    }

    private static IReadOnlyList<DiagnosticPayload> BuildProcessDiagnostics(ProcessExecutionResult processResult)
    {
        var diagnostics = new List<DiagnosticPayload>
        {
            new("Audio.FmodProcessor.ProcessExited", "info", $"ExitCode={processResult.ExitCode}")
        };

        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            diagnostics.Add(new DiagnosticPayload("Audio.FmodProcessor.Stderr", "warning", processResult.StandardError));
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            diagnostics.Add(new DiagnosticPayload("Audio.FmodProcessor.Stdout", "info", processResult.StandardOutput));
        }

        return diagnostics;
    }

    private static async Task WriteResultAsync(string? resultPath, ResultPayload payload)
    {
        if (string.IsNullOrWhiteSpace(resultPath))
        {
            return;
        }

        var normalizedResultPath = Path.GetFullPath(resultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedResultPath) ?? Environment.CurrentDirectory);
        await using var stream = new FileStream(
            normalizedResultPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        await JsonSerializer.SerializeAsync(
            stream,
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });
    }

    private static string SanitizeFileName(string? fileName)
    {
        var source = string.IsNullOrWhiteSpace(fileName) ? "input.wav" : fileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(source.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }

    private static string? NormalizeProcessText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ProcessorOptions(
        string InputPath,
        string OutputPath,
        string ResultPath,
        string FsbankPath,
        string? ValidationMessage)
    {
        public bool IsValid => string.IsNullOrWhiteSpace(ValidationMessage);

        public static ProcessorOptions Parse(IReadOnlyList<string> args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Count; index++)
            {
                var key = args[index];
                if (!key.StartsWith("--", StringComparison.Ordinal) || index >= args.Count - 1)
                {
                    continue;
                }

                values[key[2..]] = args[++index];
            }

            var inputPath = GetValue(values, "input");
            var outputPath = GetValue(values, "output");
            var resultPath = GetValue(values, "result");
            var fsbankPath = GetValue(values, "fsbank");
            var validationMessage = string.IsNullOrWhiteSpace(inputPath)
                ? "Missing --input."
                : string.IsNullOrWhiteSpace(outputPath)
                    ? "Missing --output."
                    : string.IsNullOrWhiteSpace(resultPath)
                        ? "Missing --result."
                        : string.IsNullOrWhiteSpace(fsbankPath)
                            ? "Missing --fsbank."
                            : null;

            return new ProcessorOptions(
                inputPath ?? string.Empty,
                outputPath ?? string.Empty,
                resultPath ?? string.Empty,
                fsbankPath ?? string.Empty,
                validationMessage);
        }

        private static string? GetValue(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
    }

    private sealed record ResultPayload(
        int SchemaVersion,
        string Status,
        string? Message,
        string? OutputPath,
        string? ContentType,
        IReadOnlyList<DiagnosticPayload> Diagnostics)
    {
        public static ResultPayload Succeeded(
            string outputPath,
            IReadOnlyList<DiagnosticPayload> diagnostics) =>
            new(
                ResultSchemaVersion,
                "succeeded",
                "Audio payload encoded to FSB5.",
                outputPath,
                "audio/x-fsb",
                diagnostics);

        public static ResultPayload Failed(
            string code,
            string? message,
            IReadOnlyList<DiagnosticPayload>? diagnostics = null) =>
            new(
                ResultSchemaVersion,
                "failed",
                message,
                null,
                null,
                new[]
                {
                    new DiagnosticPayload(code, "error", message)
                }.Concat(diagnostics ?? Array.Empty<DiagnosticPayload>()).ToArray());
    }

    private sealed record DiagnosticPayload(
        string Code,
        string Severity,
        string? Message);

    private sealed record ProcessExecutionResult(
        bool IsSuccess,
        int ExitCode,
        string? StandardOutput,
        string? StandardError)
    {
        public string? Diagnostic =>
            StandardError ?? StandardOutput ?? $"ExitCode={ExitCode}";
    }
}
