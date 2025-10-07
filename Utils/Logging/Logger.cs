using Rage;

namespace ReportsPlus.Utils.Logging{
    internal sealed class Logger{
        private static void Log(string message, Severity severity)
        {
            Game.LogTrivial($"ReportsPlus [{severity}]: {message}");
        }

        public static void LogDebug(string message)
        {
            Log(message, Severity.Debug);
        }

        public static void LogInfo(string message)
        {
            Log(message, Severity.Info);
        }

        public static void LogWarning(string message)
        {
            Log(message, Severity.Warning);
        }

        public static void LogError(string message)
        {
            Log(message, Severity.Error);
        }

        private enum Severity{
            Info,
            Warning,
            Debug,
            Error
        }
    }
}