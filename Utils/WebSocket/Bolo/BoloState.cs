using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Bolo
{
    /// <summary>
    ///     A single active BOLO record mirrored from the Java MDT. Times are stored
    ///     in <b>local</b> time so they line up with how Policing Redefined evaluates
    ///     <c>VehicleBOLO.IsActive</c> (which compares against the local clock).
    /// </summary>
    public sealed class BoloEntry
    {
        public string Plate { get; private set; }
        public string Reason { get; private set; }
        public DateTime Issued { get; private set; }
        public DateTime Expires { get; private set; }
        public string IssuedBy { get; private set; }

        /// <summary>
        ///     In-game coordinate where the vehicle was last seen when the BOLO was
        ///     generated. Only meaningful when <see cref="HasLocation" /> is true.
        /// </summary>
        public double X { get; private set; }
        public double Y { get; private set; }
        public bool HasLocation { get; private set; }

        /// <summary>
        ///     Builds an entry from a single JSON object of the
        ///     <c>sync_active_bolos</c> payload. Returns null when the plate is missing.
        /// </summary>
        public static BoloEntry FromJson(JToken token)
        {
            if (!(token is JObject o)) return null;

            var plate = BoloState.NormalizePlate(o["plate"]?.ToString());
            if (string.IsNullOrEmpty(plate)) return null;

            return new BoloEntry
            {
                Plate = plate,
                Reason = o["reason"]?.ToString() ?? string.Empty,
                Issued = ParseTime(o["issued"]?.ToString()),
                Expires = ParseTime(o["expires"]?.ToString()),
                IssuedBy = o["issuedBy"]?.ToString() ?? string.Empty,
                X = o["lastSeenX"]?.Value<double?>() ?? 0.0,
                Y = o["lastSeenY"]?.Value<double?>() ?? 0.0,
                HasLocation = o["hasLocation"]?.Value<bool?>() ?? false
            };
        }

        /// <summary>
        ///     Parses an ISO-8601 timestamp (e.g. "2026-06-03T18:20:00Z") into local
        ///     time. Falls back to <see cref="DateTime.Now" /> when absent/unparseable.
        /// </summary>
        private static DateTime ParseTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.Now;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed;

            return DateTime.Now;
        }
    }

    /// <summary>
    ///     Holds the BOLO configuration and the active BOLO set pushed from the Java
    ///     MDT. The MDT is the master authority: <see cref="ApplySync" /> replaces the
    ///     entire set on every push, so removals/expiries propagate automatically.
    ///     All access is guarded for safety across the socket-receive and game fibers.
    /// </summary>
    public static class BoloState
    {
        private static readonly object Lock = new object();
        private static Dictionary<string, BoloEntry> _active = new Dictionary<string, BoloEntry>();

        public static volatile bool Enabled;
        public static volatile int RadiusMeters = 250;
        public static volatile int MoveThresholdMeters = 150;
        public static volatile int IdleSeconds = 180;
        public static volatile int ExpiryMinMinutes = 20;
        public static volatile int ExpiryMaxMinutes = 40;

        /// <summary>
        ///     When true, <see cref="Actions.Continuous.BoloAttachAction" /> drops a
        ///     map blip on each in-world vehicle currently flagged with a BOLO.
        /// </summary>
        public static volatile bool ShowBlips;

        /// <summary>
        ///     When true, <see cref="Actions.Continuous.BoloExternalObserveAction" />
        ///     reports BOLOs other plugins placed on nearby vehicles' CDF data up to
        ///     the MDT so they surface in the BOLO Service. PR/CDF-only.
        /// </summary>
        public static volatile bool IngestExternal = true;

        /// <summary>
        ///     Resets all state to safe defaults. Called on (re)initialization so a
        ///     fresh session starts clean until the server re-syncs.
        /// </summary>
        public static void Reset()
        {
            lock (Lock)
            {
                _active = new Dictionary<string, BoloEntry>();
            }

            Enabled = false;
            RadiusMeters = 250;
            MoveThresholdMeters = 150;
            IdleSeconds = 180;
            ExpiryMinMinutes = 20;
            ExpiryMaxMinutes = 40;
            ShowBlips = false;
            IngestExternal = true;
        }

        /// <summary>
        ///     Applies a "bolo_config" payload pushed by the server.
        /// </summary>
        public static void ApplyConfig(JToken data)
        {
            try
            {
                if (!(data is JObject o))
                {
                    Logger.LogWarning($"[BOLO] Invalid bolo_config payload: {data}");
                    return;
                }

                Enabled = o["enabled"]?.Value<bool?>() ?? Enabled;
                RadiusMeters = Clamp(o["radiusMeters"]?.Value<int?>() ?? RadiusMeters, 1, 5000);
                MoveThresholdMeters = Clamp(o["moveThresholdMeters"]?.Value<int?>() ?? MoveThresholdMeters, 0, 5000);
                IdleSeconds = Clamp(o["idleSeconds"]?.Value<int?>() ?? IdleSeconds, 0, 86400);
                ExpiryMinMinutes = Clamp(o["expiryMinMinutes"]?.Value<int?>() ?? ExpiryMinMinutes, 1, 1440);
                ExpiryMaxMinutes = Clamp(o["expiryMaxMinutes"]?.Value<int?>() ?? ExpiryMaxMinutes, 1, 1440);
                if (ExpiryMaxMinutes < ExpiryMinMinutes) ExpiryMaxMinutes = ExpiryMinMinutes;
                ShowBlips = o["showBlips"]?.Value<bool?>() ?? ShowBlips;
                IngestExternal = o["ingestExternalBolos"]?.Value<bool?>() ?? IngestExternal;

                Logger.LogInfo(
                    $"[BOLO] Config updated. Enabled={Enabled}, Radius={RadiusMeters}m, " +
                    $"Move={MoveThresholdMeters}m, Idle={IdleSeconds}s, " +
                    $"Lifetime={ExpiryMinMinutes}-{ExpiryMaxMinutes}m, ShowBlips={ShowBlips}, " +
                    $"IngestExternal={IngestExternal}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Failed to apply bolo_config: {ex.Message}");
            }
        }

        /// <summary>
        ///     Replaces the active BOLO set from a "sync_active_bolos" payload.
        /// </summary>
        public static void ApplySync(JToken data)
        {
            try
            {
                var map = new Dictionary<string, BoloEntry>();

                if (data is JArray arr)
                    foreach (var item in arr)
                    {
                        var entry = BoloEntry.FromJson(item);
                        if (entry != null) map[entry.Plate] = entry;
                    }
                else
                    Logger.LogWarning($"[BOLO] sync_active_bolos payload was not an array: {data}");

                lock (Lock)
                {
                    _active = map;
                }

                Logger.LogInfo($"[BOLO] Active set synced. Count={map.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Failed to apply sync_active_bolos: {ex.Message}");
            }
        }

        /// <summary>
        ///     Attempts to retrieve the active BOLO for a (normalized) plate.
        /// </summary>
        public static bool TryGet(string plate, out BoloEntry entry)
        {
            entry = null;
            var key = NormalizePlate(plate);
            if (string.IsNullOrEmpty(key)) return false;

            lock (Lock)
            {
                return _active.TryGetValue(key, out entry);
            }
        }

        /// <summary>
        ///     Returns a shallow snapshot of the active BOLO set for safe iteration.
        /// </summary>
        public static Dictionary<string, BoloEntry> Snapshot()
        {
            lock (Lock)
            {
                return new Dictionary<string, BoloEntry>(_active);
            }
        }

        /// <summary>
        ///     Normalizes a plate the same way the Java side does: trimmed + uppercased.
        /// </summary>
        public static string NormalizePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return null;
            var trimmed = plate.Trim().ToUpperInvariant();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
