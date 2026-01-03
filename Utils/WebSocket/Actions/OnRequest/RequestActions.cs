using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using PolicingRedefined.API;
using PolicingRedefined.Interaction.Assets.PedAttributes;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
    public abstract class RequestActions{
        public class FindPedByNameAction : IRequestAction{
            public string Name => "findPedByName";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                Logger.LogInfo("Running FindPedByNameAction ...");
                Logger.LogInfo("Args: " + request.Args);
                var nameToFind = request.Args;
                if (string.IsNullOrEmpty(nameToFind)) return;

                foreach (var ped in World.GetAllPeds())
                {
                    if (!ped || !ped.Exists()) continue;

                    var isMatch = false;

                    var pedData = ped.GetPedData();
                    if (pedData == null) continue;

                    var pedName = pedData.FullName ?? null;
                    if (pedName == null) continue;

                    if (pedName.ToLower().Contains(nameToFind.ToLower())) isMatch = true;

                    if (!isMatch) continue;
                    var pedDataJson = PedDataHelper.GeneratePedData(ped);
                    if (pedDataJson == null) continue;

                    client.Send("pedUpdated", pedDataJson);
                    return;
                }

                client.Send("pedNotFound", new JValue(nameToFind));
            }
        }

        public class PoliceVehiclesAction : IRequestAction{
            private readonly HashSet<Vehicle> _trackedVehicles = new HashSet<Vehicle>();
            public           string           Name => "policeVehicles";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                UpdateTrackedVehicleLocations();
                var vehiclesData = GetTrackedVehiclesAsJson();
                client.Send(Name, vehiclesData);
            }

            private void UpdateTrackedVehicleLocations()
            {
                _trackedVehicles.RemoveWhere(vehicle => !vehicle.Exists() || vehicle.IsDead || vehicle == Main.LPCV);

                var allVehicles = World.GetAllVehicles();
                foreach (var vehicle in allVehicles)
                    if (vehicle.IsPoliceVehicle && vehicle != Main.LPCV && vehicle.IsAlive && !_trackedVehicles.Contains(vehicle))
                        _trackedVehicles.Add(vehicle);
            }

            private JArray GetTrackedVehiclesAsJson()
            {
                var vehiclesArray = new JArray();
                foreach (var vehicle in _trackedVehicles.Where(vehicle => vehicle.Exists()))
                    vehiclesArray.Add(new JObject
                    {
                        ["id"] = $"v-{vehicle.Handle}",
                        ["x"]  = vehicle.Position.X,
                        ["y"]  = vehicle.Position.Y
                    });
                return vehiclesArray;
            }
        }

        public class PlayerLocationAction : IRequestAction{
            public string Name => "playerLocation";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var playerPosition = Main.LPC.Position;
                var locationData = new JObject
                {
                    ["x"] = playerPosition.X,
                    ["y"] = playerPosition.Y
                };
                client.Send("playerLocation", locationData);
            }
        }

        public class GameTimeAction : IRequestAction{
            public string Name => "gametime";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
                var timeData   = new JObject { ["time"] = timeString };
                client.Send(Name, timeData);
            }
        }

        public class HeartbeatAction : IRequestAction{
            public string Name => "heartbeat";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var now = DateTime.UtcNow;
                client.Send(Name, new JObject { ["time"] = now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) });
            }
        }

        public class GiveCitationAction : IRequestAction{
            public string Name => "giveCitation";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                // Fix: Parse the payload from 'Args' because 'Data' is used by MessageHandler for routing
                if (string.IsNullOrEmpty(request.Args))
                {
                    Logger.LogError("GiveCitationAction: Request Args (Payload) is empty.");
                    return;
                }

                JObject payload;
                try
                {
                    payload = JObject.Parse(request.Args);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"GiveCitationAction: Failed to parse JSON args: {ex.Message}");
                    return;
                }

                var pedName        = payload["pedName"]?.ToString();
                var infraction     = payload["infraction"]?.ToString();
                var fineToken      = payload["fine"];
                var isArrestable   = (bool?)payload["isArrestable"] ?? false;
                var currencySymbol = payload["currency"]?.ToString() ?? "$";

                if (string.IsNullOrEmpty(pedName) || string.IsNullOrEmpty(infraction) || fineToken == null)
                {
                    Logger.LogError($"GiveCitationAction: Missing required fields. Ped: {pedName}, Infraction: {infraction}");
                    return;
                }

                if (!int.TryParse(fineToken.ToString(), out var fine))
                {
                    Logger.LogError($"GiveCitationAction: Invalid fine amount '{fineToken}'.");
                    return;
                }

                Ped targetPed = null;

                // 1. Find the Ped
                foreach (var ped in Main.LPC.GetNearbyPeds(10))
                {
                    if (!ped || !ped.Exists()) continue;
                    var pedData = ped.GetPedData();

                    if (pedData?.FullName == null || !pedData.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase)) continue;
                    targetPed = ped;
                    break;
                }

                if (targetPed == null)
                    foreach (var ped in World.GetAllPeds())
                    {
                        if (!ped || !ped.Exists()) continue;
                        var pedData = ped.GetPedData();
                        if (pedData?.FullName == null || !pedData.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase)) continue;
                        targetPed = ped;
                        break;
                    }

                if (targetPed == null)
                {
                    Logger.LogWarning($"GiveCitationAction: Could not find in-game entity for ped '{pedName}'.");
                    client.Send("citationError", new JObject { ["error"] = "Ped not found nearby", ["ped"] = pedName });
                    return;
                }

                // 2. Issue Citation via Policing Redefined
                try
                {
                    var citation = new Citation(targetPed, infraction, fine, currencySymbol, true, // Currency In Front
                        isArrestable);

                    PedAPI.GiveCitationToPed(targetPed, citation);
                    Logger.LogInfo($"Citation issued to {pedName} for {infraction} (${fine}).");
                    client.Send("citationSuccess", new JObject { ["ped"] = pedName });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"GiveCitationAction: Error invoking PR API: {ex.Message}");
                }
            }
        }

        public class GiveParkingCitationAction : IRequestAction{
            public string Name => "giveParkingCitation";

            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var parkingCitationFiber = GameFiber.StartNew(() =>
                {
                    if (string.IsNullOrEmpty(request.Args))
                    {
                        Logger.LogError("GiveParkingCitationAction: Request Args (Payload) is empty.");
                        return;
                    }

                    JObject payload;
                    try
                    {
                        payload = JObject.Parse(request.Args);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"GiveParkingCitationAction: Failed to parse JSON args: {ex.Message}");
                        return;
                    }

                    var vehiclePlate = payload["vehiclePlate"]?.ToString();
                    var infraction   = payload["infraction"]?.ToString();
                    var fineToken    = payload["fine"];

                    if (string.IsNullOrEmpty(vehiclePlate) || string.IsNullOrEmpty(infraction) || fineToken == null)
                    {
                        Logger.LogError($"GiveParkingCitationAction: Missing required fields. Plate: {vehiclePlate}, Infraction: {infraction}");
                        return;
                    }

                    if (!int.TryParse(fineToken.ToString(), out var fine))
                    {
                        Logger.LogError($"GiveParkingCitationAction: Invalid fine amount '{fineToken}'.");
                        return;
                    }

                    Vehicle targetVehicle = null;

                    var nearbyVehicles = Main.LPC.GetNearbyVehicles(15);
                    if (nearbyVehicles != null)
                    {
                        foreach (var veh in nearbyVehicles)
                        {
                            if (!veh || !veh.Exists()) continue;
                            if (!string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase)) continue;
                            targetVehicle = veh;
                            break;
                        }
                    }

                    if (targetVehicle == null)
                    {
                        foreach (var veh in World.GetAllVehicles())
                        {
                            if (!veh || !veh.Exists()) continue;
                            if (!string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase)) continue;
                            targetVehicle = veh;
                            break;
                        }
                    }

                    if (targetVehicle == null)
                    {
                        Logger.LogWarning($"GiveParkingCitationAction: Could not find vehicle with plate '{vehiclePlate}'.");
                        client.Send("citationError", new JObject { ["error"] = "Vehicle not found", ["plate"] = vehiclePlate });
                        return;
                    }

                    var giveKey    = Main.Settings.GiveParkingCitationKey;
                    var discardKey = Main.Settings.DiscardParkingCitationKey;

                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~y~Citation Request", $"~b~{vehiclePlate}~w~: {infraction}\nPress ~g~{giveKey}~w~ to Issue, ~r~{discardKey}~w~ to Discard");
                    Game.DisplaySubtitle($"~b~{vehiclePlate}~w~: Press ~g~{giveKey} ~w~to Issue | ~r~{discardKey} ~w~to Discard");
                    Logger.LogInfo($"Citation Request: {vehiclePlate}: {infraction}, Keys are Give:[{giveKey}] - Discard:[{discardKey}]");

                    while (true)
                    {
                        GameFiber.Yield();

                        if (!targetVehicle.Exists())
                        {
                            Game.DisplaySubtitle("~r~Vehicle is no longer valid.");
                            Logger.LogInfo("Vehicle is no longer valid.");
                            break;
                        }

                        if (Game.IsKeyDown(discardKey))
                        {
                            Game.DisplaySubtitle("~r~Citation Discarded.");
                            Logger.LogInfo("Citation Discarded.");
                            break;
                        }

                        if (!Game.IsKeyDown(giveKey)) continue;
                        var distance = Main.LPC.Position.DistanceTo(targetVehicle.Position);

                        if (distance > 3.0f)
                        {
                            Game.DisplaySubtitle($"~r~Too far! ~w~Move closer (~y~{distance:F1}m~w~) and press ~g~{giveKey}~w~ again.");
                            Logger.LogInfo($"Too far! Move closer ({distance:F1}m) and press {giveKey} again.");
                            GameFiber.Sleep(500);
                            continue;
                        }

                        Main.LPC.Tasks.PlayAnimation("veh@busted_std", "issue_ticket_cop", 1.0f, AnimationFlags.None);

                        GameFiber.Sleep(1600);

                        NativeFunction.CallByHash<bool>(0x28004F88151E03E0, Main.LPC, "issue_ticket_cop", "veh@busted_std", 0.5f);

                        Game.DisplaySubtitle($"~g~Placed Citation on ~w~{vehiclePlate}");
                        Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Issued", $"~y~Parking Citation Placed on: ~b~{vehiclePlate}\n~w~Infraction: ~o~{infraction}\n~w~Fine: ~g~${fine}");

                        Logger.LogInfo($"Parking Citation visually placed on {vehiclePlate} for {infraction} (${fine}).");

                        client.Send("citationSuccess", new JObject { ["plate"] = vehiclePlate });
                        break;
                    }
                }, "GiveParkingCitationFiber");

                CleanupRegistry.Register(() => Misc.Misc.CleanupFiber(parkingCitationFiber));
            }
        }
    }
}