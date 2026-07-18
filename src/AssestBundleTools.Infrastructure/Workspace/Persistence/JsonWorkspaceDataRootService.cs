using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于当前 JSON 工作区存储结构实现的数据目录检查与重置服务。
/// </summary>
/// <remarks>
/// 这里统一收口“目录里是否已有工作区元数据”“如何清理该工作区生成数据”的文件系统细节，
/// 让上层工作区向导只处理用户决策，不直接拼装存储路径。
/// </remarks>
public sealed class JsonWorkspaceDataRootService : IWorkspaceDataRootService
{
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public JsonWorkspaceDataRootService(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory
            ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory
            ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public string GetDefaultRootDirectoryPath()
    {
        return _workspaceMetadataStoreFactory.GetDefaultRootDirectoryPath();
    }

    public WorkspaceDataRootInspectionDto Inspect(string? rootDirectoryPath)
    {
        var normalizedRootDirectoryPath = NormalizeRootDirectoryPath(rootDirectoryPath);
        if (string.IsNullOrWhiteSpace(normalizedRootDirectoryPath))
        {
            return new WorkspaceDataRootInspectionDto(
                RootDirectoryPath: string.Empty,
                MatchKind: WorkspaceDataRootMatchKind.None,
                Workspace: null,
                WorkspaceCount: 0);
        }

        var workspaceMetadataEntries = LoadWorkspaceMetadataEntries(normalizedRootDirectoryPath);
        if (workspaceMetadataEntries.Length == 0)
        {
            return new WorkspaceDataRootInspectionDto(
                RootDirectoryPath: normalizedRootDirectoryPath,
                MatchKind: WorkspaceDataRootMatchKind.None,
                Workspace: null,
                WorkspaceCount: 0);
        }

        var workspaceKeyFromDataRootPath = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            sourcePath: string.Empty,
            fallbackPath: normalizedRootDirectoryPath);
        var directlyMatchedWorkspace = workspaceMetadataEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKeyFromDataRootPath, StringComparison.Ordinal));
        if (directlyMatchedWorkspace is not null)
        {
            return new WorkspaceDataRootInspectionDto(
                RootDirectoryPath: normalizedRootDirectoryPath,
                MatchKind: WorkspaceDataRootMatchKind.MatchedByDataRootKey,
                Workspace: directlyMatchedWorkspace,
                WorkspaceCount: workspaceMetadataEntries.Length);
        }

        if (workspaceMetadataEntries.Length == 1)
        {
            return new WorkspaceDataRootInspectionDto(
                RootDirectoryPath: normalizedRootDirectoryPath,
                MatchKind: WorkspaceDataRootMatchKind.SingleWorkspaceDetected,
                Workspace: workspaceMetadataEntries[0],
                WorkspaceCount: 1);
        }

        return new WorkspaceDataRootInspectionDto(
            RootDirectoryPath: normalizedRootDirectoryPath,
            MatchKind: WorkspaceDataRootMatchKind.AmbiguousMultipleWorkspaces,
            Workspace: null,
            WorkspaceCount: workspaceMetadataEntries.Length);
    }

    /// <summary>
    /// 重置工作区时只清理该工作区键归属的工具生成数据，
    /// 保留用户选择的数据目录本身和可能存在的其他目录内容。
    /// </summary>
    public void ResetWorkspace(string? rootDirectoryPath, string workspaceKey)
    {
        var normalizedRootDirectoryPath = NormalizeRootDirectoryPath(rootDirectoryPath);
        if (string.IsNullOrWhiteSpace(normalizedRootDirectoryPath) ||
            string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(normalizedRootDirectoryPath);
        metadataStore.Delete(workspaceKey);

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(normalizedRootDirectoryPath);
        bundleArtifactStore.DeleteWorkspace(workspaceKey);

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(normalizedRootDirectoryPath);
        bundleIndexCacheStore.DeleteWorkspace(workspaceKey);

        var recognitionCacheStore = _workspaceDataStoreFactory.CreateWorkspaceImportRecognitionCacheStore(normalizedRootDirectoryPath);
        recognitionCacheStore.DeleteWorkspace(workspaceKey);

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(normalizedRootDirectoryPath);
        previewCacheStore.DeleteWorkspace(workspaceKey);

        var workspaceChangeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(normalizedRootDirectoryPath);
        workspaceChangeStore.Delete(workspaceKey);
    }

    private static WorkspaceDataRootWorkspaceDto[] LoadWorkspaceMetadataEntries(string normalizedRootDirectoryPath)
    {
        var storagePaths = new JsonWorkspaceStoragePaths(normalizedRootDirectoryPath);
        if (!Directory.Exists(storagePaths.WorkspacesDirectoryPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(storagePaths.WorkspacesDirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(static filePath => JsonWorkspaceFileStorageHelper.TryReadJson<WorkspaceMetadataDto>(
                filePath,
                JsonWorkspaceSerialization.SerializerOptions))
            .Where(static metadata => metadata is not null)
            .Select(static metadata => new WorkspaceDataRootWorkspaceDto(
                WorkspaceKey: metadata!.WorkspaceKey,
                DisplayName: metadata.DisplayName,
                SourcePath: metadata.SourcePath,
                CreatedAtUtc: metadata.CreatedAtUtc,
                LastOpenedAtUtc: metadata.LastOpenedAtUtc,
                AssetBundleDecryptorId: metadata.AssetBundleDecryptorId))
            .OrderByDescending(static entry => entry.LastOpenedAtUtc)
            .ToArray();
    }

    private static string NormalizeRootDirectoryPath(string? rootDirectoryPath)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? string.Empty
            : rootDirectoryPath.Trim().TrimEnd('\\', '/');
    }
}
