using System;
using LSPD_First_Response.Mod.API;
using Rage;
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
        private static GameFiber _primaryFiber;
        private static GameClientSocket _client;

        private static bool _isOnDuty;
        public static AppSettings Settings { get; private set; }

        public static Ped LocalPlayer => Game.LocalPlayer.Character;

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            _isOnDuty = onDuty;
            Game.LogTrivial("ReportsPlus: IsOnDuty State Changed: '" + _isOnDuty + "'");
            RunFullCleanup();

            if (!_isOnDuty) return;
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + Misc.RunPluginChecks());

            ActionRegistry.Initialize();

            Settings = ConfigLoader.LoadSettings();

            _primaryFiber = GameFiber.StartNew(GameLoop, "ReportsPlus-PrimaryFiber");
        }

        // Main loop for connecting and sending updates to the server
        private static void GameLoop()
        {
            try
            {
                Game.LogTrivial("Connecting to server...");
                _client = new GameClientSocket(AppSettings.ClientAddress, AppSettings.ClientPort);

                GameClientSocket.Connect();

                if (!GameClientSocket.IsConnected)
                {
                    Game.LogTrivial("Connection failed.");
                    return;
                }

                Game.LogTrivial("--- Connection Established ---");

                // Keep sending updates while connected
                while (GameClientSocket.IsConnected)
                    /* TODO: add back
                    ActionRegistry.ExecuteAction<PlayerLocationAction>();
                    ActionRegistry.ExecuteAction<PoliceVehiclesAction>();*/
                    GameFiber.Sleep(500);
            }
            catch (Exception e)
            {
                Game.LogTrivial($"[ERROR] An exception occurred in the GameLoop: {e.Message}");
                Game.LogTrivial($"[ERROR] StackTrace: {e.StackTrace}");
            }
            finally
            {
                Game.LogTrivial("Client disconnected or fiber ended.");
                _client?.Disconnect();
            }
        }

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlus Plugin Initialized. Version: [" + Version + "]");
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            RunFullCleanup();
        }

        private static void RunFullCleanup()
        {
            Game.LogTrivial("ReportsPlus: Cleanup Running..");
            _client?.Disconnect();
            Misc.CleanupFiber(_primaryFiber);
            Game.LogTrivial("ReportsPlus: Cleaned Up.");
        }
    }
}