using System;
using System.Collections.Generic;
using System.Globalization;
using CommonDataFramework.Modules.VehicleDatabase;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Bolo;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    /// <summary>
    ///     Observes BOLOs that <b>other plugins</b> placed on nearby vehicles' CDF
    ///     <see cref="VehicleData" /> and reports them up to the Java MDT
    ///     (<c>external_bolo_observed</c>) so they surface in the BOLO Service. PR-only.
    ///
    ///     <para>
    ///     This is strictly read-only on the CDF side - it never adds, removes, or
    ///     persists anything. The MDT mirrors what we report for display and never
    ///     reconciles those mirrors back onto the entity, so the owning plugin keeps
    ///     sole control of its flag and no two parties ever fight over one BOLO.
    ///     </para>
    ///
    ///     <para>
    ///     "Ours vs theirs" is decided by plate presence in <see cref="BoloState" />:
    ///     a plate already in the MDT-owned set is skipped (we attached it, or it is
    ///     already mirrored); any other active CDF BOLO is external and reported. The
    ///     report is throttled and idempotent on the MDT, so re-sending the current
    ///     external set each cycle is safe and self-heals after an MDT restart.
    ///     </para>
    ///
    ///     <para>
    ///     Mirrored external BOLOs never expire on the MDT's clock, so removal is
    ///     driven by <b>positive confirmation</b>: when a plate we previously reported
    ///     is seen again in scope on a vehicle that no longer carries an active CDF
    ///     BOLO, we emit <c>external_bolo_cleared</c> for it. A vehicle that simply
    ///     drives out of scope is never treated as cleared (we can't see it), so an
    ///     active out-of-view BOLO is never dropped by mistake.
    ///     </para>
    /// </summary>
    public class BoloExternalObserveAction : IContinuousAction
    {
        /// <summary>How often (seconds) to scan + report. Idempotent, so a relaxed cadence is fine.</summary>
        private const double IntervalSeconds = 10.0;

        private DateTime _lastRunUtc = DateTime.MinValue;

        /// <summary>Plates we have reported as external, pending a confirmed clear.</summary>
        private readonly HashSet<string> _reported = new HashSet<string>();

        public void Execute(GameClientSocket client)
        {
            if (client == null) return;
            if (Misc.Misc.CurrentMode != Misc.Misc.IntegrationMode.PolicingRedefined) return;
            if (!BoloState.IngestExternal) return;

            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - _lastRunUtc).TotalSeconds < IntervalSeconds) return;
            _lastRunUtc = nowUtc;

            var player = Main.LPC;
            if (player == null || !player.Exists()) return;

            try
            {
                var owned = BoloState.Snapshot();
                var radius = BoloState.RadiusMeters;
                var ownVehicle = Main.LPCV;
                var playerPos = player.Position;

                // One reported entry per plate (the MDT holds one BOLO per plate).
                var byPlate = new Dictionary<string, JObject>();
                // Plates seen in scope this pass that currently carry NO active external
                // BOLO - candidates for a confirmed clear.
                var clearCandidates = new HashSet<string>();

                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (!vehicle || !vehicle.Exists()) continue;
                    if (ownVehicle && vehicle == ownVehicle) continue;
                    if (playerPos.DistanceTo(vehicle.Position) > radius) continue;

                    var plate = BoloState.NormalizePlate(vehicle.LicensePlate);
                    if (string.IsNullOrEmpty(plate)) continue;

                    // Plate is MDT-owned (we attached it) or already mirrored - not external.
                    if (owned.ContainsKey(plate)) continue;

                    var data = vehicle.GetVehicleData();
                    // Couldn't read CDF data - treat as unknown, never as a clear.
                    if (data == null) continue;

                    VehicleBOLO best;
                    try
                    {
                        best = PickExternalBolo(data);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[BOLO] Could not read CDF BOLOs on [{plate}]: {ex.Message}");
                        continue;
                    }

                    if (best == null)
                    {
                        // In scope, readable, and no active external BOLO - candidate clear.
                        clearCandidates.Add(plate);
                        continue;
                    }

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

                    var pos = vehicle.Position;
                    byPlate[plate] = new JObject
                    {
                        ["plate"] = plate,
                        ["reason"] = best.Reason ?? string.Empty,
                        ["issuedBy"] = best.IssuedBy ?? string.Empty,
                        ["issued"] = ToUtcIso(best.Issued),
                        ["vehicleClass"] = VehicleClassUtil.GetClassName(vehicle),
                        ["occupied"] = occupied,
                        ["x"] = pos.X,
                        ["y"] = pos.Y
                    };
                }

                // A plate flagged on any in-scope vehicle wins over a clear candidate
                // (same plate can appear on more than one entity).
                clearCandidates.ExceptWith(byPlate.Keys);

                // Confirmed clears: plates we reported before that are now positively
                // observed clear in scope.
                var cleared = new List<string>();
                foreach (var plate in _reported)
                    if (clearCandidates.Contains(plate))
                        cleared.Add(plate);

                foreach (var plate in cleared) _reported.Remove(plate);
                foreach (var plate in byPlate.Keys) _reported.Add(plate);

                if (byPlate.Count > 0)
                {
                    var bolos = new JArray();
                    foreach (var entry in byPlate.Values) bolos.Add(entry);
                    client.Send("external_bolo_observed", new JObject { ["bolos"] = bolos });
                }

                if (cleared.Count > 0)
                {
                    var plates = new JArray();
                    foreach (var plate in cleared) plates.Add(plate);
                    client.Send("external_bolo_cleared", new JObject { ["plates"] = plates });
                    Logger.LogInfo($"[BOLO] Confirmed {cleared.Count} external BOLO clear(s).");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] External observe failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Returns the active CDF BOLO with the latest expiry on the vehicle, or
        ///     null when it carries none that are active.
        /// </summary>
        private static VehicleBOLO PickExternalBolo(VehicleData data)
        {
            var all = data.GetAllBOLOs();
            if (all == null) return null;

            VehicleBOLO best = null;
            foreach (var b in all)
            {
                if (b == null || !b.IsActive) continue;
                if (best == null || b.Expires > best.Expires) best = b;
            }

            return best;
        }

        /// <summary>
        ///     Serializes a CDF <see cref="DateTime" /> as a round-trip UTC ISO-8601
        ///     string so the MDT parses it unambiguously. Unspecified-kind values are
        ///     treated as local (which is how plugins typically stamp them).
        /// </summary>
        private static string ToUtcIso(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime()
                : dt.ToUniversalTime();
            return utc.ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
