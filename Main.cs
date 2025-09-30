using System;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Logging;
using ReportsPlus.Updates;
using ReportsPlus.Utils;
using ReportsPlus.Utils.Config;
using ReportsPlus.WebSocket;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        private const string Version = "v2.0.0";
        private static bool _isOnDuty;
        private static GameFiber _primaryFiber;
        private static GameClientSocket _client;

        private static ReportsPlusSettings Settings { get; set; }
        public static Ped LocalPlayer => Game.LocalPlayer.Character;

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            _isOnDuty = onDuty;
            Logger.LogInfo("IsOnDuty State Changed: '" + _isOnDuty + "'");
            RunFullCleanup();

            if (!_isOnDuty) return;
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + Misc.RunPluginChecks());

            ActionRegistry.Initialize();

            Settings = ConfigLoader.LoadSettings<ReportsPlusSettings>("plugins/LSPDFR/ReportsPlus.ini");

            _primaryFiber = GameFiber.StartNew(GameLoop, "ReportsPlus-PrimaryFiber");
        }

        // Main loop for connecting and sending updates to the server
        private static void GameLoop()
        {
            try
            {
                Logger.LogInfo("Connecting to server...");
                _client = new GameClientSocket(ReportsPlusSettings.ClientAddress, ReportsPlusSettings.ClientPort);

                GameClientSocket.Connect();

                if (!GameClientSocket.IsConnected)
                {
                    Logger.LogError("Connection failed.");
                    return;
                }

                Logger.LogInfo("--- Connection Established ---");

                // Keep sending updates while connected
                while (GameClientSocket.IsConnected)
                    /* TODO: add back
                    ActionRegistry.ExecuteAction<PlayerLocationAction>();
                    ActionRegistry.ExecuteAction<PoliceVehiclesAction>();*/
                    GameFiber.Sleep(500);
            }
            catch (Exception e)
            {
                Logger.LogError($"An exception occurred in the GameLoop: {e.Message}");
                Logger.LogError($"StackTrace: {e.StackTrace}");
            }
            finally
            {
                Logger.LogWarning("Client disconnected or fiber ended.");
                _client?.Disconnect();
            }
        }

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Logger.LogInfo("ReportsPlus Plugin Initialized. Version: [" + Version + "]");
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            RunFullCleanup();
        }

        private static void RunFullCleanup()
        {
            Logger.LogDebug("Cleanup Running..");
            _client?.Disconnect();
            Misc.CleanupFiber(_primaryFiber);
            Logger.LogInfo("Cleaned Up.");
        }
    }
}