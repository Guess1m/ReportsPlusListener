using System;
using System.Collections.Generic;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.Cleanup
{
    public static class CleanupRegistry
    {
        private static readonly List<Action> CleanupActions = new List<Action>();
        private static readonly object LockObject = new object();

        /// <summary>
        ///     Thread-safely adds a new cleanup delegate to the internal registry for deferred execution.
        /// </summary>
        /// <param name="cleanupAction">The <see cref="Action" /> containing the cleanup logic to be stored.</param>
        public static void Register(Action cleanupAction)
        {
            if (cleanupAction == null)
            {
                Logger.LogWarning("Cannot register null cleanup action.");
                return;
            }

            lock (LockObject) // race cond for cleanups on WebSocket thread
            {
                Logger.LogInfo($"Registering cleanup action: {cleanupAction.Method.Name}");
                CleanupActions.Add(cleanupAction);
            }
        }

        /// <summary>
        ///     Atomically retrieves and executes every registered cleanup action in a safe manner.
        ///     Ensures that each action is invoked within a try-catch block to prevent a single failure from halting the entire
        ///     cleanup process.
        /// </summary>
        public static void RunCleanup()
        {
            List<Action> actionsToRun;
            lock (LockObject) // race cond for cleanups on WebSocket thread
            {
                actionsToRun = new List<Action>(CleanupActions);
                CleanupActions.Clear();
            }

            if (actionsToRun.Count == 0) return;

            Logger.LogInfo($"Running {actionsToRun.Count} cleanup action(s)...");
            foreach (var action in actionsToRun)
                try
                {
                    Logger.LogInfo($"Executing cleanup action: {action.Method.Name}");
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during cleanup action '{action.Method.Name}': {ex.Message}");
                }

            Logger.LogInfo("Cleanup finished.");
        }
    }
}