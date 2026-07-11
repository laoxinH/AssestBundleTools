namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal sealed record AssetBundleExternalProcessDecryptPayload(
    bool IsSuccess,
    string? OutputPath,
    string? Message,
    int SchemaVersion = 1,
    IReadOnlyDictionary<string, string>? CodecParameters = null);
