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
internal sealed class ExternalProcessAssetBundleDecryptor : IAssetBundleDecryptor, IAssetBundleEncryptor
{
    private readonly AssetBundleExternalProcessDecryptorManifest _manifest;
    private readonly string _manifestDirectoryPath;
    private readonly ExternalAssetBundleCodecProcessRunner _processRunner;

    internal ExternalProcessAssetBundleDecryptor(
        AssetBundleExternalProcessDecryptorManifest manifest,
        string manifestDirectoryPath,
        AssetBundleDecryptorKind kind = AssetBundleDecryptorKind.ExternalProcess)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _manifestDirectoryPath = string.IsNullOrWhiteSpace(manifestDirectoryPath)
            ? throw new ArgumentException("Manifest directory path is required.", nameof(manifestDirectoryPath))
            : Path.GetFullPath(manifestDirectoryPath);
        _processRunner = new ExternalAssetBundleCodecProcessRunner(_manifest, _manifestDirectoryPath);

        Descriptor = new AssetBundleDecryptorDescriptorDto(
            Id: _manifest.Id,
            DisplayName: _manifest.DisplayName,
            Version: _manifest.Version,
            Kind: kind,
            SupportsAutoDetection: _manifest.SupportsAutoDetection,
            IsFallback: _manifest.IsFallback,
            Description: _manifest.Description,
            SupportsEncryption: _manifest.EncryptArguments.Count > 0);
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

        var executionResult = await _processRunner.ExecuteAsync(
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
                    : new LocalizedMessageDto(FallbackText: payload.Message.Trim()),
                FormatId: string.IsNullOrWhiteSpace(payload.FormatId) ? null : payload.FormatId.Trim(),
                Evidence: NormalizeEvidence(payload.Evidence));
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
        ExternalProcessAssetBundleDecryptorSupport.DeleteFileIfExists(outputFilePath);

        var executionResult = await _processRunner.ExecuteAsync(
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
            if (!ExternalProcessAssetBundleDecryptorSupport.PathsEqual(resolvedOutputPath, outputFilePath) &&
                !ExternalProcessAssetBundleDecryptorSupport.IsPathWithinOrEqual(resolvedOutputPath, workingDirectoryPath))
            {
                return Result<AssetBundleDecryptionResultDto>.Failure(
                    ApplicationErrors.BundleDecryptionInvalidResult(Descriptor.Id, "OutputPathOutsideWorkingDirectory"));
            }

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

    public async Task<Result<AssetBundleEncryptionResultDto>> EncryptAsync(
        AssetBundleEncryptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceDataRootPath);

        if (_manifest.EncryptArguments.Count == 0)
        {
            return Result<AssetBundleEncryptionResultDto>.Failure(
                ApplicationErrors.BundleExportCodecEncryptionUnsupported(Descriptor.Id));
        }

        var workingDirectoryPath = ResolveEncryptionWorkingDirectory(request);
        Directory.CreateDirectory(workingDirectoryPath);
        var resultFilePath = Path.Combine(
            workingDirectoryPath,
            $"{ExternalProcessAssetBundleDecryptorSupport.SanitizeFileComponent(_manifest.Id)}.encrypt.{Guid.NewGuid():N}.json");
        var outputFilePath = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? workingDirectoryPath);
        ExternalProcessAssetBundleDecryptorSupport.DeleteFileIfExists(outputFilePath);

