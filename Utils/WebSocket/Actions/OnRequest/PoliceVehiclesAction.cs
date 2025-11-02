using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
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
}