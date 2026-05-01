using System.Diagnostics;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 负责把“已展开的导入文件列表”进一步筛成真正值得进入导入处理的候选资源包。
/// </summary>
/// <remarks>
/// 这里承接自动识别里的前两层：
/// 1. 导入源识别：先用轻量直检尽早命中普通 AB，或剔除明显无效输入；
/// 2. 解密器识别：对无法直判的来源继续执行工作区级解密器自动识别或显式解密器探测。
///
/// Reader 路径识别和缓存命中识别仍留在后续处理阶段，
/// 这样职责边界会更清晰，也便于后续继续扩展新的识别策略。
/// </remarks>
internal sealed class WorkspaceImportCandidateScreeningCoordinator
{
    private readonly IAssetBundleSourceInspector _assetBundleSourceInspector;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;

    public WorkspaceImportCandidateScreeningCoordinator(
        IAssetBundleSourceInspector assetBundleSourceInspector,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _assetBundleSourceInspector = assetBundleSourceInspector ?? throw new ArgumentNullException(nameof(assetBundleSourceInspector));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
    }

    public async Task<Result<WorkspaceImportCandidateScreeningResult>> ScreenAsync(
        string? configuredDecryptorId,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveredFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateFiles = new Dictionary<string, DiscoveredWorkspaceImportFileDto>(StringComparer.OrdinalIgnoreCase);
        var unresolvedFiles = new List<DiscoveredWorkspaceImportFileDto>();
        var issueDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>();

        foreach (var discoveredFile in discoveredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inspectionResult = _assetBundleSourceInspector.Inspect(discoveredFile.SourcePath);
            switch (inspectionResult.Kind)
            {
                case AssetBundleSourceInspectionKind.Bundle:
                    candidateFiles[discoveredFile.SourcePath] = discoveredFile;
                    break;
                case AssetBundleSourceInspectionKind.Rejected:
                    issueDiagnostics.Add(CreateScreenedOutDiagnostic(
                        discoveredFile,
                        decryptorId: null,
                        detailMessage: inspectionResult.DetailMessage ?? new LocalizedMessageDto(
                            WorkspaceImportMessageTokens.ScreeningSourceRejectedDuringInspection)));
                    break;
                default:
                    unresolvedFiles.Add(discoveredFile);
                    break;
            }
        }

        var decryptorResolutionResult = await ResolveDecryptorAsync(
            configuredDecryptorId,
            unresolvedFiles,
            cancellationToken);
        if (!decryptorResolutionResult.IsSuccess || decryptorResolutionResult.Value is null)
        {
            return Result<WorkspaceImportCandidateScreeningResult>.Failure(decryptorResolutionResult.Errors);
        }

        var decryptorResolution = decryptorResolutionResult.Value;
        var probeTelemetry = await AppendUnresolvedCandidatesAsync(
            decryptorResolution,
            unresolvedFiles,
            candidateFiles,
            issueDiagnostics,
            cancellationToken);

        return Result<WorkspaceImportCandidateScreeningResult>.Success(
            new WorkspaceImportCandidateScreeningResult(
                Decryptor: decryptorResolution.Decryptor,
                UsedAutoDetection: decryptorResolution.UsedAutoDetection,
                ShouldPersistResolvedDecryptorId: decryptorResolution.ShouldPersistResolvedDecryptorId,
                AutoDetectionDiagnostic: decryptorResolution.AutoDetectionDiagnostic,
                CandidateFiles: candidateFiles.Values
                    .OrderBy(file => file.LogicalPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ScreenedOutDiagnostics: issueDiagnostics,
                ProbeTelemetry: probeTelemetry));
    }

    private async Task<Result<WorkspaceImportDecryptorResolutionResult>> ResolveDecryptorAsync(
        string? configuredDecryptorId,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> unresolvedFiles,
        CancellationToken cancellationToken)
    {
        var normalizedDecryptorId = NormalizeDecryptorId(configuredDecryptorId);
        if (!string.IsNullOrWhiteSpace(normalizedDecryptorId) &&
            !IsBuiltInAutoDetect(normalizedDecryptorId))
        {
            var explicitDecryptorResult = WorkspaceAssetBundleDecryptorResolver.Resolve(
                _assetBundleDecryptorRegistry,
                normalizedDecryptorId);
            if (!explicitDecryptorResult.IsSuccess || explicitDecryptorResult.Value is null)
            {
                return Result<WorkspaceImportDecryptorResolutionResult>.Failure(explicitDecryptorResult.Errors);
            }

            return Result<WorkspaceImportDecryptorResolutionResult>.Success(
                new WorkspaceImportDecryptorResolutionResult(
                    Decryptor: explicitDecryptorResult.Value,
                    UsedAutoDetection: false,
                    ShouldPersistResolvedDecryptorId: false,
                    AutoDetectionDiagnostic: null,
                    SelectedProbeSummary: null,
                    ProbeTelemetry: WorkspaceImportProbeTelemetry.None));
        }

        return await WorkspaceAssetBundleDecryptorResolver.ResolveForWorkspaceImportAsync(
            _assetBundleDecryptorRegistry,
            configuredDecryptorId,
            unresolvedFiles,
            cancellationToken);
    }

    /// <summary>
    /// 对“直检未能判断”的来源继续执行工作区级解密器识别。
    /// 规则保持保守：
    /// 1. 自动识别或回退解密器未命中时，直接筛除；
    /// 2. 显式解密器且支持 Probe 时，以 Probe 结果为准；
    /// 3. 显式解密器但不支持 Probe 时，为避免误伤加密 AB，保守放行。
    /// </summary>
    private async Task<WorkspaceImportProbeTelemetry> AppendUnresolvedCandidatesAsync(
        WorkspaceImportDecryptorResolutionResult decryptorResolution,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> unresolvedFiles,
        IDictionary<string, DiscoveredWorkspaceImportFileDto> candidateFiles,
        ICollection<WorkspaceImportIssueDiagnosticDto> issueDiagnostics,
        CancellationToken cancellationToken)
    {
        var probeTelemetry = decryptorResolution.ProbeTelemetry;
        if (unresolvedFiles.Count == 0)
        {
            return probeTelemetry;
        }

        var decryptor = decryptorResolution.Decryptor;
        if (!decryptor.Descriptor.SupportsAutoDetection)
        {
            if (!decryptorResolution.UsedAutoDetection && !decryptor.Descriptor.IsFallback)
            {
                foreach (var unresolvedFile in unresolvedFiles)
                {
                    candidateFiles[unresolvedFile.SourcePath] = unresolvedFile;
                }

                return probeTelemetry;
            }

            var detail = decryptorResolution.UsedAutoDetection
                ? new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningAutoDetectionNoDecryptorMatch)
                : new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningConfiguredDecryptorProbeUnsupported);
            foreach (var unresolvedFile in unresolvedFiles)
            {
                issueDiagnostics.Add(CreateScreenedOutDiagnostic(
                    unresolvedFile,
                    decryptor.Descriptor.Id,
                    detail));
            }

            return probeTelemetry;
        }

