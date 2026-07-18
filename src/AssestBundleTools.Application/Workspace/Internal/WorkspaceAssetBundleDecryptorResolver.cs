using System.Diagnostics;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 为工作区相关用例统一提供解密器解析逻辑。
/// </summary>
internal static class WorkspaceAssetBundleDecryptorResolver
{
    public static Result<IAssetBundleDecryptor> Resolve(
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        string? decryptorId)
    {
        ArgumentNullException.ThrowIfNull(assetBundleDecryptorRegistry);

        var normalizedDecryptorId = NormalizeDecryptorId(decryptorId);
        if (!string.IsNullOrWhiteSpace(normalizedDecryptorId) &&
            !IsBuiltInAutoDetect(normalizedDecryptorId))
        {
            if (assetBundleDecryptorRegistry.TryGet(normalizedDecryptorId, out var configuredDecryptor))
            {
                return Result<IAssetBundleDecryptor>.Success(configuredDecryptor);
            }

            return Result<IAssetBundleDecryptor>.Failure(
                ApplicationErrors.BundleDecryptorCatalogNotFound(normalizedDecryptorId));
        }

        var fallbackDecryptor = FindFallbackDecryptor(assetBundleDecryptorRegistry);
        if (fallbackDecryptor is not null)
        {
            return Result<IAssetBundleDecryptor>.Success(fallbackDecryptor);
        }

        return Result<IAssetBundleDecryptor>.Failure(
            ApplicationErrors.BundleDecryptorFallbackNotFound());
    }

