using System.Collections.Concurrent;
using System.Collections.Generic;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    public static class CustomActionRegistry{
        // Thread-safe dictionary to store actions received from Java
        private static readonly ConcurrentDictionary<string, CustomActionConfig> Actions = new ConcurrentDictionary<string, CustomActionConfig>();

        /// <summary>
        ///     Clears existing actions and registers a new list received from the server.
        /// </summary>
        public static void RegisterActions(List<CustomActionConfig> newActions)
        {
            Actions.Clear();
            foreach (var action in newActions)
            {
                // Key is lowercase for case-insensitive lookup
                Actions[action.Name.ToLower()] = action;
                Logger.LogInfo($"[Registry] Registered Action: {action.Name} -> {action.Target} [{action.Parameters?.Count ?? 0} params]");
            }

            Logger.LogInfo($"[Registry] Total Custom Actions: {Actions.Count}");
        }

        public static bool TryGetAction(string name, out CustomActionConfig action)
        {
            if (string.IsNullOrEmpty(name))
            {
                action = null;
                return false;
            }

            return Actions.TryGetValue(name.ToLower(), out action);
        }
    }
}