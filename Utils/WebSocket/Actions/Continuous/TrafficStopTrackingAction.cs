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
        ///     Continuously monitors the state of LSPD First Response traffic stops.
        ///     Dispatches location updates when a pullover is initiated or the suspect changes position,
        ///     and dispatches a termination event when the pullover concludes.
        /// </summary>
        /// <param name="client">The active game client socket for sending data.</param>
        public void Execute(GameClientSocket client)
        {
            if (client == null || !client.IsConnected) return;

            var pullover = LSPD_First_Response.Mod.API.Functions.GetCurrentPullover();

            if (pullover == null)
            {
                if (_isActive)
                {
                    Logger.LogInfo("Traffic stop concluded. Dispatching termination event.");
                    client.Send("trafficStopEnded", new JObject());
                    _isActive = false;
                    _currentPullover = null;
                    _lastPosition = Vector3.Zero;
                    _lastSuspectHandle = 0;
                }
                return;
            }

            var suspectPed = LSPD_First_Response.Mod.API.Functions.GetPulloverSuspect(pullover);
            if (suspectPed == null || !suspectPed.Exists()) return;

            var currentPosition = suspectPed.Position;
            var currentHandle = suspectPed.Handle.Value;
            var distanceMoved = _lastPosition != Vector3.Zero ? Vector3.Distance(_lastPosition, currentPosition) : float.MaxValue;

            bool isNewStop = !_isActive || _currentPullover != pullover;

            if (isNewStop || _lastSuspectHandle != currentHandle || distanceMoved > 5.0f)
            {
                Logger.LogInfo("Traffic stop state or location change detected. Dispatching events.");

                var street = World.GetStreetName(currentPosition);
                var zone = Functions.GetZoneAtPosition(currentPosition);

                Vehicle suspectVehicle = suspectPed.CurrentVehicle;
                if (suspectVehicle == null || !suspectVehicle.Exists())
                {
                    suspectVehicle = suspectPed.LastVehicle;
                }

                bool hasValidVehicle = suspectVehicle != null && suspectVehicle.Exists();

                var payload = new JObject
                {
                    ["x"] = currentPosition.X,
                    ["y"] = currentPosition.Y,
                    ["street"] = street ?? string.Empty,
                    ["area"] = zone != null ? zone.RealAreaName ?? string.Empty : string.Empty,
                    ["plate"] = hasValidVehicle ? suspectVehicle.LicensePlate ?? string.Empty : string.Empty,
                    ["vehicleModel"] = hasValidVehicle && suspectVehicle.Model != null ? suspectVehicle.Model.Name ?? string.Empty : string.Empty
                };

                if (isNewStop)
                {
                    Logger.LogInfo("Traffic stop started. Dispatching events.");
                    client.Send("trafficStopStarted", payload);
                }

                client.Send("trafficStopUpdated", payload);

                _isActive = true;
                _currentPullover = pullover;
                _lastPosition = currentPosition;
                _lastSuspectHandle = currentHandle;
            }
        }
    }
}