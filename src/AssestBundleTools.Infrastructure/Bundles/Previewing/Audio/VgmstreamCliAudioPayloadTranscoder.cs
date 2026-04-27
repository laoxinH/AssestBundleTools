using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;

/// <summary>
/// 通过外部 <c>vgmstream-cli</c> 将游戏音频容器转为标准 WAV。
/// </summary>
internal sealed class VgmstreamCliAudioPayloadTranscoder : IAudioPayloadTranscoderBackend
{
    private const string TranscoderId = "audio.vgmstream-cli";
    private const string TranscoderDisplayName = "vgmstream-cli";
    private const int TimeoutMilliseconds = 20000;

    private readonly IReadOnlyList<string> _executableCandidates;

    public VgmstreamCliAudioPayloadTranscoder(IEnumerable<string>? executableCandidates = null)
    {
        _executableCandidates = (executableCandidates ?? AudioTranscoderDiscoveryPaths.GetVgmstreamExecutableCandidates())
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => candidate.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Id => TranscoderId;

    public bool CanTranscode(AudioPayloadTranscodeRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.ContainerKind == AudioContainerKind.Fsb5;
    }

    public AudioPayloadTranscoderDescriptorDto? TryResolveDescriptor()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        return new AudioPayloadTranscoderDescriptorDto(
            Id: TranscoderId,
            DisplayName: TranscoderDisplayName,
            Version: ResolveVersion(executablePath),
            Description: null);
    }

