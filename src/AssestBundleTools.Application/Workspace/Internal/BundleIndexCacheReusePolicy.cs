using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 定义资源包索引缓存的最小复用判定规则。
/// </summary>
internal static class BundleIndexCacheReusePolicy
{
    public static BundleIndexCacheReuseDecision Evaluate(
        BundleIndexCacheEntryDto cacheEntry,
        WorkspaceFileFingerprintDto currentSourceFingerprint,
        AssetBundleDecryptorDescriptorDto decryptorDescriptor,
        string currentToolVersion,
        IAssetBundleReader reader)
    {
        ArgumentNullException.ThrowIfNull(cacheEntry);
        ArgumentNullException.ThrowIfNull(currentSourceFingerprint);
        ArgumentNullException.ThrowIfNull(decryptorDescriptor);
        ArgumentNullException.ThrowIfNull(reader);

        // 这里就是导入自动识别里的“缓存命中识别”：
        // 只有当来源指纹、解密器身份、解密器版本、工具版本和解析器版本全部仍然兼容时，
        // 才允许直接复用旧索引；任意一项变化都必须重建。
        if (!WorkspaceFileFingerprintComparer.Matches(cacheEntry.SourceFingerprint, currentSourceFingerprint))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.SourceFingerprintChanged,
                Detail: "Cached source fingerprint no longer matches the current import file.");
        }

        if (!string.Equals(cacheEntry.DecryptorId, decryptorDescriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.DecryptorChanged,
                Detail: $"Cached decryptor '{cacheEntry.DecryptorId ?? "(none)"}' no longer matches current decryptor '{decryptorDescriptor.Id}'.");
        }

        if (!string.Equals(cacheEntry.DecryptorVersion, decryptorDescriptor.Version, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.DecryptorVersionChanged,
                Detail: $"Cached decryptor version '{cacheEntry.DecryptorVersion ?? "(none)"}' no longer matches current version '{decryptorDescriptor.Version}'.");
        }

        if (!string.Equals(cacheEntry.ToolVersion, currentToolVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.ToolVersionChanged,
                Detail: $"Cached tool version '{cacheEntry.ToolVersion}' no longer matches current version '{currentToolVersion}'.");
        }

        if (!reader.SupportsCacheParserVersion(cacheEntry.ParserVersion))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.ParserVersionChanged,
                Detail: $"Cached parser version '{cacheEntry.ParserVersion}' is not supported by current reader version '{reader.CacheParserVersion}'.");
        }

        return new BundleIndexCacheReuseDecision(
            CanReuse: true,
            Kind: WorkspaceImportCacheDecisionKind.Reused,
            Detail: null);
    }
}

internal sealed record BundleIndexCacheReuseDecision(
    bool CanReuse,
    WorkspaceImportCacheDecisionKind Kind,
    string? Detail);
