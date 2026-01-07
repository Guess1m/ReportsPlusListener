using System;
using System.Collections.Generic;
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
            RegisterRequestAction(new RequestActions.FindLocationAction());
            RegisterRequestAction(new RequestActions.HeartbeatAction());
            RegisterRequestAction(new RequestActions.FindPedByNameAction());
            RegisterRequestAction(new RequestActions.FindVehicleByPlateAction());
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

        /**
         * Processes an incoming request immediately.
         * Optimization: This method NO LONGER creates a new GameFiber.
         * It MUST be called from an existing GameFiber (e.g., the RequestProcessingFiber in Main).
         */
        public static void ProcessMessage(GameClientSocket client, IncomingRequest request)
        {
            try
            {
                if (request == null) return;

                switch (request.Type)
                {
                    case "request":
                        if (request.Data != null && RequestActions.TryGetValue(request.Data.ToString(), out var requestAction))
                            requestAction.Execute(client, request);
                        else
                            Logger.LogWarning($"Unknown or invalid request action: {request.Data}");
                        break;

                    case "keybinding":
                        if (request.Data != null && KeybindingActions.TryGetValue(request.Data.ToString(), out var keybindingAction))
                            keybindingAction.Execute(request);
                        else
                            Logger.LogWarning($"Unknown or invalid keybinding action: {request.Data}");
                        break;

                    case "custom_action":
                        var actionName = request.Data?.ToString();
                        if (!string.IsNullOrEmpty(actionName) && CustomActionRegistry.TryGetAction(actionName, out var customAction))
                            ActionExecutor.ExecuteTarget(customAction);
                        else
                            Logger.LogWarning($"Received unknown or invalid custom_action: {actionName}");

                        break;

                    default:
                        Logger.LogWarning($"Unknown message type received: {request.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception occurred while processing message of type '{request?.Type}': {ex.Message}");
            }
        }
    }
}