using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Commands;

public sealed record UpsertPostExportPluginCommand(
    PostExportPluginDefinitionDto Definition,
    string? PreviousPluginId = null);
