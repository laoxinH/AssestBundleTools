namespace AssestBundleTools.Application.Extensions.Dtos;

public sealed record OnlineComponentInstallResultDto(
    string ComponentId,
    string DisplayName,
    string? InstalledVersion,
    bool WasAlreadyInstalled);
