using System.Text;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class UnelevatedRestartLaunchArguments
{
    private const string RestartMarker = "--abt-unelevated-restart";

    public static bool IsCurrentProcessRestartLaunch()
    {
        return IsRestartLaunch(Environment.GetCommandLineArgs().Skip(1));
    }

    internal static bool IsRestartLaunch(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Any(argument =>
            string.Equals(argument, RestartMarker, StringComparison.Ordinal));
    }

    public static IReadOnlyList<string> Create(ShellLaunchRequest? launchRequest, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        var arguments = new List<string> { RestartMarker };
        if (launchRequest is not null)
        {
            arguments.Add(ShellLaunchArgumentCodec.CreateArgumentToken(launchRequest, localizationService));
        }

        return arguments;
    }

    internal static string BuildCommandLine(string executablePath, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var builder = new StringBuilder();
        AppendQuotedArgument(builder, executablePath);
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            AppendQuotedArgument(builder, argument ?? string.Empty);
        }

        return builder.ToString();
    }

    private static void AppendQuotedArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 &&
            !argument.Any(static character => char.IsWhiteSpace(character) || character == '"'))
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            backslashCount = 0;
            builder.Append(character);
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
    }
}
