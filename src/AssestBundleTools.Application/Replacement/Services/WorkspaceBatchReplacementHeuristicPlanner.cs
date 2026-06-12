using System.IO;
using System.Text.RegularExpressions;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 在缺少批量导出 manifest 时，以保守规则从目录、类型和名称推断批量替换候选。
/// </summary>
internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private const int MaximumDepth = 4;
    private const int HighConfidenceScore = 90;
    private const int MediumConfidenceScore = 70;
    private const int LowConfidenceScore = 50;
    private const int AmbiguousGap = 15;
    private const string SpineSegment = "Spine";
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    public static async Task<WorkspaceBatchReplacementPlanDto> BuildPlanAsync(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler,
        IWorkspaceBatchReplacementSourceFileProbe sourceFileProbe,
        WorkspaceId workspaceId,
        string sourceRootDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);
        ArgumentNullException.ThrowIfNull(sourceFileProbe);

        var sourceFilesResult = await sourceFileProbe.EnumerateFilesAsync(
            sourceRootDirectory,
            MaximumDepth,
            cancellationToken);
        if (sourceFilesResult.IsCancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var sourceFiles = sourceFilesResult.Value ?? Array.Empty<WorkspaceBatchReplacementSourceFileEntryDto>();
        var workspaceIndex = BuildWorkspaceIndex(workspaceSessionRegistry, workspaceId);
        var spineGroupIndex = await BuildSpineGroupIndexAsync(
            loadSpineAssetGroupsHandler,
            workspaceId,
            workspaceIndex,
            cancellationToken);
        var candidates = sourceFiles
            .Where(static file => !ShouldIgnoreFile(file.RelativePath))
            .Select(file => CreateCandidate(sourceRootDirectory, file, workspaceIndex, spineGroupIndex))
            .OrderBy(static candidate => candidate.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.AssetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorkspaceBatchReplacementPlanDto(
            workspaceId,
            sourceRootDirectory,
            WorkspaceBatchReplacementMatchingMode.Heuristic,
            ManifestPath: null,
            ManifestWorkspaceKey: null,
            TotalManifestItemCount: 0,
            ChangedCount: candidates.Count(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.HighConfidence),
            UnchangedCount: 0,
            MissingCount: candidates.Count(static candidate => candidate.Diagnostics.Contains("MissingTarget", StringComparer.Ordinal)),
            IssueCount: candidates.Count(static candidate => candidate.MatchStatus is
                WorkspaceBatchReplacementMatchStatus.MediumConfidence or
                WorkspaceBatchReplacementMatchStatus.LowConfidence or
                WorkspaceBatchReplacementMatchStatus.Ambiguous or
                WorkspaceBatchReplacementMatchStatus.Rejected),
            Candidates: candidates);
    }

    private static WorkspaceHeuristicIndex BuildWorkspaceIndex(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId)
    {
        if (!workspaceSessionRegistry.TryGetBundleTree(workspaceId, out var bundleTree))
        {
            return new WorkspaceHeuristicIndex(Array.Empty<BundleIndexEntry>(), Array.Empty<AssetIndexEntry>());
        }

        var bundles = FlattenBundleNodes(bundleTree)
            .Where(static node => node.CanOpenAssets)
            .Select(static node => new BundleIndexEntry(
                node.BundleId,
                node.DisplayName,
                CreateBundleMatchKeys(node)))
            .ToArray();
        var assets = new List<AssetIndexEntry>();
        foreach (var bundle in bundles)
        {
            if (!workspaceSessionRegistry.TryGetAssetList(
                    workspaceId,
                    bundle.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: null,
                    assetTypeDisplayNameFilter: null,
                    out var assetListResult) ||
                assetListResult is null)
            {
                continue;
            }

            foreach (var item in assetListResult.Items)
            {
                assets.Add(new AssetIndexEntry(
                    bundle,
                    item.AssetId,
                    item.Name,
                    item.DisplayName,
                    item.AssetPath,
                    item.AssetType,
                    item.AssetTypeDisplayName,
                    item.PathId,
                    CreateAssetMatchKeys(item)));
            }
        }

        return new WorkspaceHeuristicIndex(bundles, assets);
    }

    private static WorkspaceBatchReplacementCandidateDto CreateCandidate(
        string sourceRootDirectory,
        WorkspaceBatchReplacementSourceFileEntryDto sourceFile,
        WorkspaceHeuristicIndex workspaceIndex,
        SpineGroupHeuristicIndex spineGroupIndex)
    {
        var diagnostics = new List<string>();
        var source = AnalyzeSourceFile(sourceRootDirectory, sourceFile);
        diagnostics.AddRange(source.Diagnostics);
        if (source.IsRejected)
        {
            return CreateRejectedCandidate(sourceRootDirectory, sourceFile, source, diagnostics);
        }

        var bundleMatches = ResolveBundleMatches(source, workspaceIndex, diagnostics);
        if (TryResolveSpineGroupTarget(source, bundleMatches, spineGroupIndex, diagnostics, out var spineTarget))
        {
            return CreateMatchedCandidate(sourceFile, source, diagnostics, spineTarget, WorkspaceBatchReplacementMatchStatus.HighConfidence);
        }

        var scoredTargets = ScoreTargets(source, workspaceIndex, bundleMatches, diagnostics)
            .ApplySpineGroupPreferredScope(source)
            .OrderByDescending(static scored => scored.Score)
            .ThenBy(static scored => scored.Target.Bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static scored => scored.Target.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (scoredTargets.Length == 0)
        {
            diagnostics.Add("MissingTarget");
            return CreateRejectedCandidate(sourceRootDirectory, sourceFile, source, diagnostics);
        }

        var top = scoredTargets[0];
        var second = scoredTargets.Skip(1).FirstOrDefault();
        var isAmbiguous = second is not null &&
            (second.Score == top.Score || top.Score - second.Score < AmbiguousGap);
        var status = ResolveStatus(top.Score, isAmbiguous);
        if (status == WorkspaceBatchReplacementMatchStatus.Ambiguous)
        {
            diagnostics.Add("Ambiguous");
        }

        diagnostics.AddRange(top.Diagnostics);
        return CreateMatchedCandidate(sourceFile, source, diagnostics, top.Target, status);
    }

    private static SourceFileAnalysis AnalyzeSourceFile(
        string sourceRootDirectory,
        WorkspaceBatchReplacementSourceFileEntryDto sourceFile)
    {
        var diagnostics = new List<string>();
        var segments = sourceFile.RelativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length > MaximumDepth)
        {
            diagnostics.Add("IgnoredTooDeep");
            return new SourceFileAnalysis(segments, Path.GetExtension(sourceFile.RelativePath), string.Empty, null, null, null, Array.Empty<AssetType>(), true, diagnostics, false);
        }

        var extension = Path.GetExtension(sourceFile.RelativePath);
        var sourceTypes = ReplacementFileTypeCatalog.ResolveCandidateAssetTypes(sourceFile.RelativePath).ToArray();
        if (sourceTypes.Length == 0)
        {
            diagnostics.Add("UnsupportedType");
            return new SourceFileAnalysis(segments, extension, string.Empty, null, null, null, Array.Empty<AssetType>(), true, diagnostics, false);
        }

        var sourceRoot = CreateSourceRootContext(sourceRootDirectory);
        var isSpineContext = IsSpineContext(sourceRoot, segments);
        var typeSegment = ResolveTypeSegment(sourceRoot, segments);
        var explicitType = ParseAssetType(typeSegment);
        if (explicitType is not null &&
            !sourceTypes.Contains(explicitType.Value))
        {
            diagnostics.Add("TypeConflict");
            return new SourceFileAnalysis(segments, extension, string.Empty, null, typeSegment, null, sourceTypes, true, diagnostics, isSpineContext);
        }

        var name = Path.GetFileNameWithoutExtension(sourceFile.RelativePath);
        var spineGroupName = ResolveSpineGroupName(sourceRoot, segments);
        if (!string.IsNullOrWhiteSpace(spineGroupName))
        {
            diagnostics.Add("SpineGroupInferred");
        }

        return new SourceFileAnalysis(
            segments,
            extension,
            name,
            ResolveBundleSegment(sourceRoot, segments),
            typeSegment,
            spineGroupName,
            explicitType is null ? sourceTypes : [explicitType.Value],
            false,
            diagnostics,
            isSpineContext);
    }

    private static IReadOnlyList<BundleIndexEntry> ResolveBundleMatches(
        SourceFileAnalysis source,
        WorkspaceHeuristicIndex workspaceIndex,
        List<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(source.BundleSegment))
        {
            diagnostics.Add("CrossBundleInferred");
            return Array.Empty<BundleIndexEntry>();
        }

        var bundleKey = NormalizeName(source.BundleSegment);
        var matches = workspaceIndex.Bundles
            .Where(bundle => bundle.MatchKeys.Contains(bundleKey))
            .ToArray();
        if (matches.Length == 1)
        {
            diagnostics.Add("BundleMatched");
            return matches;
        }

        diagnostics.Add(matches.Length > 1 ? "AmbiguousBundle" : "BundleInferred");
        return matches;
    }

    private static IEnumerable<ScoredTarget> ScoreTargets(
        SourceFileAnalysis source,
        WorkspaceHeuristicIndex workspaceIndex,
        IReadOnlyList<BundleIndexEntry> bundleMatches,
        List<string> diagnostics)
    {
        var restrictToBundle = bundleMatches.Count == 1;
        var sourceNameKey = NormalizeName(source.NameWithoutExtension);
        var sourceNameWithoutOrdinal = StripOrdinalSuffix(sourceNameKey);
        var isStandardTypePath = !string.IsNullOrWhiteSpace(source.TypeSegment);
        foreach (var target in workspaceIndex.Assets)
        {
            if (!source.PossibleAssetTypes.Contains(target.AssetType))
            {
                continue;
            }

            if (restrictToBundle && !target.Bundle.BundleId.Equals(bundleMatches[0].BundleId))
            {
                continue;
            }

            var score = 0;
            var evidence = new List<string>();
            if (bundleMatches.Any(bundle => target.Bundle.BundleId.Equals(bundle.BundleId)))
            {
                score += 50;
                evidence.Add("BundleMatched");
            }
            else
            {
                score -= 10;
                evidence.Add("CrossBundleInferred");
            }

            score += isStandardTypePath ? 40 : 10;
            evidence.Add(isStandardTypePath ? "TypeDirectoryMatched" : "TypeInferredFromExtension");

            var exactNameMatch = target.MatchKeys.Contains(sourceNameKey);
            var suffixNameMatch = target.MatchKeysWithoutOrdinal.Contains(sourceNameWithoutOrdinal);
            if (exactNameMatch)
            {
                score += 40;
                evidence.Add("NameMatched");
            }
            else if (suffixNameMatch)
            {
                score += 25;
                evidence.Add("NameMatchedAfterSuffixNormalization");
            }
            else
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(source.SpineGroupName) &&
                target.MatchKeysWithoutOrdinal.Contains(StripOrdinalSuffix(NormalizeName(source.SpineGroupName))))
            {
                score += 15;
                evidence.Add("SpineGroupMatched");
            }

            if (!source.IsSpineContext &&
                source.Segments.Length < 3 &&
                string.IsNullOrWhiteSpace(source.TypeSegment))
            {
                score -= 30;
                evidence.Add("NonStandardDirectory");
            }

            yield return new ScoredTarget(target, score, evidence);
        }

        if (!restrictToBundle && !diagnostics.Contains("CrossBundleInferred", StringComparer.Ordinal))
        {
            diagnostics.Add("CrossBundleInferred");
        }
    }

    private static WorkspaceBatchReplacementMatchStatus ResolveStatus(int score, bool isAmbiguous)
    {
        if (score < LowConfidenceScore)
        {
            return WorkspaceBatchReplacementMatchStatus.Rejected;
        }

        if (isAmbiguous)
        {
            return WorkspaceBatchReplacementMatchStatus.Ambiguous;
        }

        if (score >= HighConfidenceScore)
        {
            return WorkspaceBatchReplacementMatchStatus.HighConfidence;
        }

        return score >= MediumConfidenceScore
            ? WorkspaceBatchReplacementMatchStatus.MediumConfidence
            : WorkspaceBatchReplacementMatchStatus.LowConfidence;
    }

    private static WorkspaceBatchReplacementCandidateDto CreateRejectedCandidate(
        string sourceRootDirectory,
        WorkspaceBatchReplacementSourceFileEntryDto sourceFile,
        SourceFileAnalysis source,
        IReadOnlyList<string> diagnostics)
    {
        var fullPath = string.IsNullOrWhiteSpace(sourceFile.FullPath)
            ? Path.Combine(sourceRootDirectory, sourceFile.RelativePath)
            : sourceFile.FullPath;
        return new WorkspaceBatchReplacementCandidateDto(
            CandidateId: $"heuristic:{sourceFile.RelativePath}:rejected",
            BundleId: null,
            BundleDisplayName: string.Empty,
            AssetId: null,
            PathId: 0,
            AssetType: source.PossibleAssetTypes.FirstOrDefault(),
            AssetTypeDisplayName: null,
            AssetName: string.IsNullOrWhiteSpace(source.NameWithoutExtension)
                ? Path.GetFileNameWithoutExtension(sourceFile.RelativePath)
                : source.NameWithoutExtension,
            ContainerPath: null,
            RelativePath: sourceFile.RelativePath,
            FullPath: fullPath,
            CurrentFileSizeInBytes: sourceFile.SizeInBytes,
            CurrentFileHashAlgorithm: null,
            CurrentFileHash: null,
            ExportedFileSizeInBytes: 0,
            ExportedFileHashAlgorithm: string.Empty,
            ExportedFileHash: string.Empty,
            SpineGroupId: source.SpineGroupName,
            SpineGroupDisplayName: source.SpineGroupName,
            OutputRole: source.OutputRole,
            MatchStatus: WorkspaceBatchReplacementMatchStatus.Rejected,
            MatchMode: WorkspaceBatchReplacementMatchingMode.Heuristic,
            IsDefaultSelected: false,
            Diagnostics: diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<BundleTreeNodeDto> FlattenBundleNodes(IEnumerable<BundleTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenBundleNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static bool ShouldIgnoreFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return string.Equals(fileName, "_abt-export-manifest.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("~", StringComparison.Ordinal) ||
               fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static AssetType? ParseAssetType(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out AssetType parsed) ? parsed : null;

    private static IReadOnlySet<string> CreateBundleMatchKeys(BundleTreeNodeDto node)
    {
        var keys = new HashSet<string>(OrdinalIgnoreCase)
        {
            NormalizeName(node.DisplayName),
            NormalizeName(Path.GetFileName(node.LogicalPath)),
            NormalizeName(Path.GetFileNameWithoutExtension(node.LogicalPath))
        };
        return keys;
    }

    private static AssetMatchKeys CreateAssetMatchKeys(AssetListItemDto item)
    {
        var keys = new HashSet<string>(OrdinalIgnoreCase);
        AddNameKey(keys, item.Name);
        AddNameKey(keys, item.DisplayName);
        AddNameKey(keys, item.ExportFileName);
        AddNameKey(keys, Path.GetFileNameWithoutExtension(item.AssetPath));

        var withoutOrdinal = keys
            .Select(StripOrdinalSuffix)
            .ToHashSet(OrdinalIgnoreCase);
        return new AssetMatchKeys(keys, withoutOrdinal);
    }

    private static void AddNameKey(HashSet<string> keys, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            keys.Add(NormalizeName(Path.GetFileNameWithoutExtension(value.Trim())));
        }
    }

    private static string ResolveTargetDisplayName(AssetIndexEntry target) =>
        string.IsNullOrWhiteSpace(target.DisplayName)
            ? target.Name
            : target.DisplayName!;

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Path.GetFileNameWithoutExtension(value.Trim())
            .Replace('\\', '/')
            .Trim()
            .ToLowerInvariant();
    }

    private static string StripOrdinalSuffix(string value) =>
        OrdinalSuffixRegex().Replace(value, string.Empty).Trim();

    [GeneratedRegex(@"\s\[\d+\]$", RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalSuffixRegex();
}
