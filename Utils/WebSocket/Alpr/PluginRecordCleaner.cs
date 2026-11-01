using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommonDataFramework.Modules;
using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using ReportsPlus.Utils.Logging;
using StopThePed.API;

namespace ReportsPlus.Utils.WebSocket.Alpr
{
    /// <summary>
    ///     Optional (off by default) in-game cleaning of StopThePed / Policing Redefined vehicle
    ///     documents, so the ALPR isn't flooded by the high invalid-document odds those plugins
    ///     use for traffic stops. On a clean roll (the MDT's Clean Record Rate) an expired or
    ///     missing registration / insurance is set to Valid <b>inside the plugin</b>, which stays
    ///     the single source of truth that dispatch, the plugin's own checks and the MDT all read.
    ///     <para>
    ///         <b>Consistency guarantee.</b> A vehicle is only ever cleaned if ReportsPlus watched it
    ///         appear: a sweep runs every <see cref="SweepIntervalMs" /> while the MDT is connected,
    ///         and a vehicle missing from the previous sweep is at most that old, so nothing can have
    ///         shown its documents yet. Everything else is recorded as "seen" and never touched:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>vehicles that already existed when cleaning turned on (the first sweep is a baseline);</item>
    ///         <item>vehicles that appeared while sweeps were interrupted (MDT disconnected, loading, long hitch) -
    ///             any gap over <see cref="GapMs" /> starts a new baseline;</item>
    ///         <item>vehicles first seen through a player-initiated path (dispatch / plugin plate check, MDT
    ///             lookup), via <see cref="Observe" /> with <c>passive = false</c> in the data generators;</item>
    ///         <item>persistent vehicles or drivers (callouts, pursuits, traffic stops, BOLO targets) and the
    ///             player's own vehicles;</item>
    ///         <item>stolen flags, revoked registrations and all ped data.</item>
    ///     </list>
    ///     <para>
    ///         Every data generator reads the plugin <i>after</i> this runs, so the first payload the MDT
    ///         ever receives for a cleaned vehicle already carries the cleaned values.
    ///     </para>
    ///     <para>
    ///         <b>Cost.</b> Zero when off or in base-game mode (one flag check per generator call). When on:
    ///         one vehicle-pool enumeration per sweep plus a dictionary lookup per vehicle; plugin calls
    ///         only for newly appeared vehicles that pass the roll, and writes only for those with an
    ///         invalid document. Those reads would have happened anyway when the vehicle is first tracked.
    ///     </para>
    ///     All callers run on game fibers (the game thread), so no locking is required.
    /// </summary>
    public static class PluginRecordCleaner
    {
        /// <summary>Sweep cadence while active. Bounds how old a "fresh" vehicle can be.</summary>
        public const int SweepIntervalMs = 500;

        /// <summary>Sweep cadence while inactive (only watches for activation).</summary>
        public const int IdleIntervalMs = 1000;

        /// <summary>A longer gap between sweeps means vehicles could have gone unobserved: re-baseline.</summary>
        private const uint GapMs = 2500;

        /// <summary>Entries unseen for this many sweeps (~10 s) belong to despawned vehicles.</summary>
        private const uint PruneAfterSweeps = 20;

        private const int MaxConsecutiveFailures = 5;
        private const uint StatsLogIntervalMs = 300000;

        private static readonly Dictionary<uint, Entry> Seen = new Dictionary<uint, Entry>();
        private static readonly List<uint> PruneScratch = new List<uint>();
        private static readonly Random Rng = new Random();

        private static bool _active;
        private static bool _watching;
        private static uint _lastSweep;
        private static uint _sweepId;
        private static int _consecutiveFailures;
        private static bool _disabledForSession;
        private static int _newVehicles;
        private static int _cleaned;
        private static uint _lastStatsLog;

        /// <summary>Whether cleaning is currently switched on (drives the fiber's sleep).</summary>
        public static bool IsActive => _active;