    public async Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
        AudioPayloadTranscodeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Payload is not { Length: > 0 })
        {
            return new AudioPayloadTranscodeResultDto(
                Status: AudioPayloadTranscodeStatusKind.Failed,
                Issue: AudioPayloadTranscodeIssueKind.EmptyPayload,
                Transcoder: null,
                ContentType: null,
                Content: null,
                Message: null);
        }

        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new AudioPayloadTranscodeResultDto(
                Status: AudioPayloadTranscodeStatusKind.Unavailable,
                Issue: AudioPayloadTranscodeIssueKind.BackendUnavailable,
                Transcoder: null,
                ContentType: null,
                Content: null,
                Message: null);
        }

        var descriptor = new AudioPayloadTranscoderDescriptorDto(
            Id: TranscoderId,
            DisplayName: TranscoderDisplayName,
            Version: ResolveVersion(executablePath),
            Description: null);

        var workingDirectoryPath = ResolveWorkingDirectory(request.WorkspaceKey, request.WorkspaceDataRootPath);
        Directory.CreateDirectory(workingDirectoryPath);

        var inputFilePath = Path.Combine(
            workingDirectoryPath,
            $"{Guid.NewGuid():N}_{SanitizeFileComponent(request.SourceFileName)}");
        var outputFilePath = Path.Combine(workingDirectoryPath, $"{Path.GetFileNameWithoutExtension(inputFilePath)}.wav");

        try
        {
            await File.WriteAllBytesAsync(inputFilePath, request.Payload, cancellationToken);

            var executionResult = await ExecuteProcessAsync(
                executablePath,
                inputFilePath,
                outputFilePath,
                cancellationToken);
            if (!executionResult.IsSuccess)
            {
                return new AudioPayloadTranscodeResultDto(
                    Status: AudioPayloadTranscodeStatusKind.Failed,
                    Issue: AudioPayloadTranscodeIssueKind.ExecutionFailed,
                    Transcoder: descriptor,
                    ContentType: null,
                    Content: null,
                    Message: executionResult.Detail);
            }

            if (!File.Exists(outputFilePath))
            {
                return new AudioPayloadTranscodeResultDto(
                    Status: AudioPayloadTranscodeStatusKind.Failed,
                    Issue: AudioPayloadTranscodeIssueKind.OutputMissing,
                    Transcoder: descriptor,
                    ContentType: null,
                    Content: null,
                    Message: null);
            }

            var outputContent = await File.ReadAllBytesAsync(outputFilePath, cancellationToken);
            if (outputContent.Length == 0)
            {
                return new AudioPayloadTranscodeResultDto(
                    Status: AudioPayloadTranscodeStatusKind.Failed,
                    Issue: AudioPayloadTranscodeIssueKind.OutputEmpty,
                    Transcoder: descriptor,
                    ContentType: null,
                    Content: null,
                    Message: null);
            }

            return new AudioPayloadTranscodeResultDto(
                Status: AudioPayloadTranscodeStatusKind.Succeeded,
                Issue: AudioPayloadTranscodeIssueKind.None,
                Transcoder: descriptor,
                ContentType: "audio/wav",
                Content: outputContent,
                Message: executionResult.Detail);
        }
        finally
        {
            DeleteFileIfExists(outputFilePath);
            DeleteFileIfExists(inputFilePath);
        }
    }

    private async Task<(bool IsSuccess, string? Detail)> ExecuteProcessAsync(
        string executablePath,
        string inputFilePath,
        string outputFilePath,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        CancellationTokenSource? timeoutCancellationTokenSource = null;
        CancellationTokenSource? linkedCancellationTokenSource = null;

        try
        {
            process = new Process();
            process.StartInfo.FileName = executablePath;
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(outputFilePath);
            process.StartInfo.ArgumentList.Add(inputFilePath);

            process.Start();

            timeoutCancellationTokenSource = new CancellationTokenSource(TimeoutMilliseconds);
            linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

            var effectiveCancellationToken = linkedCancellationTokenSource.Token;
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(effectiveCancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(effectiveCancellationToken);
            await process.WaitForExitAsync(effectiveCancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            var detail = NormalizeProcessDetail(standardError, standardOutput);

            return process.ExitCode == 0
                ? (true, detail)
                : (false, string.IsNullOrWhiteSpace(detail)
                    ? $"vgmstream-cli exited with code {process.ExitCode}."
                    : detail);
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource is not null &&
                  timeoutCancellationTokenSource.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            TryTerminateProcess(process);
            return (false, $"vgmstream-cli timed out after {TimeoutMilliseconds} ms.");
        }
        catch (OperationCanceledException)
        {
            TryTerminateProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            TryTerminateProcess(process);
            return (false, ex.Message);
        }
        finally
        {
            linkedCancellationTokenSource?.Dispose();
            timeoutCancellationTokenSource?.Dispose();
            process?.Dispose();
        }
    }

    private string? ResolveExecutablePath()
    {
        foreach (var candidate in _executableCandidates)
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                continue;
            }

            var resolvedPath = ResolveCommandFromPath(candidate);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return null;
    }

    private static string ResolveWorkingDirectory(string workspaceKey, string workspaceDataRootPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceKey) &&
            !string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return Path.Combine(
                Path.GetFullPath(workspaceDataRootPath),
                "staging",
                "audio-transcode",
                workspaceKey,
                SanitizeFileComponent(TranscoderId));
        }

        return Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools",
            "audio-transcode",
            SanitizeFileComponent(TranscoderId));
    }

    private static string? ResolveCommandFromPath(string commandName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directoryPath in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                continue;
            }

            try
            {
                var candidatePath = Path.Combine(directoryPath, commandName);
                if (File.Exists(candidatePath))
                {
                    return Path.GetFullPath(candidatePath);
                }
            }
            catch (Exception)
            {
                // PATH 中的异常目录不应打断整个发现流程。
            }
        }

        return null;
    }

    private static string ResolveVersion(string executablePath)
    {
        var fileInfo = new FileInfo(executablePath);
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(executablePath);
        var baseVersion = !string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion)
            ? fileVersionInfo.ProductVersion!.Trim()
            : !string.IsNullOrWhiteSpace(fileVersionInfo.FileVersion)
                ? fileVersionInfo.FileVersion!.Trim()
                : "unknown";

        // 把文件长度和时间戳一起纳入版本指纹，避免外部工具未更新显式版本号时缓存误命中。
        return $"{baseVersion}+{fileInfo.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{fileInfo.LastWriteTimeUtc.ToFileTimeUtc().ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private static string? NormalizeProcessDetail(string standardError, string standardOutput)
    {
        if (!string.IsNullOrWhiteSpace(standardError))
        {
            return standardError.Trim();
        }

        return string.IsNullOrWhiteSpace(standardOutput)
            ? null
            : standardOutput.Trim();
    }

    private static string SanitizeFileComponent(string value)
    {
        var fallbackValue = string.IsNullOrWhiteSpace(value) ? "audio_payload" : value.Trim();
        var sanitizedChars = fallbackValue
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }

    private static void DeleteFileIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception)
        {
            // 临时文件清理失败不应覆盖主流程结果。
        }
    }

    private static void TryTerminateProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 终止失败时保留原始异常或超时信息。
        }
    }
}
