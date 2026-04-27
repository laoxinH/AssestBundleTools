using System.Text;
using System.Text.Json;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 统一封装 Shell 启动参数的序列化与反序列化，
/// 避免进程启动协议散落在窗口服务和 App 入口里。
/// </summary>
internal static class ShellLaunchArgumentCodec
{
    private const string LaunchPayloadArgumentPrefix = "--abt-shell-launch=";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParseCurrentProcessLaunchRequest(out ShellLaunchRequest? launchRequest)
    {
        var commandLineArguments = Environment.GetCommandLineArgs().Skip(1);
        return TryParseLaunchRequest(commandLineArguments, out launchRequest);
    }

    public static string CreateArgumentToken(
        ShellLaunchRequest launchRequest,
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(launchRequest);
        ArgumentNullException.ThrowIfNull(localizationService);

        var payload = CreatePayload(launchRequest, localizationService);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var encodedPayload = EncodeToBase64Url(json);
        return $"{LaunchPayloadArgumentPrefix}{encodedPayload}";
    }

    private static bool TryParseLaunchRequest(
        IEnumerable<string> commandLineArguments,
        out ShellLaunchRequest? launchRequest)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        launchRequest = null;
        foreach (var argument in commandLineArguments)
        {
            if (!argument.StartsWith(LaunchPayloadArgumentPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var encodedPayload = argument[LaunchPayloadArgumentPrefix.Length..];
            if (string.IsNullOrWhiteSpace(encodedPayload))
            {
                return false;
            }

            try
            {
                var json = DecodeFromBase64Url(encodedPayload);
                var payload = JsonSerializer.Deserialize<ShellLaunchPayload>(json, SerializerOptions);
                if (payload is null || string.IsNullOrWhiteSpace(payload.NavigationKey))
                {
                    return false;
                }

                launchRequest = new ShellLaunchRequest(
                    payload.NavigationKey,
                    payload.WorkspaceNavigationRequest);
                return true;
            }
            catch (Exception)
            {
                launchRequest = null;
                return false;
            }
        }

        return false;
    }

    private static ShellLaunchPayload CreatePayload(
        ShellLaunchRequest launchRequest,
        ILocalizationService localizationService)
    {
        WorkspaceNavigationRequest? workspaceNavigationRequest = null;
        if (launchRequest.NavigationParameter is not null)
        {
            workspaceNavigationRequest = launchRequest.NavigationParameter as WorkspaceNavigationRequest
                ?? throw PresentationExceptionFactory.CreateUnsupportedLaunchNavigationParameterException(localizationService);
        }

        return new ShellLaunchPayload(
            NavigationKey: launchRequest.NavigationKey,
            WorkspaceNavigationRequest: workspaceNavigationRequest is null
                ? null
                : workspaceNavigationRequest with { OpenTarget = WorkspaceOpenTarget.CurrentWindow });
    }

    private static string EncodeToBase64Url(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string DecodeFromBase64Url(string value)
    {
        var normalized = value
            .Replace('-', '+')
            .Replace('_', '/');

        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };

        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    private sealed record ShellLaunchPayload(
        string NavigationKey,
        WorkspaceNavigationRequest? WorkspaceNavigationRequest);
}
