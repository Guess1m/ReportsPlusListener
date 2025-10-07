using System;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Config;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket;
using ReportsPlus.Utils.WebSocket.Updates;
using ReportsPlus.Utils.WebSocket.Updates.Continuous;

namespace ReportsPlus{
    public class Main : Plugin{
        private const  string           Version = "v2.0.0";
        private static bool             _isOnDuty;
        private static GameFiber        _primaryFiber;
        private static GameClientSocket _client;

        public static GameClientSocket Client { get; private set; }

        private static ReportsPlusSettings Settings { get; set; }
        public static  Ped                 LPC      => Game.LocalPlayer.Character;
        public static  Vehicle             LPCV     => Game.LocalPlayer.Character?.CurrentVehicle;

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

            ActionRegistry.Initialize();

            Settings = ConfigLoader.LoadSettings<ReportsPlusSettings>("plugins/LSPDFR/ReportsPlus.ini");

            _primaryFiber = GameFiber.StartNew(GameLoop, "ReportsPlus-PrimaryFiber");

            // Register Cleanup Actions
            CleanupRegistry.Register(() => Misc.CleanupFiber(_primaryFiber));
            CleanupRegistry.Register(PoliceVehiclesAction.ClearTrackedPoliceVehicles);
        }

        private static void GameLoop()
        {
            try
            {
                GameFiber.Yield();
                Logger.LogInfo("Connecting to server...");
                _client = new GameClientSocket(ReportsPlusSettings.ClientAddress, ReportsPlusSettings.ClientPort);
                Client  = _client;

                Client.Connect();

                if (!Client.IsConnected)
                {
                    Logger.LogError("Connection failed.");
                    _client = null; // Ensure the client is null if connection failed
                    Client  = null;
                    return;
                }

                // Only register the disconnect cleanup action AFTER a successful connection
                CleanupRegistry.Register(() => _client?.Disconnect());
                Logger.LogInfo("--- Connection Established ---");

                while (Client.IsConnected)
                {
                    ActionRegistry.ExecuteContinuousActions(Client); // You'd pass Client here if you did the DI refactor
                    GameFiber.Sleep(ReportsPlusSettings.ContinuousUpdateInterval);
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

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            // Final cleanup on plugin unload.
            CleanupRegistry.RunCleanup();
        }
    }
}