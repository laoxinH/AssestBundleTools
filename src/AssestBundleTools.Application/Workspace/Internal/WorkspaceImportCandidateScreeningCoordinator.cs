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
/// 2. 解密器识别：自动策略下为每个候选 AB 独立选择实际解密器；显式策略继续使用固定解密器。
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
        var inspectionsBySourcePath = new Dictionary<string, AssetBundleSourceInspectionResultDto>(StringComparer.OrdinalIgnoreCase);
        var issueDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>();
        var stableInspectionSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var discoveredFile in discoveredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inspectionResult = _assetBundleSourceInspector.Inspect(discoveredFile.SourcePath);
            inspectionsBySourcePath[discoveredFile.SourcePath] = inspectionResult;
            switch (inspectionResult.Kind)
            {
                case AssetBundleSourceInspectionKind.Bundle:
                    candidateFiles[discoveredFile.SourcePath] = discoveredFile;
                    break;
                case AssetBundleSourceInspectionKind.Rejected:
                    stableInspectionSourcePaths.Add(discoveredFile.SourcePath);
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

        if (IsAutoDetectionMode(configuredDecryptorId))
        {
            var autoDetectionFiles = candidateFiles.Values
                .Concat(unresolvedFiles)
                .OrderBy(static file => file.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var autoResolutionResult = await WorkspaceAssetBundleDecryptorResolver.ResolveForWorkspaceImportAsync(
                _assetBundleDecryptorRegistry,
                AssetBundleDecryptorIds.BuiltInAutoDetect,
                autoDetectionFiles,
                cancellationToken);
            if (!autoResolutionResult.IsSuccess || autoResolutionResult.Value is null)
            {
                return Result<WorkspaceImportCandidateScreeningResult>.Failure(autoResolutionResult.Errors);
            }

            var autoResolution = autoResolutionResult.Value;
            var acceptedAutoDetectionFiles = FilterAutoDetectionPassThroughCandidates(
                autoDetectionFiles,
                inspectionsBySourcePath,
                autoResolution,
                issueDiagnostics);
            return Result<WorkspaceImportCandidateScreeningResult>.Success(
                new WorkspaceImportCandidateScreeningResult(
                    Decryptor: autoResolution.Decryptor,
                    UsedAutoDetection: true,
                    AutoDetectionDiagnostic: autoResolution.AutoDetectionDiagnostic,
                    BundleCandidates: BuildBundleCandidates(acceptedAutoDetectionFiles, autoResolution),
                    ScreenedOutDiagnostics: issueDiagnostics,
                    ProbeTelemetry: autoResolution.ProbeTelemetry,
                    StableRecognitionSourcePaths: MergeStableSourcePaths(
                        stableInspectionSourcePaths,
                        autoResolution.StableRecognitionSourcePaths)));
        }

        var explicitRecognitionFiles = candidateFiles.Values
            .Concat(unresolvedFiles)
            .OrderBy(static file => file.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var decryptorResolutionResult = await ResolveDecryptorAsync(
            configuredDecryptorId,
            explicitRecognitionFiles,
            cancellationToken);
        if (!decryptorResolutionResult.IsSuccess || decryptorResolutionResult.Value is null)
        {
            return Result<WorkspaceImportCandidateScreeningResult>.Failure(decryptorResolutionResult.Errors);
        }

        var decryptorResolution = decryptorResolutionResult.Value;
        candidateFiles.Clear();
        var explicitProbeResult = await AppendExplicitDecryptorCandidatesAsync(
            decryptorResolution,
            explicitRecognitionFiles,
            candidateFiles,
            issueDiagnostics,
            cancellationToken);

        return Result<WorkspaceImportCandidateScreeningResult>.Success(
            new WorkspaceImportCandidateScreeningResult(
                Decryptor: decryptorResolution.Decryptor,
                UsedAutoDetection: decryptorResolution.UsedAutoDetection,
                AutoDetectionDiagnostic: decryptorResolution.AutoDetectionDiagnostic,
                BundleCandidates: BuildBundleCandidates(
                    candidateFiles.Values
                    .OrderBy(file => file.LogicalPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                    decryptorResolution),
                ScreenedOutDiagnostics: issueDiagnostics,
                ProbeTelemetry: explicitProbeResult.Telemetry,
                StableRecognitionSourcePaths: MergeStableSourcePaths(
                    stableInspectionSourcePaths,
                    explicitProbeResult.StableSourcePaths)));
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
                    AutoDetectionDiagnostic: null,
                    ProbeTelemetry: WorkspaceImportProbeTelemetry.None,
                    StableRecognitionSourcePaths: unresolvedFiles
                        .Select(static file => file.SourcePath)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    ResolvedDecryptorsBySourcePath: unresolvedFiles.ToDictionary(
                        static file => file.SourcePath,
                        _ => explicitDecryptorResult.Value,
                        StringComparer.OrdinalIgnoreCase)));
        }

        return await WorkspaceAssetBundleDecryptorResolver.ResolveForWorkspaceImportAsync(
            _assetBundleDecryptorRegistry,
            configuredDecryptorId,
            unresolvedFiles,
            cancellationToken);
    }

    /// <summary>
    /// 对全部候选来源执行显式解密器识别。
    /// 支持 Probe 时只放行匹配项；旧解密器没有 Probe 时，
    /// 交给后续 Decrypt + 真实 Reader 完成兼容校验。
    /// </summary>
    private async Task<ExplicitProbeResult> AppendExplicitDecryptorCandidatesAsync(
        WorkspaceImportDecryptorResolutionResult decryptorResolution,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> recognitionFiles,
        IDictionary<string, DiscoveredWorkspaceImportFileDto> candidateFiles,
        ICollection<WorkspaceImportIssueDiagnosticDto> issueDiagnostics,
        CancellationToken cancellationToken)
    {
        var probeTelemetry = decryptorResolution.ProbeTelemetry;
        if (recognitionFiles.Count == 0)
        {
            return new ExplicitProbeResult(
                probeTelemetry,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var decryptor = decryptorResolution.Decryptor;
        if (!decryptor.Descriptor.SupportsAutoDetection)
        {
            foreach (var recognitionFile in recognitionFiles)
            {
                candidateFiles[recognitionFile.SourcePath] = recognitionFile;
            }

            return new ExplicitProbeResult(
                probeTelemetry,
                recognitionFiles
                    .Select(static file => file.SourcePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        var probeStopwatch = Stopwatch.StartNew();
        var probeSummary = await WorkspaceAssetBundleDecryptorResolver.ProbeDecryptorAsync(
            decryptor,
            recognitionFiles,
            cancellationToken);
        probeStopwatch.Stop();
        probeTelemetry = probeTelemetry.Add(
            probeStopwatch.ElapsedMilliseconds,
            decryptorCount: 1,
            fileEvaluationCount: probeSummary.FileResults.Count);

        var fileResultsByPath = probeSummary.FileResults
            .ToDictionary(fileResult => fileResult.SourcePath, StringComparer.OrdinalIgnoreCase);

        foreach (var recognitionFile in recognitionFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (fileResultsByPath.TryGetValue(recognitionFile.SourcePath, out var fileResult) &&
                fileResult.IsSuccessful &&
                fileResult.IsMatch)
            {
                candidateFiles[recognitionFile.SourcePath] = recognitionFile;
                continue;
            }

            var detail = !fileResultsByPath.TryGetValue(recognitionFile.SourcePath, out var unmatchedResult)
                ? new LocalizedMessageDto(WorkspaceImportMessageTokens.ScreeningProbeResultMissing)
                : BuildProbeRejectedDetail(unmatchedResult);
            issueDiagnostics.Add(CreateScreenedOutDiagnostic(
                recognitionFile,
                decryptor.Descriptor.Id,
                detail));
        }

        return new ExplicitProbeResult(
            probeTelemetry,
            probeSummary.FileResults
                .Where(static result => result.IsSuccessful)
                .Select(static result => result.SourcePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
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

    private static IReadOnlySet<string> MergeStableSourcePaths(
        IReadOnlySet<string> first,
        IReadOnlySet<string> second)
    {
        var merged = new HashSet<string>(first, StringComparer.OrdinalIgnoreCase);
        merged.UnionWith(second);
        return merged;
    }

    private static bool IsAutoDetectionMode(string? decryptorId) =>
        string.IsNullOrWhiteSpace(decryptorId) || IsBuiltInAutoDetect(decryptorId.Trim());

    private static IReadOnlyList<WorkspaceImportBundleCandidate> BuildBundleCandidates(
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> files,
        WorkspaceImportDecryptorResolutionResult resolution) =>
        files
            .Select(file => new WorkspaceImportBundleCandidate(
                File: file,
                Decryptor: resolution.ResolveForSourcePath(file.SourcePath),
                CanPersistRecognition: resolution.StableRecognitionSourcePaths.Contains(file.SourcePath)))
            .ToArray();

    private static IReadOnlyList<DiscoveredWorkspaceImportFileDto> FilterAutoDetectionPassThroughCandidates(
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> files,
        IReadOnlyDictionary<string, AssetBundleSourceInspectionResultDto> inspectionsBySourcePath,
        WorkspaceImportDecryptorResolutionResult resolution,
        ICollection<WorkspaceImportIssueDiagnosticDto> issueDiagnostics)
    {
        var acceptedFiles = new List<DiscoveredWorkspaceImportFileDto>(files.Count);
        foreach (var file in files)
        {
            var decryptor = resolution.ResolveForSourcePath(file.SourcePath);
            if (!decryptor.Descriptor.IsFallback ||
                !inspectionsBySourcePath.TryGetValue(file.SourcePath, out var inspection) ||
                inspection.CanUsePassThrough)
            {
                acceptedFiles.Add(file);
                continue;
            }

            issueDiagnostics.Add(CreateScreenedOutDiagnostic(
                file,
                decryptor.Descriptor.Id,
                inspection.DetailMessage));
        }

        return acceptedFiles;
    }
}

internal sealed record WorkspaceImportCandidateScreeningResult(
    IAssetBundleDecryptor Decryptor,
    bool UsedAutoDetection,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    IReadOnlyList<WorkspaceImportBundleCandidate> BundleCandidates,
    IReadOnlyList<WorkspaceImportIssueDiagnosticDto> ScreenedOutDiagnostics,
    WorkspaceImportProbeTelemetry ProbeTelemetry,
    IReadOnlySet<string> StableRecognitionSourcePaths)
{
    public bool CanPersistRecognition(string sourcePath) =>
        StableRecognitionSourcePaths.Contains(sourcePath);
}

internal sealed record WorkspaceImportBundleCandidate(
    DiscoveredWorkspaceImportFileDto File,
    IAssetBundleDecryptor Decryptor,
    bool CanPersistRecognition = true);

internal sealed record ExplicitProbeResult(
    WorkspaceImportProbeTelemetry Telemetry,
    IReadOnlySet<string> StableSourcePaths);
