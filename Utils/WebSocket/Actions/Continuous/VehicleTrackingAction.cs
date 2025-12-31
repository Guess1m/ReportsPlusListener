using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public class VehicleTrackingAction : IContinuousAction{
        // Dictionary to track sent vehicles to avoid resending data every tick
        private static readonly Dictionary<int, Vehicle> TrackedVehicles = new Dictionary<int, Vehicle>();

        /**
         * Execute the vehicle tracking logic.
         * Scans for nearby vehicles, sends 'vehicleCreated' for new ones, and 'vehicleRemoved' for those that left scope.
         *
         * @param client The active GameClientSocket to send updates to.
         */
        public void Execute(GameClientSocket client)
        {
            var nearbyVehicles = new HashSet<Vehicle>(World.GetAllVehicles());

            var nearbyVehicleHandles  = new HashSet<int>(nearbyVehicles.Select(v => (int)v.Handle.Value));
            var removedVehicleHandles = new List<int>();

            // 1. Detect Removed Vehicles
            foreach (var trackedHandle in TrackedVehicles.Keys)
                if (!nearbyVehicleHandles.Contains(trackedHandle))
                {
                    removedVehicleHandles.Add(trackedHandle);
                    var removedData = new JObject { ["entityId"] = trackedHandle };
                    client.Send("vehicleRemoved", removedData);
                }

            // Cleanup dictionary
            foreach (var handle in removedVehicleHandles) TrackedVehicles.Remove(handle);

            // 2. Detect New Vehicles
            foreach (var vehicle in nearbyVehicles)
            {
                if (!vehicle || !vehicle.Exists()) continue;

                // Skip if already tracked
                if (TrackedVehicles.ContainsKey((int)vehicle.Handle.Value)) continue;

                // Generate Data
                var vehicleData = VehicleDataHelper.GenerateVehicleData(vehicle);
                if (vehicleData == null) continue;

                // Send and Track
                client.Send("vehicleCreated", vehicleData);
                TrackedVehicles.Add((int)vehicle.Handle.Value, vehicle);
            }
        }
    }
}