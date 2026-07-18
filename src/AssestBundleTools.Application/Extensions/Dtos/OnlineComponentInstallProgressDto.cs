namespace AssestBundleTools.Application.Extensions.Dtos;

public sealed record OnlineComponentInstallProgressDto(
    OnlineComponentInstallStage Stage,
    long BytesReceived = 0,
    long? TotalBytes = null);

public enum OnlineComponentInstallStage
{
    ResolvingPackage = 0,
    Downloading = 1,
    Verifying = 2,
    Extracting = 3,
    Installing = 4
}
