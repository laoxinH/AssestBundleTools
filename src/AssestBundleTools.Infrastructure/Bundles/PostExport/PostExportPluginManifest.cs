namespace AssestBundleTools.Infrastructure.Bundles.PostExport;

internal sealed class PostExportPluginManifest
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Kind { get; init; } = "postExport";

    public string ExecutablePath { get; init; } = string.Empty;

    public string? WorkingDirectoryPath { get; init; }

    public IReadOnlyList<string> ProcessArguments { get; init; } = Array.Empty<string>();
}
