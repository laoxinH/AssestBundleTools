namespace AssestBundleTools.Application.Extensions.Dtos;

/// <summary>
/// 表示一个由应用信任目录注册、可经用户确认安装的在线组件。
/// </summary>
public sealed record OnlineComponentDescriptorDto(
    string Id,
    string DisplayName,
    OnlineComponentKind Kind,
    string Publisher,
    string ProjectUri,
    string LicenseName,
    string SourceDisplayName,
    bool IsInstalled);
