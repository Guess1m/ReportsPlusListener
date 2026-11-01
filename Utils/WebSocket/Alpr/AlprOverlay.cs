using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Alpr
{
    /// <summary>
    ///     Everything ALPR draws or shows in-game, driven from one per-frame fiber:
    ///     <list type="bullet">
    ///         <item>Debug scan zones: each camera's cone outline, so players can see exactly where it reads.</item>
    ///         <item>Read rays: a short-lived line from the camera to each vehicle it just read.</item>
    ///         <item>Hit highlights and blips for vehicles the MDT flagged (<c>alpr_alert</c>).</item>
    ///
    ///     </list>
    ///     When nothing is enabled the frame cost is a couple of field reads.
    /// </summary>
    public static class AlprOverlay
    {
        private const int ConeArcSegments = 10;
        private const int ReadRayMs = 1500;
        private const int CameraFlashMs = 300;

        private static readonly List<Highlight> Highlights = new List<Highlight>();
        private static readonly List<HitBlip> Blips = new List<HitBlip>();
        private static uint _lastHousekeeping;

        private sealed class Highlight
        {
            public Vehicle Vehicle;
            public uint Until;
            public Color Color;
        }

        private sealed class HitBlip
        {
            public Blip Blip;
            public uint Until;
        }

        /// <summary>Called every frame from the ALPR frame fiber.</summary>
        public static void Frame()
        {
            PollToggleKey();
            var notice = AlprState.PendingNotice;
            if (notice != null)
            {
                AlprState.PendingNotice = null;
                Game.DisplayNotification(notice);
            }

            var now = Game.GameTime;
            if (now - _lastHousekeeping >= 1000)
            {
                _lastHousekeeping = now;
                ExpireBlips(now);
            }

            var cfg = AlprState.Config;
            if (AlprState.DebugDraw) DrawScanZones(cfg, now);
            if (Highlights.Count > 0) DrawHighlights(now);
        }

        private static void DrawScanZones(AlprConfig cfg, uint now)
        {
            var status = AlprScanner.EvaluateStatus(cfg, out var vehicle);
            if (vehicle == null) return;

            var active = status == AlprScanner.StatusActive;
            var basis = EntityBasis.Of(vehicle);

            foreach (var cam in cfg.Cameras)
            {
                if (!cam.Enabled) continue;

                var flashing = active && now - AlprScanner.LastReadFor(cam) < CameraFlashMs;
                var color = !active ? Color.FromArgb(255, 110, 110, 110) : flashing ? Color.White : cam.Color;

                var origin = basis.ToWorld(cam.Offset);
                var half = cam.FovDegrees / 2f;
                var previous = origin;

                for (var i = 0; i <= ConeArcSegments; i++)
                {
                    var angle = MathHelper.ConvertDegreesToRadians(cam.YawDegrees - half + cam.FovDegrees * i / ConeArcSegments);
                    var local = cam.Offset + new Vector3((float)Math.Sin(angle), (float)Math.Cos(angle), 0f) * cam.RangeMeters;
                    var point = basis.ToWorld(local);

                    if (i == 0 || i == ConeArcSegments) Debug.DrawLine(origin, point, color);
                    if (i > 0) Debug.DrawLine(previous, point, color);
                    previous = point;
                }

                // Centre line, so the camera's aim is obvious at a glance.
                var centre = cam.Offset + new Vector3(cam.DirX, cam.DirY, 0f) * cam.RangeMeters;
                Debug.DrawLine(origin, basis.ToWorld(centre), Color.FromArgb(120, color));
            }

            if (!active) return;

            foreach (var target in AlprScanner.RecentTargets)
            {
                if (now - target.ReadAt > ReadRayMs || !target.Vehicle || !target.Vehicle.Exists()) continue;
                Debug.DrawLine(basis.ToWorld(target.Camera.Offset), target.Vehicle.Position, Color.Yellow);
            }
        }

        private static void DrawHighlights(uint now)
        {
            var player = Main.LPC;
            var from = player && player.Exists() ? player.Position + new Vector3(0f, 0f, 1.2f) : Vector3.Zero;

            for (var i = Highlights.Count - 1; i >= 0; i--)
            {
                var h = Highlights[i];
                if (now >= h.Until || !h.Vehicle || !h.Vehicle.Exists())
                {
                    Highlights.RemoveAt(i);
                    continue;
                }

                var top = h.Vehicle.Position + new Vector3(0f, 0f, 2.2f);
                Debug.DrawLine(top + new Vector3(-0.6f, 0f, 0f), top + new Vector3(0.6f, 0f, 0f), h.Color);
                Debug.DrawLine(top + new Vector3(0f, -0.6f, 0f), top + new Vector3(0f, 0.6f, 0f), h.Color);
                Debug.DrawLine(top, h.Vehicle.Position, h.Color);
                if (from != Vector3.Zero) Debug.DrawLine(from, top, Color.FromArgb(140, h.Color));
            }
        }

        /// <summary>
        ///     Applies an <c>alpr_alert</c> from the MDT. The notification text is composed
        ///     MDT-side from the same evaluation the dashboard shows, so the in-game alert can
        ///     never disagree with the MDT.
        /// </summary>
        public static void HandleAlert(JToken data)
        {
            try
            {
                if (!(data is JObject o)) return;

                var title = o["title"]?.ToString() ?? "ALPR HIT";
                var subtitle = o["subtitle"]?.ToString() ?? string.Empty;
                var text = o["text"]?.ToString() ?? string.Empty;
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", title, subtitle, text);

                if (o["sound"]?.Value<bool?>() ?? false)
                    NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1, "Beep_Red", "DLC_HEIST_HACKING_SNAKE_SOUNDS", true);

                var cfg = AlprState.Config;
                if (!int.TryParse(o["entityId"]?.ToString(), out var handle)) return;
                if (!AlprScanner.TryGetRecent(handle, out var target) || !target.Vehicle || !target.Vehicle.Exists()) return;

                var color = ParseColor(o["color"]?.ToString(), Color.Red);
                var now = Game.GameTime;

                if (cfg.HighlightHits && (o["highlight"]?.Value<bool?>() ?? true))
                    Highlights.Add(new Highlight { Vehicle = target.Vehicle, Until = now + 15000u, Color = color });

                if (cfg.BlipHits && (o["blip"]?.Value<bool?>() ?? true)) AddBlip(target.Vehicle, color, now + (uint)cfg.BlipSeconds * 1000u);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ALPR] Failed to handle alpr_alert: {ex.Message}");
            }
        }

        private static void AddBlip(Vehicle vehicle, Color color, uint until)
        {
            foreach (var existing in Blips)
                if (existing.Blip && existing.Blip.Exists() && existing.Blip.Entity == vehicle)
                {
                    existing.Until = until;
                    return;
                }

            var blip = new Blip(vehicle) { Color = color, Scale = 0.9f };
            Blips.Add(new HitBlip { Blip = blip, Until = until });
        }

        private static void ExpireBlips(uint now)
        {
            for (var i = Blips.Count - 1; i >= 0; i--)
            {
                var b = Blips[i];
                if (now < b.Until && b.Blip && b.Blip.Exists()) continue;
                if (b.Blip && b.Blip.Exists()) b.Blip.Delete();
                Blips.RemoveAt(i);
            }
        }

        /// <summary>
        ///     The one in-game ALPR control. It only asks the MDT to toggle; the MDT flips its
        ///     pause state and pushes a config, whose arrival shows the PAUSED/RESUMED notice.
        /// </summary>
        private static void PollToggleKey()
        {
            var settings = Main.Settings;
            if (settings == null || Main.IsKeyboardOpen) return;
            var key = settings.AlprToggleKey;
            if (key.Key == System.Windows.Forms.Keys.None || !key.IsPressed()) return;

            if (!Main.IsConnected) Game.DisplayNotification("ALPR: ~r~MDT not connected");
            else if (!AlprState.Config.Enabled) Game.DisplayNotification("ALPR is ~r~disabled~w~ in the MDT settings");
            else AlprState.ToggleRequested = true;

            while (key.IsPressed()) GameFiber.Yield();
        }

        /// <summary>Removes every blip and highlight this overlay created. Safe to call repeatedly.</summary>
        public static void Cleanup()
        {
            foreach (var b in Blips)
                try
                {
                    if (b.Blip && b.Blip.Exists()) b.Blip.Delete();
                }
                catch
                {
                    // Blip already invalid - nothing to release.
                }

            Blips.Clear();
            Highlights.Clear();
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            var s = hex.Trim().TrimStart('#');
            if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return fallback;
        }
    }
}
