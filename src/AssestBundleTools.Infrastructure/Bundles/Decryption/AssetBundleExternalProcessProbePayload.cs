namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal sealed record AssetBundleExternalProcessProbePayload(
    bool IsMatch,
    int Confidence,
    string? Message);
