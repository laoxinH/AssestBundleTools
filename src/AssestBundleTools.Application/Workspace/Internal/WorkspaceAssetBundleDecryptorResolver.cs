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
                    ShouldPersistResolvedDecryptorId: false,
                    AutoDetectionDiagnostic: null,
                    SelectedProbeSummary: null));
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
                    ShouldPersistResolvedDecryptorId: true,
                    AutoDetectionDiagnostic: CreateAutoDetectionDiagnostic(
                        discoveredFiles.Count,
                        fallbackDecryptor.Descriptor.Id,
                        usedFallbackDecryptor: true,
                        probeSummaries: Array.Empty<WorkspaceImportDecryptorProbeSummary>(),
                        selectedProbeDecryptorId: null),
                    SelectedProbeSummary: null));
        }

        var probeSummaries = new List<WorkspaceImportDecryptorProbeSummary>(probeCandidates.Length);
        foreach (var probeCandidate in probeCandidates)
        {
            probeSummaries.Add(await ProbeDecryptorAsync(
                probeCandidate,
                discoveredFiles,
                cancellationToken));
        }

        var bestMatchedCandidate = probeSummaries
            .Where(static summary => summary.MatchedFileCount > 0)
            .OrderByDescending(static summary => summary.MatchedFileCount)
            .ThenByDescending(static summary => summary.TotalConfidence)
            .ThenByDescending(static summary => summary.SuccessfulProbeCount)
            .ThenBy(summary => summary.Decryptor.Descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var selectedDecryptor = bestMatchedCandidate?.Decryptor ?? fallbackDecryptor;

        return Result<WorkspaceImportDecryptorResolutionResult>.Success(
            new WorkspaceImportDecryptorResolutionResult(
                Decryptor: selectedDecryptor,
                UsedAutoDetection: true,
                ShouldPersistResolvedDecryptorId: true,
                AutoDetectionDiagnostic: CreateAutoDetectionDiagnostic(
                    discoveredFiles.Count,
                    selectedDecryptor.Descriptor.Id,
                    usedFallbackDecryptor: bestMatchedCandidate is null,
                    probeSummaries,
                    bestMatchedCandidate?.Decryptor.Descriptor.Id),
                SelectedProbeSummary: bestMatchedCandidate));
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
                Message: probeResult.DiagnosticMessage));
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
        string? selectedProbeDecryptorId)
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
                WasSelected: string.Equals(
                    summary.Decryptor.Descriptor.Id,
                    selectedProbeDecryptorId,
                    StringComparison.OrdinalIgnoreCase),
                SampleMessages: summary.SampleMessages))
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
    bool ShouldPersistResolvedDecryptorId,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    WorkspaceImportDecryptorProbeSummary? SelectedProbeSummary);

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
    LocalizedMessageDto? Message);
