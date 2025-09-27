using System;
using System.Collections.Generic;
using Rage;
using RPWebSocketPlugin.Messages;

namespace ReportsPlus.Updates
{
    public static class ActionRegistry
    {
        // Stores actions by name (string key)
        private static readonly Dictionary<string, IWebSocketAction> ActionsByName = new Dictionary<string, IWebSocketAction>();

        // Stores actions by their type
        private static readonly Dictionary<Type, IWebSocketAction> ActionsByType = new Dictionary<Type, IWebSocketAction>();

        // Initializes and registers all actions
        public static void Initialize()
        {
            ActionsByName.Clear();
            ActionsByType.Clear();

            Register(new GameTimeAction());
            Register(new PlayerLocationAction());
            Register(new PoliceVehiclesAction());
            Game.LogTrivial($"ActionRegistry initialized. {ActionsByName.Count} actions registered.");
        }

        // Registers an action in both dictionaries
        private static void Register(IWebSocketAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Name)) return;

            var actionType = action.GetType();

            if (ActionsByName.ContainsKey(action.Name) || ActionsByType.ContainsKey(actionType)) Game.LogTrivial($"[WARNING] Action '{action.Name}' or type '{actionType.Name}' is already registered. Overwriting.");

            ActionsByName[action.Name] = action;
            ActionsByType[actionType] = action;
        }

        // Handles incoming requests from the server
        public static void HandleRequest(IncomingRequest request)
        {
            if (request.Type != "request") return;

            if (ActionsByName.TryGetValue(request.Data, out var action))
            {
                Game.LogTrivial($"Executing action for request: '{request.Data}'");
                action.Execute(request);
            }
            else
            {
                Game.LogTrivial($"[WARNING] No action found for request type: '{request.Data}'");
            }
        }

        // Executes a proactive action by type (without waiting for a request)
        public static void ExecuteAction<T>() where T : IWebSocketAction
        {
            if (ActionsByType.TryGetValue(typeof(T), out var action))
                action.Execute(null);
            else
                Game.LogTrivial($"[WARNING] Could not execute proactive action. No action found with type: '{typeof(T).Name}'");
        }
    }
}