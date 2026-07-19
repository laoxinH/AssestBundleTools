namespace AssestBundleTools.Application.Updates.Models;

public enum ApplicationInstallMode
{
    Unmanaged,
    Portable,
    Installed
}

public enum ApplicationUpdateArchitecture
{
    X86,
    X64,
    Arm64
}

public sealed record ApplicationUpdateRuntimeInfo(
    string CurrentVersion,
    ApplicationInstallMode InstallMode,
    ApplicationUpdateArchitecture Architecture,
    bool CanSelfUpdate);

public sealed record ApplicationUpdateInfo(
    string TargetVersion,
    string? ReleaseNotes,
    long? DownloadSizeInBytes,
    bool IsDeltaUpdate);

public sealed record ApplicationUpdateProgress(long BytesDownloaded, long? TotalBytes)
{
    public double? Percentage => TotalBytes is > 0
        ? Math.Clamp(BytesDownloaded * 100d / TotalBytes.Value, 0d, 100d)
        : null;
}
