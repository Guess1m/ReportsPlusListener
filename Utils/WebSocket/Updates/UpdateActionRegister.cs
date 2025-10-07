using System;
using System.Collections.Generic;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WebSocket.Updates.Continuous;
using ReportsPlus.Utils.WebSocket.Updates.OnRequest;

namespace ReportsPlus.Utils.WebSocket.Updates{
    public static class ActionRegistry{
        private static readonly Dictionary<string, IWebSocketAction> ActionsByName     = new Dictionary<string, IWebSocketAction>();
        private static readonly Dictionary<Type, IWebSocketAction>   ActionsByType     = new Dictionary<Type, IWebSocketAction>();
        private static readonly List<IWebSocketAction>               ContinuousActions = new List<IWebSocketAction>(); // List for continuous actions

        public static void Initialize()
        {
            ActionsByName.Clear();
            ActionsByType.Clear();
            ContinuousActions.Clear(); // Clear the new list on initialization

            Register(new GameTimeAction());
            Register(new PlayerLocationAction());
            Register(new PoliceVehiclesAction());

            Logger.LogInfo($"ActionRegistry initialized. {ActionsByName.Count} actions registered. {ContinuousActions.Count} continuous actions found.");
        }

        private static void Register(IWebSocketAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Name)) return;

            var actionType = action.GetType();

            if (ActionsByName.ContainsKey(action.Name) || ActionsByType.ContainsKey(actionType)) Logger.LogWarning($"Action '{action.Name}' or type '{actionType.Name}' is already registered. Overwriting.");

            // Register in dictionaries
            ActionsByName[action.Name] = action;
            ActionsByType[actionType]  = action;

            if (action.IsContinuous) ContinuousActions.Add(action);
        }

        public static void ExecuteContinuousActions(GameClientSocket client)
        {
            foreach (var action in ContinuousActions) action.Execute(client, null);
        }

        public static void HandleRequest(GameClientSocket client, IncomingRequest request)
        {
            // The server sends "request" in the 'type' field
            if (request.Type != "request") return;

            // The 'data' field contains the name of the action to run
            var actionName = request.Data.ToString();
            if (ActionsByName.TryGetValue(actionName, out var action))
            {
                Logger.LogDebug($"Executing action for request: '{actionName}'");
                action.Execute(client, request);
            }
            else
            {
                Logger.LogWarning($" No action found for request type: '{actionName}'");
            }
        }

        // TODO: handle keybindings in their own class with easy registration using a dictionary
        public static void HandleKeybinding(IncomingRequest request)
        {
            // The server sends "request" in the 'type' field
            if (request.Type != "keybinding") return;

            // The 'data' field contains the name of the action to run
            var data = request.Data.ToString();

            // The 'args' field contains the name of the action to run; if there is any
            var args = request.Args;

            switch (data)
            {
                case "siren":
                    switch (args)
                    {
                        case "blip":
                            Main.LPCV.BlipSiren(true);
                            Logger.LogInfo("Siren blipped");
                            break;
                        case "toggle":
                            if (Main.LPCV.HasSiren)
                            {
                                Main.LPCV.IsSirenOn = !Main.LPCV.IsSirenOn;
                                Logger.LogInfo($"Siren toggle: {Main.LPCV.IsSirenOn}");
                            }

                            break;
                    }

                    break;
                case "repair":
                    Main.LPCV.Repair();
                    Logger.LogInfo("Vehicle repaired.");
                    break;

                case "time":
                    var random = new Random();
                    var hour   = random.Next(0, 24);
                    var minute = random.Next(0, 60);
                    World.TimeOfDay = new TimeSpan(hour, minute, 0);
                    Game.DisplayNotification($"Time set to {hour}:{minute}");
                    Logger.LogInfo($"Time set to {hour}:{minute}");
                    break;

                case "weather":
                    var random2 = new Random();
                    var weather = random2.Next(0, 4);
                    switch (weather)
                    {
                        case 0:
                            World.Weather = WeatherType.Clear;
                            Game.DisplayNotification("Weather set to Clear");
                            Logger.LogInfo("Weather set to Clear");
                            break;
                        case 1:
                            World.Weather = WeatherType.Foggy;
                            Game.DisplayNotification("Weather set to Foggy");
                            Logger.LogInfo("Weather set to Foggy");
                            break;
                        case 2:
                            World.Weather = WeatherType.Thunder;
                            Game.DisplayNotification("Weather set to Thunder");
                            Logger.LogInfo("Weather set to Thunder");
                            break;
                        case 3:
                            World.Weather = WeatherType.Snow;
                            Game.DisplayNotification("Weather set to Snow");
                            Logger.LogInfo("Weather set to Snow");
                            break;
                    }

                    break;
            }
        }
    }
}