        var executionResult = await _processRunner.ExecuteAsync(
            arguments: _manifest.EncryptArguments,
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
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    CreateEncryptionExecutionError(executionResult));
            }

            if (!File.Exists(resultFilePath))
            {
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    ApplicationErrors.BundleExportCodecResultFileMissing(Descriptor.Id));
            }

            var payload = ExternalProcessAssetBundleDecryptorSupport
                .ReadJsonFile<AssetBundleExternalProcessDecryptPayload>(resultFilePath);
            if (payload is null)
            {
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    ApplicationErrors.BundleExportCodecResultFileInvalid(Descriptor.Id));
            }

            if (!payload.IsSuccess)
            {
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    ApplicationErrors.BundleExportCodecEncryptionFailed(
                        Descriptor.Id,
                        string.IsNullOrWhiteSpace(payload.Message) ? null : payload.Message.Trim()));
            }

            var resolvedOutputPath = string.IsNullOrWhiteSpace(payload.OutputPath)
                ? outputFilePath
                : ExternalProcessAssetBundleDecryptorSupport.ResolvePath(payload.OutputPath, workingDirectoryPath);
            if (!ExternalProcessAssetBundleDecryptorSupport.PathsEqual(resolvedOutputPath, outputFilePath) &&
                !ExternalProcessAssetBundleDecryptorSupport.IsPathWithinOrEqual(resolvedOutputPath, workingDirectoryPath))
            {
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    ApplicationErrors.BundleExportCodecResultFileInvalid(
                        Descriptor.Id,
                        "OutputPathOutsideWorkingDirectory"));
            }

            if (!File.Exists(resolvedOutputPath))
            {
                return Result<AssetBundleEncryptionResultDto>.Failure(
                    ApplicationErrors.BundleExportCodecOutputMissing(Descriptor.Id));
            }

            return Result<AssetBundleEncryptionResultDto>.Success(
                new AssetBundleEncryptionResultDto(
                    Encryptor: Descriptor,
                    ResolvedBundlePath: resolvedOutputPath,
                    BundleFingerprint: AssetBundleFileFingerprintFactory.Create(resolvedOutputPath),
                    DiagnosticMessage: string.IsNullOrWhiteSpace(payload.Message)
                        ? null
                        : new LocalizedMessageDto(FallbackText: payload.Message.Trim())));
        }
        finally
        {
            ExternalProcessAssetBundleDecryptorSupport.DeleteFileIfExists(resultFilePath);
        }
    }

    private string ResolveWorkingDirectory(
        string sourcePath,
        string? workspaceKey,
        string? workspaceDataRootPath,
        string? requestWorkingDirectoryPath = null,
        string stageDirectoryName = "decrypt")
    {
        if (!string.IsNullOrWhiteSpace(requestWorkingDirectoryPath)) return Path.GetFullPath(requestWorkingDirectoryPath);
        if (!string.IsNullOrWhiteSpace(_manifest.WorkingDirectoryPath)) return ExternalProcessAssetBundleDecryptorSupport.ResolvePath(_manifest.WorkingDirectoryPath, _manifestDirectoryPath);

        if (!string.IsNullOrWhiteSpace(workspaceDataRootPath) &&
            !string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Path.Combine(
                Path.GetFullPath(workspaceDataRootPath),
                "staging",
                stageDirectoryName);
        }

        return Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools",
            stageDirectoryName);
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

    private string ResolveEncryptionWorkingDirectory(AssetBundleEncryptionRequestDto request)
    {
        var rootDirectoryPath = ResolveWorkingDirectory(
            request.SourcePath,
            request.WorkspaceKey,
            request.WorkspaceDataRootPath,
            request.WorkingDirectoryPath,
            stageDirectoryName: "encrypt");
        return Path.Combine(
            rootDirectoryPath,
            ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(request.WorkspaceKey),
            ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(_manifest.Id),
            string.IsNullOrWhiteSpace(request.BundleCacheKey)
                ? ExternalProcessAssetBundleDecryptorSupport.ComputeStableSourcePathKey(request.SourcePath)
                : ExternalProcessAssetBundleDecryptorSupport.SanitizeDirectoryComponent(request.BundleCacheKey));
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

    private AppError CreateEncryptionExecutionError(ExternalProcessExecutionResult executionResult)
    {
        return executionResult.FailureKind switch
        {
            ExternalProcessExecutionFailureKind.ProcessExited
                when string.IsNullOrWhiteSpace(executionResult.Detail) =>
                ApplicationErrors.BundleExportCodecProcessExited(
                    Descriptor.Id,
                    executionResult.ExitCode),
            ExternalProcessExecutionFailureKind.TimedOut =>
                ApplicationErrors.BundleExportCodecTimedOut(Descriptor.Id, _manifest.TimeoutMs),
            ExternalProcessExecutionFailureKind.Exception =>
                ApplicationErrors.BundleExportCodecExecutionFailed(Descriptor.Id),
            _ => ApplicationErrors.BundleExportCodecEncryptionFailed(
                Descriptor.Id,
                executionResult.Detail)
        };
    }

    private static IReadOnlyList<string> NormalizeEvidence(IReadOnlyList<string>? evidence) =>
        (evidence ?? Array.Empty<string>())
        .Where(static item => !string.IsNullOrWhiteSpace(item))
        .Select(static item => item.Trim())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

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
