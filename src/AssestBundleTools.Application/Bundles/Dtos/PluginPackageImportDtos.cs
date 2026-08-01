namespace AssestBundleTools.Application.Bundles.Dtos;

public enum PluginPackageKindDto
{
    AssetBundleCodec = 0,
    ResourceProcessor = 1,
    PostExport = 2
}

public enum PluginPackageImportStatusDto
{
    Installed = 0,
    OverwriteConfirmationRequired = 1
}

public sealed record PluginPackageImportResultDto(
    PluginPackageImportStatusDto Status,
    PluginPackageKindDto Kind,
    string PluginId,
    string DisplayName,
    string Version,
    string? InstallDirectoryPath);
