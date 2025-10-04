using System;
using System.Collections.Generic;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WebSocket.Updates.Continuous;
using ReportsPlus.Utils.WebSocket.Updates.OnRequest;

namespace ReportsPlus.Utils.WebSocket.Updates
{
    public static class ActionRegistry
    {
        private static readonly Dictionary<string, IWebSocketAction> ActionsByName = new Dictionary<string, IWebSocketAction>();
        private static readonly Dictionary<Type, IWebSocketAction> ActionsByType = new Dictionary<Type, IWebSocketAction>();
        private static readonly List<IWebSocketAction> ContinuousActions = new List<IWebSocketAction>(); // List for continuous actions

        public static void Initialize()
        {
            ActionsByName.Clear();
            ActionsByType.Clear();
            ContinuousActions.Clear(); // Clear the new list on initialization

            // Register all your actions here
            Register(new GameTimeAction());
            Register(new PlayerLocationAction());
            Register(new PoliceVehiclesAction());
            // Register other actions like CalloutUpdate if needed, their IsContinuous property will handle the rest.

            Logger.LogInfo($"ActionRegistry initialized. {ActionsByName.Count} actions registered. {ContinuousActions.Count} continuous actions found.");
        }

        private static void Register(IWebSocketAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Name)) return;

            var actionType = action.GetType();

            if (ActionsByName.ContainsKey(action.Name) || ActionsByType.ContainsKey(actionType)) Logger.LogWarning($"Action '{action.Name}' or type '{actionType.Name}' is already registered. Overwriting.");

            // Register in dictionaries
            ActionsByName[action.Name] = action;
            ActionsByType[actionType] = action;

            // If the action is continuous, add it to our dedicated list
            if (action.IsContinuous) ContinuousActions.Add(action);
        }

        // New method to execute all registered continuous actions
        public static void ExecuteContinuousActions()
        {
            foreach (var action in ContinuousActions) action.Execute(null);
        }

        public static void HandleRequest(IncomingRequest request)
        {
            // The server sends "request" in the 'type' field
            if (request.Type != "request") return;

            // The 'data' field contains the name of the action to run
            var actionName = request.Data.ToString();
            if (ActionsByName.TryGetValue(actionName, out var action))
            {
                Logger.LogDebug($"Executing action for request: '{actionName}'");
                action.Execute(request);
            }
            else
            {
                Logger.LogWarning($" No action found for request type: '{actionName}'");
            }
        }

        public static void ExecuteAction<T>() where T : IWebSocketAction
        {
            if (ActionsByType.TryGetValue(typeof(T), out var action))
                action.Execute(null);
            else
                Logger.LogWarning($" Could not execute proactive action. No action found with type: '{typeof(T).Name}'");
        }
    }
}