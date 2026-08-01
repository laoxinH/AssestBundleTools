namespace AssestBundleTools.Application.Bundles.Commands;

public sealed record ImportPluginPackageCommand(
    string SourcePath,
    bool ReplaceExisting = false);
