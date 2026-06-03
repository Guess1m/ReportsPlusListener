using System;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Bolo;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    /// <summary>
    ///     Harvests vehicles around the player and emits an
    ///     <c>ambient_proximity_sweep</c> for the Java MDT to evaluate for BOLO
    ///     generation. Lazy and throttled: a sweep only fires once the player has
    ///     moved more than the configured distance from the last sweep, or has been
    ///     stationary longer than the configured idle window. Sends only the minimal
    ///     fields (plate / class / occupancy); the MDT enriches the rest from its
    ///     own cache.
    /// </summary>
    public class BoloProximityAction : IContinuousAction
    {
        private bool _hasSwept;
        private DateTime _lastSweepUtc = DateTime.MinValue;
        private Vector3 _lastSweepPos = Vector3.Zero;

        public void Execute(GameClientSocket client)
        {
            if (client == null || !BoloState.Enabled) return;

            var player = Main.LPC;
            if (player == null || !player.Exists()) return;

            var pos = player.Position;
            var nowUtc = DateTime.UtcNow;

            var moved = _hasSwept ? _lastSweepPos.DistanceTo(pos) : float.MaxValue;
            var idleElapsed = (nowUtc - _lastSweepUtc).TotalSeconds;

            var shouldSweep = !_hasSwept
                              || moved >= BoloState.MoveThresholdMeters
                              || idleElapsed >= BoloState.IdleSeconds;
            if (!shouldSweep) return;

            try
            {
                var radius = BoloState.RadiusMeters;
                var ownVehicle = Main.LPCV;
                var vehicles = new JArray();

                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (!vehicle || !vehicle.Exists()) continue;
                    if (ownVehicle && vehicle == ownVehicle) continue; // never self-BOLO
                    if (player.Position.DistanceTo(vehicle.Position) > radius) continue;

                    var plate = vehicle.LicensePlate;
                    if (string.IsNullOrWhiteSpace(plate)) continue;

                    var occupied = false;
                    try
                    {
                        var driver = vehicle.Driver;
                        occupied = driver && driver.Exists();
                    }
                    catch
                    {
                        // ignore - treat as unoccupied
                    }

                    var vehiclePos = vehicle.Position;

                    vehicles.Add(new JObject
                    {
                        ["plate"] = plate.Trim(),
                        ["vehicleClass"] = VehicleClassUtil.GetClassName(vehicle),
                        ["occupied"] = occupied,
                        ["x"] = vehiclePos.X,
                        ["y"] = vehiclePos.Y
                    });
                }

                client.Send("ambient_proximity_sweep", new JObject { ["vehicles"] = vehicles });

                _lastSweepPos = pos;
                _lastSweepUtc = nowUtc;
                _hasSwept = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Proximity sweep failed: {ex.Message}");
            }
        }
    }
}
