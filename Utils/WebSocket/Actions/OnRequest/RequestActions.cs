using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using Rage;
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
    }
}