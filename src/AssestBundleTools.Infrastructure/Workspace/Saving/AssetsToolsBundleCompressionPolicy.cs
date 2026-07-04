using AssetsTools.NET;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class AssetsToolsBundleCompressionPolicy
{
    public static AssetBundleCompressionType ResolveOriginalCompression(
        string? cachedOriginalCompression,
        AssetBundleCompressionType loadedOriginalCompression)
    {
        if (string.IsNullOrWhiteSpace(cachedOriginalCompression))
        {
            return loadedOriginalCompression;
        }

        return Enum.TryParse<AssetBundleCompressionType>(cachedOriginalCompression.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : loadedOriginalCompression;
    }

    public static AssetBundleCompressionType ResolveTargetCompression(
        AssetBundleCompressionType originalCompression,
        WorkspaceBundleExportCompressionModeDto compressionMode) =>
        compressionMode switch
        {
            WorkspaceBundleExportCompressionModeDto.PreserveOriginal => originalCompression,
            WorkspaceBundleExportCompressionModeDto.FastLz4 => AssetBundleCompressionType.LZ4Fast,
            WorkspaceBundleExportCompressionModeDto.Lz4 => AssetBundleCompressionType.LZ4,
            WorkspaceBundleExportCompressionModeDto.Lzma => AssetBundleCompressionType.LZMA,
            WorkspaceBundleExportCompressionModeDto.Uncompressed => AssetBundleCompressionType.None,
            _ => originalCompression
        };
}
