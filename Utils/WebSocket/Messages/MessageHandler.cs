using System.Collections.Generic;
using Rage;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.CustomEvents;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Continuous;
using ReportsPlus.Utils.WebSocket.Actions.Keybindings;
using ReportsPlus.Utils.WebSocket.Actions.OnRequest;

namespace ReportsPlus.Utils.WebSocket.Messages{
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
            ContinuousActions.Add(new VehicleTrackingAction());

            // Register On-Request Actions
            RegisterRequestAction(new RequestActions.PlayerLocationAction());
            RegisterRequestAction(new RequestActions.GameTimeAction());
            RegisterRequestAction(new RequestActions.HeartbeatAction());
            RegisterRequestAction(new RequestActions.FindPedByNameAction());
            RegisterRequestAction(new RequestActions.PoliceVehiclesAction());
            RegisterRequestAction(new RequestActions.FindPedByNameAction());
            RegisterRequestAction(new RequestActions.GiveCitationAction());
            RegisterRequestAction(new RequestActions.GiveParkingCitationAction());

            // Register Keybinding Actions
            RegisterKeybindingAction(new KeybindingActions.SirenKeybinding());
            RegisterKeybindingAction(new KeybindingActions.RepairKeybinding());
            RegisterKeybindingAction(new KeybindingActions.TimeKeybinding());
            RegisterKeybindingAction(new KeybindingActions.WeatherKeybinding());
            RegisterKeybindingAction(new KeybindingActions.InputLockKeybinding());
            RegisterKeybindingAction(new KeybindingActions.PanicKeybinding());

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
                case "custom_action":
                    var actionName = request.Data.ToString();
                    if (CustomActionRegistry.TryGetAction(actionName, out var customAction))
                    {
                        var customActionFiber = GameFiber.StartNew(() => ActionExecutor.ExecuteTarget(customAction), "ReportsPlus-CustomActionFiber");
                        CleanupRegistry.Register(() => Misc.Misc.CleanupFiber(customActionFiber));
                    }
                    else
                    {
                        Logger.LogWarning($"Received unknown custom_action: {actionName}");
                    }

                    break;
            }
        }
    }
}