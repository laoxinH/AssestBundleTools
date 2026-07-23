using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 执行 Spine 资源组整组提取。
/// </summary>
public sealed class ExtractSpineAssetGroupHandler : ICommandHandler<ExtractSpineAssetGroupCommand, Result<SpineAssetGroupExtractionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IQueryHandler<LoadSpineAssetGroupDetailsQuery, Result<SpineAssetGroupDetailsDto>> _loadSpineAssetGroupDetailsHandler;
    private readonly IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? _loadSpineAssetGroupsHandler;
    private readonly ExtractAssetHandler _extractAssetHandler;
    private readonly WorkspaceExtractionOperationRecorder? _operationRecorder;

    public ExtractSpineAssetGroupHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<LoadSpineAssetGroupDetailsQuery, Result<SpineAssetGroupDetailsDto>> loadSpineAssetGroupDetailsHandler,
        ExtractAssetHandler extractAssetHandler,
        WorkspaceExtractionOperationRecorder? operationRecorder = null,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _loadSpineAssetGroupDetailsHandler = loadSpineAssetGroupDetailsHandler ?? throw new ArgumentNullException(nameof(loadSpineAssetGroupDetailsHandler));
        _extractAssetHandler = extractAssetHandler ?? throw new ArgumentNullException(nameof(extractAssetHandler));
        _operationRecorder = operationRecorder;
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler;
    }

    public async Task<Result<SpineAssetGroupExtractionResultDto>> HandleAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<SpineAssetGroupExtractionResultDto>.Cancelled();
        }

        var validationResult = ValidateCommand(command);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var planResult = await ResolveExtractionPlanAsync(command, cancellationToken);
        if (planResult.IsCancelled)
        {
            return Result<SpineAssetGroupExtractionResultDto>.Cancelled();
        }

        if (!planResult.IsSuccess || planResult.Value is null)
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(planResult.Errors);
        }

        var extractionPlan = planResult.Value;
        var plannedParts = extractionPlan.Parts;
        if (plannedParts.Count == 0)
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(
                ApplicationErrors.NoSpineAssetGroupExtractionCandidates(command.GroupId));
        }

        var groupOutputDirectory = BuildGroupOutputDirectory(command.OutputDirectory, extractionPlan.Group);
        var extractedResults = new List<AssetExtractionResultDto>(plannedParts.Count);
        var failures = new List<AppError>();

        foreach (var part in plannedParts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<SpineAssetGroupExtractionResultDto>.Cancelled();
            }

            var extractionResult = await _extractAssetHandler.HandleAsync(
                new ExtractAssetCommand(
                    command.WorkspaceId,
                    part.BundleId,
                    part.AssetId,
                    command.WorkspaceKey,
                    command.WorkspaceDataRootPath,
                    BuildPartSuggestedOutputPath(groupOutputDirectory, part),
                    SpineSkeletonExtensionMode: part.Role == SpineAssetGroupPartRole.Skeleton
                        ? command.UseContentAwareSkeletonFileExtension
                            ? SpineSkeletonExportExtensionMode.ContentAware
                            : SpineSkeletonExportExtensionMode.UniformSkel
                        : SpineSkeletonExportExtensionMode.None),
                cancellationToken);
            if (extractionResult.IsCancelled)
            {
                return Result<SpineAssetGroupExtractionResultDto>.Cancelled();
            }

            if (!extractionResult.IsSuccess || extractionResult.Value is null)
            {
                failures.AddRange(extractionResult.Errors);
                continue;
            }

            extractedResults.Add(extractionResult.Value);
        }

        if (extractedResults.Count == 0)
        {
            return failures.Count == 0
                ? Result<SpineAssetGroupExtractionResultDto>.Failure(
                    ApplicationErrors.NoSpineAssetGroupExtractionCandidates(command.GroupId))
                : Result<SpineAssetGroupExtractionResultDto>.Failure(failures);
        }

        var result = new SpineAssetGroupExtractionResultDto(
            command.WorkspaceId,
            command.BundleId,
            extractionPlan.Group.GroupId,
            extractionPlan.Group.DisplayName,
            groupOutputDirectory,
            extractedResults.Count,
            failures.Count,
            DateTimeOffset.UtcNow,
            extractedResults,
            plannedParts);
        _operationRecorder?.RecordSpineAssetGroupExtraction(command, result);

        return Result<SpineAssetGroupExtractionResultDto>.Success(result);
    }

    private async Task<Result<SpineAssetGroupExtractionPlan>> ResolveExtractionPlanAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken)
    {
        var catalogPlanResult = await TryResolveCatalogExtractionPlanAsync(command, cancellationToken);
        if (catalogPlanResult is not null)
        {
            return catalogPlanResult;
        }

        var detailsResult = await _loadSpineAssetGroupDetailsHandler.HandleAsync(
            new LoadSpineAssetGroupDetailsQuery(
                command.WorkspaceId,
                command.WorkspaceKey,
                command.WorkspaceDataRootPath,
                command.BundleId,
                command.GroupId,
                LoadMode: SpineAssetGroupDetailsLoadMode.ExtractionPlan),
            cancellationToken);
        if (detailsResult.IsCancelled)
        {
            return Result<SpineAssetGroupExtractionPlan>.Cancelled();
        }

        if (!detailsResult.IsSuccess || detailsResult.Value is null)
        {
            return Result<SpineAssetGroupExtractionPlan>.Failure(detailsResult.Errors);
        }

        return Result<SpineAssetGroupExtractionPlan>.Success(
            new SpineAssetGroupExtractionPlan(
                detailsResult.Value.Group,
                ResolveExtractionParts(detailsResult.Value)));
    }

    private async Task<Result<SpineAssetGroupExtractionPlan>?> TryResolveCatalogExtractionPlanAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (_loadSpineAssetGroupsHandler is null)
        {
            return null;
        }

        var groupsResult = await _loadSpineAssetGroupsHandler.HandleAsync(
            new LoadSpineAssetGroupsQuery(
                command.WorkspaceId,
                command.BundleId,
                WorkspaceDataRootPath: command.WorkspaceDataRootPath),
            cancellationToken);
        if (groupsResult.IsCancelled)
        {
            return Result<SpineAssetGroupExtractionPlan>.Cancelled();
        }

        if (!groupsResult.IsSuccess || groupsResult.Value is null)
        {
            return null;
        }

        var group = groupsResult.Value.Items.FirstOrDefault(candidate =>
            string.Equals(candidate.GroupId, command.GroupId, StringComparison.Ordinal));
        if (group is null)
        {
            return null;
        }

        var plannedParts = ResolveCatalogExtractionParts(group);
        return plannedParts.Count == 0
            ? null
            : Result<SpineAssetGroupExtractionPlan>.Success(new SpineAssetGroupExtractionPlan(group, plannedParts));
    }

    private Result<SpineAssetGroupExtractionResultDto>? ValidateCommand(ExtractSpineAssetGroupCommand command)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.WorkspaceCatalogKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.BundleIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.GroupId))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.SpineAssetGroupIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.OutputDirectory))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.AssetExtractionOutputDirectoryRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(command.WorkspaceId, command.BundleId))
        {
            return Result<SpineAssetGroupExtractionResultDto>.Failure(ApplicationErrors.BundleNotFound(command.BundleId));
        }

        return null;
    }

    private static IReadOnlyList<SpineAssetGroupPartDto> ResolveExtractionParts(SpineAssetGroupDetailsDto details)
    {
        var parts = new List<SpineAssetGroupPartDto>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRange(parts, keys, details.Group.SkeletonAssets);
        AddRange(parts, keys, details.Group.AtlasAssets);

        var matchedTextureCount = 0;
        foreach (var atlasPage in details.AtlasPages)
        {
            if (atlasPage.TexturePageAsset is null)
            {
                continue;
            }

            matchedTextureCount++;
            Add(parts, keys, atlasPage.TexturePageAsset);

            var alphaTexturePageAsset = SpineAssetGroupAlphaTexturePageResolver.Resolve(details, atlasPage);
            if (alphaTexturePageAsset is not null)
            {
                Add(parts, keys, alphaTexturePageAsset);
            }
        }

        if (matchedTextureCount == 0)
        {
            AddRange(parts, keys, details.Group.TexturePageAssets);
        }

        return parts;
    }

    private static void AddRange(
        ICollection<SpineAssetGroupPartDto> parts,
        ISet<string> keys,
        IEnumerable<SpineAssetGroupPartDto> candidates)
    {
        foreach (var candidate in candidates)
        {
            Add(parts, keys, candidate);
        }
    }

    private static void Add(
        ICollection<SpineAssetGroupPartDto> parts,
        ISet<string> keys,
        SpineAssetGroupPartDto candidate)
    {
        if (keys.Add(CreatePartKey(candidate)))
        {
            parts.Add(candidate);
        }
    }

    private static IReadOnlyList<SpineAssetGroupPartDto> ResolveCatalogExtractionParts(SpineAssetGroupDto group)
    {
        var parts = new List<SpineAssetGroupPartDto>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRange(parts, keys, group.SkeletonAssets);
        AddRange(parts, keys, group.AtlasAssets);
        AddRange(parts, keys, group.TexturePageAssets);
        return parts;
    }

    private static string CreatePartKey(SpineAssetGroupPartDto part) =>
        string.Concat(part.BundleId.Value, "\n", part.AssetId.Value);

    private static string BuildGroupOutputDirectory(
        string outputDirectory,
        SpineAssetGroupDto group) =>
        Path.Combine(outputDirectory.Trim(), SanitizePathSegment(ResolveGroupFolderName(group)));

    private static string BuildPartSuggestedOutputPath(
        string groupOutputDirectory,
        SpineAssetGroupPartDto part) =>
        Path.Combine(groupOutputDirectory, ResolvePartSuggestedFileName(part));

    private static string ResolveGroupFolderName(SpineAssetGroupDto group)
    {
        if (!string.IsNullOrWhiteSpace(group.DisplayName))
        {
            return group.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(group.GroupKey))
        {
            return group.GroupKey.Trim();
        }

        return group.GroupId.Trim();
    }

    private static string ResolvePartSuggestedFileName(SpineAssetGroupPartDto part)
    {
        var fileName = ResolveFileNameFromPath(part.AssetPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return string.IsNullOrWhiteSpace(part.Name)
            ? part.AssetId.Value
            : part.Name.Trim();
    }

    private static string? ResolveFileNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var fileName = lastSeparatorIndex >= 0
            ? normalizedPath[(lastSeparatorIndex + 1)..]
            : normalizedPath;
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    private static string SanitizePathSegment(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(segment
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "spine-group"
            : sanitized;
    }

    private sealed record SpineAssetGroupExtractionPlan(
        SpineAssetGroupDto Group,
        IReadOnlyList<SpineAssetGroupPartDto> Parts);
}
