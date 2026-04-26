using System.IO;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 将工作区导入入口收集到的外部来源登记到轻量元数据中。
/// </summary>
/// <remarks>
/// 当前阶段这里只负责“登记来源描述”，
/// 后续真实 AB 接入时再由读取链路把解析结果写入缓存层。
/// </remarks>
public sealed class RegisterWorkspaceImportHandler
    : ICommandHandler<RegisterWorkspaceImportCommand, Result<WorkspaceImportRegistrationResultDto>>
{
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;

    public RegisterWorkspaceImportHandler(IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory
            ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
    }

    public Task<Result<WorkspaceImportRegistrationResultDto>> HandleAsync(
        RegisterWorkspaceImportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Failure(
                ApplicationErrors.WorkspaceImportWorkspaceKeyRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Failure(
                ApplicationErrors.WorkspaceImportDataRootPathRequired()));
        }

        var normalizedPaths = NormalizePaths(command.Paths);
        if (normalizedPaths.Length == 0)
        {
            return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Failure(
                ApplicationErrors.WorkspaceImportPathsRequired()));
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var existingMetadata = metadataStore.Load(command.WorkspaceKey);
        var recordedAtUtc = DateTimeOffset.UtcNow;
        var mergedSources = new Dictionary<string, WorkspaceImportedSourceDto>(StringComparer.OrdinalIgnoreCase);
        var excludedBundleSourcePaths = new HashSet<string>(
            (existingMetadata?.ExcludedBundleSourcePaths ?? Array.Empty<string>())
            .Select(WorkspaceImportPathUtility.NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var existingSource in existingMetadata?.ImportedSources ?? Array.Empty<WorkspaceImportedSourceDto>())
        {
            var normalizedExistingPath = WorkspaceImportPathUtility.NormalizePath(existingSource.Path);
            if (string.IsNullOrWhiteSpace(normalizedExistingPath))
            {
                continue;
            }

            mergedSources[normalizedExistingPath] = existingSource with
            {
                Path = normalizedExistingPath
            };
        }

        var addedPathCount = 0;
        var updatedPathCount = 0;
        var fileCount = 0;
        var directoryCount = 0;

        foreach (var normalizedPath in normalizedPaths)
        {
            excludedBundleSourcePaths.Remove(normalizedPath);
            var pathKind = ResolvePathKind(normalizedPath);
            if (pathKind == WorkspaceImportPathKind.File)
            {
                fileCount++;
            }
            else if (pathKind == WorkspaceImportPathKind.Directory)
            {
                directoryCount++;
            }

            var importedSource = new WorkspaceImportedSourceDto(
                Path: normalizedPath,
                ImportOrigin: command.ImportOrigin,
                PathKind: pathKind,
                RecordedAtUtc: recordedAtUtc);

            if (mergedSources.ContainsKey(normalizedPath))
            {
                updatedPathCount++;
            }
            else
            {
                addedPathCount++;
            }

            mergedSources[normalizedPath] = importedSource;
        }

        var orderedSources = mergedSources.Values
            .OrderByDescending(static source => source.RecordedAtUtc)
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: command.WorkspaceKey.Trim(),
            DisplayName: ResolveDisplayName(command.WorkspaceDisplayName, existingMetadata),
            SourcePath: ResolveSourcePath(command.WorkspaceSourcePath, existingMetadata),
            CreatedAtUtc: existingMetadata?.CreatedAtUtc ?? recordedAtUtc,
            LastOpenedAtUtc: existingMetadata?.LastOpenedAtUtc ?? recordedAtUtc,
            LastBundleId: existingMetadata?.LastBundleId,
            LastAssetId: existingMetadata?.LastAssetId,
            LastOutputDirectory: existingMetadata?.LastOutputDirectory,
            BundleSearchKeyword: existingMetadata?.BundleSearchKeyword ?? string.Empty,
            AssetSearchKeyword: existingMetadata?.AssetSearchKeyword ?? string.Empty,
            AssetTypeFilter: existingMetadata?.AssetTypeFilter,
            ShowModifiedOnly: existingMetadata?.ShowModifiedOnly ?? false,
            ShowPreviewableOnly: existingMetadata?.ShowPreviewableOnly ?? false,
            OpenBundleTabs: existingMetadata?.OpenBundleTabs ?? Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: existingMetadata?.ActiveBundleTabId,
            ImportedSources: orderedSources,
            AssetBundleDecryptorId: existingMetadata?.AssetBundleDecryptorId,
            ExcludedBundleSourcePaths: excludedBundleSourcePaths.ToArray());

        metadataStore.Save(metadata);

        var result = new WorkspaceImportRegistrationResultDto(
            WorkspaceKey: metadata.WorkspaceKey,
            WorkspaceDisplayName: metadata.DisplayName,
            ImportOrigin: command.ImportOrigin,
            RequestedPathCount: normalizedPaths.Length,
            AddedPathCount: addedPathCount,
            UpdatedPathCount: updatedPathCount,
            FileCount: fileCount,
            DirectoryCount: directoryCount,
            TotalRegisteredSourceCount: orderedSources.Length,
            RecordedAtUtc: recordedAtUtc);

        return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Success(result));
    }

    private static string ResolveDisplayName(string? workspaceDisplayName, WorkspaceMetadataDto? existingMetadata)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDisplayName))
        {
            return workspaceDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existingMetadata?.DisplayName))
        {
            return existingMetadata.DisplayName;
        }

        return string.Empty;
    }

    private static string ResolveSourcePath(string? workspaceSourcePath, WorkspaceMetadataDto? existingMetadata)
    {
        if (!string.IsNullOrWhiteSpace(existingMetadata?.SourcePath))
        {
            return existingMetadata.SourcePath;
        }

        return string.IsNullOrWhiteSpace(workspaceSourcePath)
            ? string.Empty
            : workspaceSourcePath.Trim();
    }

    private static string[] NormalizePaths(IReadOnlyList<string> paths)
    {
        return paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(WorkspaceImportPathUtility.NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkspaceImportPathKind ResolvePathKind(string path)
    {
        if (File.Exists(path))
        {
            return WorkspaceImportPathKind.File;
        }

        if (Directory.Exists(path))
        {
            return WorkspaceImportPathKind.Directory;
        }

        return WorkspaceImportPathKind.Unknown;
    }
}
