namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal sealed record AssetBundleExternalProcessProbePayload(
    bool IsMatch,
    int Confidence,
    string? Message,
    int SchemaVersion = 1,
    string? FormatId = null,
    IReadOnlyList<string>? Evidence = null);
