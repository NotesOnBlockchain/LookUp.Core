using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace LookUp.Core.Helpers
{
    public static class EnvironmentHelpers
    {
        // appName, dataDir
        private static ConcurrentDictionary<string, string> DataDirDict { get; } = new ConcurrentDictionary<string, string>();

        // Do not change the output of this function. Backwards compatibility depends on it.
        public static string GetDataDir(string appName)
        {
            if (DataDirDict.TryGetValue(appName, out string? dataDir))
            {
                return dataDir;
            }

            string directory;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home))
                {
                    directory = Path.Combine(home, "." + appName.ToLowerInvariant());
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find suitable datadir.");
                }
            }
            else
            {
                var localAppData = Environment.GetEnvironmentVariable("APPDATA");
                if (!string.IsNullOrEmpty(localAppData))
                {
                    directory = Path.Combine(localAppData, appName);
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find suitable datadir.");
                }
            }

            if (Directory.Exists(directory))
            {
                DataDirDict.TryAdd(appName, directory);
                return directory;
            }

            Directory.CreateDirectory(directory);

            DataDirDict.TryAdd(appName, directory);
            return directory;
        }
    }
}
