using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CommonDataFramework.Modules.PedDatabase;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using PolicingRedefined.API;
using PolicingRedefined.Interaction.Assets.PedAttributes;
using Rage;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public abstract class RequestActions
    {
        public class PlayerLocationTrackingAction : IRequestAction
        {
            public string Name => "playerLocationTracking";

            /// <summary>
            ///     Retrieves the local player's current X and Y coordinates and sends them to the client upon request.
            /// </summary>
            /// <param name="client">The socket client to send the location to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                if (client == null || request == null) return;

                var player = Game.LocalPlayer;
                if (player == null) return;

                var character = player.Character;
                if (character == null || !character.Exists()) return;

                var position = character.Position;

                var locationData = new JObject
                {
                    ["x"] = position.X,
                    ["y"] = position.Y
                };

                client.Send("playerLocation", locationData);
            }
        }

        public class FindPedByNameAction : IRequestAction
        {
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

                var notFoundData = new JObject { ["searchQuery"] = nameToFind };
                client.Send("pedNotFound", notFoundData);
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

        public class FindVehicleByPlateAction : IRequestAction
        {
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

                var notFoundData = new JObject { ["searchQuery"] = plateToFind };
                client.Send("vehicleNotFound", notFoundData);
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

        public class PoliceVehiclesAction : IRequestAction
        {
            private readonly HashSet<Vehicle> _trackedVehicles = new HashSet<Vehicle>();
            public string Name => "policeVehicles";

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
                        ["x"] = vehicle.Position.X,
                        ["y"] = vehicle.Position.Y
                    });
                return vehiclesArray;
            }
        }

        public class PlayerLocationAction : IRequestAction
        {
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

        public class GameTimeAction : IRequestAction
        {
            public string Name => "gametime";

            /// <summary>
            ///     Retrieves the current in-game world time and sends it to the client.
            /// </summary>
            /// <param name="client">The socket client to send the time to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
                var timeData = new JObject { ["time"] = timeString };
                client.Send(Name, timeData);
            }
        }

        public class FindLocationAction : IRequestAction
        {
            public string Name => "locationData";

            /// <summary>
            ///     Retrieves detailed location data (street, area, county) for the player's current position.
            /// </summary>
            /// <param name="client">The socket client to send the location data to.</param>
            /// <param name="request">The incoming request object.</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var currentStreet = World.GetStreetName(Main.LPC.Position);
                var currentZone = Functions.GetZoneAtPosition(Main.LPC.Position).RealAreaName;
                var currentCounty = Regex.Replace(Functions.GetZoneAtPosition(Main.LPC.Position).County.ToString(), "(?<!^)([A-Z])", " $1");

                var locationData = new JObject
                {
                    ["street"] = currentStreet ?? string.Empty,
                    ["area"] = currentZone ?? string.Empty,
                    ["county"] = currentCounty ?? string.Empty
                };

                client.Send("locationData", locationData);
            }
        }

        public class HeartbeatAction : IRequestAction
        {
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

        // Will only execute if PR is being used (Assigns citation to ped rather than custom animation)
        public class GiveCitationActionPR : IRequestAction
        {
            public string Name => "giveCitation";

            /// <summary>
            ///     Processes a request to issue a citation to a pedestrian, including validation and nearby entity lookup.
            /// </summary>
            /// <param name="client">The socket client to send success or error notifications to.</param>
            /// <param name="request">The request containing the citation payload (name, infraction, fine).</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var citationFiber = GameFiber.StartNew(() =>
                {
                    GameFiber.Yield();

                    if (string.IsNullOrEmpty(request?.Args))
                    {
                        Logger.LogError("GiveCitationActionPR: Request Args is empty.");
                        return;
                    }

                    JObject payload;
                    try
                    {
                        payload = JObject.Parse(request.Args);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"GiveCitationActionPR: JSON parse failed: {ex.Message}");
                        return;
                    }

                    var pedName = payload["pedName"]?.ToString();
                    var infraction = payload["infraction"]?.ToString();
                    var fineToken = payload["fine"];
                    var isArrestable = (bool?)payload["isArrestable"] ?? false;
                    var currencySymbol = payload["currency"]?.ToString() ?? "$";

                    if (string.IsNullOrEmpty(pedName) || string.IsNullOrEmpty(infraction) || fineToken == null)
                    {
                        Logger.LogError($"GiveCitationActionPR: Missing fields. Ped: {pedName}, Infraction: {infraction}");
                        return;
                    }

                    if (!int.TryParse(fineToken.ToString(), out var fine))
                    {
                        Logger.LogError($"GiveCitationActionPR: Invalid fine amount '{fineToken}'.");
                        return;
                    }

                    Ped targetPed = null;
                    var nearbyPeds = Main.LPC?.GetNearbyPeds(10);

                    if (nearbyPeds != null)
                    {
                        foreach (var ped in nearbyPeds)
                        {
                            if (ped == null || !ped.Exists()) continue;
                            var pedData = ped.GetPedData();
                            if (pedData?.FullName != null && pedData.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetPed = ped;
                                break;
                            }
                        }
                    }

                    if (targetPed == null)
                    {
                        var allPeds = World.GetAllPeds();
                        if (allPeds != null)
                        {
                            foreach (var ped in allPeds)
                            {
                                if (ped == null || !ped.Exists()) continue;
                                var pedData = ped.GetPedData();
                                if (pedData?.FullName != null && pedData.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetPed = ped;
                                    break;
                                }
                            }
                        }
                    }

                    if (targetPed == null)
                    {
                        Logger.LogWarning($"GiveCitationActionPR: Ped '{pedName}' not found.");
                        client.Send("citationError", new JObject { ["error"] = "Ped not found nearby", ["ped"] = pedName });
                        return;
                    }

                    Game.DisplayHelp($"~y~Citation Written: ~b~[{pedName}]~w~...");
                    GameFiber.Sleep(1500);

                    try
                    {
                        var citation = new Citation(targetPed, infraction, fine, currencySymbol, true, isArrestable);
                        PedAPI.GiveCitationToPed(targetPed, citation);

                        Game.HideHelp();
                        Logger.LogInfo($"Citation issued to {pedName} for {infraction} (${fine}).");
                        client.Send("citationSuccess", new JObject { ["ped"] = pedName });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"GiveCitationActionPR: Error invoking PR API: {ex.Message}");
                    }
                }, "GiveCitationActionPRFiber");

                CleanupRegistry.Register(() => Misc.Misc.CleanupFiber(citationFiber));
            }
        }

        // Standard GiveCitation for STP/base
        public class GiveCitationAction : IRequestAction
        {
            public string Name => "giveCitation";

            /// <summary>
            ///     Processes a request to issue a citation to a pedestrian, initiating a fiber-based interaction sequence that
            ///     requires the player to approach the target.
            /// </summary>
            /// <param name="client">The socket client to send success or error notifications to.</param>
            /// <param name="request">The request containing the citation payload (name, infraction, fine).</param>
            public void Execute(GameClientSocket client, IncomingRequest request)
            {
                var citationFiber = GameFiber.StartNew(() =>
                {
                    GameFiber.Yield();

                    try
                    {
                        Main.TriggerGiveCitation = false;
                        Main.TriggerDiscardCitation = false;
                        Main.IsCitationPending = true;

                        if (string.IsNullOrEmpty(request?.Args))
                        {
                            Logger.LogError("GiveCitationAction: Request Args is empty.");
                            return;
                        }

                        JObject payload;
                        try
                        {
                            payload = JObject.Parse(request.Args);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"GiveCitationAction: JSON parse failed: {ex.Message}");
                            return;
                        }

                        var pedName = payload["pedName"]?.ToString();
                        var infraction = payload["infraction"]?.ToString();
                        var fineToken = payload["fine"];

                        if (string.IsNullOrEmpty(pedName) || string.IsNullOrEmpty(infraction) || fineToken == null)
                        {
                            Logger.LogError($"GiveCitationAction: Missing fields. Ped: {pedName}, Infraction: {infraction}");
                            return;
                        }

                        if (!int.TryParse(fineToken.ToString(), out var fine))
                        {
                            Logger.LogError($"GiveCitationAction: Invalid fine amount '{fineToken}'.");
                            return;
                        }

                        Ped targetPed = null;
                        var nearbyPeds = Main.LPC?.GetNearbyPeds(10);

                        if (nearbyPeds != null)
                        {
                            foreach (var ped in nearbyPeds)
                            {
                                if (ped == null || !ped.Exists()) continue;
                                var persona = Functions.GetPersonaForPed(ped);
                                if (persona?.FullName != null && persona.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetPed = ped;
                                    break;
                                }
                            }
                        }

                        if (targetPed == null)
                        {
                            var allPeds = World.GetAllPeds();
                            if (allPeds != null)
                            {
                                foreach (var ped in allPeds)
                                {
                                    if (ped == null || !ped.Exists()) continue;
                                    var persona = Functions.GetPersonaForPed(ped);
                                    if (persona?.FullName != null && persona.FullName.Equals(pedName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        targetPed = ped;
                                        break;
                                    }
                                }
                            }
                        }

                        if (targetPed == null)
                        {
                            Logger.LogWarning($"GiveCitationAction: Ped '{pedName}' not found.");
                            client.Send("citationError", new JObject { ["error"] = "Ped not found nearby", ["ped"] = pedName });
                            return;
                        }

                        var giveKey = Main.Settings.GiveCitationKey;
                        var discardKey = Main.Settings.DiscardCitationKey;

                        Game.DisplayHelp($"~y~Written Citation: ~b~[{pedName}]~n~~w~Infraction(s): ~o~{infraction}~n~~w~Press ~g~{giveKey}~w~ to Issue~n~Press ~r~{discardKey}~w~ to Discard");
                        Logger.LogInfo($"Citation Request: {pedName}: {infraction}, Keys are Give:[{giveKey}] - Discard:[{discardKey}]");

                        while (true)
                        {
                            GameFiber.Yield();

                            if (targetPed == null || !targetPed.Exists())
                            {
                                Game.HideHelp();
                                Game.DisplaySubtitle("~r~Pedestrian is no longer valid.");
                                Logger.LogInfo("Pedestrian is no longer valid.");
                                break;
                            }

                            if (Main.Settings.DiscardCitationKey.IsPressed() || Main.TriggerDiscardCitation)
                            {
                                Main.TriggerDiscardCitation = false;
                                Game.HideHelp();
                                Game.DisplaySubtitle("~r~Citation Discarded.");
                                Logger.LogInfo("Citation Discarded.");
                                break;
                            }

                            if (Main.Settings.GiveCitationKey.IsPressed() || Main.TriggerGiveCitation)
                            {
                                Main.TriggerGiveCitation = false;

                                var distance = Main.LPC.Position.DistanceTo(targetPed.Position);
                                if (distance > 2.5f)
                                {
                                    Game.DisplaySubtitle($"~r~Too far! ~w~Move closer (~y~{distance:F1}m~w~) and try again.");
                                    Logger.LogInfo($"Too far! Move closer ({distance:F1}m).");
                                    GameFiber.Sleep(500);
                                    continue;
                                }

                                Game.HideHelp();
                                Misc.Misc.PerformCitationAnimation(Main.LPC);

                                Game.DisplaySubtitle($"~g~Issued Citation to ~w~{pedName}");
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Issued", $"~y~Citation Issued to: ~b~{pedName}~n~~w~Infraction: ~o~{infraction}~n~~w~Fine: ~g~${fine}");

                                Logger.LogInfo($"Citation visually issued to {pedName} for {infraction} (${fine}).");
                                client.Send("citationSuccess", new JObject { ["ped"] = pedName });
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"GiveCitationAction Fiber Error: {ex.Message}");
                    }
                    finally
                    {
                        Main.IsCitationPending = false;
                        Main.TriggerGiveCitation = false;
                        Main.TriggerDiscardCitation = false;
                    }
                }, "GiveCitationFiber");

                CleanupRegistry.Register(() => Misc.Misc.CleanupFiber(citationFiber));
            }
        }

        public class GiveParkingCitationAction : IRequestAction
        {
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
                    GameFiber.Yield();

                    try
                    {
                        Main.TriggerGiveCitation = false;
                        Main.TriggerDiscardCitation = false;
                        Main.IsCitationPending = true;

                        if (string.IsNullOrEmpty(request?.Args))
                        {
                            Logger.LogError("GiveParkingCitationAction: Request Args is empty.");
                            return;
                        }

                        JObject payload;
                        try
                        {
                            payload = JObject.Parse(request.Args);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"GiveParkingCitationAction: JSON parse failed: {ex.Message}");
                            return;
                        }

                        var vehiclePlate = payload["vehiclePlate"]?.ToString();
                        var infraction = payload["infraction"]?.ToString();
                        var fineToken = payload["fine"];

                        if (string.IsNullOrEmpty(vehiclePlate) || string.IsNullOrEmpty(infraction) || fineToken == null)
                        {
                            Logger.LogError($"GiveParkingCitationAction: Missing fields. Plate: {vehiclePlate}, Infraction: {infraction}");
                            return;
                        }

                        if (!int.TryParse(fineToken.ToString(), out var fine))
                        {
                            Logger.LogError($"GiveParkingCitationAction: Invalid fine amount '{fineToken}'.");
                            return;
                        }

                        Vehicle targetVehicle = null;
                        var nearbyVehicles = Main.LPC?.GetNearbyVehicles(10);

                        if (nearbyVehicles != null)
                        {
                            foreach (var veh in nearbyVehicles)
                            {
                                if (veh == null || !veh.Exists()) continue;
                                if (string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetVehicle = veh;
                                    break;
                                }
                            }
                        }

                        if (targetVehicle == null)
                        {
                            var allVehicles = World.GetAllVehicles();
                            if (allVehicles != null)
                            {
                                foreach (var veh in allVehicles)
                                {
                                    if (veh == null || !veh.Exists()) continue;
                                    if (string.Equals(veh.LicensePlate, vehiclePlate, StringComparison.OrdinalIgnoreCase))
                                    {
                                        targetVehicle = veh;
                                        break;
                                    }
                                }
                            }
                        }

                        if (targetVehicle == null)
                        {
                            Logger.LogWarning($"GiveParkingCitationAction: Vehicle '{vehiclePlate}' not found.");
                            client.Send("citationError", new JObject { ["error"] = "Vehicle not found", ["plate"] = vehiclePlate });
                            return;
                        }

                        var giveKey = Main.Settings.GiveCitationKey;
                        var discardKey = Main.Settings.DiscardCitationKey;

                        Game.DisplayHelp($"~y~Parking Citation: ~b~[{vehiclePlate}]~n~~w~Infraction(s): ~o~{infraction}~n~~w~Press ~g~{giveKey}~w~ to Issue~n~Press ~r~{discardKey}~w~ to Discard");
                        Logger.LogInfo($"Citation Request: {vehiclePlate}: {infraction}, Keys are Give:[{giveKey}] - Discard:[{discardKey}]");

                        while (true)
                        {
                            GameFiber.Yield();

                            if (targetVehicle == null || !targetVehicle.Exists())
                            {
                                Game.HideHelp();
                                Game.DisplaySubtitle("~r~Vehicle is no longer valid.");
                                Logger.LogInfo("Vehicle is no longer valid.");
                                break;
                            }

                            if (Main.Settings.DiscardCitationKey.IsPressed() || Main.TriggerDiscardCitation)
                            {
                                Main.TriggerDiscardCitation = false;
                                Game.HideHelp();
                                Game.DisplaySubtitle("~r~Citation Discarded.");
                                Logger.LogInfo("Citation Discarded.");
                                break;
                            }

                            if (Main.Settings.GiveCitationKey.IsPressed() || Main.TriggerGiveCitation)
                            {
                                Main.TriggerGiveCitation = false;

                                var distance = Main.LPC.Position.DistanceTo(targetVehicle.Position);
                                if (distance > 3.0f)
                                {
                                    Game.DisplaySubtitle($"~r~Too far! ~w~Move closer (~y~{distance:F1}m~w~) and try again.");
                                    Logger.LogInfo($"Too far! Move closer ({distance:F1}m).");
                                    GameFiber.Sleep(500);
                                    continue;
                                }

                                Game.HideHelp();
                                Misc.Misc.PerformCitationAnimation(Main.LPC);

                                Game.DisplaySubtitle($"~g~Placed Citation on ~w~{vehiclePlate}");
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Issued", $"~y~Parking Citation Placed on: ~b~{vehiclePlate}~n~~w~Infraction: ~o~{infraction}~n~~w~Fine: ~g~${fine}");

                                Logger.LogInfo($"Parking Citation visually placed on {vehiclePlate} for {infraction} (${fine}).");
                                client.Send("citationSuccess", new JObject { ["plate"] = vehiclePlate });
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"GiveParkingCitationAction Fiber Error: {ex.Message}");
                    }
                    finally
                    {
                        Main.IsCitationPending = false;
                        Main.TriggerGiveCitation = false;
                        Main.TriggerDiscardCitation = false;
                    }
                }, "GiveParkingCitationFiber");

                CleanupRegistry.Register(() => Misc.Misc.CleanupFiber(parkingCitationFiber));
            }
        }
    }
}