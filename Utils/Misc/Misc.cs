using System;
using System.Linq;
using System.Text;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Events;
using Object = Rage.Object;

namespace ReportsPlus.Utils.Misc
{
    public static class Misc
    {
        /// <summary>
        ///     Enumeration for different integration modes and which functionality to use
        /// </summary>
        public enum IntegrationMode
        {
            PolicingRedefined,
            StopThePed,
            BaseGame
        }

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

            // Callouts: always tracked through LSPDFR's own event. CalloutInterface is optional and only
            // used as a fallback resolver and for postals (CommonDataFramework postals are the second choice).
            var hasCi = IsPluginInstalled("CalloutInterface");
            var hasCdf = IsPluginInstalled("CommonDataFramework");
            Logger.LogInfo(hasCi
                ? "CalloutInterface found. Used as a fallback callout resolver and for postals."
                : "CalloutInterface not found. Callouts are tracked natively through LSPDFR.");
            CalloutTracker.Establish(hasCi, hasCdf);

            // Check functions to use PR > STP > Base
            var hasPolicingRedefined = IsPluginInstalled("PolicingRedefined") && IsPluginInstalled("CommonDataFramework");
            var hasStopThePed = IsPluginInstalled("StopThePed");

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

            CalloutTracker.Cleanup();
            Logger.LogInfo("Callout Events Cleaned up.");

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

        /// <summary>
        ///     Executes the visual animation sequence for an officer writing a citation on a notepad.
        ///     Handles resource loading, attachment, animation playback, and cleanup.
        /// </summary>
        /// <param name="officer">The pedestrian entity (usually the player) performing the animation.</param>
        public static void PerformCitationAnimation(Ped officer)
        {
            if (!officer.Exists() || officer.IsDead) return;

            var clipboardModel = new Model("prop_notepad_02");
            var animDict = new AnimationDictionary("veh@busted_std");
            const string animName = "issue_ticket_cop";

            clipboardModel.Load();
            animDict.Load();

            var timeout = 0;
            while ((!clipboardModel.IsLoaded || !animDict.IsLoaded) && timeout < 2000)
            {
                GameFiber.Sleep(10);
                timeout += 10;
            }

            if (!clipboardModel.IsLoaded || !animDict.IsLoaded) return;

            Object clipboard = null;

            try
            {
                if (!officer.Exists()) return;
                clipboard = new Object(clipboardModel, officer.Position);
                if (!clipboard.Exists()) return;
                var boneIndex = officer.GetBoneIndex(PedBoneId.RightHand);
                var offsetPos = new Vector3(0.156f, 0.072f, -0.012f);
                var offsetRot = new Rotator(44.0f, -143.0f, -10.0f);

                clipboard.AttachTo(officer, boneIndex, offsetPos, offsetRot);

                officer.Tasks.PlayAnimation(animDict, animName, 1.0f, AnimationFlags.None);

                GameFiber.Sleep(1600);

                if (officer.Exists()) NativeFunction.CallByHash<bool>(0x28004F88151E03E0, officer, animName, (string)animDict, 0.5f);

                GameFiber.Sleep(100);
            }
            finally
            {
                if (clipboard != null && clipboard.Exists()) clipboard.Delete();

                clipboardModel.Dismiss();
                animDict.Dismiss();
            }
        }
    }
}