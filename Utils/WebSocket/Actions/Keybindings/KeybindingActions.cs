using System;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.Misc;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.Keybindings{
    public abstract class KeybindingActions{
        public class InputLockKeybinding : IKeybindingAction{
            public string Name => "inputlock";

            /// <summary>
            ///     Handles the toggling or setting of the global game input lock via the CAD interface.
            /// </summary>
            /// <param name="request">The request containing the target state (enable/disable/toggle).</param>
            public void Execute(IncomingRequest request)
            {
                switch (request.Args)
                {
                    case "enable":
                        Main.IsInputDisabled = true;
                        Logger.LogInfo("Enabling input lock");
                        break;
                    case "disable":
                        Main.IsInputDisabled = false;
                        Logger.LogInfo("Disabling input lock");
                        break;
                    case "toggle":
                        Main.IsInputDisabled = !Main.IsInputDisabled;
                        Logger.LogInfo($"InputLock toggled. Is input disabled: {Main.IsInputDisabled}");
                        break;
                }
            }
        }

        public class SirenKeybinding : IKeybindingAction{
            public string Name => "siren";

            /// <summary>
            ///     Remotely controls the local vehicle's siren states, such as blipping or toggling.
            /// </summary>
            /// <param name="request">The request specifying the siren action (blip/toggle).</param>
            public void Execute(IncomingRequest request)
            {
                switch (request.Args)
                {
                    case "blip":
                        Main.LPCV?.BlipSiren(true);
                        Logger.LogInfo("Siren blipped");
                        break;
                    case "toggle":
                        if (Main.LPCV != null && Main.LPCV.HasSiren)
                        {
                            Main.LPCV.IsSirenOn = !Main.LPCV.IsSirenOn;
                            Logger.LogInfo($"Siren toggle: {Main.LPCV.IsSirenOn}");
                        }

                        break;
                }
            }
        }

        public class WeatherKeybinding : IKeybindingAction{
            public string Name => "weather";

            /// <summary>
            ///     Randomizes the world weather based on a selection of predefined types.
            /// </summary>
            /// <param name="request">The incoming request object.</param>
            public void Execute(IncomingRequest request)
            {
                var random2 = new Random();
                var weather = random2.Next(0, 4);
                switch (weather)
                {
                    case 0:
                        World.Weather = WeatherType.Clear;
                        Game.DisplayNotification("Weather set to Clear");
                        Logger.LogInfo("Weather set to Clear");
                        break;
                    case 1:
                        World.Weather = WeatherType.Foggy;
                        Game.DisplayNotification("Weather set to Foggy");
                        Logger.LogInfo("Weather set to Foggy");
                        break;
                    case 2:
                        World.Weather = WeatherType.Thunder;
                        Game.DisplayNotification("Weather set to Thunder");
                        Logger.LogInfo("Weather set to Thunder");
                        break;
                    case 3:
                        World.Weather = WeatherType.Snow;
                        Game.DisplayNotification("Weather set to Snow");
                        Logger.LogInfo("Weather set to Snow");
                        break;
                }
            }
        }

        public class TimeKeybinding : IKeybindingAction{
            public string Name => "time";

            /// <summary>
            ///     Sets the world time to a randomized hour and minute.
            /// </summary>
            /// <param name="request">The incoming request object.</param>
            public void Execute(IncomingRequest request)
            {
                var random = new Random();
                var hour   = random.Next(0, 24);
                var minute = random.Next(0, 60);
                World.TimeOfDay = new TimeSpan(hour, minute, 0);
                Game.DisplayNotification($"Time set to {hour}:{minute}");
                Logger.LogInfo($"Time set to {hour}:{minute}");
            }
        }

        public class RepairKeybinding : IKeybindingAction{
            public string Name => "repair";

            /// <summary>
            ///     Instantly repairs the local player's current vehicle.
            /// </summary>
            /// <param name="request">The incoming request object.</param>
            public void Execute(IncomingRequest request)
            {
                Main.LPCV?.Repair();
                Logger.LogInfo("Vehicle repaired.");
            }
        }

        public class KeyPressKeybinding : IKeybindingAction{
            public string Name => "keypress";

            /// <summary>
            ///     Receives a keybinding request and attempts to simulate the key press via KeyMapUtils.
            /// </summary>
            /// <param name="request">The request containing the key name in the arguments.</param>
            public void Execute(IncomingRequest request)
            {
                if (string.IsNullOrEmpty(request.Args))
                {
                    Logger.LogWarning("KeyPressKeybinding: Execute called with empty arguments.");
                    return;
                }

                KeyMapUtils.Press(request.Args);
            }
        }
    }
}