        var probeSummary = decryptorResolution.SelectedProbeSummary;
        if (probeSummary is null ||
            !string.Equals(
                probeSummary.Decryptor.Descriptor.Id,
                decryptor.Descriptor.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            var probeStopwatch = Stopwatch.StartNew();
            probeSummary = await WorkspaceAssetBundleDecryptorResolver.ProbeDecryptorAsync(
                decryptor,
                unresolvedFiles,
                cancellationToken);
            probeStopwatch.Stop();
            probeTelemetry = probeTelemetry.Add(
                probeStopwatch.ElapsedMilliseconds,
                decryptorCount: 1,
                fileEvaluationCount: probeSummary.FileResults.Count);
        }

        var fileResultsByPath = probeSummary.FileResults
            .ToDictionary(fileResult => fileResult.SourcePath, StringComparer.OrdinalIgnoreCase);

        foreach (var unresolvedFile in unresolvedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (fileResultsByPath.TryGetValue(unresolvedFile.SourcePath, out var fileResult) &&
                fileResult.IsSuccessful &&
                fileResult.IsMatch)
            {
                candidateFiles[unresolvedFile.SourcePath] = unresolvedFile;
                continue;
            }

            var detail = !fileResultsByPath.TryGetValue(unresolvedFile.SourcePath, out var unmatchedResult)
                ? new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningProbeResultMissing)
                : BuildProbeRejectedDetail(unmatchedResult);
            issueDiagnostics.Add(CreateScreenedOutDiagnostic(
                unresolvedFile,
                decryptor.Descriptor.Id,
                detail));
        }

        return probeTelemetry;
    }

    private static WorkspaceImportIssueDiagnosticDto CreateScreenedOutDiagnostic(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string? decryptorId,
        LocalizedMessageDto? detailMessage)
    {
        return new WorkspaceImportIssueDiagnosticDto(
            SourcePath: discoveredFile.SourcePath,
            LogicalPath: discoveredFile.LogicalPath,
            Kind: WorkspaceImportIssueKind.CandidateScreenedOut,
            DecryptorId: decryptorId,
            DetailMessage: detailMessage,
            Errors: Array.Empty<Domain.Common.Errors.AppError>());
    }

    private static LocalizedMessageDto BuildProbeRejectedDetail(WorkspaceImportDecryptorProbeFileResult fileResult)
    {
        if (!fileResult.IsSuccessful)
        {
            return fileResult.Message ?? new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningProbeFailed);
        }

        return fileResult.Message ?? new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningProbeDidNotMatch);
    }

    private static string? NormalizeDecryptorId(string? decryptorId)
    {
        return string.IsNullOrWhiteSpace(decryptorId)
            ? null
            : decryptorId.Trim();
    }

    private static bool IsBuiltInAutoDetect(string? decryptorId)
    {
        return string.Equals(
            decryptorId,
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record WorkspaceImportCandidateScreeningResult(
    IAssetBundleDecryptor Decryptor,
    bool UsedAutoDetection,
    bool ShouldPersistResolvedDecryptorId,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    IReadOnlyList<DiscoveredWorkspaceImportFileDto> CandidateFiles,
    IReadOnlyList<WorkspaceImportIssueDiagnosticDto> ScreenedOutDiagnostics,
    WorkspaceImportProbeTelemetry ProbeTelemetry);
