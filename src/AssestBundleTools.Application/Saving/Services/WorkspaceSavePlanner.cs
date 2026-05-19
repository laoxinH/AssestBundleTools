using System.IO;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 负责把当前工作区的保存前信息整理为统一的输出计划。
/// </summary>
/// <remarks>
/// 当前阶段既需要保存前预览，也需要“执行保存骨架”时复用同一份计划。
/// 因此把资源包聚合、输出目录解析和统计计算集中在这里，避免在多个 Handler 中重复拼装。
/// </remarks>
internal static class WorkspaceSavePlanner
{
    public static WorkspaceSavePlan BuildPlan(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        string? outputDirectory,
        WorkspaceSaveArtifactInputResolver? artifactInputResolver = null,
        IWorkspaceSaveOutputProbe? outputProbe = null,
        string? workspaceKey = null,
        string? workspaceDataRootPath = null,
        DateTimeOffset? plannedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);

        if (!workspaceSessionRegistry.TryGetWorkspaceChangeSummary(workspaceId, out var changeSummary) ||
            changeSummary is null)
        {
            return new WorkspaceSavePlan(
                WorkspaceId: workspaceId,
                WorkspaceDisplayName: string.Empty,
                OutputDirectory: string.Empty,
                TotalChangeCount: 0,
                ModifiedAssetCount: 0,
                ExtractedAssetCount: 0,
                WorkspaceReplacementCount: 0,
                FileReplacementCount: 0,
                ResolvedArtifactInputCount: 0,
                UnresolvedArtifactInputCount: 0,
                OutputReadyBundleCount: 0,
                OutputBlockedBundleCount: 0,
                OutputUnprobedBundleCount: 0,
                IsReadyForWrite: false,
                BundlePlans: Array.Empty<PlannedWorkspaceBundleSave>());
        }

        var resolvedOutputDirectory = ResolveOutputDirectory(outputDirectory, changeSummary.WorkspaceDisplayName);
        var bundleGroups = changeSummary.Items
            .GroupBy(
                item => new { item.BundleId, item.BundleDisplayName },
                static item => item)
            .OrderBy(group => group.Key.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var artifactInputs = artifactInputResolver?.ResolveInputs(
            workspaceKey,
            workspaceDataRootPath,
            bundleGroups.Select(static group => group.Key.BundleId).Distinct().ToArray(),
            plannedAtUtc ?? DateTimeOffset.UtcNow);

        var bundlePlans = bundleGroups
            .Select(group =>
            {
                var items = group.ToArray();
                var artifactInput = ResolveArtifactInput(artifactInputs, group.Key.BundleId);
                var outputFilePath = Path.Combine(resolvedOutputDirectory, group.Key.BundleDisplayName);
                var outputPreflight = ResolveOutputPreflight(outputProbe, outputFilePath);
                return new PlannedWorkspaceBundleSave(
                    BundleId: group.Key.BundleId,
                    BundleDisplayName: group.Key.BundleDisplayName,
                    OutputFilePath: outputFilePath,
                    ChangeCount: items.Length,
                    ModifiedAssetCount: items.Count(static item => item.IsModified),
                    ExtractedAssetCount: items.Count(static item => item.LastExtractedAtUtc.HasValue),
                    WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
                    FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
                    ArtifactInput: artifactInput,
                    OutputPreflight: outputPreflight);
            })
            .ToArray();
        var outputReadyBundleCount = bundlePlans.Count(static plan => plan.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready);
        var outputBlockedBundleCount = bundlePlans.Count(static plan => plan.OutputPreflight.IsWriteBlocked);
        var outputUnprobedBundleCount = bundlePlans.Count(static plan => plan.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed);
        var unresolvedArtifactInputCount = bundlePlans.Count(static plan => !plan.ArtifactInput.HasStandardizedArtifact);

        return new WorkspaceSavePlan(
            WorkspaceId: changeSummary.WorkspaceId,
            WorkspaceDisplayName: changeSummary.WorkspaceDisplayName,
            OutputDirectory: resolvedOutputDirectory,
            TotalChangeCount: changeSummary.TotalChangeCount,
            ModifiedAssetCount: changeSummary.ModifiedAssetCount,
            ExtractedAssetCount: changeSummary.ExtractedAssetCount,
            WorkspaceReplacementCount: changeSummary.WorkspaceReplacementCount,
            FileReplacementCount: changeSummary.FileReplacementCount,
            ResolvedArtifactInputCount: bundlePlans.Count(static plan => plan.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount: unresolvedArtifactInputCount,
            OutputReadyBundleCount: outputReadyBundleCount,
            OutputBlockedBundleCount: outputBlockedBundleCount,
            OutputUnprobedBundleCount: outputUnprobedBundleCount,
            IsReadyForWrite: unresolvedArtifactInputCount == 0 &&
                             outputBlockedBundleCount == 0 &&
                             outputUnprobedBundleCount == 0,
            BundlePlans: bundlePlans);
    }

    /// <summary>
    /// 当前阶段统一给出一个稳定的建议输出目录，
    /// 先服务于保存前预览和保存执行骨架，后续再接入真实目录选择与冲突检查。
    /// </summary>
    private static string ResolveOutputDirectory(string? outputDirectory, string workspaceDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory.Trim();
        }

        return Path.Combine("Outputs", workspaceDisplayName);
    }

