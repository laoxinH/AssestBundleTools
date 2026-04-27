using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
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
                DetailMessage: new LocalizedMessageDto(WorkspaceImportMessageTokens.CacheSourceFingerprintChanged));
        }

        if (!string.Equals(cacheEntry.DecryptorId, decryptorDescriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.DecryptorChanged,
                DetailMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.CacheDecryptorChanged,
                    [cacheEntry.DecryptorId ?? string.Empty, decryptorDescriptor.Id]));
        }

        if (!string.Equals(cacheEntry.DecryptorVersion, decryptorDescriptor.Version, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.DecryptorVersionChanged,
                DetailMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.CacheDecryptorVersionChanged,
                    [cacheEntry.DecryptorVersion ?? string.Empty, decryptorDescriptor.Version]));
        }

        if (!string.Equals(cacheEntry.ToolVersion, currentToolVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.ToolVersionChanged,
                DetailMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.CacheToolVersionChanged,
                    [cacheEntry.ToolVersion, currentToolVersion]));
        }

        if (!reader.SupportsCacheParserVersion(cacheEntry.ParserVersion))
        {
            return new BundleIndexCacheReuseDecision(
                CanReuse: false,
                Kind: WorkspaceImportCacheDecisionKind.ParserVersionChanged,
                DetailMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.CacheParserVersionChanged,
                    [cacheEntry.ParserVersion, reader.CacheParserVersion]));
        }

        return new BundleIndexCacheReuseDecision(
            CanReuse: true,
            Kind: WorkspaceImportCacheDecisionKind.Reused,
            DetailMessage: null);
    }
}

internal sealed record BundleIndexCacheReuseDecision(
    bool CanReuse,
    WorkspaceImportCacheDecisionKind Kind,
    LocalizedMessageDto? DetailMessage);
