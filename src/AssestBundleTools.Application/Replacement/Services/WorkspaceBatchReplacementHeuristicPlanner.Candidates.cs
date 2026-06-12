using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static WorkspaceBatchReplacementCandidateDto CreateMatchedCandidate(
        WorkspaceBatchReplacementSourceFileEntryDto sourceFile,
        SourceFileAnalysis source,
        IReadOnlyList<string> diagnostics,
        AssetIndexEntry target,
        WorkspaceBatchReplacementMatchStatus status)
    {
        return new WorkspaceBatchReplacementCandidateDto(
            CandidateId: $"heuristic:{sourceFile.RelativePath}:{target.Bundle.BundleId.Value}:{target.AssetId.Value}",
            BundleId: target.Bundle.BundleId,
            BundleDisplayName: target.Bundle.DisplayName,
            AssetId: target.AssetId,
            PathId: target.PathId,
            AssetType: target.AssetType,
            AssetTypeDisplayName: target.AssetTypeDisplayName,
            AssetName: ResolveTargetDisplayName(target),
            ContainerPath: target.AssetPath,
            RelativePath: sourceFile.RelativePath,
            FullPath: sourceFile.FullPath,
            CurrentFileSizeInBytes: sourceFile.SizeInBytes,
            CurrentFileHashAlgorithm: null,
            CurrentFileHash: null,
            ExportedFileSizeInBytes: 0,
            ExportedFileHashAlgorithm: string.Empty,
            ExportedFileHash: string.Empty,
            SpineGroupId: source.SpineGroupName,
            SpineGroupDisplayName: source.SpineGroupName,
            OutputRole: source.OutputRole,
            MatchStatus: status,
            MatchMode: WorkspaceBatchReplacementMatchingMode.Heuristic,
            IsDefaultSelected: status == WorkspaceBatchReplacementMatchStatus.HighConfidence,
            Diagnostics: diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }
}
