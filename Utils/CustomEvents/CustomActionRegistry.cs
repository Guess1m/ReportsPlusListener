using System.Collections.Concurrent;
using System.Collections.Generic;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    /// <summary>
    ///     Manages the registration and retrieval of custom actions received from external sources.
    /// </summary>
    public static class CustomActionRegistry{
        private static readonly ConcurrentDictionary<string, CustomActionConfig> Actions = new ConcurrentDictionary<string, CustomActionConfig>();

        /// <summary>
        ///     Clears the existing registry and populates it with a new collection of action configurations received from the
        ///     server.
        /// </summary>
        /// <param name="newActions">The list of <see cref="CustomActionConfig" /> objects to register.</param>
        public static void RegisterActions(List<CustomActionConfig> newActions)
        {
            if (newActions == null)
            {
                Logger.LogError("[Registry] Received null action list.");
                return;
            }

            Actions.Clear();
            foreach (var action in newActions)
            {
                if (action == null || string.IsNullOrEmpty(action.Name)) continue;
                Actions[action.Name.ToLower()] = action;
                Logger.LogInfo($"[Registry] Registered Action: {action.Name} -> {action.Target} [{action.Parameters?.Count ?? 0} params]");
            }

            Logger.LogInfo($"[Registry] Total Custom Actions: {Actions.Count}");
        }

        /// <summary>
        ///     Attempts to retrieve a registered action configuration by its unique identifier.
        /// </summary>
        /// <param name="name">The name of the action to search for.</param>
        /// <param name="action">
        ///     When this method returns, contains the found <see cref="CustomActionConfig" />, or null if the
        ///     action was not found.
        /// </param>
        /// <returns>True if the action is registered; otherwise, false.</returns>
        public static bool TryGetAction(string name, out CustomActionConfig action)
        {
            if (!string.IsNullOrEmpty(name)) return Actions.TryGetValue(name.ToLower(), out action);
            action = null;
            return false;
        }
    }
}