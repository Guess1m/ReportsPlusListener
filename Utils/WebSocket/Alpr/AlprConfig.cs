using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Alpr
{
    /// <summary>
    ///     One ALPR camera mounted on the player's vehicle. Geometry is expressed in the
    ///     vehicle's local space (X = right, Y = forward, Z = up). Immutable once built so
    ///     the scan and draw fibers can share it without locking.
    /// </summary>
    public sealed class AlprCamera
    {
        public AlprCamera(string id, string name, bool enabled, float yawDegrees, float fovDegrees, float rangeMeters,
            Vector3 offset, Color color)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? "Camera" : name;
            Enabled = enabled;
            YawDegrees = yawDegrees;
            FovDegrees = MathHelper.Clamp(fovDegrees, 1f, 179f);
            RangeMeters = MathHelper.Clamp(rangeMeters, 1f, 150f);
            Offset = offset;
            Color = color;

            // Yaw is measured clockwise from the vehicle's nose: 0 = front, 90 = right,
            // 180 = rear, 270 = left. Local forward is +Y and right is +X.
            var yawRad = MathHelper.ConvertDegreesToRadians(yawDegrees);
            DirX = (float)Math.Sin(yawRad);
            DirY = (float)Math.Cos(yawRad);
            CosHalfFov = (float)Math.Cos(MathHelper.ConvertDegreesToRadians(FovDegrees / 2f));
        }

        public string Id { get; }
        public string Name { get; }
        public bool Enabled { get; }
        public float YawDegrees { get; }
        public float FovDegrees { get; }
        public float RangeMeters { get; }
        public Vector3 Offset { get; }
        public Color Color { get; }

        /// <summary>Unit view direction in vehicle-local XY.</summary>
        public float DirX { get; }

        public float DirY { get; }

        /// <summary>Cosine of half the field of view, precomputed for the cone test.</summary>
        public float CosHalfFov { get; }

        public static AlprCamera FromJson(JToken token)
        {
            if (!(token is JObject o)) return null;
            return new AlprCamera(
                o["id"]?.ToString(),
                o["name"]?.ToString(),
                o["enabled"]?.Value<bool?>() ?? true,
                o["yaw"]?.Value<float?>() ?? 0f,
                o["fov"]?.Value<float?>() ?? 40f,
                o["range"]?.Value<float?>() ?? 30f,
                new Vector3(
                    o["offsetX"]?.Value<float?>() ?? 0f,
                    o["offsetY"]?.Value<float?>() ?? 0f,
                    o["offsetZ"]?.Value<float?>() ?? 0.8f),
                ParseColor(o["color"]?.ToString()));
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.Cyan;
            var s = hex.Trim().TrimStart('#');
            if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return Color.Cyan;
        }
    }

    /// <summary>
    ///     Immutable ALPR configuration snapshot pushed by the MDT (<c>alpr_config</c>).
    ///     A new instance replaces the old one atomically, so readers just grab
    ///     <see cref="AlprState.Config" /> once per tick and use it consistently.
    /// </summary>
    public sealed class AlprConfig
    {
        public bool Enabled { get; private set; }

        /// <summary>Officer pause from the MDT (ALPR toggle). Enabled stays the admin switch.</summary>
        public bool Paused { get; private set; }
        public bool RequireEmergencyVehicle { get; private set; } = true;
        public bool RequireDriverSeat { get; private set; }
        public int ScanIntervalMs { get; private set; } = 400;
        public int PlateCooldownSeconds { get; private set; } = 30;
        public int MaxReadsPerTick { get; private set; } = 6;
        public int MaxNewRecordsPerTick { get; private set; } = 3;
        public float PlateAngleDegrees { get; private set; } = 65f;
        public float MaxHeightDelta { get; private set; } = 6f;
        public bool LineOfSight { get; private set; } = true;
        public bool ReadEmptyVehicles { get; private set; } = true;
        public bool ReadEmergencyVehicles { get; private set; }

        /// <summary>Suspends scanning while an LSPDFR traffic stop is in progress.</summary>
        public bool PauseDuringTrafficStop { get; private set; }
        public bool DebugDraw { get; private set; }
        public bool HighlightHits { get; private set; } = true;
        public bool BlipHits { get; private set; } = true;
        public int BlipSeconds { get; private set; } = 45;

        /// <summary>Hit blip scale on the map (MDT "Blip Size": Small / Normal / Large).</summary>
        public float BlipScale { get; private set; } = 0.9f;

        /// <summary>Names hit blips "ALPR: plate" in the pause-map legend.</summary>
        public bool BlipShowPlate { get; private set; } = true;

        /// <summary>In-world plate tags on every read while scan zones are shown.</summary>
        public bool PlateTags { get; private set; } = true;

        /// <summary>
        ///     Optional cleaning of StopThePed / Policing Redefined documents for vehicles as they
        ///     appear (see <see cref="PluginRecordCleaner" />). Off unless the MDT turns it on.
        /// </summary>
        public bool CleanPluginRecords { get; private set; }

        /// <summary>Share (0-100) of newly appeared vehicles whose invalid documents are cleaned.</summary>
        public int CleanRecordChance { get; private set; } = 85;

        public IReadOnlyList<AlprCamera> Cameras { get; private set; } = new AlprCamera[0];

        /// <summary>Cosine of the max plate read angle, precomputed for the facing test.</summary>
        public float CosPlateAngle { get; private set; } = (float)Math.Cos(MathHelper.ConvertDegreesToRadians(65f));

        /// <summary>Largest enabled camera range; bounds the world entity query.</summary>
        public float MaxRange { get; private set; }

        public static AlprConfig Disabled()
        {
            return new AlprConfig();
        }

        public static AlprConfig FromJson(JObject o, AlprConfig previous)
        {
            var p = previous ?? Disabled();
            var c = new AlprConfig
            {
                Enabled = o["enabled"]?.Value<bool?>() ?? p.Enabled,
                Paused = o["paused"]?.Value<bool?>() ?? p.Paused,
                RequireEmergencyVehicle = o["requireEmergencyVehicle"]?.Value<bool?>() ?? p.RequireEmergencyVehicle,
                RequireDriverSeat = o["requireDriverSeat"]?.Value<bool?>() ?? p.RequireDriverSeat,
                ScanIntervalMs = Clamp(o["scanIntervalMs"]?.Value<int?>() ?? p.ScanIntervalMs, 100, 5000),
                PlateCooldownSeconds = Clamp(o["plateCooldownSeconds"]?.Value<int?>() ?? p.PlateCooldownSeconds, 1, 3600),
                MaxReadsPerTick = Clamp(o["maxReadsPerTick"]?.Value<int?>() ?? p.MaxReadsPerTick, 1, 50),
                MaxNewRecordsPerTick = Clamp(o["maxNewRecordsPerTick"]?.Value<int?>() ?? p.MaxNewRecordsPerTick, 1, 20),
                PlateAngleDegrees = MathHelper.Clamp(o["plateAngleDegrees"]?.Value<float?>() ?? p.PlateAngleDegrees, 10f, 90f),
                MaxHeightDelta = MathHelper.Clamp(o["maxHeightDelta"]?.Value<float?>() ?? p.MaxHeightDelta, 1f, 50f),
                LineOfSight = o["lineOfSight"]?.Value<bool?>() ?? p.LineOfSight,
                ReadEmptyVehicles = o["readEmptyVehicles"]?.Value<bool?>() ?? p.ReadEmptyVehicles,
                ReadEmergencyVehicles = o["readEmergencyVehicles"]?.Value<bool?>() ?? p.ReadEmergencyVehicles,
                PauseDuringTrafficStop = o["pauseDuringTrafficStop"]?.Value<bool?>() ?? p.PauseDuringTrafficStop,
                DebugDraw = o["debugDraw"]?.Value<bool?>() ?? p.DebugDraw,
                HighlightHits = o["highlightHits"]?.Value<bool?>() ?? p.HighlightHits,
                BlipHits = o["blipHits"]?.Value<bool?>() ?? p.BlipHits,
                BlipSeconds = Clamp(o["blipSeconds"]?.Value<int?>() ?? p.BlipSeconds, 5, 600),
                BlipScale = o["blipSize"] != null ? ParseBlipScale(o["blipSize"].ToString()) : p.BlipScale,
                BlipShowPlate = o["blipShowPlate"]?.Value<bool?>() ?? p.BlipShowPlate,
                PlateTags = o["plateTags"]?.Value<bool?>() ?? p.PlateTags,
                CleanPluginRecords = o["cleanPluginRecords"]?.Value<bool?>() ?? p.CleanPluginRecords,
                CleanRecordChance = Clamp(o["cleanRecordChance"]?.Value<int?>() ?? p.CleanRecordChance, 0, 100)
            };

            c.CosPlateAngle = (float)Math.Cos(MathHelper.ConvertDegreesToRadians(c.PlateAngleDegrees));

            if (o["cameras"] is JArray arr)
            {
                var list = new List<AlprCamera>();
                foreach (var item in arr)
                {
                    var cam = AlprCamera.FromJson(item);
                    if (cam != null) list.Add(cam);
                }

                c.Cameras = list;
            }
            else
            {
                c.Cameras = p.Cameras;
            }

            var max = 0f;
            foreach (var cam in c.Cameras)
                if (cam.Enabled && cam.RangeMeters > max)
                    max = cam.RangeMeters;
            c.MaxRange = max;
            return c;
        }

        private static float ParseBlipScale(string size)
        {
            switch ((size ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "SMALL": return 0.7f;
                case "LARGE": return 1.2f;
                default: return 0.9f;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }

    /// <summary>
    ///     Process-wide ALPR state. The MDT owns everything, including the officer's
    ///     pause and scan-zone toggles; there is no in-game ALPR menu or keybind.
    /// </summary>
    public static class AlprState
    {
        private static volatile AlprConfig _config = AlprConfig.Disabled();

        /// <summary>Current configuration snapshot. Read once per tick.</summary>
        public static AlprConfig Config => _config;

        /// <summary>Scan-zone overlay flag. Owned by the MDT and set by each config push.</summary>
        public static bool DebugDraw => _config.DebugDraw;

        /// <summary>Set when a config arrives so the scanner re-reports its status.</summary>
        public static volatile bool ConfigChanged;

        /// <summary>Set by the toggle keybind; the scan fiber sends the request to the MDT.</summary>
        public static volatile bool ToggleRequested;

        /// <summary>Notification to show on the next frame (config pushes arrive off the game fiber).</summary>
        public static volatile string PendingNotice;

        private static bool _received;

        public static void Reset()
        {
            _config = AlprConfig.Disabled();
            ToggleRequested = false;
            PendingNotice = null;
            _received = false;
            ConfigChanged = true;
        }

        public static void ApplyConfig(JToken data)
        {
            try
            {
                if (!(data is JObject o))
                {
                    Logger.LogWarning($"[ALPR] Invalid alpr_config payload: {data}");
                    return;
                }

                var next = AlprConfig.FromJson(o, _config);
                var prev = _config;
                if (_received && next.Enabled && next.Paused != prev.Paused)
                    PendingNotice = next.Paused ? "ALPR scanning ~r~PAUSED" : "ALPR scanning ~g~RESUMED";
                _received = true;
                _config = next;
                ConfigChanged = true;
                Logger.LogInfo($"[ALPR] Config applied. Enabled={next.Enabled}, Paused={next.Paused}, Debug={next.DebugDraw}, Cameras={next.Cameras.Count}, " +
                               $"Interval={next.ScanIntervalMs}ms, Cooldown={next.PlateCooldownSeconds}s, LOS={next.LineOfSight}, " +
                               $"CleanPluginRecords={next.CleanPluginRecords} ({next.CleanRecordChance}%)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ALPR] Failed to apply alpr_config: {ex.Message}");
            }
        }
    }
}
