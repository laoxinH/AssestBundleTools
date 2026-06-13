using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 基于批量导出 manifest 生成工作区级批量替换计划。
/// </summary>
public sealed class GetWorkspaceBatchReplacementPlanHandler : IQueryHandler<GetWorkspaceBatchReplacementPlanQuery, Result<WorkspaceBatchReplacementPlanDto>>
{
    private const string ManifestFileName = "_abt-export-manifest.json";
    private const string Md5Algorithm = "MD5";
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceExportManifestStore _manifestStore;
    private readonly IWorkspaceBatchReplacementSourceFileProbe _sourceFileProbe;
    private readonly IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? _loadSpineAssetGroupsHandler;

    public GetWorkspaceBatchReplacementPlanHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceExportManifestStore manifestStore,
        IWorkspaceBatchReplacementSourceFileProbe sourceFileProbe,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        _sourceFileProbe = sourceFileProbe ?? throw new ArgumentNullException(nameof(sourceFileProbe));
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler;
    }

    public async Task<Result<WorkspaceBatchReplacementPlanDto>> HandleAsync(
        GetWorkspaceBatchReplacementPlanQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchReplacementPlanDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Result<WorkspaceBatchReplacementPlanDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Result<WorkspaceBatchReplacementPlanDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId));
        }

        if (string.IsNullOrWhiteSpace(query.SourceRootDirectory))
        {
            return Result<WorkspaceBatchReplacementPlanDto>.Failure(ApplicationErrors.BatchReplacementSourceDirectoryRequired());
        }

        var sourceRootDirectory = query.SourceRootDirectory.Trim();
        var manifestPath = Path.Combine(sourceRootDirectory, ManifestFileName);
        try
        {
            if (query.MatchingMode == WorkspaceBatchReplacementMatchingMode.Heuristic)
            {
                return Result<WorkspaceBatchReplacementPlanDto>.Success(await WorkspaceBatchReplacementHeuristicPlanner.BuildPlanAsync(
                    _workspaceSessionRegistry,
                    _loadSpineAssetGroupsHandler,
                    _sourceFileProbe,
                    query.WorkspaceId,
                    sourceRootDirectory,
                    cancellationToken));
            }

            var manifestResult = await _manifestStore.LoadAsync(manifestPath, cancellationToken);
            if (manifestResult.IsCancelled)
            {
                return Result<WorkspaceBatchReplacementPlanDto>.Cancelled();
            }

            if (!manifestResult.IsSuccess || manifestResult.Value is null)
            {
                if (query.MatchingMode == WorkspaceBatchReplacementMatchingMode.Auto)
                {
                    return Result<WorkspaceBatchReplacementPlanDto>.Success(await WorkspaceBatchReplacementHeuristicPlanner.BuildPlanAsync(
                        _workspaceSessionRegistry,
                        _loadSpineAssetGroupsHandler,
                        _sourceFileProbe,
                        query.WorkspaceId,
                        sourceRootDirectory,
                        cancellationToken));
                }

                return Result<WorkspaceBatchReplacementPlanDto>.Failure(manifestResult.Errors);
            }

            var manifest = manifestResult.Value;
            if (!string.IsNullOrWhiteSpace(query.WorkspaceKey) &&
                !string.Equals(manifest.WorkspaceKey, query.WorkspaceKey.Trim(), StringComparison.Ordinal))
            {
                return Result<WorkspaceBatchReplacementPlanDto>.Success(
                    CreateManifestIssuePlan(
                        query,
                        sourceRootDirectory,
                        manifestPath,
                        manifest.WorkspaceKey,
                        manifest.Items.Count,
                        "WorkspaceKeyMismatch"));
            }

            var candidates = new List<WorkspaceBatchReplacementCandidateDto>(manifest.Items.Count);
            foreach (var item in manifest.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = await CreateManifestCandidateAsync(
                    query,
                    sourceRootDirectory,
                    item,
                    cancellationToken);
                if (query.IncludeUnchanged || candidate.MatchStatus != WorkspaceBatchReplacementMatchStatus.Unchanged)
                {
                    candidates.Add(candidate);
                }
            }

            return Result<WorkspaceBatchReplacementPlanDto>.Success(CreatePlan(
                query,
                sourceRootDirectory,
                WorkspaceBatchReplacementMatchingMode.Manifest,
                manifestPath,
                manifest.WorkspaceKey,
                manifest.Items.Count,
                candidates));
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchReplacementPlanDto>.Cancelled();
        }
    }

    private async Task<WorkspaceBatchReplacementCandidateDto> CreateManifestCandidateAsync(
        GetWorkspaceBatchReplacementPlanQuery query,
        string sourceRootDirectory,
        WorkspaceExportManifestItemDto item,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var status = WorkspaceBatchReplacementMatchStatus.Changed;
        var shouldComputeHash = string.Equals(item.ExportedFileHashAlgorithm, Md5Algorithm, StringComparison.OrdinalIgnoreCase);
        var snapshotResult = await _sourceFileProbe.GetSnapshotAsync(
            sourceRootDirectory,
            item.RelativePath,
            computeHash: false,
            cancellationToken);
        WorkspaceBatchReplacementSourceFileSnapshotDto? snapshot = snapshotResult.Value;
        if (!snapshotResult.IsSuccess || snapshot is null || !snapshot.Exists)
        {
            status = WorkspaceBatchReplacementMatchStatus.Missing;
            diagnostics.Add("Missing");
        }
        else if (snapshot.SizeInBytes == item.ExportedFileSizeInBytes)
        {
            if (!shouldComputeHash)
            {
                status = WorkspaceBatchReplacementMatchStatus.HashUnavailable;
                diagnostics.Add("HashUnsupported");
            }
            else
            {
                snapshotResult = await _sourceFileProbe.GetSnapshotAsync(
                    sourceRootDirectory,
                    item.RelativePath,
                    computeHash: true,
                    cancellationToken);
                snapshot = snapshotResult.Value ?? snapshot;
                if (!snapshotResult.IsSuccess || string.IsNullOrWhiteSpace(snapshot.Hash))
                {
                    status = WorkspaceBatchReplacementMatchStatus.HashUnavailable;
                    diagnostics.Add("HashUnavailable");
                }
                else
                {
                    status = string.Equals(snapshot.Hash, item.ExportedFileHash, StringComparison.OrdinalIgnoreCase)
                        ? WorkspaceBatchReplacementMatchStatus.Unchanged
                        : WorkspaceBatchReplacementMatchStatus.Changed;
                }
            }
        }

        BundleId? bundleId = null;
        AssetId? assetId = null;
        var assetType = ParseAssetType(item.AssetType);
        var assetName = item.DisambiguatedName;
        if (BundleId.TryParse(item.BundleId, out var parsedBundleId) &&
            AssetId.TryParse(item.AssetId, out var parsedAssetId))
        {
            bundleId = parsedBundleId;
            assetId = parsedAssetId;
            if (!_workspaceSessionRegistry.TryGetAssetDetails(
                    query.WorkspaceId,
                    parsedBundleId,
                    parsedAssetId,
                    out var details) ||
                details is null)
            {
                status = WorkspaceBatchReplacementMatchStatus.TargetMissing;
                diagnostics.Add("TargetMissing");
            }
            else
            {
                assetType = details.AssetType;
                assetName = details.Name;
                if (!IsManifestTypeCompatible(item.AssetType, details.AssetType))
                {
                    status = WorkspaceBatchReplacementMatchStatus.TypeMismatch;
                    diagnostics.Add("TypeMismatch");
                }
            }
        }
        else
        {
            status = WorkspaceBatchReplacementMatchStatus.ManifestInvalid;
            diagnostics.Add("InvalidTargetIdentity");
        }

        var fullPath = snapshot?.FullPath ?? Path.Combine(sourceRootDirectory, item.RelativePath);
        return new WorkspaceBatchReplacementCandidateDto(
            CandidateId: $"{item.BundleId}:{item.AssetId}:{item.OutputRole}:{item.RelativePath}",
            BundleId: bundleId,
            BundleDisplayName: item.BundleDisplayName,
            AssetId: assetId,
            PathId: item.PathId,
            AssetType: assetType,
            AssetTypeDisplayName: item.AssetTypeDisplayName,
            AssetName: assetName,
            ContainerPath: item.ContainerPath,
            RelativePath: item.RelativePath,
            FullPath: fullPath,
            CurrentFileSizeInBytes: snapshot?.SizeInBytes,
            CurrentFileHashAlgorithm: snapshot?.HashAlgorithm,
            CurrentFileHash: snapshot?.Hash,
            ExportedFileSizeInBytes: item.ExportedFileSizeInBytes,
            ExportedFileHashAlgorithm: item.ExportedFileHashAlgorithm,
            ExportedFileHash: item.ExportedFileHash,
            SpineGroupId: item.SpineGroupId,
            SpineGroupDisplayName: item.SpineGroupDisplayName,
            OutputRole: item.OutputRole.ToString(),
            MatchStatus: status,
            MatchMode: WorkspaceBatchReplacementMatchingMode.Manifest,
            IsDefaultSelected: status == WorkspaceBatchReplacementMatchStatus.Changed,
            Diagnostics: diagnostics);
    }

    private static WorkspaceBatchReplacementPlanDto CreateManifestIssuePlan(
        GetWorkspaceBatchReplacementPlanQuery query,
        string sourceRootDirectory,
        string manifestPath,
        string manifestWorkspaceKey,
        int manifestItemCount,
        string diagnostic) =>
        new(
            query.WorkspaceId,
            sourceRootDirectory,
            WorkspaceBatchReplacementMatchingMode.Manifest,
            manifestPath,
            manifestWorkspaceKey,
            manifestItemCount,
            ChangedCount: 0,
            UnchangedCount: 0,
            MissingCount: 0,
            IssueCount: 1,
            Candidates:
            [
                new WorkspaceBatchReplacementCandidateDto(
                    CandidateId: "manifest-issue",
                    BundleId: null,
                    BundleDisplayName: string.Empty,
                    AssetId: null,
                    PathId: 0,
                    AssetType: AssetType.Unknown,
                    AssetTypeDisplayName: null,
                    AssetName: string.Empty,
                    ContainerPath: null,
                    RelativePath: string.Empty,
                    FullPath: manifestPath,
                    CurrentFileSizeInBytes: null,
                    CurrentFileHashAlgorithm: null,
                    CurrentFileHash: null,
                    ExportedFileSizeInBytes: 0,
                    ExportedFileHashAlgorithm: string.Empty,
                    ExportedFileHash: string.Empty,
                    SpineGroupId: null,
                    SpineGroupDisplayName: null,
                    OutputRole: string.Empty,
                    MatchStatus: WorkspaceBatchReplacementMatchStatus.ManifestInvalid,
                    MatchMode: WorkspaceBatchReplacementMatchingMode.Manifest,
                    IsDefaultSelected: false,
                    Diagnostics: [diagnostic])
            ]);

    private static WorkspaceBatchReplacementPlanDto CreatePlan(
        GetWorkspaceBatchReplacementPlanQuery query,
        string sourceRootDirectory,
        WorkspaceBatchReplacementMatchingMode matchingMode,
        string? manifestPath,
        string? manifestWorkspaceKey,
        int totalManifestItemCount,
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto> candidates) =>
        new(
            query.WorkspaceId,
            sourceRootDirectory,
            matchingMode,
            manifestPath,
            manifestWorkspaceKey,
            totalManifestItemCount,
            ChangedCount: candidates.Count(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.Changed),
            UnchangedCount: candidates.Count(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.Unchanged),
            MissingCount: candidates.Count(static candidate => candidate.MatchStatus == WorkspaceBatchReplacementMatchStatus.Missing),
            IssueCount: candidates.Count(static candidate => candidate.MatchStatus is not WorkspaceBatchReplacementMatchStatus.Changed and not WorkspaceBatchReplacementMatchStatus.Unchanged),
            candidates);

    private static AssetType ParseAssetType(string assetType) =>
        Enum.TryParse(assetType, ignoreCase: true, out AssetType parsed)
            ? parsed
            : AssetType.Unknown;

    private static bool IsManifestTypeCompatible(string manifestAssetType, AssetType targetType) =>
        ParseAssetType(manifestAssetType) == targetType ||
        targetType == AssetType.Unknown;
}
