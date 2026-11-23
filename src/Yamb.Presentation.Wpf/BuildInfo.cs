using System.Reflection;

namespace Yamb.Presentation.Wpf;

public static class BuildInfo
{
    public const int BuildNumber = 11;
    public const string ReleaseDate = "2026-11-24";

    public static string Version { get; } = ReadVersion();

    public static string Stage =>
        Version.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? "Alpha" : "Release";

    public static string Display => $"{Stage} {Version} · build {BuildNumber} · {ReleaseDate}";

    private static string ReadVersion()
    {
        var informational = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
            return "1.0.0-alpha.11";

        int metadataIndex = informational.IndexOf('+');
        return metadataIndex >= 0 ? informational[..metadataIndex] : informational;
    }
}
