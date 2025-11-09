using System.Collections.Generic;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Continuous;
using ReportsPlus.Utils.WebSocket.Actions.Interfaces;
using ReportsPlus.Utils.WebSocket.Actions.Keybindings;
using ReportsPlus.Utils.WebSocket.Actions.OnRequest;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket{
    public static class MessageHandler{
        private static readonly List<IContinuousAction>               ContinuousActions = new List<IContinuousAction>();
        private static readonly Dictionary<string, IRequestAction>    RequestActions    = new Dictionary<string, IRequestAction>();
        private static readonly Dictionary<string, IKeybindingAction> KeybindingActions = new Dictionary<string, IKeybindingAction>();

        public static void Initialize()
        {
            ContinuousActions.Clear();
            RequestActions.Clear();
            KeybindingActions.Clear();

            // Register Continuous Actions
            ContinuousActions.Add(new EntityTrackingAction());

            // Register On-Request Actions
            RegisterRequestAction(new PlayerLocationAction());
            RegisterRequestAction(new GameTimeAction());
            RegisterRequestAction(new HeartbeatAction());
            RegisterRequestAction(new FindPedByNameAction());
            RegisterRequestAction(new PoliceVehiclesAction());
            RegisterRequestAction(new FindPedByNameAction());

            // Register Keybinding Actions
            RegisterKeybindingAction(new SirenKeybinding());
            RegisterKeybindingAction(new RepairKeybinding());
            RegisterKeybindingAction(new TimeKeybinding());
            RegisterKeybindingAction(new WeatherKeybinding());
            RegisterKeybindingAction(new InputLockKeybinding());

            Logger.LogInfo("MessageHandler initialized.");
        }

        private static void RegisterRequestAction(IRequestAction action)
        {
            RequestActions[action.Name] = action;
        }

        private static void RegisterKeybindingAction(IKeybindingAction action)
        {
            KeybindingActions[action.Name] = action;
        }

        public static void ExecuteContinuousActions(GameClientSocket client)
        {
            foreach (var action in ContinuousActions) action.Execute(client);
        }

        public static void ProcessMessage(GameClientSocket client, IncomingRequest request)
        {
            switch (request.Type)
            {
                case "request":
                    if (RequestActions.TryGetValue(request.Data.ToString(), out var requestAction)) requestAction.Execute(client, request);
                    break;

                case "keybinding":
                    if (KeybindingActions.TryGetValue(request.Data.ToString(), out var keybindingAction)) keybindingAction.Execute(request);
                    break;
            }
        }
    }
}