using System;
using System.Collections.Generic;
using System.Linq;
using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Bolo;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    /// <summary>
    ///     Reconciles the active MDT BOLO set onto in-world vehicles using Policing
    ///     Redefined's CDF <see cref="VehicleBOLO" /> API. PR-only.
    ///
    ///     <para>
    ///     This is a polling reconcile rather than a spawn hook, which makes it
    ///     resilient to GTA entity culling: when a flagged vehicle is unloaded its
    ///     attached BOLO evaporates with the entity, and when it (or a fresh instance
    ///     with the same plate) re-enters scope it is re-flagged on the next tick.
    ///     </para>
    ///
    ///     <para>
    ///     So flagged vehicles don't vanish before a player can find them, <b>every</b>
    ///     BOLO vehicle is marked persistent (and its driver too, if occupied, so the
    ///     suspect doesn't despawn). Persistence is released the instant the BOLO
    ///     clears/expires, the vehicle drops out of tracking, the player's own vehicle
    ///     is involved, or the plugin tears down (off-duty / unload / re-init) - so the
    ///     world never fills up with stuck entities.
    ///     </para>
    ///
    ///     <para>
    ///     Map blips are handled separately by <see cref="BoloBlipAction" /> (static,
    ///     last-seen, all modes).
    ///     </para>
    /// </summary>
    public class BoloAttachAction : IContinuousAction
    {
        private readonly Dictionary<int, Attached> _attached = new Dictionary<int, Attached>();

        public void Execute(GameClientSocket client)
        {
            if (Misc.Misc.CurrentMode != Misc.Misc.IntegrationMode.PolicingRedefined) return;

            try
            {
                var active = BoloState.Snapshot();
                var present = new HashSet<int>();
                var ownVehicle = Main.LPCV;

                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (!vehicle || !vehicle.Exists()) continue;
                    // Never flag/persist the player's own vehicle (e.g. a manual BOLO
                    // that happens to target the player's plate) - persisting the
                    // player or their car would be disruptive and immersion-breaking.
                    if (ownVehicle && vehicle == ownVehicle) continue;

                    var handle = (int)vehicle.Handle.Value;
                    present.Add(handle);

                    var plate = BoloState.NormalizePlate(vehicle.LicensePlate);
                    BoloEntry entry = null;
                    var hasActive = plate != null && active.TryGetValue(plate, out entry);

                    _attached.TryGetValue(handle, out var current);

                    if (hasActive)
                    {
                        var changed = current != null &&
                                      (current.ExpiresTicks != entry.Expires.Ticks
                                       || current.Reason != entry.Reason
                                       || current.Plate != entry.Plate);

                        if (current == null || changed)
                            ApplyBolo(vehicle, handle, entry, current);
                        else if (!current.Persisted)
                            EnsurePersistent(vehicle, current);
                    }
                    else if (current != null)
                    {
                        RemoveBolo(vehicle, handle, current);
                    }
                }

                if (_attached.Count > 0)
                {
                    var stale = _attached.Keys.Where(h => !present.Contains(h)).ToList();
                    foreach (var h in stale)
                    {
                        if (_attached.TryGetValue(h, out var gone)) ReleasePersistence(gone);
                        _attached.Remove(h);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Attach reconcile failed: {ex.Message}");
            }
        }

        private void ApplyBolo(Vehicle vehicle, int handle, BoloEntry entry, Attached current)
        {
            var data = vehicle.GetVehicleData();
            if (data == null) return;

            if (current?.Bolo != null)
                try
                {
                    data.RemoveBOLO(current.Bolo);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[BOLO] Could not remove stale BOLO on [{entry.Plate}]: {ex.Message}");
                }

            try
            {
                var bolo = FindMatching(data, entry);
                if (bolo == null)
                {
                    bolo = new VehicleBOLO(entry.Reason ?? string.Empty, entry.Issued, entry.Expires,
                        entry.IssuedBy ?? string.Empty);
                    bolo.SetIsActive(true);
                    data.AddBOLO(bolo);
                    Logger.LogInfo($"[BOLO] Flagged in-world vehicle [{entry.Plate}]: {entry.Reason}");
                }
                else
                {
                    Logger.LogInfo($"[BOLO] Adopted existing in-world flag on [{entry.Plate}]");
                }

                var record = new Attached
                {
                    Plate = entry.Plate,
                    Reason = entry.Reason,
                    ExpiresTicks = entry.Expires.Ticks,
                    Bolo = bolo,
                    Vehicle = vehicle
                };

                // Track the record BEFORE touching engine persistence so a mid-teardown
                // fiber abort can always find and release whatever we persist below.
                _attached[handle] = record;

                if (current != null && current.Persisted)
                {
                    record.Persisted = true;
                    record.Driver = current.Driver;
                }
                else
                {
                    EnsurePersistent(vehicle, record);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Failed to attach BOLO to [{entry.Plate}]: {ex.Message}");
            }
        }

        /// <summary>
        ///     Marks the vehicle persistent so the engine won't cull a flagged vehicle
        ///     before a player can find it - occupied or not. If it is occupied, the
        ///     driver is persisted too so the wanted suspect doesn't despawn. What was
        ///     persisted is recorded on <paramref name="record" /> for later release.
        ///
        ///     <para>
        ///     Bookkeeping (<c>record.Persisted</c> / <c>Vehicle</c> / <c>Driver</c>) is
        ///     set <b>before</b> the engine calls, so even if the fiber is aborted
        ///     mid-call during teardown, <see cref="ReleaseAllPersistence" /> still sees
        ///     the entry and can release it. Releasing a not-actually-persistent entity
        ///     is a harmless no-op.
        ///     </para>
        /// </summary>
        private static void EnsurePersistent(Vehicle vehicle, Attached record)
        {
            if (!(vehicle && vehicle.Exists())) return;

            Ped driver = null;
            try
            {
                driver = vehicle.Driver;
            }
            catch
            {
                // ignore - treat as unoccupied
            }

            var hasDriver = driver && driver.Exists();

            try
            {
                record.Vehicle = vehicle;
                record.Driver = hasDriver ? driver : null;
                record.Persisted = true;

                if (!vehicle.IsPersistent) vehicle.IsPersistent = true;
                if (hasDriver && !driver.IsPersistent) driver.IsPersistent = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BOLO] Could not persist vehicle/driver [{record.Plate}]: {ex.Message}");
            }
        }

        /// <summary>Releases the vehicle + driver we persisted, letting the engine reclaim them.</summary>
        private static void ReleasePersistence(Attached record)
        {
            if (record == null || !record.Persisted) return;

            try
            {
                if (record.Vehicle && record.Vehicle.Exists()) record.Vehicle.IsPersistent = false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BOLO] Could not release vehicle persistence on [{record.Plate}]: {ex.Message}");
            }

            try
            {
                if (record.Driver && record.Driver.Exists()) record.Driver.IsPersistent = false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BOLO] Could not release driver persistence on [{record.Plate}]: {ex.Message}");
            }

            record.Driver = null;
            record.Persisted = false;
        }

        /// <summary>
        ///     Finds an existing BOLO on the vehicle that matches this entry
        ///     (reason + issuer + issued time), so we adopt rather than duplicate.
        /// </summary>
        private static VehicleBOLO FindMatching(VehicleData data, BoloEntry entry)
        {
            try
            {
                var all = data.GetAllBOLOs();
                if (all == null) return null;

                foreach (var b in all)
                {
                    if (b == null) continue;
                    if (string.Equals(b.Reason ?? string.Empty, entry.Reason ?? string.Empty, StringComparison.Ordinal)
                        && string.Equals(b.IssuedBy ?? string.Empty, entry.IssuedBy ?? string.Empty,
                            StringComparison.Ordinal)
                        && b.Issued == entry.Issued)
                        return b;
                }
            }
            catch
            {
                // ignore - treat as no match
            }

            return null;
        }

        private void RemoveBolo(Vehicle vehicle, int handle, Attached current)
        {
            try
            {
                var data = vehicle.GetVehicleData();
                if (data != null && current.Bolo != null) data.RemoveBOLO(current.Bolo);
                ReleasePersistence(current);
                Logger.LogInfo($"[BOLO] Cleared in-world flag for [{current.Plate}]");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BOLO] Could not remove BOLO from [{current.Plate}]: {ex.Message}");
            }
            finally
            {
                _attached.Remove(handle);
            }
        }

        /// <summary>
        ///     Releases persistence on every vehicle + driver we flagged and forgets all
        ///     tracking. Must run on plugin teardown / off-duty so nothing stays stuck.
        /// </summary>
        public void ReleaseAllPersistence()
        {
            foreach (var attached in _attached.Values)
                ReleasePersistence(attached);
            _attached.Clear();
        }

        private sealed class Attached
        {
            public string Plate;
            public string Reason;
            public long ExpiresTicks;
            public VehicleBOLO Bolo;
            public Vehicle Vehicle;
            public Ped Driver;
            public bool Persisted;
        }
    }
}
