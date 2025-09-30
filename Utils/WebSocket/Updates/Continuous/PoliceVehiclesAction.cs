using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.Continuous
{
    public class PoliceVehiclesAction : IWebSocketAction
    {
        private static readonly List<Vehicle> TrackedVehicles = new List<Vehicle>(); // Tracked police vehicles
        public string Name => "policeVehicles";
        public bool IsContinuous => true;

        public void Execute(IncomingRequest request)
        {
            UpdateTrackedVehicleLocations();

            var vehiclesJson = GetTrackedVehiclesAsJson();

            GameClientSocket.Send(Name, vehiclesJson);
        }

        // for each vehicle in the world, check if it is a police vehicle and if it is valid, if so add to tracked vehicles
        private void UpdateTrackedVehicleLocations()
        {
            // remove all that are invalid
            TrackedVehicles.RemoveAll(vehicle => !vehicle.Exists() || vehicle.IsDead || vehicle == Main.LPCV);

            // add all vehicles that are valid, not already tracked
            var allVehicles = World.GetAllVehicles();
            foreach (var vehicle in allVehicles)
                if (vehicle.IsPoliceVehicle && vehicle != Main.LPCV && vehicle.IsAlive && !TrackedVehicles.Contains(vehicle))
                    TrackedVehicles.Add(vehicle);
        }

        // get all tracked vehicles as JSON
        private string GetTrackedVehiclesAsJson()
        {
            var vehiclesArray = new JArray();
            foreach (var vehicle in TrackedVehicles.Where(vehicle => vehicle.Exists()))
                vehiclesArray.Add(new JObject
                {
                    ["id"] = $"v-{vehicle.Handle}",
                    ["x"] = vehicle.Position.X,
                    ["y"] = vehicle.Position.Y
                });

            return vehiclesArray.ToString(Formatting.None);
        }

        // Used in Program.cs for cleanup
        public static void ClearTrackedPoliceVehicles()
        {
            TrackedVehicles.Clear();
        }
    }
}