    /// <summary>
    /// 为“工作区导入处理”解析实际要使用的解密器。
    /// 显式指定具体解密器时直接使用；
    /// 指定为“自动识别”时，则先探测非回退解密器，最后再回退到直通解密器。
    /// </summary>
    public static async Task<Result<WorkspaceImportDecryptorResolutionResult>> ResolveForWorkspaceImportAsync(
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        string? decryptorId,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetBundleDecryptorRegistry);
        ArgumentNullException.ThrowIfNull(discoveredFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedDecryptorId = NormalizeDecryptorId(decryptorId);
        if (!string.IsNullOrWhiteSpace(normalizedDecryptorId) &&
            !IsBuiltInAutoDetect(normalizedDecryptorId))
        {
            var explicitDecryptorResult = Resolve(assetBundleDecryptorRegistry, normalizedDecryptorId);
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
                    StableRecognitionSourcePaths: discoveredFiles
                        .Select(static file => file.SourcePath)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    ResolvedDecryptorsBySourcePath: discoveredFiles.ToDictionary(
                        static file => file.SourcePath,
                        _ => explicitDecryptorResult.Value,
                        StringComparer.OrdinalIgnoreCase)));
        }

        var fallbackDecryptor = FindFallbackDecryptor(assetBundleDecryptorRegistry);
        if (fallbackDecryptor is null)
        {
            return Result<WorkspaceImportDecryptorResolutionResult>.Failure(
                ApplicationErrors.BundleDecryptorFallbackNotFound());
        }

        var probeCandidates = assetBundleDecryptorRegistry.GetAll()
            .Where(static decryptor => !decryptor.Descriptor.IsFallback && decryptor.Descriptor.SupportsAutoDetection)
            .ToArray();

        if (discoveredFiles.Count == 0 || probeCandidates.Length == 0)
        {
            return Result<WorkspaceImportDecryptorResolutionResult>.Success(
                new WorkspaceImportDecryptorResolutionResult(
                    Decryptor: fallbackDecryptor,
                    UsedAutoDetection: true,
                    AutoDetectionDiagnostic: CreateAutoDetectionDiagnostic(
                        discoveredFiles.Count,
                        AssetBundleDecryptorIds.BuiltInAutoDetect,
                        usedFallbackDecryptor: discoveredFiles.Count > 0,
                        probeSummaries: Array.Empty<WorkspaceImportDecryptorProbeSummary>(),
                    selectedProbeDecryptorIds: Array.Empty<string>()),
                    ProbeTelemetry: WorkspaceImportProbeTelemetry.None,
                    StableRecognitionSourcePaths: discoveredFiles
                        .Select(static file => file.SourcePath)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    ResolvedDecryptorsBySourcePath: discoveredFiles.ToDictionary(
                        static file => file.SourcePath,
                        _ => fallbackDecryptor,
                        StringComparer.OrdinalIgnoreCase)));
        }

        var probeSummaries = new List<WorkspaceImportDecryptorProbeSummary>(probeCandidates.Length);
        var probeStopwatch = Stopwatch.StartNew();
        foreach (var probeCandidate in probeCandidates)
        {
            probeSummaries.Add(await ProbeDecryptorAsync(
                probeCandidate,
                discoveredFiles,
                cancellationToken));
        }
        probeStopwatch.Stop();

        var resolvedDecryptorsBySourcePath = discoveredFiles.ToDictionary(
            static file => file.SourcePath,
            file => ResolveFileDecryptor(file.SourcePath, probeSummaries) ?? fallbackDecryptor,
            StringComparer.OrdinalIgnoreCase);
        var selectedProbeDecryptorIds = resolvedDecryptorsBySourcePath.Values
            .Where(static decryptor => !decryptor.Descriptor.IsFallback)
            .Select(static decryptor => decryptor.Descriptor.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stableRecognitionSourcePaths = discoveredFiles
            .Where(file => probeSummaries.All(summary => summary.FileResults.Any(result =>
                string.Equals(result.SourcePath, file.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                result.IsSuccessful)))
            .Select(static file => file.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Result<WorkspaceImportDecryptorResolutionResult>.Success(
            new WorkspaceImportDecryptorResolutionResult(
                Decryptor: fallbackDecryptor,
                UsedAutoDetection: true,
                AutoDetectionDiagnostic: CreateAutoDetectionDiagnostic(
                    discoveredFiles.Count,
                    AssetBundleDecryptorIds.BuiltInAutoDetect,
                    usedFallbackDecryptor: resolvedDecryptorsBySourcePath.Values.Any(static decryptor =>
                        decryptor.Descriptor.IsFallback),
                    probeSummaries,
                    selectedProbeDecryptorIds),
                ProbeTelemetry: new WorkspaceImportProbeTelemetry(
                    DurationMs: probeStopwatch.ElapsedMilliseconds,
                    DecryptorCount: probeSummaries.Count,
                    FileEvaluationCount: probeSummaries.Sum(static summary => summary.FileResults.Count)),
                StableRecognitionSourcePaths: stableRecognitionSourcePaths,
                ResolvedDecryptorsBySourcePath: resolvedDecryptorsBySourcePath));
    }

    private static IAssetBundleDecryptor? ResolveFileDecryptor(
        string sourcePath,
        IReadOnlyList<WorkspaceImportDecryptorProbeSummary> probeSummaries)
    {
        return probeSummaries
            .Select(summary => new
            {
                summary.Decryptor,
                FileResult = summary.FileResults.FirstOrDefault(result =>
                    string.Equals(result.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            })
            .Where(static candidate =>
                candidate.FileResult is { IsSuccessful: true, IsMatch: true })
            .OrderByDescending(static candidate => candidate.FileResult!.Confidence)
            .ThenBy(static candidate => candidate.Decryptor.Descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.Decryptor.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static candidate => candidate.Decryptor)
            .FirstOrDefault();
    }

    internal static async Task<WorkspaceImportDecryptorProbeSummary> ProbeDecryptorAsync(
        IAssetBundleDecryptor decryptor,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles,
        CancellationToken cancellationToken)
    {
        var matchedFileCount = 0;
        var successfulProbeCount = 0;
        var totalConfidence = 0;
        var sampleMessages = new List<LocalizedMessageDto>(capacity: 3);
        var fileResults = new List<WorkspaceImportDecryptorProbeFileResult>(discoveredFiles.Count);

        foreach (var discoveredFile in discoveredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AssetBundleDecryptorProbeResultDto probeResult;
            try
            {
                probeResult = await decryptor.ProbeAsync(
                    new AssetBundleDecryptorProbeRequestDto(
                        SourcePath: discoveredFile.SourcePath,
                        Hints: CreateProbeHints(discoveredFile)),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 探测失败只记为当前候选未命中，不让单个外部脚本异常中断整轮导入。
                probeResult = new AssetBundleDecryptorProbeResultDto(
                    IsSuccessful: false,
                    IsMatch: false,
                    Confidence: 0,
                    DiagnosticMessage: null);
            }

            fileResults.Add(new WorkspaceImportDecryptorProbeFileResult(
                SourcePath: discoveredFile.SourcePath,
                IsSuccessful: probeResult.IsSuccessful,
                IsMatch: probeResult.IsMatch,
                Confidence: Math.Clamp(probeResult.Confidence, 0, 100),
                Message: probeResult.DiagnosticMessage,
                FormatId: probeResult.FormatId,
                Evidence: probeResult.Evidence));
            CollectSampleMessage(sampleMessages, probeResult.DiagnosticMessage);

            if (probeResult.IsSuccessful)
            {
                successfulProbeCount++;
            }

            if (!probeResult.IsSuccessful || !probeResult.IsMatch)
            {
                continue;
            }

            matchedFileCount++;
            totalConfidence += Math.Clamp(probeResult.Confidence, 0, 100);
        }

        return new WorkspaceImportDecryptorProbeSummary(
            Decryptor: decryptor,
            MatchedFileCount: matchedFileCount,
            SuccessfulProbeCount: successfulProbeCount,
            TotalConfidence: totalConfidence,
            SampleMessages: sampleMessages,
            FileResults: fileResults);
    }

    private static IReadOnlyDictionary<string, string> CreateProbeHints(DiscoveredWorkspaceImportFileDto discoveredFile)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LogicalPath"] = discoveredFile.LogicalPath
        };
    }

    private static IAssetBundleDecryptor? FindFallbackDecryptor(IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        return assetBundleDecryptorRegistry.GetAll()
            .FirstOrDefault(static decryptor => decryptor.Descriptor.IsFallback);
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

    private static void CollectSampleMessage(ICollection<LocalizedMessageDto> messages, LocalizedMessageDto? message)
    {
        if (messages.Count >= 3 || message is null)
        {
            return;
        }

        var fingerprint = BuildMessageFingerprint(message);
        if (messages.Any(existingMessage => string.Equals(
                BuildMessageFingerprint(existingMessage),
                fingerprint,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        messages.Add(message);
    }

    private static string BuildMessageFingerprint(LocalizedMessageDto message)
    {
        if (!string.IsNullOrWhiteSpace(message.MessageToken))
        {
            return message.Arguments is { Count: > 0 }
                ? $"{message.MessageToken}|{string.Join("|", message.Arguments)}"
                : message.MessageToken;
        }

        return message.FallbackText?.Trim() ?? string.Empty;
    }

    private static WorkspaceImportAutoDetectionDiagnosticDto CreateAutoDetectionDiagnostic(
        int probeScopeFileCount,
        string selectedDecryptorId,
        bool usedFallbackDecryptor,
        IReadOnlyList<WorkspaceImportDecryptorProbeSummary> probeSummaries,
        IReadOnlyCollection<string> selectedProbeDecryptorIds)
    {
        // 自动识别诊断只表达“为什么最终选择了这个解密器”，
        // 不携带 UI 文本，方便后续在日志页或独立诊断面板中复用。
        // 这里沿用历史字段名 DiscoveredFileCount，但当前实际记录的是“进入 Probe 的待判定文件数”。
        var probeDiagnostics = probeSummaries
            .Select(summary => new WorkspaceImportDecryptorProbeDiagnosticDto(
                DecryptorId: summary.Decryptor.Descriptor.Id,
                DecryptorDisplayName: summary.Decryptor.Descriptor.DisplayName,
                MatchedFileCount: summary.MatchedFileCount,
                SuccessfulProbeCount: summary.SuccessfulProbeCount,
                TotalConfidence: summary.TotalConfidence,
                WasSelected: selectedProbeDecryptorIds.Contains(
                    summary.Decryptor.Descriptor.Id,
                    StringComparer.OrdinalIgnoreCase),
                SampleMessages: summary.SampleMessages,
                DetectedFormatIds: summary.FileResults
                    .Select(static result => result.FormatId)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Evidence: summary.FileResults
                    .SelectMany(static result => result.Evidence ?? Array.Empty<string>())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

        return new WorkspaceImportAutoDetectionDiagnosticDto(
            DiscoveredFileCount: probeScopeFileCount,
            SelectedDecryptorId: selectedDecryptorId,
            UsedFallbackDecryptor: usedFallbackDecryptor,
            ProbeDiagnostics: probeDiagnostics);
    }
}

internal sealed record WorkspaceImportDecryptorResolutionResult(
    IAssetBundleDecryptor Decryptor,
    bool UsedAutoDetection,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    WorkspaceImportProbeTelemetry ProbeTelemetry,
    IReadOnlySet<string> StableRecognitionSourcePaths,
    IReadOnlyDictionary<string, IAssetBundleDecryptor> ResolvedDecryptorsBySourcePath)
{
    public IAssetBundleDecryptor ResolveForSourcePath(string sourcePath) =>
        ResolvedDecryptorsBySourcePath.TryGetValue(sourcePath, out var decryptor)
            ? decryptor
            : Decryptor;
}

internal sealed record WorkspaceImportDecryptorProbeSummary(
    IAssetBundleDecryptor Decryptor,
    int MatchedFileCount,
    int SuccessfulProbeCount,
    int TotalConfidence,
    IReadOnlyList<LocalizedMessageDto> SampleMessages,
    IReadOnlyList<WorkspaceImportDecryptorProbeFileResult> FileResults);

internal sealed record WorkspaceImportDecryptorProbeFileResult(
    string SourcePath,
    bool IsSuccessful,
    bool IsMatch,
    int Confidence,
    LocalizedMessageDto? Message,
    string? FormatId = null,
    IReadOnlyList<string>? Evidence = null);
