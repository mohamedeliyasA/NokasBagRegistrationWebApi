using System.Reflection;

namespace Nokas.CashMgmt.BagReg.Func
{
    /// <summary>
    /// Identifies the build currently deployed to the Function App, so a
    /// deployment can be confirmed by calling any function directly.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>Compact "version @ timestamp" stamp returned in function responses.</summary>
        public static string Stamp { get; }

        static BuildInfo()
        {
            var assembly = typeof(BuildInfo).Assembly;

            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? assembly.GetName().Version?.ToString()
                          ?? "unknown";

            Stamp = $"{version} @ {ReadAssemblyTimestamp(assembly)}";
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
