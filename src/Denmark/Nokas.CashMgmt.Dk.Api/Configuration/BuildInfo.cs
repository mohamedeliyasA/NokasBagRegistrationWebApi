using System.Reflection;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

// Lets a deployment be confirmed from outside the App Service without shell access.
public static class BuildInfo
{
    public static string Version { get; }
    public static string BuiltUtc { get; }
    public static string Stamp { get; }

    static BuildInfo()
    {
        var assembly = typeof(BuildInfo).Assembly;

        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? assembly.GetName().Version?.ToString()
                  ?? "unknown";

        BuiltUtc = ReadAssemblyTimestamp(assembly);
        Stamp = $"{Version} @ {BuiltUtc}";
    }

    private static string ReadAssemblyTimestamp(Assembly assembly)
    {
        try
        {
            var location = assembly.Location;
            return string.IsNullOrEmpty(location)
                ? "unknown"
                : File.GetLastWriteTimeUtc(location).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        }
        catch (IOException)
        {
            return "unknown";
        }
        catch (UnauthorizedAccessException)
        {
            return "unknown";
        }
    }
}