        /// <summary>Clears all state (unload / off duty). The next activation starts with a baseline.</summary>
        public static void Reset()
        {
            Seen.Clear();
            _active = false;
            _watching = false;
            _lastSweep = 0;
            _consecutiveFailures = 0;
            _newVehicles = 0;
            _cleaned = 0;
        }

        /// <summary>
        ///     One sweep. Call from a game fiber, only while the MDT is connected (a skipped sweep is
        ///     detected as a gap and handled safely).
        /// </summary>
        public static void Tick()
        {
            var cfg = AlprState.Config;
            var active = ComputeActive(cfg);
            if (active != _active)
            {
                _active = active;
                Seen.Clear();
                _watching = false;
                Logger.LogInfo(active
                    ? $"[ALPR] Plugin record cleaning ON ({Misc.Misc.CurrentMode}, clean rate {cfg.CleanRecordChance}%). Existing vehicles are left untouched."
                    : "[ALPR] Plugin record cleaning OFF.");
            }

            if (!active) return;

            var now = Game.GameTime;
            // Only a continuous run of sweeps proves an unknown vehicle is new. After a gap
            // (or on activation) this sweep just records everything as seen.
            var continuous = _watching && now - _lastSweep <= GapMs;
            if (_watching && !continuous)
                Logger.LogInfo($"[ALPR] Plugin record cleaning resumed after a {(now - _lastSweep) / 1000f:0.0}s gap; vehicles that appeared meanwhile are left untouched.");

            var sweep = ++_sweepId;
            var vehicles = World.GetAllVehicles();
            if (vehicles != null)
                foreach (var v in vehicles)
                {
                    if (!v || !v.Exists()) continue;
                    var handle = v.Handle.Value;
                    var model = v.Model.Hash;
                    if (Seen.TryGetValue(handle, out var known) && known.Model == model)
                    {
                        known.LastSweep = sweep;
                        continue;
                    }

                    Seen[handle] = new Entry { Model = model, LastSweep = sweep };
                    if (continuous) TryClean(v, cfg.CleanRecordChance);
                }

            _watching = true;
            _lastSweep = now;

            if (sweep % PruneAfterSweeps == 0) Prune(sweep);
            LogStats(now);
        }

        /// <summary>
        ///     Called by every vehicle data generator before it reads plugin data. Registers the
        ///     vehicle as seen so it can never be cleaned later. <paramref name="passive" /> callers
        ///     (the background vehicle tracker and the ALPR sensor) may clean a vehicle that appeared
        ///     since the last sweep; player-initiated paths pass false and never clean.
        /// </summary>
        public static void Observe(Vehicle vehicle, bool passive)
        {
            if (!_active) return;
            try
            {
                var cfg = AlprState.Config;
                if (!ComputeActive(cfg) || !vehicle || !vehicle.Exists()) return;

                var handle = vehicle.Handle.Value;
                var model = vehicle.Model.Hash;
                if (Seen.TryGetValue(handle, out var known) && known.Model == model) return;
                Seen[handle] = new Entry { Model = model, LastSweep = _sweepId };

                // Unknown to an uninterrupted run of sweeps => appeared after the last one.
                if (passive && _watching && Game.GameTime - _lastSweep <= GapMs) TryClean(vehicle, cfg.CleanRecordChance);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ALPR] Plugin record cleaning skipped a vehicle: {ex.Message}");
            }
        }

        private static bool ComputeActive(AlprConfig cfg)
        {
            if (_disabledForSession || cfg == null || !cfg.Enabled || !cfg.CleanPluginRecords || cfg.CleanRecordChance <= 0) return false;
            var mode = Misc.Misc.CurrentMode;
            return mode == Misc.Misc.IntegrationMode.StopThePed || mode == Misc.Misc.IntegrationMode.PolicingRedefined;
        }

