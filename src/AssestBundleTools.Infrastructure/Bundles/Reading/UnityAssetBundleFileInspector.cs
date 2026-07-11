using System.Text;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Messages;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 提供 Unity 资源包文件的轻量识别与路径规范化逻辑。
/// </summary>
internal static class UnityAssetBundleFileInspector
{
    private static readonly string[] KnownExtensions = [".ab", ".bundle", ".assetbundle", ".unity3d"];
    private static readonly string[] KnownHeaders = ["UnityFS", "UnityRaw", "UnityWeb", "UnityArchive"];
    private static readonly (byte[] Signature, string FormatName)[] CommonNonBundleSignatures =
    [
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "PNG"),
        ([0xFF, 0xD8, 0xFF], "JPEG"),
        (Encoding.ASCII.GetBytes("GIF87a"), "GIF"),
        (Encoding.ASCII.GetBytes("GIF89a"), "GIF"),
        (Encoding.ASCII.GetBytes("BM"), "BMP"),
        ([0x49, 0x49, 0x2A, 0x00], "TIFF"),
        ([0x4D, 0x4D, 0x00, 0x2A], "TIFF"),
        ([0x50, 0x4B, 0x03, 0x04], "ZIP"),
        ([0x50, 0x4B, 0x05, 0x06], "ZIP"),
        ([0x50, 0x4B, 0x07, 0x08], "ZIP"),
        ([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07], "RAR"),
        ([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], "7z"),
        ([0x1F, 0x8B], "GZip"),
        (Encoding.ASCII.GetBytes("OggS"), "Ogg"),
        (Encoding.ASCII.GetBytes("fLaC"), "FLAC"),
        (Encoding.ASCII.GetBytes("ID3"), "MP3"),
        (Encoding.ASCII.GetBytes("RIFF"), "RIFF media"),
        (Encoding.ASCII.GetBytes("%PDF"), "PDF")
    ];

    /// <summary>
    /// 对原始来源文件执行“解密前”的保守识别。
    /// 这里只做正向命中和明显无效输入剔除，
    /// 不会因为扩展名未知就直接判死刑，避免误伤无后缀或加密资源包。
    /// </summary>
    public static AssetBundleSourceInspectionResultDto Inspect(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath) || !File.Exists(bundlePath))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Rejected,
                new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionSourceFileMissing));
        }

        try
        {
            var fileInfo = new FileInfo(bundlePath);
            if (!fileInfo.Exists)
            {
                return new AssetBundleSourceInspectionResultDto(
                    AssetBundleSourceInspectionKind.Rejected,
                    new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionSourceFileMissing));
            }

            if (fileInfo.Length <= 0)
            {
                return new AssetBundleSourceInspectionResultDto(
                    AssetBundleSourceInspectionKind.Rejected,
                    new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionSourceFileEmpty));
            }
        }
        catch (Exception)
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Rejected,
                new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionSourceMetadataUnreadable));
        }

        var prefix = TryReadPrefix(bundlePath);
        var header = DecodeHeader(prefix);
        var matchedHeader = KnownHeaders.FirstOrDefault(knownHeader => header.StartsWith(knownHeader, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(matchedHeader))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Bundle,
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.SourceInspectionMatchedKnownHeader,
                    [matchedHeader]));
        }

        var commonFormat = MatchCommonNonBundleFormat(prefix);
        if (!string.IsNullOrWhiteSpace(commonFormat))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Undetermined,
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.SourceInspectionMatchedCommonNonBundleSignature,
                    [commonFormat]),
                CanUsePassThrough: false);
        }

        var extension = Path.GetExtension(bundlePath);
        if (KnownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Bundle,
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.SourceInspectionMatchedKnownExtension,
                    [extension]));
        }

        return new AssetBundleSourceInspectionResultDto(
            AssetBundleSourceInspectionKind.Undetermined,
            new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionNoKnownPlainSignature));
    }

    public static bool CanTreatAsBundle(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath) || !File.Exists(bundlePath))
        {
            return false;
        }

        var header = DecodeHeader(TryReadPrefix(bundlePath));
        return KnownHeaders.Any(knownHeader => header.StartsWith(knownHeader, StringComparison.Ordinal));
    }

    public static string NormalizeLogicalPath(string logicalPath, string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            return Path.GetFileName(bundlePath);
        }

        return logicalPath.Trim().Replace('\\', '/');
    }

    private static byte[] TryReadPrefix(string bundlePath)
    {
        try
        {
            using var stream = File.OpenRead(bundlePath);
            var buffer = new byte[16];
            var bytesRead = stream.Read(buffer);
            if (bytesRead <= 0)
            {
                return [];
            }

            return buffer[..bytesRead];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string DecodeHeader(byte[] prefix) =>
        Encoding.ASCII.GetString(prefix).TrimEnd('\0', ' ', '\r', '\n', '\t');

    private static string? MatchCommonNonBundleFormat(byte[] prefix)
    {
        foreach (var (signature, formatName) in CommonNonBundleSignatures)
        {
            if (prefix.AsSpan().StartsWith(signature))
            {
                return formatName;
            }
        }

        return prefix.Length >= 8 && prefix.AsSpan(4).StartsWith("ftyp"u8)
            ? "MP4/QuickTime"
            : null;
    }
}
