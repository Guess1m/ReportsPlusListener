using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public class VehicleTrackingAction : IContinuousAction{
        // Dict to track sent vehicles to the client without dups
        private static readonly Dictionary<int, Vehicle> TrackedVehicles = new Dictionary<int, Vehicle>();

        /// <summary>
        ///     Scans the world for vehicles to synchronize state with the client.
        ///     Identifies newly discovered vehicles to send creation data and detects vehicles that are no longer in scope to
        ///     trigger removal updates.
        /// </summary>
        /// <param name="client">The active <see cref="GameClientSocket" /> instance used for communication.</param>
        public void Execute(GameClientSocket client)
        {
            var nearbyVehicles = new HashSet<Vehicle>(World.GetAllVehicles());

            var nearbyVehicleHandles  = new HashSet<int>(nearbyVehicles.Select(v => (int)v.Handle.Value));
            var removedVehicleHandles = new List<int>();

            // find removed vehs and send to the client
            foreach (var trackedHandle in TrackedVehicles.Keys)
                if (!nearbyVehicleHandles.Contains(trackedHandle))
                {
                    removedVehicleHandles.Add(trackedHandle);
                    var removedData = new JObject { ["entityId"] = trackedHandle };
                    client.Send("vehicleRemoved", removedData);
                }

            // cleanup dict
            foreach (var handle in removedVehicleHandles) TrackedVehicles.Remove(handle);

            // find new vehicles
            foreach (var vehicle in nearbyVehicles)
            {
                if (!vehicle || !vehicle.Exists()) continue;
                // Skip if already tracked
                if (TrackedVehicles.ContainsKey((int)vehicle.Handle.Value)) continue;
                var vehicleData = Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined ? VehicleDataHelper.GenerateVehicleDataPR(vehicle) : VehicleDataHelper.GenerateVehicleData(vehicle);
                if (vehicleData == null) continue;
                client.Send("vehicleCreated", vehicleData);
                TrackedVehicles.Add((int)vehicle.Handle.Value, vehicle);
            }
        }
    }
}