        private static void TryClean(Vehicle vehicle, int cleanChance)
        {
            _newVehicles++;
            // Roll first: a dirty roll costs no plugin call at all.
            if (Rng.Next(100) >= cleanChance) return;
            if (!IsEligible(vehicle)) return;

            try
            {
                var changed = Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined
                    ? CleanPolicingRedefined(vehicle)
                    : CleanStopThePed(vehicle);
                if (changed) _cleaned++;
                _consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                // Whatever was written before the failure is what the plugin holds, and every
                // generator reads the plugin afterwards, so the MDT still matches it.
                if (++_consecutiveFailures <= 3)
                    Logger.LogWarning($"[ALPR] Plugin record cleaning failed for a vehicle: {ex.Message}");
                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _disabledForSession = true;
                    Logger.LogError("[ALPR] Plugin record cleaning disabled for this session after repeated plugin API failures " +
                                    "(incompatible StopThePed / Policing Redefined version?). Vehicle data is left as the plugin generates it.");
                }
            }
        }

        /// <summary>
        ///     Freshness already rules out anything the player could have checked. This additionally
        ///     protects vehicles a script owns: callouts mark their vehicles and suspects persistent
        ///     (and may set documents deliberately), as do pursuits, traffic stops and BOLO attachment.
        /// </summary>
        private static bool IsEligible(Vehicle vehicle)
        {
            if (!vehicle || !vehicle.Exists() || vehicle.IsPersistent) return false;

            var player = Game.LocalPlayer.Character;
            if (player && player.Exists())
            {
                var current = player.CurrentVehicle;
                if (current && current == vehicle) return false;
                var last = player.LastVehicle;
                if (last && last == vehicle) return false;
            }

            var driver = vehicle.Driver;
            return !(driver && driver.Exists() && driver.IsPersistent);
        }

        // Plugin calls live in their own non-inlined methods so only the installed plugin's
        // assembly is ever loaded (same pattern as GetValueMethods).

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CleanStopThePed(Vehicle vehicle)
        {
            var changed = false;
            if (Functions.getVehicleRegistrationStatus(vehicle) != STPVehicleStatus.Valid)
            {
                Functions.setVehicleRegistrationStatus(vehicle, STPVehicleStatus.Valid);
                changed = true;
            }

            if (Functions.getVehicleInsuranceStatus(vehicle) != STPVehicleStatus.Valid)
            {
                Functions.setVehicleInsuranceStatus(vehicle, STPVehicleStatus.Valid);
                changed = true;
            }

            return changed;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CleanPolicingRedefined(Vehicle vehicle)
        {
            var data = vehicle.GetVehicleData();
            if (data == null) return false;
            var changed = CleanDocument(data.Registration);
            changed |= CleanDocument(data.Insurance);
            return changed;
        }

        /// <summary>
        ///     Expired / missing -> Valid with a future expiration (kept if the plugin's own date is
        ///     already in the future). Revoked is a deliberate status and is left alone.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CleanDocument(VehicleDocument doc)
        {
            if (doc == null) return false;
            if (doc.Status != EDocumentStatus.Expired && doc.Status != EDocumentStatus.None) return false;

            doc.Status = EDocumentStatus.Valid;
            var today = DateTime.Today;
            if (!doc.ExpirationDate.HasValue || doc.ExpirationDate.Value.Date <= today)
                doc.ExpirationDate = today.AddDays(Rng.Next(30, 731));
            // In case the plugin derives status from the date when it is assigned.
            if (doc.Status != EDocumentStatus.Valid) doc.Status = EDocumentStatus.Valid;
            return true;
        }

        private static void Prune(uint sweep)
        {
            PruneScratch.Clear();
            foreach (var kv in Seen)
                if (sweep - kv.Value.LastSweep > PruneAfterSweeps)
                    PruneScratch.Add(kv.Key);
            foreach (var key in PruneScratch) Seen.Remove(key);
        }

        private static void LogStats(uint now)
        {
            if (now - _lastStatsLog < StatsLogIntervalMs || _newVehicles == 0) return;
            _lastStatsLog = now;
            Logger.LogInfo($"[ALPR] Plugin record cleaning: cleaned {_cleaned} of {_newVehicles} newly appeared vehicles this session " +
                           $"(tracking {Seen.Count}).");
        }

        private sealed class Entry
        {
            public uint LastSweep;
            public uint Model;
        }
    }
}
