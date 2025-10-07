using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.Continuous{
    public class PoliceVehiclesAction : IWebSocketAction{
        private static readonly HashSet<Vehicle> TrackedVehicles = new HashSet<Vehicle>();
        public                  string           Name         => "policeVehicles";
        public                  bool             IsContinuous => true;

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            UpdateTrackedVehicleLocations();
            var vehiclesData = GetTrackedVehiclesAsJson();
            client.Send(Name, vehiclesData);
        }

        private void UpdateTrackedVehicleLocations()
        {
            // This is faster than RemoveAll with a lambda on a HashSet
            TrackedVehicles.RemoveWhere(vehicle => !vehicle.Exists() || vehicle.IsDead || vehicle == Main.LPCV);

            var allVehicles = World.GetAllVehicles();
            foreach (var vehicle in allVehicles)
                // The 'Contains' check here is now extremely fast
                if (vehicle.IsPoliceVehicle && vehicle != Main.LPCV && vehicle.IsAlive && !TrackedVehicles.Contains(vehicle))
                    TrackedVehicles.Add(vehicle);
        }

        private JArray GetTrackedVehiclesAsJson()
        {
            var vehiclesArray = new JArray();
            foreach (var vehicle in TrackedVehicles.Where(vehicle => vehicle.Exists()))
                vehiclesArray.Add(new JObject
                {
                    ["id"] = $"v-{vehicle.Handle}",
                    ["x"]  = vehicle.Position.X,
                    ["y"]  = vehicle.Position.Y
                });
            return vehiclesArray;
        }

        public static void ClearTrackedPoliceVehicles()
        {
            TrackedVehicles.Clear();
        }
    }
}