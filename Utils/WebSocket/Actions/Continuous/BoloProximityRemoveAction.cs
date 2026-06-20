using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Bolo;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    /// <summary>
    ///     Drops active BOLOs once the player travels too far from the flagged
    ///     vehicle. The MDT owns the active set, so this only <b>reports</b> the
    ///     out-of-range plates via <c>bolo_proximity_remove</c>; the MDT removes them
    ///     and re-syncs.
    ///
    ///     <para>
    ///     Distance is measured against the vehicle's live position while it is still
    ///     streamed in (found in one world pass), and otherwise against the last-seen
    ///     coordinates the MDT sent with the BOLO. Entries with neither are left for
    ///     normal expiry. Throttled to <see cref="IntervalSeconds" /> - it never runs
    ///     per game tick - and is a no-op unless the feature is enabled and BOLOs are
    ///     active, so it has no measurable cost when idle.
    ///     </para>
    ///
    ///     <para>
    ///     The request is idempotent: if a removal message is missed, the next pass
    ///     re-reports the still-active out-of-range plate. Only MDT-owned BOLOs are
    ///     ever in the synced set (external mirrors are display-only and never pushed
    ///     to the client), so this can never request removal of someone else's BOLO.
    ///     </para>
    /// </summary>
    public class BoloProximityRemoveAction : IContinuousAction
    {
        /// <summary>How often (seconds) to evaluate distances</summary>
        private const double IntervalSeconds = 5.0;

        private DateTime _lastRunUtc = DateTime.MinValue;

        public void Execute(GameClientSocket client)
        {
            if (client == null) return;
            if (!BoloState.ProximityRemovalEnabled) return;

            var maxDistance = BoloState.ProximityRemovalDistanceMeters;
            if (maxDistance <= 0) return;

            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - _lastRunUtc).TotalSeconds < IntervalSeconds) return;
            _lastRunUtc = nowUtc;

            var active = BoloState.Snapshot();
            if (active.Count == 0) return;

            var player = Main.LPC;
            if (player == null || !player.Exists()) return;

            try
            {
                var playerPos = player.Position;
                var livePositions = new Dictionary<string, Vector3>();
                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (!vehicle || !vehicle.Exists()) continue;
                    var plate = BoloState.NormalizePlate(vehicle.LicensePlate);
                    if (string.IsNullOrEmpty(plate)) continue;
                    if (!active.ContainsKey(plate)) continue;
                    livePositions[plate] = vehicle.Position;

                }

                var toRemove = new List<string>();
                foreach (var kvp in active)
                {
                    var plate = kvp.Key;
                    var entry = kvp.Value;
                    if (entry == null) continue;

                    float distance;
                    if (livePositions.TryGetValue(plate, out var livePos))
                    {
                        distance = playerPos.DistanceTo(livePos);
                    }
                    else if (entry.HasLocation)
                    {
                        var lastSeen = new Vector3((float)entry.X, (float)entry.Y, playerPos.Z);
                        distance = playerPos.DistanceTo(lastSeen);
                    }
                    else
                    {
                        // No live entity and no recorded location
                        continue;
                    }

                    if (distance > maxDistance) toRemove.Add(plate);
                }

                if (toRemove.Count == 0) return;

                var plates = new JArray();
                foreach (var plate in toRemove) plates.Add(plate);
                client.Send("bolo_proximity_remove", new JObject { ["plates"] = plates });

                Logger.LogInfo(
                    $"[BOLO] Requested proximity removal of {toRemove.Count} BOLO(s) beyond {maxDistance}m.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Proximity removal check failed: {ex.Message}");
            }
        }
    }
}
