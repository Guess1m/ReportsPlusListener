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
    ///     To stop wanted suspects from vanishing, an <b>occupied</b> BOLO vehicle and
    ///     its driver are marked persistent so the engine won't cull them. Persistence
    ///     is released the moment the BOLO clears/expires, the vehicle drops out of
    ///     tracking, or the plugin tears down - so the world never fills up with stuck
    ///     entities. Only occupied vehicles are persisted (the ones that actually drive
    ///     off and despawn); parked/unoccupied ones are left to the engine.
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

                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (!vehicle || !vehicle.Exists()) continue;

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

                if (current != null && current.Persisted)
                {
                    record.Persisted = true;
                    record.Driver = current.Driver;
                }
                else
                {
                    EnsurePersistent(vehicle, record);
                }

                _attached[handle] = record;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Failed to attach BOLO to [{entry.Plate}]: {ex.Message}");
            }
        }

        /// <summary>
        ///     If the vehicle is occupied, marks it and its driver persistent so the
        ///     engine won't cull the wanted suspect, recording what was persisted on the
        ///     <paramref name="record" /> for later release. No-op for unoccupied vehicles.
        /// </summary>
        private static void EnsurePersistent(Vehicle vehicle, Attached record)
        {
            Ped driver = null;
            try
            {
                driver = vehicle.Driver;
            }
            catch
            {
                // ignore - treat as unoccupied
            }

            if (!(driver && driver.Exists())) return;

            try
            {
                if (!vehicle.IsPersistent) vehicle.IsPersistent = true;
                if (!driver.IsPersistent) driver.IsPersistent = true;
                record.Vehicle = vehicle;
                record.Driver = driver;
                record.Persisted = true;
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
