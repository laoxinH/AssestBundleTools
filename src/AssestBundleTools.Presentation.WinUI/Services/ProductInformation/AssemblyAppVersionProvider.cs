using System.Reflection;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 从入口程序集元数据读取由 Git tag 生成的应用版本。
/// </summary>
internal sealed class AssemblyAppVersionProvider
{
    public string GetCurrentVersion() =>
        ResolveVersion(typeof(AssemblyAppVersionProvider).Assembly);

    internal static string ResolveVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return ResolveVersion(informationalVersion, assembly.GetName().Version);
    }

    internal static string ResolveVersion(string? informationalVersion, Version? assemblyVersion)
    {
        informationalVersion = informationalVersion?.Trim();
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assemblyVersion is null
            ? "0.0.0"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}";
    }
}
