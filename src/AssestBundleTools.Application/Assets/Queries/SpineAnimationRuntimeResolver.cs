using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class SpineAnimationRuntimeResolver
{
    private static readonly IReadOnlyDictionary<(int Major, int Minor), string> AdapterKeys =
        new Dictionary<(int Major, int Minor), string>
        {
            [(2, 1)] = "V21",
            [(3, 4)] = "V34",
            [(3, 5)] = "V35",
            [(3, 6)] = "V36",
            [(3, 7)] = "V37",
            [(3, 8)] = "V38",
            [(4, 0)] = "V40",
            [(4, 1)] = "V41",
            [(4, 2)] = "V42"
        };

    public static SpineAnimationRuntimeDto Resolve(string? spineVersion)
    {
        if (!TryParseMajorMinor(spineVersion, out var major, out var minor))
        {
            return new SpineAnimationRuntimeDto(
                spineVersion,
                Major: null,
                Minor: null,
                AdapterKey: null,
                SpineAnimationRuntimeSupportKind.VersionMissing);
        }

        return AdapterKeys.TryGetValue((major, minor), out var adapterKey)
            ? new SpineAnimationRuntimeDto(
                spineVersion,
                major,
                minor,
                adapterKey,
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable)
            : new SpineAnimationRuntimeDto(
                spineVersion,
                major,
                minor,
                AdapterKey: null,
                SpineAnimationRuntimeSupportKind.UnsupportedVersion);
    }

    private static bool TryParseMajorMinor(
        string? spineVersion,
        out int major,
        out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(spineVersion))
        {
            return false;
        }

        var segments = spineVersion
            .Trim()
            .Split('.', 3, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 &&
               TryParseLeadingInt(segments[0], out major) &&
               TryParseLeadingInt(segments[1], out minor);
    }

    private static bool TryParseLeadingInt(string text, out int value)
    {
        value = 0;
        var digitCount = 0;
        foreach (var character in text)
        {
            if (!char.IsDigit(character))
            {
                break;
            }

            value = (value * 10) + (character - '0');
            digitCount++;
        }

        return digitCount > 0;
    }
}
