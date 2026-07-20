using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

public enum WorkspacePostExportPluginLaunchStatusDto
{
    Launched,
    LaunchFailed
}

public sealed record WorkspacePostExportPluginLaunchRequestDto(
    PostExportPluginCatalogEntryDto Plugin,
    string ManifestPath,
    string OutputDirectory,
    WorkspaceId WorkspaceId,
    string? WorkspaceKey);

public sealed record WorkspacePostExportPluginLaunchResultDto(
    string PluginId,
    string PluginDisplayName,
    WorkspacePostExportPluginLaunchStatusDto Status,
    string? DiagnosticCode = null,
    string? DiagnosticDetail = null);
