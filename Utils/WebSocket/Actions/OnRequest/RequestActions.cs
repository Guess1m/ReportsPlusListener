using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CommonDataFramework.Modules.PedDatabase;
using LSPD_First_Response.Mod.API;
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

            /// <summary>
            ///     Executes a search for a pedestrian by their full name, prioritizing nearby entities.
            /// </summary>
            /// <param name="client">The socket client to send the result to.</param>
            /// <param name="request">The request containing the target pedestrian's name in the arguments.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                Logger.LogInfo("Running FindPedByNameAction ...");
                Logger.LogInfo("Args: " + request.Args);
                var nameToFind = request.Args;
                if (string.IsNullOrEmpty(nameToFind)) return;

                if (Main.LPC.Exists())
                {
                    var nearbyPeds = Main.LPC.GetNearbyPeds(10);
                    if (nearbyPeds != null)
                        foreach (var ped in nearbyPeds)
                            if (TryProcessPed(client, ped, nameToFind))
                                return;
                }

                foreach (var ped in World.GetAllPeds())
                    if (TryProcessPed(client, ped, nameToFind))
                        return;

                client.Send("pedNotFound", new JValue(nameToFind));
            }

            /// <summary>
            ///     Attempts to match a specific pedestrian entity against a search string and sends data if found.
            /// </summary>
            /// <param name="client">The socket client to send the data to.</param>
            /// <param name="ped">The pedestrian entity to evaluate.</param>
            /// <param name="nameToFind">The name string to search for.</param>
            /// <returns><c>true</c> if a match was found and processed; otherwise, <c>false</c>.</returns>
            private bool TryProcessPed(GameClientSocket client, Ped ped, string nameToFind)
            {
                if (!ped || !ped.Exists()) return false;

                var pedData = ped.GetPedData();
                if (pedData == null) return false;

                var pedName = pedData.FullName;
                if (string.IsNullOrEmpty(pedName)) return false;

                if (!pedName.ToLower().Contains(nameToFind.ToLower())) return false;
                var pedDataJson = Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined ? PedDataHelper.GeneratePedDataPR(ped) : PedDataHelper.GeneratePedData(ped);
                if (pedDataJson == null) return false;
                client.Send("pedUpdated", pedDataJson);
                return true;
            }
        }

        public class FindVehicleByPlateAction : IRequestAction{
            public string Name => "findVehicleByPlate";

            /// <summary>
            ///     Executes a search for a vehicle by its license plate, prioritizing nearby vehicles.
            /// </summary>
            /// <param name="client">The socket client to send the result to.</param>
            /// <param name="request">The request containing the license plate string in the arguments.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                Logger.LogInfo("Running FindVehicleByPlateAction ...");
                var plateToFind = request.Args;
                if (string.IsNullOrEmpty(plateToFind)) return;

                var searchClean = plateToFind.Replace(" ", "").ToLower();

                if (Main.LPC.Exists())
                {
                    var nearbyVehicles = Main.LPC.GetNearbyVehicles(10);
                    if (nearbyVehicles != null)
                        foreach (var veh in nearbyVehicles)
                            if (TryProcessVehicle(client, veh, searchClean))
                                return;
                }

                foreach (var veh in World.GetAllVehicles())
                    if (TryProcessVehicle(client, veh, searchClean))
                        return;

                client.Send("vehicleNotFound", new JValue(plateToFind));
            }

            /// <summary>
            ///     Attempts to match a specific vehicle's license plate against a search string and sends data if found.
            /// </summary>
            /// <param name="client">The socket client to send the data to.</param>
            /// <param name="veh">The vehicle entity to evaluate.</param>
            /// <param name="searchClean">The sanitized license plate string to search for.</param>
            /// <returns><c>true</c> if a match was found and processed; otherwise, <c>false</c>.</returns>
            private bool TryProcessVehicle(GameClientSocket client, Vehicle veh, string searchClean)
            {
                if (!veh || !veh.Exists()) return false;

                var plate = veh.LicensePlate ?? "";

                if (!plate.Replace(" ", "").ToLower().Contains(searchClean)) return false;
                var vehData = Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined ? VehicleDataHelper.GenerateVehicleDataPR(veh) : VehicleDataHelper.GenerateVehicleData(veh);
                if (vehData == null) return false;
                client.Send("vehicleUpdated", vehData);
                return true;
            }
        }

        public class PoliceVehiclesAction : IRequestAction{
            private readonly HashSet<Vehicle> _trackedVehicles = new HashSet<Vehicle>();
            public           string           Name => "policeVehicles";

            /// <summary>
            ///     Retrieves and sends the coordinates of all active police vehicles to the client.
            /// </summary>
            /// <param name="client">The socket client to send the vehicle data to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                UpdateTrackedVehicleLocations();
                var vehiclesData = GetTrackedVehiclesAsJson();
                client.Send(Name, vehiclesData);
            }

            /// <summary>
            ///     Updates the internal list of tracked police vehicles by removing invalid entities and adding new ones.
            /// </summary>
            private void UpdateTrackedVehicleLocations()
            {
                _trackedVehicles.RemoveWhere(vehicle => !vehicle.Exists() || vehicle.IsDead || vehicle == Main.LPCV);

                var allVehicles = World.GetAllVehicles();
                foreach (var vehicle in allVehicles)
                    if (vehicle.IsPoliceVehicle && vehicle != Main.LPCV && vehicle.IsAlive && !_trackedVehicles.Contains(vehicle))
                        _trackedVehicles.Add(vehicle);
            }

            /// <summary>
            ///     Serializes the currently tracked police vehicles into a JSON array of coordinates and IDs.
            /// </summary>
            /// <returns>A <see cref="JArray" /> containing the position data of tracked vehicles.</returns>
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

            /// <summary>
            ///     Sends the local player's current X and Y coordinates to the client.
            /// </summary>
            /// <param name="client">The socket client to send the location to.</param>
            /// <param name="request">The incoming request object.</param>
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

            /// <summary>
            ///     Retrieves the current in-game world time and sends it to the client.
            /// </summary>
            /// <param name="client">The socket client to send the time to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
                var timeData   = new JObject { ["time"] = timeString };
                client.Send(Name, timeData);
            }
        }

        public class FindLocationAction : IRequestAction{
            public string Name => "locationData";

            /// <summary>
            ///     Retrieves detailed location data (street, area, county) for the player's current position.
            /// </summary>
            /// <param name="client">The socket client to send the location data to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var currentStreet = World.GetStreetName(Main.LPC.Position);
                var currentZone   = Functions.GetZoneAtPosition(Main.LPC.Position).RealAreaName;
                var currentCounty = Regex.Replace(Functions.GetZoneAtPosition(Main.LPC.Position).County.ToString(), "(?<!^)([A-Z])", " $1");

                var locationData = new JObject
                {
                    ["street"] = currentStreet ?? string.Empty,
                    ["area"]   = currentZone ?? string.Empty,
                    ["county"] = currentCounty ?? string.Empty
                };

                client.Send("locationData", locationData);
            }
        }

        public class HeartbeatAction : IRequestAction{
            public string Name => "heartbeat";

            /// <summary>
            ///     Sends the current UTC system time to the client to verify connection health.
            /// </summary>
            /// <param name="client">The socket client to send the heartbeat to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var now = DateTime.UtcNow;
                client.Send(Name, new JObject { ["time"] = now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) });
            }
        }

        public class GiveCitationActionPR : IRequestAction{
            public string Name => "giveCitation";

            /// <summary>
            ///     Processes a request to issue a citation to a pedestrian, including validation and nearby entity lookup.
            /// </summary>
            /// <param name="client">The socket client to send success or error notifications to.</param>
            /// <param name="request">The request containing the citation payload (name, infraction, fine).</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
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

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Issued", $"~y~Citation For: ~b~{pedName}\n~w~Infraction: ~o~{infraction}\n~w~Fine: ~g~${fine}");

                try
                {
                    var citation = new Citation(targetPed, infraction, fine, currencySymbol, true, isArrestable);

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

        public class GiveCitationAction : IRequestAction{
            public string Name => "giveCitation";

            /// <summary>
            ///     Processes a request to issue a citation to a pedestrian, including validation and nearby entity lookup.
            /// </summary>
            /// <param name="client">The socket client to send success or error notifications to.</param>
            /// <param name="request">The request containing the citation payload (name, infraction, fine).</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
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

                foreach (var ped in Main.LPC.GetNearbyPeds(10))
                {
                    if (!ped || !ped.Exists()) continue;
                    var name = Functions.GetPersonaForPed(ped).FullName;
                    if (name == null || !name.Equals(pedName, StringComparison.OrdinalIgnoreCase)) continue;
                    targetPed = ped;
                    break;
                }

                if (targetPed == null)
                    foreach (var ped in World.GetAllPeds())
                    {
                        if (!ped || !ped.Exists()) continue;
                        var name = Functions.GetPersonaForPed(ped).FullName;
                        if (name == null || !name.Equals(pedName, StringComparison.OrdinalIgnoreCase)) continue;
                        targetPed = ped;
                        break;
                    }

                if (targetPed == null)
                {
                    Logger.LogWarning($"GiveCitationAction: Could not find in-game entity for ped '{pedName}'.");
                    client.Send("citationError", new JObject { ["error"] = "Ped not found nearby", ["ped"] = pedName });
                    return;
                }

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Issued", $"~y~Citation For: ~b~{pedName}\n~w~Infraction: ~o~{infraction}\n~w~Fine: ~g~${fine}");

                try
                {
                    //TODO: Implement Animation,etc. for citation
                    Game.DisplayNotification($"(ANIM NOT IMPLEMENTED YET)\nCitation issued to {pedName} for {infraction} (${fine}).");
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

            /// <summary>
            ///     Initiates a fiber-based interaction to allow the player to physically place a parking citation on a vehicle.
            /// </summary>
            /// <param name="client">The socket client for sending result updates.</param>
            /// <param name="request">The request containing vehicle plate and infraction details.</param>
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
                        foreach (var veh in nearbyVehicles)
                        {
                            if (!veh || !veh.Exists()) continue;
                            if (!string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase)) continue;
                            targetVehicle = veh;
                            break;
                        }

                    if (targetVehicle == null)
                        foreach (var veh in World.GetAllVehicles())
                        {
                            if (!veh || !veh.Exists()) continue;
                            if (!string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase)) continue;
                            targetVehicle = veh;
                            break;
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