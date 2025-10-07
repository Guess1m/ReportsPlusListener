using System;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using ReportsPlus.Utils;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Config;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket;

namespace ReportsPlus{
    public class Main : Plugin{
        private const  string              Version = "v2.0.0";
        private static bool                _isOnDuty;
        private static GameFiber           _primaryFiber;
        private static GameFiber           _inputLockFiber;
        private static GameClientSocket    _client;
        public static  bool                IsInputDisabled;
        private static ReportsPlusSettings Settings { get; set; }

        private static GameClientSocket Client { get; set; }

        public static Ped     LPC  => Game.LocalPlayer.Character;
        public static Vehicle LPCV => Game.LocalPlayer.Character?.CurrentVehicle;

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Logger.LogInfo("ReportsPlus Plugin Initialized. Version: [" + Version + "]");
        }

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            _isOnDuty = onDuty;
            Logger.LogInfo("IsOnDuty State Changed: '" + _isOnDuty + "'");

            // Cleanup previous data
            CleanupRegistry.RunCleanup();
            Client = null; // Clear the client when going off-duty

            if (!_isOnDuty) return;

            // On-Duty Initialization
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + Misc.RunPluginChecks());

            // Load settings before starting the fiber that uses them
            Settings = ConfigLoader.LoadSettings<ReportsPlusSettings>("plugins/LSPDFR/ReportsPlus.ini");

            MessageHandler.Initialize();

            _primaryFiber   = GameFiber.StartNew(GameLoop, "ReportsPlus-PrimaryFiber");
            _inputLockFiber = GameFiber.StartNew(CheckForInputLock, "ReportsPlus-InputLockFiber");

            // Register Cleanup Actions
            CleanupRegistry.Register(() => Misc.CleanupFiber(_primaryFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_inputLockFiber));
        }

        private void GameLoop()
        {
            try
            {
                GameFiber.Yield();
                Logger.LogInfo("Connecting to server...");
                _client = new GameClientSocket(Settings.ClientAddress, Settings.ClientPort);
                Client  = _client;
                EventManager.SetClient(Client);
                CleanupRegistry.Register(() => EventManager.SetClient(null));

                Client.Connect();

                if (!Client.IsConnected)
                {
                    Logger.LogError("Connection failed.");
                    _client = null;
                    Client  = null;
                    return;
                }

                // Only register the disconnect cleanup action AFTER a successful connection
                CleanupRegistry.Register(() => _client?.Disconnect());
                Logger.LogInfo("--- Connection Established ---");

                while (Client.IsConnected)
                {
                    GameFiber.Yield();
                    MessageHandler.ExecuteContinuousActions(Client);
                    GameFiber.Sleep(Settings.ContinuousUpdateInterval);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"An exception occurred in the GameLoop: {e.Message}");
                Logger.LogError($"StackTrace: {e.StackTrace}");
            }
            finally
            {
                Logger.LogWarning("Client disconnected or fiber ended.");
            }
        }

        private static void CheckForInputLock()
        {
            while (_isOnDuty)
            {
                GameFiber.Yield();

                if (Game.IsKeyDown(Settings.InputLockKey))
                {
                    IsInputDisabled = !IsInputDisabled;
                    Logger.LogDebug($"InputLock Key Pressed. Is input disabled: [{IsInputDisabled}]");
                    Game.DisplayNotification(IsInputDisabled ? "All input DISABLED via keybind." : "All input ENABLED via keybind.");
                }

                if (IsInputDisabled) NativeFunction.CallByHash<int>(0x5F4B6931816E599B, 0);
            }
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            // Final cleanup on plugin unload.
            CleanupRegistry.RunCleanup();
        }
    }
}