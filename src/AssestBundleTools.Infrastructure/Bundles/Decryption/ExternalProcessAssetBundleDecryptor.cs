using System.Diagnostics;
using System.Text.Json;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Common.Errors;

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
                DiagnosticMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.DecryptorProbeAutoDetectionDisabled));
        }

        var workingDirectoryPath = ResolveWorkingDirectory(request.SourcePath, workspaceKey: null, workspaceDataRootPath: null);
        Directory.CreateDirectory(workingDirectoryPath);

        var resultFilePath = Path.Combine(
            workingDirectoryPath,
            $"{ExternalProcessAssetBundleDecryptorSupport.SanitizeFileComponent(_manifest.Id)}.probe.{Guid.NewGuid():N}.json");

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
                    DiagnosticMessage: CreateProbeExecutionMessage(executionResult));
            }

            if (!File.Exists(resultFilePath))
            {
                return new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    DiagnosticMessage: new LocalizedMessageDto(
                        WorkspaceImportMessageTokens.DecryptorProbeResultFileMissing));
            }

            var payload = ExternalProcessAssetBundleDecryptorSupport.ReadJsonFile<AssetBundleExternalProcessProbePayload>(resultFilePath);
            if (payload is null)
            {
                return new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    DiagnosticMessage: new LocalizedMessageDto(
                        WorkspaceImportMessageTokens.DecryptorProbeResultFileInvalid));
            }

            return new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: payload.IsMatch,
                Confidence: Math.Clamp(payload.Confidence, 0, 100),
                DiagnosticMessage: string.IsNullOrWhiteSpace(payload.Message)
                    ? null
                    : new LocalizedMessageDto(FallbackText: payload.Message.Trim()));
        }
        finally
        {
            ExternalProcessAssetBundleDecryptorSupport.DeleteFileIfExists(resultFilePath);
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
                ApplicationErrors.BundleDecryptionArgumentsRequired(Descriptor.Id));
        }

        var workingDirectoryPath = ResolveDecryptionWorkingDirectory(request);
        Directory.CreateDirectory(workingDirectoryPath);

        var resultFilePath = Path.Combine(
            workingDirectoryPath,
            $"{ExternalProcessAssetBundleDecryptorSupport.SanitizeFileComponent(_manifest.Id)}.decrypt.{Guid.NewGuid():N}.json");
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
                var executionError = CreateDecryptionExecutionError(executionResult);
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    executionError is null
                        ? ApplicationErrors.BundleDecryptionFailed(Descriptor.Id, executionResult.Detail)
                        : executionError);
            }

            if (!File.Exists(resultFilePath))
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionResultFileMissing(Descriptor.Id));
            }

            var payload = ExternalProcessAssetBundleDecryptorSupport.ReadJsonFile<AssetBundleExternalProcessDecryptPayload>(resultFilePath);
            if (payload is null)
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionResultFileInvalid(Descriptor.Id));
            }

            if (!payload.IsSuccess)
            {
                var payloadMessage = string.IsNullOrWhiteSpace(payload.Message)
                    ? null
                    : payload.Message.Trim();
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionFailed(Descriptor.Id, payloadMessage));
            }

            var resolvedOutputPath = string.IsNullOrWhiteSpace(payload.OutputPath)
                ? outputFilePath
                : ExternalProcessAssetBundleDecryptorSupport.ResolvePath(payload.OutputPath, workingDirectoryPath);
            if (!File.Exists(resolvedOutputPath))
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionOutputMissing(Descriptor.Id));
            }

            var result = new AssetBundleDecryptionResultDto(
                Decryptor: Descriptor,
                ResolvedBundlePath: resolvedOutputPath,
                ProducedDerivedFile: !ExternalProcessAssetBundleDecryptorSupport.PathsEqual(request.SourcePath, resolvedOutputPath),
                BundleFingerprint: AssetBundleFileFingerprintFactory.Create(resolvedOutputPath),
                DiagnosticMessage: string.IsNullOrWhiteSpace(payload.Message)
                    ? null
                    : new LocalizedMessageDto(FallbackText: payload.Message.Trim()));

            return Result<AssetBundleDecryptionResultDto>.Success(result);
        }
        finally
        {
            ExternalProcessAssetBundleDecryptorSupport.DeleteFileIfExists(resultFilePath);
        }
    }

    private async Task<ExternalProcessExecutionResult> ExecuteProcessAsync(
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
            process.StartInfo.FileName = ExternalProcessAssetBundleDecryptorSupport.ResolveExecutablePath(_manifest.ExecutablePath, _manifestDirectoryPath);
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
                return new ExternalProcessExecutionResult(
                    IsSuccess: false,
                    Detail: string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
                    FailureKind: ExternalProcessExecutionFailureKind.ProcessExited,
                    ExitCode: process.ExitCode);
            }

            return new ExternalProcessExecutionResult(
                IsSuccess: true,
                Detail: null,
                FailureKind: ExternalProcessExecutionFailureKind.None,
                ExitCode: 0);
        }
        catch (OperationCanceledException)
            when (timeoutCancellationTokenSource is not null &&
                  timeoutCancellationTokenSource.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            return new ExternalProcessExecutionResult(
                IsSuccess: false,
                Detail: null,
                FailureKind: ExternalProcessExecutionFailureKind.TimedOut,
                ExitCode: 0);
        }
        catch (OperationCanceledException)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            throw;
        }
        catch (Exception)
        {
            ExternalProcessAssetBundleDecryptorSupport.TryTerminateProcess(process);
            return new ExternalProcessExecutionResult(
                IsSuccess: false,
                Detail: null,
                FailureKind: ExternalProcessExecutionFailureKind.Exception,
                ExitCode: 0);
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
        if (!string.IsNullOrWhiteSpace(requestWorkingDirectoryPath)) return Path.GetFullPath(requestWorkingDirectoryPath);
        if (!string.IsNullOrWhiteSpace(_manifest.WorkingDirectoryPath)) return ExternalProcessAssetBundleDecryptorSupport.ResolvePath(_manifest.WorkingDirectoryPath, _manifestDirectoryPath);

        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath) &&
            !string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Path.Combine(
                Path.GetFullPath(workspaceDataRootPath),
                "staging",
                "decrypt");
        }

        return Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools",
            "decrypt");
    }

    private string ResolveDecryptionWorkingDirectory(AssetBundleDecryptionRequestDto request)
    {
        var rootDirectoryPath = ResolveWorkingDirectory(
            request.SourcePath,
            request.WorkspaceKey,
            request.WorkspaceDataRootPath,
            request.WorkingDirectoryPath);
        var workspaceSegment = ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(request.WorkspaceKey);
        var decryptorSegment = ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(_manifest.Id);
        var workItemSegment = ResolveWorkItemDirectorySegment(request);
        return Path.Combine(rootDirectoryPath, workspaceSegment, decryptorSegment, workItemSegment);
    }

    private string ResolveWorkItemDirectorySegment(AssetBundleDecryptionRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.BundleCacheKey)) return ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(request.BundleCacheKey);
        return ExternalProcessAssetBundleDecryptorSupport.ComputeStableSourcePathKey(request.SourcePath);
    }

    private static string ResolveOutputFileName(string sourcePath, string? preferredOutputFileName)
    {
        if (!string.IsNullOrWhiteSpace(preferredOutputFileName)) return preferredOutputFileName.Trim();

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

    private AppError? CreateDecryptionExecutionError(ExternalProcessExecutionResult executionResult)
    {
        return executionResult.FailureKind switch
        {
            ExternalProcessExecutionFailureKind.ProcessExited
                when string.IsNullOrWhiteSpace(executionResult.Detail) =>
                ApplicationErrors.BundleDecryptionProcessExited(Descriptor.Id, executionResult.ExitCode),
            ExternalProcessExecutionFailureKind.TimedOut =>
                ApplicationErrors.BundleDecryptionTimedOut(Descriptor.Id, _manifest.TimeoutMs),
            ExternalProcessExecutionFailureKind.Exception =>
                ApplicationErrors.BundleDecryptionExecutionFailed(Descriptor.Id),
            _ => null
        };
    }

    private LocalizedMessageDto? CreateProbeExecutionMessage(ExternalProcessExecutionResult executionResult)
    {
        return executionResult.FailureKind switch
        {
            ExternalProcessExecutionFailureKind.ProcessExited
                when string.IsNullOrWhiteSpace(executionResult.Detail) =>
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.DecryptorProbeProcessExited,
                    [executionResult.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)]),
            ExternalProcessExecutionFailureKind.TimedOut =>
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.DecryptorProbeProcessTimedOut,
                    [_manifest.TimeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture)]),
            ExternalProcessExecutionFailureKind.Exception =>
                new LocalizedMessageDto(WorkspaceImportMessageTokens.DecryptorProbeExecutionFailed),
            _ => string.IsNullOrWhiteSpace(executionResult.Detail)
                ? null
                : new LocalizedMessageDto(FallbackText: executionResult.Detail)
        };
    }

}