    private static WorkspaceSaveBundleArtifactInput ResolveArtifactInput(
        IReadOnlyDictionary<string, WorkspaceSaveBundleArtifactInput>? artifactInputs,
        Domain.Bundles.BundleId bundleId)
    {
        if (artifactInputs is not null &&
            artifactInputs.TryGetValue(bundleId.Value, out var artifactInput))
        {
            return artifactInput;
        }

        return WorkspaceSaveBundleArtifactInput.Unresolved(
            bundleCacheKey: null,
            diagnosticCode: "Save.Artifact.NotResolved");
    }

    private static WorkspaceSaveBundleOutputPreflight ResolveOutputPreflight(
        IWorkspaceSaveOutputProbe? outputProbe,
        string outputFilePath)
    {
        if (outputProbe is null)
        {
            return new WorkspaceSaveBundleOutputPreflight(
                WorkspaceSaveOutputReadiness.NotProbed,
                IsWriteBlocked: false,
                DiagnosticCode: null);
        }

        var result = outputProbe.Probe(new WorkspaceSaveOutputProbeRequest(outputFilePath));
        return new WorkspaceSaveBundleOutputPreflight(
            result.Readiness,
            result.IsWriteBlocked,
            result.DiagnosticCode);
    }
}

/// <summary>
/// 表示当前工作区的保存计划内部模型。
/// </summary>
internal sealed record WorkspaceSavePlan(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    string OutputDirectory,
    int TotalChangeCount,
    int ModifiedAssetCount,
    int ExtractedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    int ResolvedArtifactInputCount,
    int UnresolvedArtifactInputCount,
    int OutputReadyBundleCount,
    int OutputBlockedBundleCount,
    int OutputUnprobedBundleCount,
    bool IsReadyForWrite,
    IReadOnlyList<PlannedWorkspaceBundleSave> BundlePlans);

/// <summary>
/// 表示单个资源包在保存计划中的输出项。
/// </summary>
internal sealed record PlannedWorkspaceBundleSave(
    Domain.Bundles.BundleId BundleId,
    string BundleDisplayName,
    string OutputFilePath,
    int ChangeCount,
    int ModifiedAssetCount,
    int ExtractedAssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    WorkspaceSaveBundleArtifactInput ArtifactInput,
    WorkspaceSaveBundleOutputPreflight OutputPreflight);

internal sealed record WorkspaceSaveBundleOutputPreflight(
    WorkspaceSaveOutputReadiness Readiness,
    bool IsWriteBlocked,
    string? DiagnosticCode);
