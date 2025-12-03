using System;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Events;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.Keybindings{
    public abstract class KeybindingActions{
        public class InputLockKeybinding : IKeybindingAction{
            public string Name => "inputlock";

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

            public void Execute(IncomingRequest request)
            {
                Main.LPCV?.Repair();
                Logger.LogInfo("Vehicle repaired.");
            }
        }

        public class PanicKeybinding : IKeybindingAction{
            public string Name => "panic";

            public void Execute(IncomingRequest request)
            {
                Logger.LogInfo("Panic button pressed!");

                // 1. Visual feedback in game
                Game.DisplayNotification("~r~PANIC BUTTON ACTIVATED");

                // 2. Create the event
                var panicEvent = new DynamicEvents.PanicButtonEvent("OFFICER IN DISTRESS - 10-99");

                // 3. Send it via the EventManager
                EventManager.SendDynamicEvent(panicEvent);
            }
        }
    }
}