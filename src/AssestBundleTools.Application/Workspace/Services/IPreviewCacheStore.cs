using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单工作区下的预览缓存。
/// </summary>
public interface IPreviewCacheStore
{
    PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey);

    IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey);

    string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor);

    void Save(PreviewCachePayloadDto previewCachePayload);

    void Delete(string workspaceKey, string previewCacheKey);

    void DeleteWorkspace(string workspaceKey);
}
