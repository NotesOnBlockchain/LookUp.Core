using System.Text;

namespace LookUp.Logger
{
    public static class Logger
    {
        private static string FilePath { get; set; } = "Logs.txt";
        private static object FileLock = new object();
        public static void Initialize(string filePath)
        {
            SetFilePath(filePath);
        }

        private static void SetFilePath(string filePath)
        {
            FilePath = Path.Combine(filePath, "Logs.txt");
            Helpers.EnvironmentHelpers.EnsureDirectoryExists(filePath);
        }

        private static void Log(string message, LogLevel logLevel)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{logLevel.ToString().ToUpperInvariant()}] [{Environment.CurrentManagedThreadId}]\t");

            messageBuilder.Append(message);
            messageBuilder.Append('\n');

            string finalMessage = messageBuilder.ToString();
            lock (FileLock) 
            {
                File.AppendAllText(FilePath, finalMessage);
            }
        }

        public static void LogInfo(string message) => Log(message, LogLevel.Info);
        public static void LogInfo(Exception exception) => Log(exception.ToString(), LogLevel.Info);

        public static void LogWarning(string message) => Log(message, LogLevel.Warning);
        public static void LogWarning(Exception exception) => Log(exception.ToString(), LogLevel.Warning);

        public static void LogCritical(string message) => Log(message, LogLevel.Critical);
        public static void LogCritical(Exception exception) => Log(exception.ToString(), LogLevel.Critical);

    }

    public enum LogLevel
    {
        Info,
        Warning,
        Critical
    }
}
