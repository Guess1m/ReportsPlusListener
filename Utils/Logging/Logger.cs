using Rage;

namespace ReportsPlus.Utils.Logging{
    internal static class Logger{
        private static void Log(string message, Severity severity)
        {
            var prefix = $"ReportsPlus [{severity.ToString().ToUpperInvariant()}]: ";

            switch (severity)
            {
                case Severity.Debug:
                    Game.LogVeryVerboseDebug(prefix + message);
                    break;
                case Severity.Info:
                    Game.LogTrivial(prefix + message);
                    break;
                case Severity.Warning:
                    Game.LogVerbose(prefix + message);
                    break;
                case Severity.Error:
                    Game.LogVeryVerbose(prefix + message);
                    break;
                default:
                    Game.LogTrivial($"ReportsPlus [FATAL/UNKNOWN]: {message}");
                    break;
            }
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