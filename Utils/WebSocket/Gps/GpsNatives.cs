using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Gps
{
    /// <summary>
    ///     Thin, exception-safe wrappers around the HUD/GPS natives used for route mirroring.
    ///     <para>
    ///     Natives are invoked by hash (the <c>x&lt;hash&gt;</c> form) so they don't depend on
    ///     RPH's native-name table. Every wrapper swallows and rate-limits failures: a missing
    ///     or misbehaving native must degrade the GPS feature, never the plugin.
    ///     </para>
    /// </summary>
    internal static class GpsNatives
    {
        /// <summary>Radar sprite id of the player's map waypoint blip.</summary>
        private const int WaypointBlipSprite = 8;

        /// <summary>
        ///     GPS route slots probed by <see cref="TryGetPointAlongRoute" />. The game keeps a
        ///     separate route per slot (waypoint, route-enabled blip, script/discrete route).
        /// </summary>
        public static readonly int[] RouteSlots = { 0, 1, 2 };

        private static readonly Dictionary<string, int> FailureCounts = new Dictionary<string, int>();

        /// <summary>IS_WAYPOINT_ACTIVE</summary>
        public static bool IsWaypointActive()
        {
            try
            {
                return NativeFunction.Natives.x1DD1F58F493F1DA5<bool>();
            }
            catch (Exception ex)
            {
                LogFailure("IS_WAYPOINT_ACTIVE", ex);
                return false;
            }
        }

        /// <summary>
        ///     Resolves the world position of the active waypoint blip
        ///     (GET_FIRST_BLIP_INFO_ID + DOES_BLIP_EXIST + GET_BLIP_INFO_ID_COORD).
        /// </summary>
        public static bool TryGetWaypointCoord(out Vector3 coord)
        {
            coord = Vector3.Zero;
            try
            {
                if (!IsWaypointActive()) return false;

                int blip = NativeFunction.Natives.x1BEDE233E6CD2A1F<int>(WaypointBlipSprite);
                if (blip == 0) return false;
                if (!NativeFunction.Natives.xA6DB27D19ECBB7DA<bool>(blip)) return false;

                Vector3 pos = NativeFunction.Natives.xFA7C7F0AADF25D09<Vector3>(blip);
                if (!IsUsable(pos)) return false;

                coord = pos;
                return true;
            }
            catch (Exception ex)
            {
                LogFailure("GET_BLIP_INFO_ID_COORD", ex);
                return false;
            }
        }

        /// <summary>
        ///     GET_POS_ALONG_GPS_TYPE_ROUTE: the point <paramref name="distance" /> meters along the
        ///     GPS route held in <paramref name="slot" />, measured from the player's position.
        /// </summary>
        public static bool TryGetPointAlongRoute(int slot, float distance, out Vector3 point)
        {
            point = Vector3.Zero;
            if (RouteNativeDisabled || slot < 0 || slot > 2) return false;
            try
            {
                Vector3 p;
                // 0xF3162836C28F9DA5: BOOL GET_POS_ALONG_GPS_TYPE_ROUTE(Vector3* result, BOOL p1, float p2, int p3), p3 in 0..2
                bool ok = NativeFunction.Natives.xF3162836C28F9DA5<bool>(out p, true, distance, slot);
                if (!ok || !IsUsable(p)) return false;

                point = p;
                return true;
            }
            catch (Exception ex)
            {
                // Never retry a native that threw: an access violation means game memory was
                // misused, and hammering it every tick risks a crash. GPS mirroring turns off
                // for this session instead.
                RouteNativeDisabled = true;
                LogFailure("GET_POS_ALONG_GPS_TYPE_ROUTE", ex);
                Logger.LogError("[GPS] Route reading disabled for this session after a native failure.");
                return false;
            }
        }

        /// <summary>True once the route native has failed; route reading stays off until the plugin reloads.</summary>
        public static bool RouteNativeDisabled { get; private set; }

        /// <summary>SET_NEW_WAYPOINT</summary>
        public static bool SetWaypoint(float x, float y)
        {
            try
            {
                NativeFunction.Natives.xFE43368D2AA4F2FC(x, y);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure("SET_NEW_WAYPOINT", ex);
                return false;
            }
        }

        /// <summary>SET_WAYPOINT_OFF</summary>
        public static bool ClearWaypoint()
        {
            try
            {
                NativeFunction.Natives.xA7E4E2D361C2627F();
                return true;
            }
            catch (Exception ex)
            {
                LogFailure("SET_WAYPOINT_OFF", ex);
                return false;
            }
        }

        /// <summary>
        ///     Rejects NaN/infinite vectors and the (0,0) origin, which these natives return
        ///     instead of failing when a route or blip is not available.
        /// </summary>
        public static bool IsUsable(Vector3 v)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.X) || float.IsInfinity(v.Y)) return false;
            return !(Math.Abs(v.X) < 0.01f && Math.Abs(v.Y) < 0.01f);
        }

        /// <summary>Logs the first few failures per native, then goes quiet to avoid log spam at 1 Hz.</summary>
        private static void LogFailure(string native, Exception ex)
        {
            FailureCounts.TryGetValue(native, out var count);
            FailureCounts[native] = count + 1;
            if (count < 3) Logger.LogWarning($"[GPS] Native {native} failed: {ex.Message}");
            else if (count == 3) Logger.LogWarning($"[GPS] Native {native} keeps failing; suppressing further logs.");
        }
    }
}
