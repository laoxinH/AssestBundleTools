using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于“描述文件 + 二进制内容文件”的预览缓存存储。
/// </summary>
public sealed class JsonPreviewCacheStore : IPreviewCacheStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonPreviewCacheStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(previewCacheKey))
        {
            return null;
        }

        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(workspaceKey, previewCacheKey);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        if (descriptor is null)
        {
            return null;
        }

        var payloadFilePath = _storagePaths.GetPreviewPayloadFilePath(
            workspaceKey,
            previewCacheKey,
            descriptor.RelativePayloadPath);
        var payload = JsonWorkspaceFileStorageHelper.TryReadBytes(payloadFilePath);
        if (payload is null)
        {
            return null;
        }

        return new PreviewCachePayloadDto(descriptor, payload);
    }

    public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Array.Empty<PreviewCacheDescriptorDto>();
        }

        var workspacePreviewDirectoryPath = _storagePaths.GetPreviewCacheDirectoryPath(workspaceKey);
        if (!Directory.Exists(workspacePreviewDirectoryPath))
        {
            return Array.Empty<PreviewCacheDescriptorDto>();
        }

        var descriptors = new List<PreviewCacheDescriptorDto>();

        foreach (var descriptorFilePath in Directory.EnumerateFiles(workspacePreviewDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
                descriptorFilePath,
                JsonWorkspaceSerialization.SerializerOptions);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(descriptor.PreviewCacheKey) ||
            string.IsNullOrWhiteSpace(descriptor.RelativePayloadPath))
        {
            return null;
        }

        try
        {
            return _storagePaths.GetPreviewPayloadFilePath(
                descriptor.WorkspaceKey,
                descriptor.PreviewCacheKey,
                descriptor.RelativePayloadPath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public void Save(PreviewCachePayloadDto previewCachePayload)
    {
        ArgumentNullException.ThrowIfNull(previewCachePayload);

        var descriptor = previewCachePayload.Descriptor;
        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(descriptor.WorkspaceKey, descriptor.PreviewCacheKey);
        var payloadFilePath = _storagePaths.GetPreviewPayloadFilePath(
            descriptor.WorkspaceKey,
            descriptor.PreviewCacheKey,
            descriptor.RelativePayloadPath);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            descriptorFilePath,
            descriptor,
            JsonWorkspaceSerialization.SerializerOptions);
        JsonWorkspaceFileStorageHelper.WriteBytesAtomically(payloadFilePath, previewCachePayload.Content);
    }

    public void Delete(string workspaceKey, string previewCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(previewCacheKey))
        {
            return;
        }

        var descriptorFilePath = _storagePaths.GetPreviewDescriptorFilePath(workspaceKey, previewCacheKey);
        var descriptor = JsonWorkspaceFileStorageHelper.TryReadJson<PreviewCacheDescriptorDto>(
            descriptorFilePath,
            JsonWorkspaceSerialization.SerializerOptions);

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(descriptorFilePath);

        if (descriptor is null)
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(
            _storagePaths.GetPreviewPayloadFilePath(workspaceKey, previewCacheKey, descriptor.RelativePayloadPath));

        var payloadDirectoryPath = Path.GetDirectoryName(
            _storagePaths.GetPreviewPayloadFilePath(workspaceKey, previewCacheKey, descriptor.RelativePayloadPath));
        if (!string.IsNullOrWhiteSpace(payloadDirectoryPath) &&
            Directory.Exists(payloadDirectoryPath) &&
            !Directory.EnumerateFileSystemEntries(payloadDirectoryPath).Any())
        {
            Directory.Delete(payloadDirectoryPath, recursive: false);
        }
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetPreviewCacheDirectoryPath(workspaceKey));
    }
}
