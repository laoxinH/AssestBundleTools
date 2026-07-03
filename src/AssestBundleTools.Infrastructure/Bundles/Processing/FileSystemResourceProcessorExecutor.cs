using System.Diagnostics;
using System.Text.Json;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Processing;

public sealed class FileSystemResourceProcessorExecutor : IWorkspaceResourceProcessorExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<WorkspaceResourceProcessorExecutionResultDto>> ExecuteAsync(
        WorkspaceResourceProcessorWorkItemDto workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();

        var processor = workItem.Processor;
        if (string.IsNullOrWhiteSpace(processor.ExecutablePath))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorUnavailable(processor.Id));
        }

        var stagingDirectory = Path.GetFullPath(workItem.StagingDirectoryPath);
        var inputPath = Path.GetFullPath(workItem.InputPayloadPath);
        var outputPath = Path.Combine(stagingDirectory, "output");
        var resultPath = Path.Combine(stagingDirectory, "result.json");
        Directory.CreateDirectory(stagingDirectory);

        if (!File.Exists(inputPath))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorExecutionFailed(processor.Id, "InputMissing"));
        }

        var executionResult = await ExecuteProcessAsync(
            workItem,
            inputPath,
            outputPath,
            resultPath,
            cancellationToken);
        if (executionResult.IsCancelled)
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Cancelled();
        }

        if (!executionResult.IsSuccess)
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorExecutionFailed(processor.Id, executionResult.Detail));
        }

        if (!File.Exists(resultPath))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorResultInvalid(processor.Id, "ResultMissing"));
        }

        ResourceProcessorResultPayload? payload;
        try
        {
            await using var stream = File.OpenRead(resultPath);
            payload = await JsonSerializer.DeserializeAsync<ResourceProcessorResultPayload>(
                stream,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorResultInvalid(processor.Id, "JsonInvalid"));
        }

        if (payload is null || !TryParseStatus(payload.Status, out var status))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorResultInvalid(processor.Id, "StatusInvalid"));
        }

        var resolvedOutputPath = ResolveOutputPath(payload.OutputPath, outputPath, stagingDirectory);
        if (status == WorkspaceResourceProcessorStatusDto.Succeeded &&
            (string.IsNullOrWhiteSpace(resolvedOutputPath) || !File.Exists(resolvedOutputPath)))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorResultInvalid(processor.Id, "OutputMissing"));
        }

        if (!string.IsNullOrWhiteSpace(resolvedOutputPath) &&
            !IsPathUnderDirectory(resolvedOutputPath, stagingDirectory))
        {
            return Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                ApplicationErrors.ResourceProcessorResultInvalid(processor.Id, "OutputPathOutOfScope"));
        }

        var diagnostics = BuildDiagnostics(payload, executionResult).ToArray();
        return Result<WorkspaceResourceProcessorExecutionResultDto>.Success(
            new WorkspaceResourceProcessorExecutionResultDto(
                BundleId: workItem.BundleId,
                AssetId: workItem.AssetId,
                AssetName: workItem.AssetName,
                ResourceKind: workItem.ResourceKind,
                ProcessorId: processor.Id,
                Status: status,
                InputPath: inputPath,
                OutputPath: resolvedOutputPath,
                ResultPath: resultPath,
                Diagnostics: diagnostics,
                ContentType: string.IsNullOrWhiteSpace(payload.ContentType) ? null : payload.ContentType.Trim()));
    }

    private async Task<ResourceProcessorProcessExecutionResult> ExecuteProcessAsync(
        WorkspaceResourceProcessorWorkItemDto workItem,
        string inputPath,
        string outputPath,
        string resultPath,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        CancellationTokenSource? timeoutCancellationTokenSource = null;
        CancellationTokenSource? linkedCancellationTokenSource = null;

        try
        {
            var processor = workItem.Processor;
            process = new Process();
            process.StartInfo.FileName = ResolveExecutablePath(processor.ExecutablePath!, processor.ManifestFilePath);
            process.StartInfo.WorkingDirectory = ResolveWorkingDirectory(processor, workItem.StagingDirectoryPath);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            foreach (var argument in ExpandArguments(workItem, inputPath, outputPath, resultPath))
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            if (processor.TimeoutMs > 0)
            {
                timeoutCancellationTokenSource = new CancellationTokenSource(processor.TimeoutMs);
                linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCancellationTokenSource.Token);
            }

            var effectiveCancellationToken = linkedCancellationTokenSource?.Token ?? cancellationToken;
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(effectiveCancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(effectiveCancellationToken);
            await process.WaitForExitAsync(effectiveCancellationToken);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            if (process.ExitCode != 0)
            {
                return ResourceProcessorProcessExecutionResult.Failed(
                    ResolveProcessDetail(standardOutput, standardError, $"ExitCode={process.ExitCode}"));
            }

            return ResourceProcessorProcessExecutionResult.Succeeded(
                NormalizeProcessText(standardOutput),
                NormalizeProcessText(standardError));
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource is not null &&
                  timeoutCancellationTokenSource.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            TryTerminateProcess(process);
            return ResourceProcessorProcessExecutionResult.Failed("TimedOut");
        }
        catch (OperationCanceledException)
        {
            TryTerminateProcess(process);
            return ResourceProcessorProcessExecutionResult.Cancelled();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            TryTerminateProcess(process);
            return ResourceProcessorProcessExecutionResult.Failed(exception.GetType().Name);
        }
        finally
        {
            linkedCancellationTokenSource?.Dispose();
            timeoutCancellationTokenSource?.Dispose();
            process?.Dispose();
        }
    }

    private static IEnumerable<string> ExpandArguments(
        WorkspaceResourceProcessorWorkItemDto workItem,
        string inputPath,
        string outputPath,
        string resultPath)
    {
        var processor = workItem.Processor;
        var manifestDirectory = string.IsNullOrWhiteSpace(processor.ManifestFilePath)
            ? string.Empty
            : Path.GetDirectoryName(Path.GetFullPath(processor.ManifestFilePath)) ?? string.Empty;

        foreach (var argument in processor.ProcessArguments)
        {
            yield return argument
                .Replace("{inputPath}", inputPath, StringComparison.Ordinal)
                .Replace("{outputPath}", outputPath, StringComparison.Ordinal)
                .Replace("{resultPath}", resultPath, StringComparison.Ordinal)
                .Replace("{resourceKind}", workItem.ResourceKind, StringComparison.Ordinal)
                .Replace("{assetName}", workItem.AssetName, StringComparison.Ordinal)
                .Replace("{bundleName}", workItem.BundleDisplayName, StringComparison.Ordinal)
                .Replace("{workspaceKey}", workItem.WorkspaceKey, StringComparison.Ordinal)
                .Replace("{workspaceDataRootPath}", Path.GetFullPath(workItem.WorkspaceDataRootPath), StringComparison.Ordinal)
                .Replace("{manifestDirectory}", manifestDirectory, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<WorkspaceResourceProcessorDiagnosticDto> BuildDiagnostics(
        ResourceProcessorResultPayload payload,
        ResourceProcessorProcessExecutionResult executionResult)
    {
        if (!string.IsNullOrWhiteSpace(payload.Message))
        {
            yield return new WorkspaceResourceProcessorDiagnosticDto(
                "Processor.Message",
                "info",
                payload.Message.Trim());
        }

        if (payload.Diagnostics is not null)
        {
            foreach (var diagnostic in payload.Diagnostics)
            {
                if (string.IsNullOrWhiteSpace(diagnostic.Code))
                {
                    continue;
                }

                yield return new WorkspaceResourceProcessorDiagnosticDto(
                    diagnostic.Code.Trim(),
                    string.IsNullOrWhiteSpace(diagnostic.Severity) ? "info" : diagnostic.Severity.Trim(),
                    string.IsNullOrWhiteSpace(diagnostic.Message) ? null : diagnostic.Message.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(executionResult.StandardError))
        {
            yield return new WorkspaceResourceProcessorDiagnosticDto(
                "Processor.Stderr",
                "warning",
                executionResult.StandardError);
        }
    }

    private static bool TryParseStatus(
        string? status,
        out WorkspaceResourceProcessorStatusDto parsedStatus)
    {
        parsedStatus = WorkspaceResourceProcessorStatusDto.Failed;
        return status?.Trim().ToLowerInvariant() switch
        {
            "succeeded" or "success" => SetStatus(WorkspaceResourceProcessorStatusDto.Succeeded, out parsedStatus),
            "skipped" or "skip" => SetStatus(WorkspaceResourceProcessorStatusDto.Skipped, out parsedStatus),
            "failed" or "failure" => SetStatus(WorkspaceResourceProcessorStatusDto.Failed, out parsedStatus),
            _ => false
        };
    }

    private static bool SetStatus(
        WorkspaceResourceProcessorStatusDto value,
        out WorkspaceResourceProcessorStatusDto parsedStatus)
    {
        parsedStatus = value;
        return true;
    }

    private static string? ResolveOutputPath(
        string? payloadOutputPath,
        string defaultOutputPath,
        string stagingDirectory)
    {
        var candidate = string.IsNullOrWhiteSpace(payloadOutputPath)
            ? defaultOutputPath
            : payloadOutputPath.Trim();
        return Path.GetFullPath(
            Path.IsPathRooted(candidate)
                ? candidate
                : Path.Combine(stagingDirectory, candidate));
    }

    private static bool IsPathUnderDirectory(string path, string directory)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   normalizedDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExecutablePath(string executablePath, string? manifestFilePath)
    {
        var trimmedPath = executablePath.Trim();
        if (Path.IsPathRooted(trimmedPath))
        {
            return Path.GetFullPath(trimmedPath);
        }

        var manifestDirectory = string.IsNullOrWhiteSpace(manifestFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(manifestFilePath)) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(manifestDirectory, trimmedPath));
    }

    private static string ResolveWorkingDirectory(
        Application.Bundles.Dtos.ResourceProcessorCatalogEntryDto processor,
        string stagingDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(processor.WorkingDirectoryPath))
        {
            return Path.GetFullPath(stagingDirectoryPath);
        }

        if (Path.IsPathRooted(processor.WorkingDirectoryPath))
        {
            return Path.GetFullPath(processor.WorkingDirectoryPath);
        }

        var manifestDirectory = string.IsNullOrWhiteSpace(processor.ManifestFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(processor.ManifestFilePath)) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(manifestDirectory, processor.WorkingDirectoryPath));
    }

    private static string? ResolveProcessDetail(
        string standardOutput,
        string standardError,
        string fallback)
    {
        return NormalizeProcessText(standardError) ??
               NormalizeProcessText(standardOutput) ??
               fallback;
    }

    private static string? NormalizeProcessText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void TryTerminateProcess(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private sealed record ResourceProcessorResultPayload(
        int SchemaVersion,
        string? Status,
        string? Message,
        string? OutputPath,
        string? ContentType,
        IReadOnlyList<ResourceProcessorDiagnosticPayload>? Diagnostics);

    private sealed record ResourceProcessorDiagnosticPayload(
        string? Code,
        string? Severity,
        string? Message);

    private sealed record ResourceProcessorProcessExecutionResult(
        bool IsSuccess,
        bool IsCancelled,
        string? Detail,
        string? StandardOutput,
        string? StandardError)
    {
        public static ResourceProcessorProcessExecutionResult Succeeded(
            string? standardOutput,
            string? standardError) =>
            new(true, false, null, standardOutput, standardError);

        public static ResourceProcessorProcessExecutionResult Failed(string? detail) =>
            new(false, false, detail, null, null);

        public static ResourceProcessorProcessExecutionResult Cancelled() =>
            new(false, true, null, null, null);
    }
}
