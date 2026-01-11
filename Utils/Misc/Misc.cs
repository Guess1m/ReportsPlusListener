using System;
using System.Linq;
using System.Text;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Events;

namespace ReportsPlus.Utils.Misc{
    public static class Misc{
        /// <summary>
        ///     Enumeration for different integration modes and which functionality to use
        /// </summary>
        public enum IntegrationMode{
            PolicingRedefined,
            StopThePed,
            BaseGame
        }

        private static bool _usingCi;

        public static IntegrationMode CurrentMode { get; private set; } = IntegrationMode.BaseGame;

        /// <summary>
        ///     Determines if a specific LSPDFR plugin is currently loaded in the user's environment.
        /// </summary>
        /// <param name="pluginName">The assembly name of the plugin to verify.</param>
        /// <returns>True if the plugin is installed and detected; otherwise, false.</returns>
        private static bool IsPluginInstalled(string pluginName)
        {
            var plugins = Functions.GetAllUserPlugins();
            return plugins.Any(x => x.GetName().Name.Equals(pluginName));
        }

        /// <summary>
        ///     Evaluates the environment for supported third-party plugins and initializes the appropriate integration mode based
        ///     on priority.
        ///     Priority Order: Policing Redefined > StopThePed > Base Game.
        /// </summary>
        /// <returns>A <see cref="StringBuilder" /> containing localized status messages regarding plugin availability.</returns>
        public static StringBuilder RunPluginChecks()
        {
            Logger.LogInfo("Running Plugin Checks...");
            var statusMessage = new StringBuilder();

            // Check CI
            _usingCi = IsPluginInstalled("CalloutInterface");
            if (_usingCi)
            {
                Logger.LogInfo("CalloutInterface found. Establishing Events...");
                EventUtils.EstablishCiEvent();
            }
            else
            {
                Logger.LogWarning("CalloutInterface not found. Required for Callout Functions.");
                statusMessage.Append("~r~CalloutInterface Not Found\n~o~- Required for Callout Functions.\n");
            }

            // Check functions to use PR > STP > Base
            var hasPolicingRedefined = IsPluginInstalled("PolicingRedefined") && IsPluginInstalled("CommonDataFramework");
            var hasStopThePed        = IsPluginInstalled("StopThePed");

            if (hasPolicingRedefined)
            {
                CurrentMode = IntegrationMode.PolicingRedefined;
                Logger.LogInfo("Integration Mode: Policing Redefined (Priority 1)");
                EventUtils.EstablishEventsPr();
            }
            else if (hasStopThePed)
            {
                CurrentMode = IntegrationMode.StopThePed;
                Logger.LogInfo("Integration Mode: StopThePed (Priority 2)");
                EventUtils.EstablishEventsStp();
                statusMessage.Append("~b~Mode: StopThePed\n");
            }
            else
            {
                CurrentMode = IntegrationMode.BaseGame;
                Logger.LogInfo("Integration Mode: Base Game (Fallback)");
                EventUtils.EstablishEventsBaseGame();
                statusMessage.Append("~y~Mode: Base Game (Fallback)\n");
            }

            return statusMessage;
        }

        /// <summary>
        ///     Tears down all event subscriptions and releases resources based on the active integration mode.
        /// </summary>
        public static void CleanupPluginEvents()
        {
            Logger.LogInfo($"Cleaning up plugin event subscriptions (Mode: {CurrentMode})...");

            if (_usingCi)
            {
                EventUtils.CleanupCiEvent();
                Logger.LogInfo("CI Events Cleaned up.");
            }

            switch (CurrentMode)
            {
                case IntegrationMode.PolicingRedefined:
                    EventUtils.CleanupEventsPr();
                    Logger.LogInfo("PR Events Cleaned up.");
                    break;
                case IntegrationMode.StopThePed:
                    EventUtils.CleanupEventsStp();
                    Logger.LogInfo("STP Events Cleaned up.");
                    break;
                case IntegrationMode.BaseGame:
                    EventUtils.CleanupEventsBaseGame();
                    Logger.LogInfo("Base Game Events Cleaned up.");
                    break;
                default: throw new ArgumentOutOfRangeException(); // Should never happen
            }
        }

        /// <summary>
        ///     Safely terminates an active <see cref="GameFiber" /> if it is currently running.
        /// </summary>
        /// <param name="fiber">The fiber instance to abort.</param>
        public static void CleanupFiber(GameFiber fiber)
        {
            if (!(fiber is { IsAlive: true })) return;
            fiber.Abort();
            Logger.LogInfo($"Fiber {fiber.Name} was cleaned up.");
        }
    }
}