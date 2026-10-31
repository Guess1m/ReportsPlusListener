using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Gps
{
    /// <summary>
    ///     Mirrors the game's own GPS route (map waypoint, or a route-enabled blip such as a
    ///     callout) to the MDT so it can be drawn on the live map with turn-by-turn guidance.
    ///
    ///     <para>
    ///     The route is not computed here: the game already pathfinds over its full road network
    ///     (one-ways, junction rules, live rerouting). This class only samples that route with
    ///     GET_POS_ALONG_GPS_TYPE_ROUTE, simplifies it, tags it with street names, and sends it
    ///     when the geometry actually changes. Between geometry changes only a small progress
    ///     packet (player position/heading/speed) is sent; the MDT projects it onto the route.
    ///     </para>
    ///
    ///     <para>
    ///     Messages sent:
    ///     <list type="bullet">
    ///         <item><c>gpsRoute</c>: route state + geometry, on change or resync.</item>
    ///         <item><c>gpsProgress</c>: player position each tick while a route target exists.</item>
    ///     </list>
    ///     All state is touched only from game fibers (cooperative, single-threaded), so no locking.
    ///     </para>
    /// </summary>
    public static class GpsRouteTracker
    {
        public const string SourceNone = "none";
        public const string SourceWaypoint = "waypoint";
        public const string SourceBlip = "blip";

        public const string StatusNone = "none";
        public const string StatusCalculating = "calculating";
        public const string StatusActive = "active";
        public const string StatusNoRoute = "noRoute";
        public const string StatusArrived = "arrived";

        /// <summary>Spacing between route samples. Smaller = more accurate corners, more native calls.</summary>
        private const float SampleStepMeters = 12f;

        /// <summary>Hard cap on samples per read (~30 km of route), so a bad native can't loop forever.</summary>
        private const int MaxSamples = 2500;

        private const float SimplifyToleranceMeters = 1.5f;
        private const int MaxStreetLookups = 600;
        private const float ArrivalRadiusMeters = 75f;
        private const float OffRouteMeters = 40f;
        private const float SameRouteToleranceMeters = 12f;
        private const float RouteEndMovedMeters = 20f;
        private const float WaypointMovedMeters = 5f;
        private const float WaypointSlotMatchMeters = 150f;
        private const int CalculatingGraceMs = 4000;
        private const int MinOffRouteResampleMs = 1500;
        private const int BlipSuppressMs = 5000;

        /// <summary>Generous bound on valid world coordinates (the playable map is roughly ±4.5k / -4k..8.5k).</summary>
        public const float WorldLimit = 10000f;

        private static string _source = SourceNone;
        private static string _status = StatusNone;
        private static Vector3? _destination;
        private static List<Vector3> _route;
        private static JArray _streets;
        private static bool _truncated;
        private static int _routeId;
        private static int _blipSlot = -1;
        private static int _waypointSlot = -1;
        private static long _targetSince;
        private static long _lastSampleAt = long.MinValue / 2;
        private static long _blipSuppressUntil;
        private static bool _forceSend = true;
        private static bool _forceResample;

        /// <summary>Set when the MDT asks for something that should be reflected without waiting a full interval.</summary>
        public static bool WakeRequested { get; private set; }

        private static long NowMs => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        /// <summary>
        ///     Runs one GPS update: detects the route target, re-reads the route when due,
        ///     and sends route/progress messages.
        /// </summary>
        public static void Tick(GameClientSocket client)
        {
            WakeRequested = false;
            if (client == null || !client.IsConnected) return;

            var settings = Main.Settings;
            if (settings == null) return;

            var player = Main.LPC;
            if (player == null || !player.Exists()) return;

            var playerPos = player.Position;
            var now = NowMs;

            if (!settings.GpsEnabled)
            {
                if (_source != SourceNone || _forceSend)
                {
                    ResetRoute();
                    SendRoute(client);
                }

                SendPlayerLocation(client, player);
                return;
            }

            DetectTarget(settings.GpsTrackBlipRoutes, now, out var source, out var waypoint);

            if (source == SourceNone)
            {
                HandleNoTarget(client, playerPos);
                // No route: keep the live map's player arrow (position + heading) moving at GPS rate.
                SendPlayerLocation(client, player);
                return;
            }

            var targetChanged = source != _source
                                || source == SourceWaypoint
                                && (!_destination.HasValue || Dist2D(waypoint, _destination.Value) > WaypointMovedMeters);

            if (targetChanged)
            {
                ResetRoute();
                _source = source;
                _destination = source == SourceWaypoint ? waypoint : (Vector3?)null;
                _status = StatusCalculating;
                _targetSince = now;
                _forceResample = true;
                _forceSend = true;
            }

            var resampleInterval = Math.Max(1000, settings.GpsRouteResampleInterval);
            var sinceSample = now - _lastSampleAt;
            var due = _forceResample
                      || _status == StatusCalculating
                      || sinceSample >= resampleInterval
                      || sinceSample >= MinOffRouteResampleMs && _route != null && DistanceToPolyline(playerPos, _route) > OffRouteMeters;

            if (due)
            {
                _forceResample = false;
                _lastSampleAt = now;
                Resample(playerPos, now);
            }

            if (_forceSend) SendRoute(client);
            if (_source != SourceNone) SendProgress(client, player);
        }

        /// <summary>Makes the next tick resend the full route state (MDT reconnected / reloaded).</summary>
        public static void RequestResync()
        {
            _forceSend = true;
            _forceResample = true;
            WakeRequested = true;
        }

        /// <summary>Makes the next tick re-read the route immediately (e.g. the MDT just set a waypoint).</summary>
        public static void RequestImmediateUpdate()
        {
            _forceResample = true;
            WakeRequested = true;
        }

        /// <summary>Forgets all route state; the next tick sends a fresh full state. Safe to call repeatedly.</summary>
        public static void Reset()
        {
            ResetRoute();
            _blipSuppressUntil = 0;
            _lastSampleAt = long.MinValue / 2;
            _forceSend = true;
            _forceResample = false;
            WakeRequested = false;
        }

        /// <summary>True when the coordinate is finite and inside a generous world bound.</summary>
        public static bool IsWithinWorld(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
            return Math.Abs(x) <= WorldLimit && Math.Abs(y) <= WorldLimit;
        }

        private static void ResetRoute()
        {
            _source = SourceNone;
            _status = StatusNone;
            _destination = null;
            _route = null;
            _streets = null;
            _truncated = false;
        }

        /// <summary>
        ///     The target went away: either the player arrived (the game auto-removes the waypoint
        ///     on arrival) or it was cleared. Sends one terminal message, then stays quiet.
        /// </summary>
        private static void HandleNoTarget(GameClientSocket client, Vector3 playerPos)
        {
            if (_source != SourceNone)
            {
                var lastDest = _destination;
                if (!lastDest.HasValue && _route != null && _route.Count > 0) lastDest = _route[_route.Count - 1];

                var arrived = lastDest.HasValue && Dist2D(playerPos, lastDest.Value) <= ArrivalRadiusMeters;

                ResetRoute();
                if (arrived)
                {
                    _status = StatusArrived;
                    _destination = lastDest;
                }

                SendRoute(client);

                // "arrived" is a one-shot event; a later resync must not replay it.
                _status = StatusNone;
                _destination = null;
                return;
            }

            if (_forceSend) SendRoute(client);
        }

        private static void DetectTarget(bool trackBlipRoutes, long now, out string source, out Vector3 waypoint)
        {
            if (GpsNatives.TryGetWaypointCoord(out waypoint))
            {
                source = SourceWaypoint;
                return;
            }

            if (trackBlipRoutes && now >= _blipSuppressUntil)
                foreach (var slot in GpsNatives.RouteSlots)
                {
                    if (slot == _waypointSlot) continue; // known waypoint slot; no waypoint means it's empty
                    if (!GpsNatives.TryGetPointAlongRoute(slot, 0f, out _)) continue;

                    _blipSlot = slot;
                    source = SourceBlip;
                    return;
                }

            source = SourceNone;
        }

        /// <summary>
        ///     Re-reads the route for the current target and decides whether the geometry changed
        ///     enough (reroute, new target) to send it again.
        /// </summary>
        private static void Resample(Vector3 playerPos, long now)
        {
            bool truncated;
            List<Vector3> raw;

            if (_source == SourceWaypoint && _destination.HasValue)
                raw = SampleWaypointRoute(_destination.Value, out truncated);
            else
                raw = SampleRoute(_blipSlot, out truncated);

            if (raw.Count < 2)
            {
                if (_source == SourceBlip)
                {
                    // A probe hit but there's no real route behind it (finished/phantom blip route).
                    // Treat it as no target and back off so we don't flap every tick.
                    var wasVisible = _status != StatusNone;
                    ResetRoute();
                    _blipSuppressUntil = now + BlipSuppressMs;
                    _forceSend = wasVisible;
                    return;
                }

                // The game computes GPS routes asynchronously: give a fresh waypoint a few seconds
                // before declaring it unreachable by road (water, aircraft, off-map, interiors).
                var status = now - _targetSince < CalculatingGraceMs ? StatusCalculating : StatusNoRoute;
                if (status != _status || _route != null)
                {
                    _route = null;
                    _streets = null;
                    _truncated = false;
                    _status = status;
                    _forceSend = true;
                }

                return;
            }

            var simplified = Simplify(raw, SimplifyToleranceMeters);
            if (_source == SourceBlip) _destination = simplified[simplified.Count - 1];

            var changed = _route == null || _status != StatusActive || !IsSameRoute(simplified, _route, playerPos);
            if (!changed) return;

            _route = simplified;
            _truncated = truncated;
            _streets = BuildStreetRuns(simplified);
            _routeId++;
            _status = StatusActive;
            _forceSend = true;
        }

        /// <summary>
        ///     Samples the waypoint route. Which GPS slot holds the waypoint route is discovered once
        ///     by comparing each slot's route end against the waypoint, then cached.
        /// </summary>
        private static List<Vector3> SampleWaypointRoute(Vector3 waypoint, out bool truncated)
        {
            if (_waypointSlot >= 0)
            {
                var cached = SampleRoute(_waypointSlot, out truncated);
                if (cached.Count >= 2) return cached;
            }

            List<Vector3> best = new List<Vector3>();
            var bestTruncated = false;
            var bestSlot = -1;
            var bestEndDist = float.MaxValue;

            foreach (var slot in GpsNatives.RouteSlots)
            {
                if (slot == _waypointSlot) continue;

                var pts = SampleRoute(slot, out var slotTruncated);
                if (pts.Count < 2) continue;

                var endDist = Dist2D(pts[pts.Count - 1], waypoint);
                if (endDist >= bestEndDist) continue;

                best = pts;
                bestTruncated = slotTruncated;
                bestSlot = slot;
                bestEndDist = endDist;
            }

            if (bestSlot >= 0 && bestEndDist <= WaypointSlotMatchMeters && _waypointSlot != bestSlot)
            {
                _waypointSlot = bestSlot;
                Logger.LogInfo($"[GPS] Waypoint route found in GPS slot {bestSlot}.");
            }

            truncated = bestTruncated;
            return best;
        }

        /// <summary>
        ///     Walks the route in <paramref name="slot" /> at a fixed step until the native fails or
        ///     stops advancing (end of route). Yields periodically to avoid frame hitches.
        /// </summary>
        private static List<Vector3> SampleRoute(int slot, out bool truncated)
        {
            var pts = new List<Vector3>(256);
            truncated = false;
            if (slot < 0) return pts;

            var stall = 0;
            for (var i = 0; i < MaxSamples; i++)
            {
                if (!GpsNatives.TryGetPointAlongRoute(slot, i * SampleStepMeters, out var p)) break;

                if (pts.Count > 0 && Dist2D(pts[pts.Count - 1], p) < 0.5f)
                {
                    // Past the end the native clamps to the final point.
                    if (++stall >= 2) break;
                    continue;
                }

                stall = 0;
                pts.Add(p);

                if (i == MaxSamples - 1) truncated = true;
                if (i > 0 && i % 200 == 0) GameFiber.Yield();
            }

            return pts;
        }

        /// <summary>
        ///     Same route if it ends in the same place and every (sub-sampled) point of the new read
        ///     lies on the old geometry. Points near the player are skipped: the route's first leg
        ///     always starts from wherever the player currently is.
        /// </summary>
        private static bool IsSameRoute(List<Vector3> fresh, List<Vector3> old, Vector3 playerPos)
        {
            if (old == null || old.Count < 2 || fresh.Count < 2) return false;
            if (Dist2D(fresh[fresh.Count - 1], old[old.Count - 1]) > RouteEndMovedMeters) return false;

            var stride = Math.Max(1, fresh.Count / 60);
            for (var i = 0; i < fresh.Count; i += stride)
            {
                if (Dist2D(fresh[i], playerPos) < 30f) continue;
                if (DistanceToPolyline(fresh[i], old) > SameRouteToleranceMeters) return false;
            }

            return true;
        }

        /// <summary>
        ///     Street name per route segment, run-length encoded as [{i: segmentIndex, name}].
        ///     Looked up at segment midpoints, which are less ambiguous than junction vertices.
        /// </summary>
        private static JArray BuildStreetRuns(List<Vector3> pts)
        {
            var runs = new JArray();
            var segments = pts.Count - 1;
            if (segments <= 0) return runs;

            var stride = Math.Max(1, (int)Math.Ceiling(segments / (double)MaxStreetLookups));
            string last = null;
            var lookups = 0;

            for (var i = 0; i < segments; i += stride)
            {
                string name;
                try
                {
                    var mid = (pts[i] + pts[i + 1]) * 0.5f;
                    name = (World.GetStreetName(mid) ?? string.Empty).Trim();
                }
                catch
                {
                    name = string.Empty;
                }

                if (name != last)
                {
                    runs.Add(new JObject { ["i"] = i, ["name"] = name });
                    last = name;
                }

                if (++lookups % 100 == 0) GameFiber.Yield();
            }

            return runs;
        }

        private static void SendRoute(GameClientSocket client)
        {
            _forceSend = false;

            var points = new JArray();
            double length = 0;
            if (_route != null)
                for (var i = 0; i < _route.Count; i++)
                {
                    points.Add(new JArray(Math.Round(_route[i].X, 1), Math.Round(_route[i].Y, 1)));
                    if (i > 0) length += Dist2D(_route[i - 1], _route[i]);
                }

            var data = new JObject
            {
                ["routeId"] = _routeId,
                ["source"] = _source,
                ["status"] = _status,
                ["hasDestination"] = _destination.HasValue,
                ["destX"] = _destination.HasValue ? Math.Round(_destination.Value.X, 1) : 0,
                ["destY"] = _destination.HasValue ? Math.Round(_destination.Value.Y, 1) : 0,
                ["points"] = points,
                ["streets"] = _streets ?? new JArray(),
                ["lengthMeters"] = Math.Round(length, 1),
                ["truncated"] = _truncated
            };

            client.Send("gpsRoute", data);
        }

        /// <summary>
        ///     Builds the <c>playerLocation</c> payload: position plus the heading of the player's
        ///     vehicle (or the ped on foot), so the MDT map can draw a direction arrow.
        /// </summary>
        public static JObject BuildPlayerLocation(Ped player)
        {
            var vehicle = player.CurrentVehicle;
            var inVehicle = vehicle != null && vehicle.Exists();
            var pos = player.Position;
            return new JObject
            {
                ["x"] = Math.Round(pos.X, 1),
                ["y"] = Math.Round(pos.Y, 1),
                ["heading"] = Math.Round(inVehicle ? vehicle.Heading : player.Heading, 1)
            };
        }

        private static void SendPlayerLocation(GameClientSocket client, Ped player)
        {
            client.Send("playerLocation", BuildPlayerLocation(player));
        }

        private static void SendProgress(GameClientSocket client, Ped player)
        {
            var vehicle = player.CurrentVehicle;
            var inVehicle = vehicle != null && vehicle.Exists();
            var pos = player.Position;

            client.Send("gpsProgress", new JObject
            {
                ["routeId"] = _routeId,
                ["x"] = Math.Round(pos.X, 1),
                ["y"] = Math.Round(pos.Y, 1),
                ["heading"] = Math.Round(inVehicle ? vehicle.Heading : player.Heading, 1),
                ["speed"] = Math.Round(inVehicle ? vehicle.Speed : player.Speed, 2),
                ["inVehicle"] = inVehicle
            });
        }

        /// <summary>Iterative Douglas-Peucker simplification in 2D.</summary>
        private static List<Vector3> Simplify(List<Vector3> pts, float tolerance)
        {
            if (pts.Count <= 2) return new List<Vector3>(pts);

            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;

            var stack = new Stack<KeyValuePair<int, int>>();
            stack.Push(new KeyValuePair<int, int>(0, pts.Count - 1));

            while (stack.Count > 0)
            {
                var range = stack.Pop();
                int first = range.Key, last = range.Value;
                var maxDist = 0f;
                var index = -1;

                for (var i = first + 1; i < last; i++)
                {
                    var d = DistanceToSegment(pts[i], pts[first], pts[last]);
                    if (d <= maxDist) continue;
                    maxDist = d;
                    index = i;
                }

                if (index < 0 || maxDist <= tolerance) continue;

                keep[index] = true;
                stack.Push(new KeyValuePair<int, int>(first, index));
                stack.Push(new KeyValuePair<int, int>(index, last));
            }

            var result = new List<Vector3>();
            for (var i = 0; i < pts.Count; i++)
                if (keep[i])
                    result.Add(pts[i]);
            return result;
        }

        private static float DistanceToPolyline(Vector3 p, List<Vector3> line)
        {
            if (line == null || line.Count == 0) return float.MaxValue;
            if (line.Count == 1) return Dist2D(p, line[0]);

            var best = float.MaxValue;
            for (var i = 1; i < line.Count; i++)
            {
                var d = DistanceToSegment(p, line[i - 1], line[i]);
                if (d < best) best = d;
            }

            return best;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            float vx = b.X - a.X, vy = b.Y - a.Y;
            float wx = p.X - a.X, wy = p.Y - a.Y;
            var len2 = vx * vx + vy * vy;
            var t = len2 <= 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, (vx * wx + vy * wy) / len2));
            float dx = wx - t * vx, dy = wy - t * vy;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Dist2D(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
