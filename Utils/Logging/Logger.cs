using Rage;

namespace ReportsPlus.Utils.Logging
{
    internal static class Logger
    {
        /// <summary>
        ///     Dispatches a log message to the Rage Plugin Hook console with a formatted prefix and appropriate verbosity level.
        /// </summary>
        /// <param name="message">The content of the log entry.</param>
        /// <param name="severity">The <see cref="Severity" /> level determining the logging channel and prefix.</param>
        private static void Log(string message, Severity severity)
        {
            if (string.IsNullOrEmpty(message)) return;

            switch (severity)
            {
                case Severity.Info:
                    Game.LogTrivial($"ReportsPlus [INFO]: {message}");
                    break;
                case Severity.Warning:
                    Game.LogVerbose($"ReportsPlus [WARNING]: {message}");
                    break;
                case Severity.Error:
                    Game.LogVeryVerbose($"ReportsPlus [ERROR]: {message}");
                    break;
                default:
                    Game.LogTrivial($"ReportsPlus [FATAL/UNKNOWN]: {message}");
                    break;
            }
        }

        /// <summary>
        ///     Logs an informational message to the trivial log.
        /// </summary>
        /// <param name="message">The content of the log entry.</param>
        public static void LogInfo(string message)
        {
            Log(message, Severity.Info);
        }

        /// <summary>
        ///     Logs a warning message to the verbose log.
        /// </summary>
        /// <param name="message">The content of the log entry.</param>
        public static void LogWarning(string message)
        {
            Log(message, Severity.Warning);
        }

        /// <summary>
        ///     Logs an error message to the very verbose log for high-visibility debugging.
        /// </summary>
        /// <param name="message">The content of the log entry.</param>
        public static void LogError(string message)
        {
            Log(message, Severity.Error);
        }

        private enum Severity
        {
            Info,
            Warning,
            Error
        }
    }
}