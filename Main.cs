using System;
using System.Collections.Concurrent;
using System.Reflection;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Config;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.Misc;
using ReportsPlus.Utils.WebSocket;
using ReportsPlus.Utils.WebSocket.Actions.Events;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus{
    public class Main : Plugin{
        private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        //Fibers
        private static GameFiber _continuousUpdateFiber;
        private static GameFiber _inputLockFiber;
        private static GameFiber _requestProcessingFiber;

        // WebSocket
        private static          GameClientSocket                 _client;
        private static readonly ConcurrentQueue<IncomingRequest> _requestQueue = new ConcurrentQueue<IncomingRequest>();

        // Data
        public static  bool                IsInputDisabled;
        public static  ReportsPlusSettings Settings { get; set; }
        private static GameClientSocket    Client   { get; set; }

        // Getters for lp and lpv
        public static Ped     LPC  => Game.LocalPlayer.Character;
        public static Vehicle LPCV => Game.LocalPlayer.Character?.CurrentVehicle;

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += LSPDFRFunctions_OnOnDutyStateChanged;
            Logger.LogInfo("ReportsPlus Plugin Initialized. Version: [" + Version + "]");
        }

        private void LSPDFRFunctions_OnOnDutyStateChanged(bool onduty)
        {
            Logger.LogInfo("IsOnDuty State Changed: '" + onduty + "'");

            Misc.CleanupPluginEvents();
            CleanupRegistry.RunCleanup();
            Client = null; // Clear the client when going off-duty

            if (!onduty) return;
            // On-Duty Initialization
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + Misc.RunPluginChecks());

            // TODO: TEMPORARY exit if not using policing redefined and common data framework
            if (!Misc.UsingPrFunctions)
            {
                Logger.LogError("Policing Redefined and Common Data Framework not found.");
                return;
            }

            Settings = ConfigLoader.LoadSettings<ReportsPlusSettings>("plugins/LSPDFR/ReportsPlus.ini");

            MessageHandler.Initialize();

            // Start Fibers
            _continuousUpdateFiber = GameFiber.StartNew(ContinuousUpdateLoop, "ReportsPlus-ContinuousUpdateFiber");
            _inputLockFiber        = GameFiber.StartNew(CheckForInputLock, "ReportsPlus-InputLockFiber");

            _requestProcessingFiber = GameFiber.StartNew(ProcessRequestQueueLoop, "ReportsPlus-RequestProcessingFiber");

            CleanupRegistry.Register(() => Misc.CleanupFiber(_continuousUpdateFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_inputLockFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_requestProcessingFiber));
        }

        private void ContinuousUpdateLoop()
        {
            try
            {
                Logger.LogInfo("Initializing Background Socket Client...");

                _client = new GameClientSocket(Settings.ClientAddress, Settings.ClientPort);
                Client  = _client;
                EventManager.SetClient(Client);

                // --- Event Subscriptions ---

                // 1. Enqueue incoming messages (ThreadPool -> Queue)
                _client.OnMessageReceived += request => _requestQueue.Enqueue(request);

                // 2. Handle Connection Event
                _client.OnConnected += () =>
                {
                    GameFiber.StartNew(() =>
                    {
                        Logger.LogInfo("Socket Connected!");
                        Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Connection Established", "Successfully connected to the CAD server.");
                    });
                };

                // 3. Handle Disconnection Event
                _client.OnDisconnected += () =>
                {
                    GameFiber.StartNew(() =>
                    {
                        Logger.LogWarning("Socket Disconnected!");
                        Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~r~Connection Lost", "Disconnected from the CAD server.");
                    });
                };

                CleanupRegistry.Register(() => EventManager.SetClient(null));

                _client.Start();

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Background Client Started", $"~y~Target: ~b~{Settings.ClientAddress}~y~:~b~{Settings.ClientPort}");

                while (true)
                {
                    GameFiber.Yield();

                    if (Client is { IsConnected: true }) MessageHandler.ExecuteContinuousActions(Client);
                    GameFiber.Sleep(Settings.ContinuousUpdateInterval);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"An exception occurred in the ContinuousUpdateLoop: {e.Message}");
                Logger.LogError($"StackTrace: {e.StackTrace}");
            }
        }

        /**
         * Fiber Loop: Consumes the request queue.
         * Runs on the Game Thread, so RAGE API calls in MessageHandler are safe.
         */
        private void ProcessRequestQueueLoop()
        {
            while (true)
            {
                GameFiber.Yield();

                // Process all pending requests in the queue
                while (_requestQueue.TryDequeue(out var request))
                    if (_client != null)
                        MessageHandler.ProcessMessage(_client, request);
            }
        }

        private static void CheckForInputLock()
        {
            while (true)
            {
                GameFiber.Yield();
                if (Game.IsKeyDown(Settings.InputLockKey))
                {
                    IsInputDisabled = !IsInputDisabled;
                    Logger.LogInfo($"InputLock Key Pressed. Is input disabled: [{IsInputDisabled}]");
                    Game.DisplayNotification(IsInputDisabled ? "All input DISABLED via keybind." : "All input ENABLED via keybind.");
                }

                if (Game.IsKeyDown(Settings.ReconnectKey))
                {
                    Logger.LogInfo("Reconnect Key Pressed..");
                    _client.AttemptConnection();
                }

                if (IsInputDisabled) NativeFunction.CallByHash<int>(0x5F4B6931816E599B, 0);
            }
        }

        public override void Finally()
        {
            if (_client != null)
            {
                _client.Stop();
                _client = null;
                Client  = null;
            }

            Misc.CleanupPluginEvents();
            CleanupRegistry.RunCleanup();
        }
    }
}