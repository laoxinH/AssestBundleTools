using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 执行由 manifest 或启发式匹配生成的工作区级批量替换计划。
/// </summary>
public sealed class ExecuteWorkspaceBatchReplacementHandler : ICommandHandler<ExecuteWorkspaceBatchReplacementCommand, Result<WorkspaceBatchReplacementResultDto>>
{
    private const string BatchRelationPrefix = "batch-replacement";
    private const string SpineRelationPrefix = "spine-group-replacement";

    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly ReplaceAssetFromFileHandler _replaceAssetFromFileHandler;
    private readonly IWorkspaceBatchReplacementSourceFileProbe _sourceFileProbe;

    public ExecuteWorkspaceBatchReplacementHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        ReplaceAssetFromFileHandler replaceAssetFromFileHandler,
        IWorkspaceBatchReplacementSourceFileProbe sourceFileProbe)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _replaceAssetFromFileHandler = replaceAssetFromFileHandler ?? throw new ArgumentNullException(nameof(replaceAssetFromFileHandler));
        _sourceFileProbe = sourceFileProbe ?? throw new ArgumentNullException(nameof(sourceFileProbe));
    }

    public async Task<Result<WorkspaceBatchReplacementResultDto>> HandleAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchReplacementResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<WorkspaceBatchReplacementResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<WorkspaceBatchReplacementResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        var candidates = (command.Candidates ?? Array.Empty<WorkspaceBatchReplacementCandidateDto>())
            .Where(IsUserExecutableCandidate)
            .OrderBy(static candidate => string.IsNullOrWhiteSpace(candidate.SpineGroupId) ? 0 : 1)
            .ThenBy(static candidate => candidate.SpineGroupDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.OutputRole, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => GetExecutionOrder(candidate.AssetType))
            .ThenBy(static candidate => candidate.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 0)
        {
            return Result<WorkspaceBatchReplacementResultDto>.Failure(ApplicationErrors.NoBatchReplacementCandidates(command.WorkspaceId));
        }

        var batchId = $"{BatchRelationPrefix}:{Guid.NewGuid():N}";
        var itemResults = new List<WorkspaceBatchReplacementItemResultDto>(candidates.Length);
        ReportProgress(command, WorkspaceBatchReplacementProgressKind.Preparing, 0, candidates.Length, itemResults);

        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            var result = await ExecuteCandidateAsync(command, candidate, batchId, candidates, cancellationToken);
            itemResults.Add(result);
            ReportProgress(command, CreateProgressKind(candidate), index + 1, candidates.Length, itemResults, candidate);
        }

        var replacedCount = itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Replaced);
        var skippedCount = itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Skipped);
        var failedCount = itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Failed);
        var processedBundleCount = itemResults
            .Where(static item => item.Status == WorkspaceBatchReplacementItemStatus.Replaced)
            .Select(static item => item.BundleId?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var processedSpineGroupCount = itemResults
            .Where(static item => item.Status == WorkspaceBatchReplacementItemStatus.Replaced)
            .Select(static item => item.SpineGroupId)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();

        return Result<WorkspaceBatchReplacementResultDto>.Success(
            new WorkspaceBatchReplacementResultDto(
                command.WorkspaceId,
                command.SourceRootDirectory,
                command.MatchingMode,
                candidates.Length,
                replacedCount,
                skippedCount,
                failedCount,
                processedBundleCount,
                processedSpineGroupCount,
                DateTimeOffset.UtcNow,
                itemResults));
    }

    private async Task<WorkspaceBatchReplacementItemResultDto> ExecuteCandidateAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        WorkspaceBatchReplacementCandidateDto candidate,
        string batchId,
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto> allCandidates,
        CancellationToken cancellationToken)
    {
        if (candidate.BundleId is null || candidate.AssetId is null)
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, "target-missing");
        }

        if (!IsSupportedReplacementPayload(candidate))
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, "unsupported-payload");
        }

        var sourceSnapshotResult = await _sourceFileProbe.GetSnapshotAsync(
            command.SourceRootDirectory,
            candidate.RelativePath,
            computeHash: false,
            cancellationToken);
        if (sourceSnapshotResult.IsCancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!sourceSnapshotResult.IsSuccess)
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Failed, sourceSnapshotResult.Errors.FirstOrDefault()?.Code);
        }

        if (sourceSnapshotResult.Value?.Exists != true)
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, "source-missing");
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, candidate.BundleId.Value, candidate.AssetId.Value, out var assetDetails) ||
            assetDetails is null)
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, "target-missing");
        }

        if (!TryResolveExecutionTarget(command.WorkspaceId, candidate, assetDetails, out var executionTarget, out var targetDiagnosticCode))
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, targetDiagnosticCode);
        }

        if (!executionTarget.Details.CanReplaceFromFile)
        {
            return CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Skipped, "target-not-replaceable");
        }

        var replaceResult = await _replaceAssetFromFileHandler.HandleAsync(
            new ReplaceAssetFromFileCommand(
                command.WorkspaceId,
                executionTarget.BundleId,
                executionTarget.AssetId,
                candidate.FullPath,
                executionTarget.AssetType,
                command.WorkspaceKey,
                command.WorkspaceDataRootPath,
                CreateOperationContext(batchId, candidate, allCandidates)),
            cancellationToken);

        if (replaceResult.IsCancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return replaceResult.IsSuccess
            ? CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Replaced)
            : CreateItemResult(candidate, WorkspaceBatchReplacementItemStatus.Failed, replaceResult.Errors.FirstOrDefault()?.Code);
    }

    private static ReplacementOperationContextDto CreateOperationContext(
        string batchId,
        WorkspaceBatchReplacementCandidateDto candidate,
        IReadOnlyList<WorkspaceBatchReplacementCandidateDto> allCandidates)
    {
        var relations = new List<WorkspaceOperationRelationDto>
        {
            new(
                RelationId: batchId,
                RelationKind: WorkspaceOperationRelationNames.LinkedResourceOperation,
                Role: WorkspaceOperationRelationNames.SubjectRole)
        };

        if (!string.IsNullOrWhiteSpace(candidate.SpineGroupId))
        {
            var relationId = $"{SpineRelationPrefix}:{candidate.SpineGroupId.Trim()}";
            foreach (var groupCandidate in allCandidates
                         .Where(other => string.Equals(other.SpineGroupId, candidate.SpineGroupId, StringComparison.Ordinal))
                         .Where(static other => other.BundleId is not null && other.AssetId is not null))
            {
                relations.Add(new WorkspaceOperationRelationDto(
                    RelationId: relationId,
                    RelationKind: WorkspaceOperationRelationNames.LinkedResourceOperation,
                    Role: WorkspaceOperationRelationNames.SubjectRole,
                    RelatedTarget: new WorkspaceOperationTargetDto(
                        BundleId: groupCandidate.BundleId!.Value.Value,
                        AssetId: groupCandidate.AssetId!.Value.Value,
                        AssetName: groupCandidate.AssetName,
                        AssetType: groupCandidate.AssetType.ToString(),
                        AssetTypeDisplayName: groupCandidate.AssetTypeDisplayName,
                        PathId: groupCandidate.PathId)));
            }
        }

        return new ReplacementOperationContextDto(batchId, relations);
    }

    private static bool IsUserExecutableCandidate(WorkspaceBatchReplacementCandidateDto candidate) =>
        candidate.MatchStatus is
            WorkspaceBatchReplacementMatchStatus.Changed or
            WorkspaceBatchReplacementMatchStatus.HighConfidence or
            WorkspaceBatchReplacementMatchStatus.MediumConfidence or
            WorkspaceBatchReplacementMatchStatus.LowConfidence;

    private bool TryResolveExecutionTarget(
        WorkspaceId workspaceId,
        WorkspaceBatchReplacementCandidateDto candidate,
        AssetDetailsDto candidateDetails,
        out ReplacementExecutionTarget executionTarget,
        out string? diagnosticCode)
    {
        if (candidate.AssetType != AssetType.Sprite)
        {
            executionTarget = new ReplacementExecutionTarget(
                candidate.BundleId!.Value,
                candidate.AssetId!.Value,
                candidate.AssetType,
                candidateDetails);
            diagnosticCode = null;
            return true;
        }

        var textureDependencies = ResolveSpriteTextureDependencies(candidateDetails);
        if (textureDependencies.Count == 0)
        {
            executionTarget = default;
            diagnosticCode = "sprite-texture-target-missing";
            return false;
        }

        if (textureDependencies.Count > 1)
        {
            executionTarget = default;
            diagnosticCode = "sprite-texture-target-ambiguous";
            return false;
        }

        var texture = textureDependencies[0];
        if (!_workspaceSessionRegistry.TryGetAssetDetails(workspaceId, texture.BundleId, texture.AssetId, out var textureDetails) ||
            textureDetails is null)
        {
            executionTarget = default;
            diagnosticCode = "sprite-texture-target-missing";
            return false;
        }

        if (textureDetails.AssetType != AssetType.Texture2D)
        {
            executionTarget = default;
            diagnosticCode = "sprite-texture-target-type-mismatch";
            return false;
        }

        executionTarget = new ReplacementExecutionTarget(texture.BundleId, texture.AssetId, AssetType.Texture2D, textureDetails);
        diagnosticCode = null;
        return true;
    }

    private static IReadOnlyList<AssetDependencyDto> ResolveSpriteTextureDependencies(AssetDetailsDto details)
    {
        var textureAssociations = (details.TextureAssociations ?? Array.Empty<AssetDependencyDto>())
            .Where(static dependency => dependency.AssetType == AssetType.Texture2D)
            .ToArray();
        var dependencies = textureAssociations.Length > 0
            ? textureAssociations
            : details.Dependencies.Where(static dependency => dependency.AssetType == AssetType.Texture2D);

        return dependencies
            .GroupBy(static dependency => (dependency.BundleId.Value, dependency.AssetId.Value))
            .Select(static group => group.First())
            .ToArray();
    }

    private static bool IsSupportedReplacementPayload(WorkspaceBatchReplacementCandidateDto candidate)
    {
        var extension = Path.GetExtension(candidate.FullPath);
        return candidate.AssetType switch
        {
            AssetType.Texture2D or AssetType.Sprite => string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase),
            AssetType.TextAsset => extension is not null && (
                string.Equals(extension, ".atlas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".skel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)),
            AssetType.AudioClip => extension is not null && (
                string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static int GetExecutionOrder(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => 0,
            AssetType.Sprite => 1,
            _ => 2
        };

    private static WorkspaceBatchReplacementProgressKind CreateProgressKind(WorkspaceBatchReplacementCandidateDto candidate) =>
        string.IsNullOrWhiteSpace(candidate.SpineGroupId)
            ? WorkspaceBatchReplacementProgressKind.Asset
            : WorkspaceBatchReplacementProgressKind.SpineGroup;

    private static WorkspaceBatchReplacementItemResultDto CreateItemResult(
        WorkspaceBatchReplacementCandidateDto candidate,
        WorkspaceBatchReplacementItemStatus status,
        string? diagnosticCode = null) =>
        new(
            candidate.CandidateId,
            candidate.BundleId,
            candidate.AssetId,
            candidate.AssetType,
            candidate.AssetName,
            candidate.FullPath,
            candidate.SpineGroupId,
            candidate.SpineGroupDisplayName,
            status,
            diagnosticCode);

    private static void ReportProgress(
        ExecuteWorkspaceBatchReplacementCommand command,
        WorkspaceBatchReplacementProgressKind kind,
        int completedCount,
        int totalCount,
        IReadOnlyList<WorkspaceBatchReplacementItemResultDto> itemResults,
        WorkspaceBatchReplacementCandidateDto? candidate = null)
    {
        command.Progress?.Report(new WorkspaceBatchReplacementProgressDto(
            kind,
            completedCount,
            totalCount,
            itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Replaced),
            itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Skipped),
            itemResults.Count(static item => item.Status == WorkspaceBatchReplacementItemStatus.Failed),
            candidate?.BundleDisplayName,
            candidate?.AssetName,
            candidate?.AssetType,
            candidate?.AssetTypeDisplayName,
            candidate?.SpineGroupDisplayName));
    }

    private readonly record struct ReplacementExecutionTarget(
        BundleId BundleId,
        AssetId AssetId,
        AssetType AssetType,
        AssetDetailsDto Details);
}
