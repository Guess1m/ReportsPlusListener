using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Continuous;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Alpr
{
    /// <summary>
    ///     Orthonormal basis of an entity, captured once per tick so world/local
    ///     conversions are plain dot products instead of per-call native round trips.
    ///     GTA local space: X = right, Y = forward, Z = up.
    /// </summary>
    public struct EntityBasis
    {
        public Vector3 Position;
        public Vector3 Right;
        public Vector3 Forward;
        public Vector3 Up;

        public static EntityBasis Of(Entity e)
        {
            return new EntityBasis { Position = e.Position, Right = e.RightVector, Forward = e.ForwardVector, Up = e.UpVector };
        }

        public Vector3 ToWorld(Vector3 local)
        {
            return Position + Right * local.X + Forward * local.Y + Up * local.Z;
        }

        public Vector3 ToLocal(Vector3 world)
        {
            var d = world - Position;
            return new Vector3(Vector3.Dot(d, Right), Vector3.Dot(d, Forward), Vector3.Dot(d, Up));
        }
    }

    /// <summary>
    ///     ALPR sensor. Runs on its own fiber at the MDT-configured interval and reports
    ///     raw plate reads; it never decides what counts as a "hit" - the MDT resolves
    ///     every read against the same vehicle records the DMV lookup uses, which is what
    ///     keeps ALPR and lookups consistent.
    ///     <para>
    ///         Per tick: one bounded world query around the player's vehicle, then for each
    ///         candidate a cone test per camera (pure math), a plate-facing test (cameras
    ///         can't read a plate side-on), and - only for survivors - an optional world
    ///         line-of-sight trace. A per-plate cooldown keeps the same car from being
    ///         re-sent every tick, and both reads and new-record payloads are capped per tick
    ///         so a dense traffic jam can never spike frame time.
    ///     </para>
    /// </summary>
    public static class AlprScanner
    {
        public const string StatusActive = "ACTIVE";

        private const int StatusHeartbeatMs = 30000;
        private const int PruneIntervalMs = 10000;
        private const int RecentTargetTtlMs = 120000;

        private static readonly Dictionary<string, uint> PlateCooldowns = new Dictionary<string, uint>();
        private static readonly Dictionary<int, RecentTarget> Recent = new Dictionary<int, RecentTarget>();
        private static readonly Dictionary<string, uint> CameraLastRead = new Dictionary<string, uint>();
        private static readonly List<string> PruneScratch = new List<string>();
        private static readonly List<int> PruneHandleScratch = new List<int>();

        private static uint _lastPrune;
        private static uint _lastStatusSent;
        private static string _lastStatus;
        private static long _seq;

        /// <summary>Current sensor status as last reported to the MDT.</summary>
        public static string Status => _lastStatus ?? "OFFLINE";

        /// <summary>A vehicle read recently, kept so alerts and the overlay can find it by handle.</summary>
        public sealed class RecentTarget
        {
            public Vehicle Vehicle;
            public uint ReadAt;
            public AlprCamera Camera;

            /// <summary>Normalized plate as read, cached so the overlay needn't query it every frame.</summary>
            public string Plate;
        }

        public static void Reset()
        {
            PlateCooldowns.Clear();
            Recent.Clear();
            CameraLastRead.Clear();
            _lastStatus = null;
            _lastStatusSent = 0;
        }

        public static bool TryGetRecent(int handle, out RecentTarget target)
        {
            return Recent.TryGetValue(handle, out target);
        }

        public static IEnumerable<RecentTarget> RecentTargets => Recent.Values;

        public static uint LastReadFor(AlprCamera camera)
        {
            return camera != null && CameraLastRead.TryGetValue(camera.Id, out var t) ? t : 0;
        }

        /// <summary>
        ///     Resolves whether the sensor can run right now and, if so, the vehicle it is
        ///     mounted on. Shared by the scanner and the debug overlay so both agree.
        /// </summary>
        public static string EvaluateStatus(AlprConfig cfg, out Vehicle vehicle)
        {
            vehicle = null;
            if (cfg == null || !cfg.Enabled) return "DISABLED";
            if (cfg.Paused) return "PAUSED";
            if (cfg.MaxRange <= 0f) return "NO_CAMERAS";

            var player = Main.LPC;
            if (!player || !player.Exists()) return "NOT_IN_VEHICLE";

            var v = player.CurrentVehicle;
            if (!v || !v.Exists()) return "NOT_IN_VEHICLE";
            vehicle = v;

            if (cfg.RequireEmergencyVehicle && !v.IsPoliceVehicle) return "NOT_EMERGENCY_VEHICLE";
            if (cfg.RequireDriverSeat && v.Driver != player) return "NOT_DRIVER";
            return StatusActive;
        }

        public static void Tick(GameClientSocket client)
        {
            if (AlprState.ToggleRequested)
            {
                AlprState.ToggleRequested = false;
                client.Send("alpr_command", new JObject { ["command"] = "toggle_scanning" });
            }

            var cfg = AlprState.Config;
            var now = Game.GameTime;
            var status = EvaluateStatus(cfg, out var vehicle);

            ReportStatus(client, cfg, status, now);
            Prune(cfg, now);
            if (status != StatusActive) return;

            var own = EntityBasis.Of(vehicle);
            var entities = World.GetEntities(own.Position, cfg.MaxRange + 3f,
                GetEntitiesFlags.ConsiderGroundVehicles | GetEntitiesFlags.ExcludePlayerVehicle);
            if (entities == null || entities.Length == 0) return;

            var reads = new JArray();
            var newRecords = 0;

            foreach (var entity in entities)
            {
                if (reads.Count >= cfg.MaxReadsPerTick) break;

                var target = entity as Vehicle;
                if (!target || !target.Exists() || target == vehicle) continue;

                string plate;
                try
                {
                    plate = NormalizePlate(target.LicensePlate);
                }
                catch
                {
                    continue;
                }

                if (plate == null) continue;
                if (PlateCooldowns.TryGetValue(plate, out var lastRead) && now - lastRead < cfg.PlateCooldownSeconds * 1000u) continue;
                if (!cfg.ReadEmergencyVehicles && target.IsPoliceVehicle) continue;

                var occupied = IsOccupied(target);
                if (!cfg.ReadEmptyVehicles && !occupied) continue;

                var targetPos = target.Position;
                var local = own.ToLocal(targetPos);
                var camera = MatchCamera(cfg, local, out var distance);
                if (camera == null) continue;
                if (HasNoPlate(target)) continue;

                var cameraWorld = own.ToWorld(camera.Offset);
                if (!PlateFacesCamera(cfg, target, targetPos, cameraWorld, out var plateSide)) continue;
                if (cfg.LineOfSight && !HasLineOfSight(cameraWorld, targetPos, vehicle, target)) continue;

                var handle = (int)target.Handle.Value;
                JObject vehiclePayload = null;
                if (!VehicleTrackingAction.IsTracked(handle))
                {
                    // Defer rather than send a read the MDT can't resolve yet. The plate
                    // stays off cooldown, so it's simply picked up on the next tick.
                    if (newRecords >= cfg.MaxNewRecordsPerTick) continue;
                    vehiclePayload = BuildVehiclePayload(target);
                    newRecords++;
                    if (vehiclePayload != null) VehicleTrackingAction.MarkTracked(target);
                }

                reads.Add(BuildRead(plate, handle, target, targetPos, camera, distance, plateSide, occupied, vehiclePayload));

                PlateCooldowns[plate] = now;
                CameraLastRead[camera.Id] = now;
                Recent[handle] = new RecentTarget { Vehicle = target, ReadAt = now, Camera = camera, Plate = plate };
            }

            if (reads.Count == 0) return;

            client.Send("alpr_reads", new JObject
            {
                ["seq"] = ++_seq,
                ["gameTime"] = now,
                ["player"] = new JObject
                {
                    ["x"] = Math.Round(own.Position.X, 2),
                    ["y"] = Math.Round(own.Position.Y, 2),
                    ["heading"] = Math.Round(vehicle.Heading, 1),
                    ["speed"] = Math.Round(vehicle.Speed, 2)
                },
                ["reads"] = reads
            });
        }

        /// <summary>
        ///     Returns the first enabled camera whose cone contains the target, or null.
        ///     All math is in the player vehicle's local space.
        /// </summary>
        private static AlprCamera MatchCamera(AlprConfig cfg, Vector3 local, out float distance)
        {
            distance = 0f;
            var cameras = cfg.Cameras;
            for (var i = 0; i < cameras.Count; i++)
            {
                var cam = cameras[i];
                if (!cam.Enabled) continue;

                var rx = local.X - cam.Offset.X;
                var ry = local.Y - cam.Offset.Y;
                var rz = local.Z - cam.Offset.Z;
                if (Math.Abs(rz) > cfg.MaxHeightDelta) continue;

                var d2 = rx * rx + ry * ry;
                if (d2 < 0.25f || d2 > cam.RangeMeters * cam.RangeMeters) continue;

                var d = (float)Math.Sqrt(d2);
                if ((rx * cam.DirX + ry * cam.DirY) / d < cam.CosHalfFov) continue;

                distance = d;
                return cam;
            }

            return null;
        }

        /// <summary>
        ///     Plates sit on a vehicle's front and rear faces, so the camera must be roughly
        ///     ahead of or behind the target - not beside it. Uses the target's own basis.
        /// </summary>
        private static bool PlateFacesCamera(AlprConfig cfg, Vehicle target, Vector3 targetPos, Vector3 cameraWorld, out string plateSide)
        {
            plateSide = "REAR";
            var d = cameraWorld - targetPos;
            var lx = Vector3.Dot(d, target.RightVector);
            var ly = Vector3.Dot(d, target.ForwardVector);
            var len = (float)Math.Sqrt(lx * lx + ly * ly);
            if (len < 0.01f) return false;

            plateSide = ly >= 0f ? "FRONT" : "REAR";
            if (Math.Abs(ly) / len < cfg.CosPlateAngle) return false;

            // Motorcycles only carry a rear plate, so "reading" a bike's front would
            // report a plate that doesn't exist.
            return plateSide != "FRONT" || !IsRearPlateOnly(target);
        }

        /// <summary>Two-wheelers: rear plate only. Bicycles never reach here (see <see cref="HasNoPlate" />).</summary>
        private static bool IsRearPlateOnly(Vehicle target)
        {
            try
            {
                return target.Model.IsBike;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Bicycles have a plate string in game data but no physical plate to photograph.</summary>
        private static bool HasNoPlate(Vehicle target)
        {
            try
            {
                return target.Model.IsBicycle;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>World-geometry-only trace, so buildings and terrain block reads but traffic doesn't.</summary>
        private static bool HasLineOfSight(Vector3 from, Vector3 targetPos, Vehicle own, Vehicle target)
        {
            try
            {
                var result = World.TraceLine(from, targetPos + new Vector3(0f, 0f, 0.3f), TraceFlags.IntersectWorld, own, target);
                return !result.Hit;
            }
            catch
            {
                // A failed trace must not silently disable ALPR; treat as visible.
                return true;
            }
        }

        private static bool IsOccupied(Vehicle vehicle)
        {
            try
            {
                var driver = vehicle.Driver;
                return driver && driver.Exists();
            }
            catch
            {
                return false;
            }
        }

        private static JObject BuildVehiclePayload(Vehicle vehicle)
        {
            try
            {
                // Passive sensor read: a just-appeared vehicle may be cleaned before its data is built.
                PluginRecordCleaner.Observe(vehicle, true);
                return Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined
                    ? VehicleDataHelper.GenerateVehicleDataPR(vehicle)
                    : VehicleDataHelper.GenerateVehicleData(vehicle);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ALPR] Vehicle data generation failed: {ex.Message}");
                return null;
            }
        }

        private static JObject BuildRead(string plate, int handle, Vehicle target, Vector3 pos, AlprCamera camera,
            float distance, string plateSide, bool occupied, JObject vehiclePayload)
        {
            string street;
            try
            {
                street = World.GetStreetName(pos) ?? string.Empty;
            }
            catch
            {
                street = string.Empty;
            }

            var read = new JObject
            {
                ["plate"] = plate,
                ["entityId"] = handle.ToString(),
                ["cameraId"] = camera.Id,
                ["cameraName"] = camera.Name,
                ["plateSide"] = plateSide,
                ["distance"] = Math.Round(distance, 1),
                ["x"] = Math.Round(pos.X, 2),
                ["y"] = Math.Round(pos.Y, 2),
                ["z"] = Math.Round(pos.Z, 2),
                ["heading"] = Math.Round(target.Heading, 1),
                ["speed"] = Math.Round(target.Speed, 2),
                ["street"] = street,
                ["model"] = target.Model.Name ?? string.Empty,
                ["vehicleClass"] = VehicleClassUtil.GetClassName(target),
                ["occupied"] = occupied
            };
            if (vehiclePayload != null) read["vehicle"] = vehiclePayload;
            return read;
        }

        private static void ReportStatus(GameClientSocket client, AlprConfig cfg, string status, uint now)
        {
            var changed = status != _lastStatus || AlprState.ConfigChanged;
            if (!changed && now - _lastStatusSent < StatusHeartbeatMs) return;

            AlprState.ConfigChanged = false;
            _lastStatus = status;
            _lastStatusSent = now;

            var enabledCams = 0;
            foreach (var cam in cfg.Cameras)
                if (cam.Enabled)
                    enabledCams++;

            client.Send("alpr_status", new JObject
            {
                ["status"] = status,
                ["active"] = status == StatusActive,
                ["cameras"] = enabledCams,
                ["scanIntervalMs"] = cfg.ScanIntervalMs,
                ["debugDraw"] = AlprState.DebugDraw
            });
        }

        private static void Prune(AlprConfig cfg, uint now)
        {
            if (now - _lastPrune < PruneIntervalMs) return;
            _lastPrune = now;

            var cooldownMs = cfg.PlateCooldownSeconds * 1000u;
            PruneScratch.Clear();
            foreach (var kv in PlateCooldowns)
                if (now - kv.Value >= cooldownMs)
                    PruneScratch.Add(kv.Key);
            foreach (var key in PruneScratch) PlateCooldowns.Remove(key);

            PruneHandleScratch.Clear();
            foreach (var kv in Recent)
                if (now - kv.Value.ReadAt >= RecentTargetTtlMs || !kv.Value.Vehicle || !kv.Value.Vehicle.Exists())
                    PruneHandleScratch.Add(kv.Key);
            foreach (var key in PruneHandleScratch) Recent.Remove(key);
        }

        /// <summary>Same normalization as the MDT and <see cref="Bolo.BoloState" />: trimmed + uppercased.</summary>
        public static string NormalizePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return null;
            var trimmed = plate.Trim().ToUpperInvariant();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
