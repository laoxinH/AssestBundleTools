using System.Diagnostics;
using System.Text.Json;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 通过外部进程协议执行资源包解密。
/// </summary>
/// <remarks>
/// 当前实现不关心外部工具使用 Python、.NET 或其他语言，
/// 主程序只负责传递标准化的文件路径和读取结果文件。
/// </remarks>
internal sealed class ExternalProcessAssetBundleDecryptor : IAssetBundleDecryptor
{
    private readonly AssetBundleExternalProcessDecryptorManifest _manifest;
    private readonly string _manifestDirectoryPath;

    internal ExternalProcessAssetBundleDecryptor(
        AssetBundleExternalProcessDecryptorManifest manifest,
        string manifestDirectoryPath)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _manifestDirectoryPath = string.IsNullOrWhiteSpace(manifestDirectoryPath)
            ? throw new ArgumentException("Manifest directory path is required.", nameof(manifestDirectoryPath))
            : Path.GetFullPath(manifestDirectoryPath);

        Descriptor = new AssetBundleDecryptorDescriptorDto(
            Id: _manifest.Id,
            DisplayName: _manifest.DisplayName,
            Version: _manifest.Version,
            Kind: AssetBundleDecryptorKind.ExternalProcess,
            SupportsAutoDetection: _manifest.SupportsAutoDetection,
            IsFallback: _manifest.IsFallback,
            Description: _manifest.Description);
    }

    public AssetBundleDecryptorDescriptorDto Descriptor { get; }

    public async Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
        AssetBundleDecryptorProbeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!_manifest.SupportsAutoDetection || _manifest.ProbeArguments.Count == 0)
        {
            return new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: false,
                Confidence: 0,
                Message: "Auto-detection is disabled for this decryptor.");
        }

        var workingDirectoryPath = ResolveWorkingDirectory(request.SourcePath, workspaceKey: null, workspaceDataRootPath: null);
        Directory.CreateDirectory(workingDirectoryPath);

        var resultFilePath = Path.Combine(
            workingDirectoryPath,
            $"{SanitizeFileComponent(_manifest.Id)}.probe.{Guid.NewGuid():N}.json");

        var executionResult = await ExecuteProcessAsync(
            arguments: _manifest.ProbeArguments,
            sourcePath: request.SourcePath,
            outputPath: string.Empty,
            resultPath: resultFilePath,
            workspaceKey: string.Empty,
            workspaceDataRootPath: string.Empty,
            workingDirectoryPath: workingDirectoryPath,
            cancellationToken: cancellationToken);

        try
        {
            if (!executionResult.IsSuccess)
            {
                return new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    Message: executionResult.Detail);
            }

            if (!File.Exists(resultFilePath))
            {
                return new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    Message: "Probe result file was not produced.");
            }

            var payload = ReadJsonFile<AssetBundleExternalProcessProbePayload>(resultFilePath);
            if (payload is null)
            {
                return new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    Message: "Probe result file could not be parsed.");
            }

            return new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: payload.IsMatch,
                Confidence: Math.Clamp(payload.Confidence, 0, 100),
                Message: payload.Message);
        }
        finally
        {
            DeleteFileIfExists(resultFilePath);
        }
    }

    public async Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
        AssetBundleDecryptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceDataRootPath);

        if (_manifest.DecryptArguments.Count == 0)
        {
            return Result<AssetBundleDecryptionResultDto>.Failure(
                ApplicationErrors.BundleDecryptionInvalidResult(
                    Descriptor.Id,
                    "Decrypt arguments are not configured."));
        }

        var workingDirectoryPath = ResolveWorkingDirectory(
            request.SourcePath,
            request.WorkspaceKey,
            request.WorkspaceDataRootPath,
            request.WorkingDirectoryPath);
        Directory.CreateDirectory(workingDirectoryPath);

        var resultFilePath = Path.Combine(
            workingDirectoryPath,
            $"{SanitizeFileComponent(_manifest.Id)}.decrypt.{Guid.NewGuid():N}.json");
        var outputFilePath = Path.Combine(
            workingDirectoryPath,
            ResolveOutputFileName(request.SourcePath, request.PreferredOutputFileName));

        var executionResult = await ExecuteProcessAsync(
            arguments: _manifest.DecryptArguments,
            sourcePath: request.SourcePath,
            outputPath: outputFilePath,
            resultPath: resultFilePath,
            workspaceKey: request.WorkspaceKey,
            workspaceDataRootPath: request.WorkspaceDataRootPath,
            workingDirectoryPath: workingDirectoryPath,
            cancellationToken: cancellationToken);

        try
        {
            if (!executionResult.IsSuccess)
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionFailed(Descriptor.Id, executionResult.Detail));
            }

            if (!File.Exists(resultFilePath))
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionInvalidResult(
                        Descriptor.Id,
                        "Decrypt result file was not produced."));
            }

            var payload = ReadJsonFile<AssetBundleExternalProcessDecryptPayload>(resultFilePath);
            if (payload is null)
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionInvalidResult(
                        Descriptor.Id,
                        "Decrypt result file could not be parsed."));
            }

            if (!payload.IsSuccess)
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionFailed(Descriptor.Id, payload.Message));
            }

            var resolvedOutputPath = string.IsNullOrWhiteSpace(payload.OutputPath)
                ? outputFilePath
                : ResolvePath(payload.OutputPath, workingDirectoryPath);
            if (!File.Exists(resolvedOutputPath))
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionInvalidResult(
                        Descriptor.Id,
                        "Resolved decrypt output file does not exist."));
            }

            var result = new AssetBundleDecryptionResultDto(
                Decryptor: Descriptor,
                ResolvedBundlePath: resolvedOutputPath,
                ProducedDerivedFile: !PathsEqual(request.SourcePath, resolvedOutputPath),
                BundleFingerprint: AssetBundleFileFingerprintFactory.Create(resolvedOutputPath),
                Message: payload.Message);

            return Result<AssetBundleDecryptionResultDto>.Success(result);
        }
        finally
        {
            DeleteFileIfExists(resultFilePath);
        }
    }

    private async Task<(bool IsSuccess, string? Detail)> ExecuteProcessAsync(
        IReadOnlyList<string> arguments,
        string sourcePath,
        string outputPath,
        string resultPath,
        string workspaceKey,
        string workspaceDataRootPath,
        string workingDirectoryPath,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        CancellationTokenSource? timeoutCancellationTokenSource = null;
        CancellationTokenSource? linkedCancellationTokenSource = null;

        try
        {
            process = new Process();
            process.StartInfo.FileName = ResolveExecutablePath(_manifest.ExecutablePath, _manifestDirectoryPath);
            process.StartInfo.WorkingDirectory = workingDirectoryPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            foreach (var argument in ExpandArguments(
                         arguments,
                         sourcePath,
                         outputPath,
                         resultPath,
                         workspaceKey,
                         workspaceDataRootPath))
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();

            if (_manifest.TimeoutMs > 0)
            {
                timeoutCancellationTokenSource = new CancellationTokenSource(_manifest.TimeoutMs);
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
                var detail = string.IsNullOrWhiteSpace(standardError)
                    ? standardOutput
                    : standardError;
                return (false, string.IsNullOrWhiteSpace(detail)
                    ? $"External decryptor exited with code {process.ExitCode}."
                    : detail.Trim());
            }

            return (true, null);
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource is not null &&
                  timeoutCancellationTokenSource.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            TryTerminateProcess(process);
            return (false, $"External decryptor timed out after {_manifest.TimeoutMs} ms.");
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

    private IEnumerable<string> ExpandArguments(
        IReadOnlyList<string> arguments,
        string sourcePath,
        string outputPath,
        string resultPath,
        string workspaceKey,
        string workspaceDataRootPath)
    {
        foreach (var argument in arguments)
        {
            yield return argument
                .Replace("{inputPath}", Path.GetFullPath(sourcePath), StringComparison.Ordinal)
                .Replace("{outputPath}", outputPath, StringComparison.Ordinal)
                .Replace("{resultPath}", resultPath, StringComparison.Ordinal)
                .Replace("{workspaceKey}", workspaceKey, StringComparison.Ordinal)
                .Replace("{workspaceDataRootPath}", workspaceDataRootPath, StringComparison.Ordinal)
                .Replace("{manifestDirectory}", _manifestDirectoryPath, StringComparison.Ordinal);
        }
    }

    private string ResolveWorkingDirectory(
        string sourcePath,
        string? workspaceKey,
        string? workspaceDataRootPath,
        string? requestWorkingDirectoryPath = null)
    {
        if (!string.IsNullOrWhiteSpace(requestWorkingDirectoryPath))
        {
            return Path.GetFullPath(requestWorkingDirectoryPath);
        }

        if (!string.IsNullOrWhiteSpace(_manifest.WorkingDirectoryPath))
        {
            return ResolvePath(_manifest.WorkingDirectoryPath, _manifestDirectoryPath);
        }

        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath) &&
            !string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Path.Combine(
                Path.GetFullPath(workspaceDataRootPath),
                "staging",
                "decrypt",
                workspaceKey,
                SanitizeFileComponent(_manifest.Id));
        }

        return Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools",
            "decrypt",
            SanitizeFileComponent(_manifest.Id),
            Path.GetFileNameWithoutExtension(sourcePath));
    }

    private static string ResolveOutputFileName(string sourcePath, string? preferredOutputFileName)
    {
        if (!string.IsNullOrWhiteSpace(preferredOutputFileName))
        {
            return preferredOutputFileName.Trim();
        }

        var extension = Path.GetExtension(sourcePath);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "bundle";
        }

        return string.IsNullOrWhiteSpace(extension)
            ? $"{baseName}.decrypted"
            : $"{baseName}.decrypted{extension}";
    }

    private static TPayload? ReadJsonFile<TPayload>(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<TPayload>(stream, AssetBundleDecryptorJsonSerialization.SerializerOptions);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static string ResolvePath(string path, string baseDirectoryPath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectoryPath, path));
    }

    private static string ResolveExecutablePath(string executablePath, string baseDirectoryPath)
    {
        if (Path.IsPathRooted(executablePath))
        {
            return Path.GetFullPath(executablePath);
        }

        if (executablePath.Contains(Path.DirectorySeparatorChar) ||
            executablePath.Contains(Path.AltDirectorySeparatorChar))
        {
            return Path.GetFullPath(Path.Combine(baseDirectoryPath, executablePath));
        }

        return executablePath;
    }

    private static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
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
            // 结果文件清理失败不应中断主流程。
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
            // 外部进程终止失败不应覆盖原始错误。
        }
    }
}
