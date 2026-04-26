using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal static class AssetBundleFileFingerprintFactory
{
    public static WorkspaceFileFingerprintDto Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        return new WorkspaceFileFingerprintDto(
            SourcePath: fileInfo.FullName,
            FileSizeInBytes: fileInfo.Exists ? fileInfo.Length : 0,
            LastWriteTimeUtc: fileInfo.Exists
                ? new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
                : DateTimeOffset.MinValue,
            ContentHash: null);
    }
}
