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

        var extension = Path.GetExtension(bundlePath);
        if (KnownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Bundle,
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.SourceInspectionMatchedKnownExtension,
                    [extension]));
        }

        var header = TryReadHeader(bundlePath);
        var matchedHeader = KnownHeaders.FirstOrDefault(knownHeader => header.StartsWith(knownHeader, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(matchedHeader))
        {
            return new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Bundle,
                new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.SourceInspectionMatchedKnownHeader,
                    [matchedHeader]));
        }

        return new AssetBundleSourceInspectionResultDto(
            AssetBundleSourceInspectionKind.Undetermined,
            new LocalizedMessageDto(WorkspaceImportMessageTokens.SourceInspectionNoKnownPlainSignature));
    }

    public static bool CanTreatAsBundle(string bundlePath)
    {
        return Inspect(bundlePath).Kind is AssetBundleSourceInspectionKind.Bundle;
    }

    public static string NormalizeLogicalPath(string logicalPath, string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            return Path.GetFileName(bundlePath);
        }

        return logicalPath.Trim().Replace('\\', '/');
    }

    private static string TryReadHeader(string bundlePath)
    {
        try
        {
            using var stream = File.OpenRead(bundlePath);
            Span<byte> buffer = stackalloc byte[16];
            var bytesRead = stream.Read(buffer);
            if (bytesRead <= 0)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(buffer[..bytesRead]).TrimEnd('\0', ' ', '\r', '\n', '\t');
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
