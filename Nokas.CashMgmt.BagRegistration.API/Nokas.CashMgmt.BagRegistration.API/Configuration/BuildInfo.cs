using System.Reflection;

namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    /// <summary>
    /// Identifies the build that is currently running, so a deployment can be
    /// confirmed from outside the App Service without shell access.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>Informational version of the running assembly.</summary>
        public static string Version { get; }

        /// <summary>UTC write time of the deployed assembly, as an ISO-8601 string.</summary>
        public static string BuiltUtc { get; }

        /// <summary>Compact "version @ timestamp" stamp surfaced in responses and Swagger.</summary>
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
}
