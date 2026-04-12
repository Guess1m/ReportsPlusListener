using LSPD_First_Response.Engine.Scripting;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    public class TrafficStopTrackingAction : IContinuousAction
    {
        private static LSPD_First_Response.Mod.API.LHandle _currentPullover;
        private static Vector3 _lastPosition = Vector3.Zero;
        private static bool _isActive;
        private static uint _lastSuspectHandle;

        /// <summary>
        /// Continuously monitors the state of LSPD First Response traffic stops.
        /// Dispatches location updates when a pullover is initiated or the suspect changes position,
        /// and dispatches a termination event when the pullover concludes.
        /// </summary>
        /// <param name="client">The active game client socket for sending data.</param>
        public void Execute(GameClientSocket client)
        {
            if (client == null || !client.IsConnected)
            {
                return;
            }

            LHandle pullover = LSPD_First_Response.Mod.API.Functions.GetCurrentPullover();

            if (pullover == null)
            {
                if (_isActive)
                {
                    client.Send("trafficStopEnded", new JObject());
                    _isActive = false;
                    _currentPullover = null;
                    _lastPosition = Vector3.Zero;
                    _lastSuspectHandle = 0;
                }
                return;
            }

            Ped suspectPed = LSPD_First_Response.Mod.API.Functions.GetPulloverSuspect(pullover);

            if (suspectPed == null || !suspectPed.Exists())
            {
                return;
            }

            Vector3 currentPosition = suspectPed.Position;
            uint currentHandle = suspectPed.Handle.Value;
            float distanceMoved = _lastPosition != Vector3.Zero ? Vector3.Distance(_lastPosition, currentPosition) : float.MaxValue;

            bool isNewStop = !_isActive || (_lastSuspectHandle != 0 && _lastSuspectHandle != currentHandle);

            if (isNewStop || distanceMoved > 5.0f)
            {
                string streetName = World.GetStreetName(currentPosition) ?? string.Empty;
                WorldZone zone = Functions.GetZoneAtPosition(currentPosition);
                string areaName = zone != null && zone.RealAreaName != null ? zone.RealAreaName : string.Empty;

                Vehicle suspectVehicle = suspectPed.CurrentVehicle;

                if (suspectVehicle == null || !suspectVehicle.Exists())
                {
                    suspectVehicle = suspectPed.LastVehicle;
                }

                bool hasValidVehicle = suspectVehicle != null && suspectVehicle.Exists();
                string licensePlate = hasValidVehicle && suspectVehicle.LicensePlate != null ? suspectVehicle.LicensePlate : string.Empty;
                string vehicleModelName = hasValidVehicle && suspectVehicle.Model != null && suspectVehicle.Model.Name != null ? suspectVehicle.Model.Name : string.Empty;

                JObject payload = new JObject
                {
                    ["x"] = currentPosition.X,
                    ["y"] = currentPosition.Y,
                    ["street"] = streetName,
                    ["area"] = areaName,
                    ["plate"] = licensePlate,
                    ["vehicleModel"] = vehicleModelName
                };

                if (isNewStop)
                {
                    client.Send("trafficStopStarted", payload);
                }
                else
                {
                    client.Send("trafficStopUpdated", payload);
                }

                _isActive = true;
                _currentPullover = pullover;
                _lastPosition = currentPosition;
                _lastSuspectHandle = currentHandle;
            }
        }
    }
}