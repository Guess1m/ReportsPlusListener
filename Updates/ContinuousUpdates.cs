using System.Collections.Generic;
using ReportsPlus.Logging;

namespace ReportsPlus.Updates
{
    public class ContinuousUpdates
    {
        public static List<IWebSocketAction> Actions = new List<IWebSocketAction>();

        public static void Register(IWebSocketAction action)
        {
            if (action == null)
            {
                Logger.LogWarning("Attempted to register null action");
                return;
            }

            if (Actions.Contains(action))
            {
                Logger.LogWarning($"Action '{action.Name}' is already registered in ContinuousUpdates.");
                return;
            }

            Actions.Add(action);
        }
    }
}