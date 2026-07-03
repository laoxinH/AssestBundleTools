using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 提供导出资源处理器目录项的加载与用户管理清单写入能力。
/// </summary>
public interface IResourceProcessorCatalogStore
{
    IReadOnlyList<ResourceProcessorCatalogEntryDto> Load();

    void SaveUserManagedProcessor(
        ResourceProcessorDefinitionDto definition,
        string? previousProcessorId = null);

    void DeleteUserManagedProcessor(string processorId);
}
