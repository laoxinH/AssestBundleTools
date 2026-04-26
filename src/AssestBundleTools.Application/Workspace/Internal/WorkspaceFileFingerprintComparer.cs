using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 提供工作区文件指纹的统一比对逻辑。
/// </summary>
internal static class WorkspaceFileFingerprintComparer
{
    public static bool Matches(
        WorkspaceFileFingerprintDto cachedFingerprint,
        WorkspaceFileFingerprintDto currentFingerprint)
    {
        ArgumentNullException.ThrowIfNull(cachedFingerprint);
        ArgumentNullException.ThrowIfNull(currentFingerprint);

        if (!string.Equals(
                cachedFingerprint.SourcePath,
                currentFingerprint.SourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (cachedFingerprint.FileSizeInBytes != currentFingerprint.FileSizeInBytes ||
            cachedFingerprint.LastWriteTimeUtc != currentFingerprint.LastWriteTimeUtc)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(cachedFingerprint.ContentHash) &&
            !string.IsNullOrWhiteSpace(currentFingerprint.ContentHash) &&
            !string.Equals(
                cachedFingerprint.ContentHash,
                currentFingerprint.ContentHash,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
