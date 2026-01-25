using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Forms;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using RAGENativeUI;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Config;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.Menu;
using ReportsPlus.Utils.Misc;
using ReportsPlus.Utils.WebSocket;
using ReportsPlus.Utils.WebSocket.Actions.Events;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        //Fibers
        private static GameFiber _continuousUpdateFiber;
        private static GameFiber _inputLockFiber;
        private static GameFiber _menuPoolFiber;
        private static GameFiber _requestProcessingFiber;

        // WebSocket
        private static GameClientSocket _client;
        private static readonly ConcurrentQueue<IncomingRequest> _requestQueue = new ConcurrentQueue<IncomingRequest>(); // incoming request queue

        // Data
        public static bool IsInputDisabled;
        public static bool IsKeyboardOpen;
        private static GameClientSocket Client { get; set; }
        public static bool IsConnected => Client is { IsConnected: true }; // safe access to client state
        public static ReportsPlusSettings Settings { get; set; }

        // Citation State Triggers
        public static bool IsCitationPending { get; set; }
        public static bool TriggerGiveCitation { get; set; }

        public static bool TriggerDiscardCitation { get; set; }

        // Menu Data
        public static MenuPool Pool { get; private set; } // primary menu-pool
        private static ReportsPlusMenu MainMenu { get; set; }         // primary menu

        // Getters for lp and lpv
        public static Ped LPC => Game.LocalPlayer.Character;
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
            Client = null;
            Pool = null;

            if (!onduty) return;
            // On-Duty Initialization
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + Misc.RunPluginChecks());

            Settings = ConfigLoader.LoadSettings<ReportsPlusSettings>("plugins/LSPDFR/ReportsPlus.ini");

            // initialize menu-pool
            Pool = new MenuPool();
            MainMenu = new ReportsPlusMenu();

            MessageHandler.Initialize();

            // Start Fibers
            _continuousUpdateFiber = GameFiber.StartNew(ContinuousUpdateLoop, "ReportsPlus-ContinuousUpdateFiber");
            _inputLockFiber = GameFiber.StartNew(CheckForInputLock, "ReportsPlus-InputLockFiber");
            _menuPoolFiber = GameFiber.StartNew(ProcessMenuPool, "ReportsPlus-MenuPoolFiber");
            _requestProcessingFiber = GameFiber.StartNew(ProcessRequestQueueLoop, "ReportsPlus-RequestProcessingFiber");

            // Register Cleanups
            CleanupRegistry.Register(() => Misc.CleanupFiber(_continuousUpdateFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_inputLockFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_menuPoolFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_requestProcessingFiber));
            CleanupRegistry.Register(() => Pool = null);
            CleanupRegistry.Register(() => EventManager.SetClient(null));
        }

        /// <summary>
        ///     Background loop that executes continuous update actions if a client connection exists.
        /// </summary>
        private void ContinuousUpdateLoop()
        {
            try
            {
                Logger.LogInfo("Starting Background Service Loop...");

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

        /// <summary>
        ///     Initializes or resets the GameClientSocket connection using current settings and logs the connection details.
        /// </summary>
        public static void AttemptConnection()
        {
            if (Settings == null)
            {
                Logger.LogError("Cannot attempt connection: Settings are null.");
                return;
            }

            Logger.LogInfo($"Initiating connection attempt | Host: {Settings.ClientAddress} | Port: {Settings.ClientPort}");

            if (_client != null)
                try
                {
                    Logger.LogInfo("Stopping existing client instance...");
                    _client.Stop();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error stopping old client (safe to ignore): {ex.Message}");
                }
                finally
                {
                    _client = null;
                    Client = null;
                }

            try
            {
                _client = new GameClientSocket(Settings.ClientAddress, Settings.ClientPort);
                Client = _client;
                EventManager.SetClient(Client);

                // enqueue incoming messages (ThreadPool to Queue)
                _client.OnMessageReceived += request => _requestQueue.Enqueue(request);

                // connection Event
                _client.OnConnected += () =>
                {
                    var socketConnectedFiber = GameFiber.StartNew(() =>
                    {
                        Logger.LogInfo($"Socket Connection Established -> {Settings.ClientAddress}:{Settings.ClientPort}");
                        Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Connection Established", $"Connected to ~y~{Settings.ClientAddress}~w~:~b~{Settings.ClientPort}");
                    });
                    CleanupRegistry.Register(() => Misc.CleanupFiber(socketConnectedFiber));
                };

                // disconnection Event
                _client.OnDisconnected += () =>
                {
                    var socketDisconnectedFiber = GameFiber.StartNew(() =>
                    {
                        Logger.LogWarning("Socket Disconnected!");
                        Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~r~Connection Lost", $"~r~Disconnected ~w~from the CAD server at ~y~{Settings.ClientAddress}~w~:~b~{Settings.ClientPort}");
                    });
                    CleanupRegistry.Register(() => Misc.CleanupFiber(socketDisconnectedFiber));
                };

                _client.Start();
                Logger.LogInfo("Client start sequence completed.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize client: {ex.Message}");
                Game.DisplayNotification("~r~Failed to initialize client connection.");
            }
        }

        /// <summary>
        ///     Fiber loop that processes the incoming request queue on the main game thread for safe API access.
        /// </summary>
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
                if (Settings != null && Settings.InputLockKey.Key != Keys.None && Settings.InputLockKey.IsPressed())
                {
                    IsInputDisabled = !IsInputDisabled;
                    Logger.LogInfo($"InputLock Key Pressed. Is input disabled: [{IsInputDisabled}]");
                    Game.DisplayNotification(IsInputDisabled ? "All input DISABLED via keybind." : "All input ENABLED via keybind.");
                    while (Settings != null && Settings.InputLockKey.IsPressed())
                        GameFiber.Yield();
                }
                if (IsInputDisabled) NativeFunction.CallByHash<int>(0x5F4B6931816E599B, 0);
            }
        }
        private static void ProcessMenuPool()
        {
            while (true)
            {
                GameFiber.Yield();
                if (Pool == null) continue;
                if (!IsKeyboardOpen) Pool.ProcessMenus();
                if (Settings == null || Settings.MenuKey.Key == Keys.None || !Settings.MenuKey.IsPressed())
                    continue;
                if (Pool.IsAnyMenuOpen())
                {
                    Pool.CloseAllMenus();
                }
                else
                {
                    if (MainMenu == null) continue;
                    Pool.CloseAllMenus();
                    MainMenu.Visible = true;
                }
                // Wait for release so the menu doesn't flicker on/off
                while (Settings.MenuKey.IsPressed())
                    GameFiber.Yield();
            }
        }

        /// <summary>
        ///     Performs final cleanup of socket connections and fibers when the plugin is stopped.
        /// </summary>
        public override void Finally()
        {
            if (_client != null)
            {
                _client.Stop();
                _client = null;
                Client = null;
            }

            Misc.CleanupPluginEvents();
            CleanupRegistry.RunCleanup();
        }
    }
}