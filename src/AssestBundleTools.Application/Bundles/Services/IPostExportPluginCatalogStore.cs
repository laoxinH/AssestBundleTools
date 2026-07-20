using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Services;

public interface IPostExportPluginCatalogStore
{
    IReadOnlyList<PostExportPluginCatalogEntryDto> Load();

    void SaveUserManagedPlugin(PostExportPluginDefinitionDto definition, string? previousPluginId = null);

    void DeleteUserManagedPlugin(string pluginId);
}
