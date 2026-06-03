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
        private static GameFiber _autoConnectFiber;
        private static GameFiber _statusOverlayFiber;

        // WebSocket
        private static GameClientSocket _client;
        private static readonly ConcurrentQueue<IncomingRequest> _requestQueue = new ConcurrentQueue<IncomingRequest>(); // incoming request queue

        // Data
        private static GameClientSocket Client { get; set; }
        public static ReportsPlusSettings Settings { get; set; }
        public static bool IsInputDisabled;
        public static bool IsKeyboardOpen;
        public static bool IsConnected => Client is { IsConnected: true };
        public static bool IsConnecting => Client is { IsConnecting: true };

        // Citation State Triggers
        public static bool IsCitationPending { get; set; }
        public static bool TriggerGiveCitation { get; set; }
        public static bool TriggerDiscardCitation { get; set; }

        // Menu Data
        public static MenuPool Pool { get; private set; }
        private static ReportsPlusMenu MainMenu { get; set; }

        // Getters for lp and lpv
        public static Ped LPC => Game.LocalPlayer.Character;
        public static Vehicle LPCV => Game.LocalPlayer.Character?.CurrentVehicle;

        private static volatile int _autoConnectAttempts = 0;

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
            _autoConnectFiber = GameFiber.StartNew(AutoConnectLoop, "ReportsPlus-AutoConnectFiber");
            _continuousUpdateFiber = GameFiber.StartNew(ContinuousUpdateLoop, "ReportsPlus-ContinuousUpdateFiber");
            _inputLockFiber = GameFiber.StartNew(CheckForInputLock, "ReportsPlus-InputLockFiber");
            _menuPoolFiber = GameFiber.StartNew(ProcessMenuPool, "ReportsPlus-MenuPoolFiber");
            _requestProcessingFiber = GameFiber.StartNew(ProcessRequestQueueLoop, "ReportsPlus-RequestProcessingFiber");
            _statusOverlayFiber = GameFiber.StartNew(StatusOverlayLoop, "ReportsPlus-StatusOverlayFiber");

            // Register Cleanups
            CleanupRegistry.Register(() => Misc.CleanupFiber(_autoConnectFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_continuousUpdateFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_inputLockFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_menuPoolFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_requestProcessingFiber));
            CleanupRegistry.Register(() => Misc.CleanupFiber(_statusOverlayFiber));
            CleanupRegistry.Register(() => Pool = null);
            CleanupRegistry.Register(() => EventManager.SetClient(null));
            CleanupRegistry.Register(MessageHandler.Shutdown);
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
        public static void AttemptConnection(bool silent = false)
        {
            if (Settings == null)
            {
                Logger.LogError("Cannot attempt connection: Settings are null.");
                return;
            }

            if (!silent) _autoConnectAttempts = 0;

            if (!silent) Logger.LogInfo($"Initiating connection attempt | Host: {Settings.ClientAddress} | Port: {Settings.ClientPort}");

            if (_client != null)
                try
                {
                    if (!silent) Logger.LogInfo("Stopping existing client instance...");
                    _client.Stop(silent: silent);
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

                _client.OnMessageReceived += request => _requestQueue.Enqueue(request);

                _client.OnConnected += () =>
                {
                    _autoConnectAttempts = 0;
                    var socketConnectedFiber = GameFiber.StartNew(() =>
                    {
                        Logger.LogInfo($"Socket Connection Established -> {Settings.ClientAddress}:{Settings.ClientPort}");
                        Game.DisplayHelp($"~b~ReportsPlus ~g~Connection Established\n~w~[~y~{Settings.ClientAddress}~w~:~y~{Settings.ClientPort}~w~]");
                    });
                    CleanupRegistry.Register(() => Misc.CleanupFiber(socketConnectedFiber));
                };

                _client.OnDisconnected += () =>
                {
                    var socketDisconnectedFiber = GameFiber.StartNew(() =>
                    {
                        Logger.LogWarning("Socket Connection Lost.");
                        if (!silent || _autoConnectAttempts == 0)
                            Game.DisplayHelp($"~b~ReportsPlus ~r~Connection Unavailable\n~w~[~y~{Settings.ClientAddress}~w~:~y~{Settings.ClientPort}~w~]");
                    });
                    CleanupRegistry.Register(() => Misc.CleanupFiber(socketDisconnectedFiber));
                };

                _client.Start(silent: silent);
                if (!silent) Logger.LogInfo("Client start sequence completed.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize client: {ex.Message}");
                Game.DisplayNotification("~r~Failed to initialize client connection.");
            }
        }

        /// <summary>
        ///     Fiber loop that monitors connection state and automatically attempts reconnection
        ///     when disconnected and auto-connect is enabled
        /// </summary>
        private static void AutoConnectLoop()
        {
            Logger.LogInfo("AutoConnect: fiber started.");
            GameFiber.Sleep(2000);
            var skipWait = true;

            while (true)
            {
                GameFiber.Sleep(1000);

                if (Settings == null || !Settings.AutoConnectEnabled || IsConnected || IsConnecting)
                {
                    skipWait = false;
                    continue;
                }

                if (!skipWait)
                {
                    var elapsed = 0;
                    var interval = Settings.AutoConnectInterval;
                    const int step = 500;

                    if (_autoConnectAttempts == 0 || _autoConnectAttempts % 5 == 0)
                        Logger.LogInfo($"AutoConnect: Waiting {interval}ms before attempt #{_autoConnectAttempts + 1}.");

                    while (elapsed < interval)
                    {
                        GameFiber.Sleep(step);
                        elapsed += step;

                        if (Settings == null || !Settings.AutoConnectEnabled || IsConnected || IsConnecting)
                            goto NextCycle;
                    }
                }

                if (Settings == null || !Settings.AutoConnectEnabled || IsConnected || IsConnecting)
                    goto NextCycle;

                Logger.LogInfo($"AutoConnect: Initiating attempt #{_autoConnectAttempts + 1}.");
                AttemptConnection(silent: true);
                _autoConnectAttempts++;
                NotifyAutoConnectStatus();

            NextCycle:
                skipWait = false;
            }
        }

        /// <summary>
        ///     Shows a user-facing notification at attempt 3, then every 5 after (3, 8, 13...),
        ///     so the player knows reconnection is ongoing without being spammed every cycle.
        /// </summary>
        private static void NotifyAutoConnectStatus()
        {
            if (Settings == null) return;

            var isThreshold = _autoConnectAttempts == 3 ||
                              (_autoConnectAttempts > 3 && (_autoConnectAttempts - 3) % 5 == 0);
            if (!isThreshold) return;

            var attempts = _autoConnectAttempts;
            var fiber = GameFiber.StartNew(() =>
                Game.DisplayHelp(
                    $"~b~ReportsPlus~w~: ~r~Cannot reach server~w~ at " +
                    $"~y~{Settings?.ClientAddress}~w~:~y~{Settings?.ClientPort}~w~.\n" +
                    $"Auto-reconnect active (~y~{attempts} attempts~w~)."));
            CleanupRegistry.Register(() => Misc.CleanupFiber(fiber));
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
                if (IsInputDisabled)
                {
                    NativeFunction.CallByHash<int>(0x5F4B6931816E599B, 0);
                }
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

        private static void StatusOverlayLoop()
        {
            while (true)
            {
                GameFiber.Yield();
                if (Settings == null || !Settings.StatusOverlayEnabled) continue;
                DrawStatusOverlay();
            }
        }

        private static void DrawStatusOverlay()
        {
            string colorCode;
            string statusText;

            if (IsConnected)
            {
                colorCode = "~g~";
                statusText = "Connected";
            }
            else if (IsConnecting)
            {
                colorCode = "~y~";
                statusText = "Connecting...";
            }
            else
            {
                colorCode = "~r~";
                statusText = "Disconnected";
            }

            var label = Settings.StatusOverlayLabel ?? "MDT Status:";
            var x = Settings.StatusOverlayX / 100f;
            var y = Settings.StatusOverlayY / 100f;
            var scale = Settings.StatusOverlaySize / 100f;
            var displayText = $"~w~{label} {colorCode}{statusText}";

            NativeFunction.CallByHash<int>(0x66E0276CC5F6B9DA, 0);               // SET_TEXT_FONT
            NativeFunction.CallByHash<int>(0x07C837F9A01C34C9, 0.0f, scale);     // SET_TEXT_SCALE
            NativeFunction.CallByHash<int>(0x1CA3E9EAC9D93E5E, 2, 0, 0, 0, 200); // SET_TEXT_DROPSHADOW
            NativeFunction.CallByHash<int>(0x25FBB336DF1804CB, "STRING");         // SET_TEXT_ENTRY
            NativeFunction.CallByHash<int>(0x6C188BE134E074AA, displayText);      // ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            NativeFunction.CallByHash<int>(0xCD015E5BB0D96A57, x, y);            // DRAW_TEXT
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