using System;
using System.Collections.Generic;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.Cleanup{
    public static class CleanupRegistry{
        private static readonly List<Action> CleanupActions = new List<Action>();
        private static readonly object       LockObject     = new object();

        /// <summary>
        ///     Registers a cleanup action to be executed later.
        /// </summary>
        /// <param name="cleanupAction">The action to perform on cleanup.</param>
        public static void Register(Action cleanupAction)
        {
            if (cleanupAction == null)
            {
                Logger.LogWarning("Cannot register null cleanup action.");
                return;
            }

            lock (LockObject)
            {
                Logger.LogDebug($"Registering cleanup action: {cleanupAction.Method.Name}");
                CleanupActions.Add(cleanupAction);
            }
        }

        /// <summary>
        ///     Executes all registered cleanup actions and clears the registry.
        /// </summary>
        public static void RunCleanup()
        {
            List<Action> actionsToRun;
            lock (LockObject)
            {
                actionsToRun = new List<Action>(CleanupActions);
                CleanupActions.Clear();
            }

            if (actionsToRun.Count == 0) return;

            Logger.LogInfo($"Running {actionsToRun.Count} cleanup action(s)...");
            foreach (var action in actionsToRun)
                try
                {
                    Logger.LogDebug($"Executing cleanup action: {action.Method.Name}");
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