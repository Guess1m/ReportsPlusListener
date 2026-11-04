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
    ///         <item>In-world plate tags (<see cref="AlprPlateTags" />).</item>
    ///     </list>
    ///     Every debug <i>line</i> (zones, read rays, hit guide lines) is part of the scan-zone view
    ///     and only drawn while it is on. Hit vehicles still get a plate tag and blip without it.
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

        /// <summary>A vehicle the MDT flagged, shown with a plate tag (and guide lines in scan-zone view).</summary>
        internal sealed class Highlight
        {
            public Color Color;
            public string Label;
            public string Plate;
            public bool Pulse;
            public uint Until;
            public Vehicle Vehicle;
        }

        private sealed class HitBlip
        {
            public Blip Blip;
            public bool Routed;
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
            var debug = AlprState.DebugDraw;
            if (debug) DrawScanZones(cfg, now);
            if (Highlights.Count > 0) UpdateHighlights(now, debug);
            AlprPlateTags.Draw(cfg, debug, now, Highlights);
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

        /// <summary>Expires highlights; draws their guide lines only in scan-zone view.</summary>
        private static void UpdateHighlights(uint now, bool drawLines)
        {
            var player = Main.LPC;
            var from = drawLines && player && player.Exists() ? player.Position + new Vector3(0f, 0f, 1.2f) : Vector3.Zero;

            for (var i = Highlights.Count - 1; i >= 0; i--)
            {
                var h = Highlights[i];
                if (now >= h.Until || !h.Vehicle || !h.Vehicle.Exists())
                {
                    Highlights.RemoveAt(i);
                    continue;
                }

                if (!drawLines) continue;
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

                // "notify" is absent from older MDT builds, which only sent alerts they wanted shown.
                if (o["notify"]?.Value<bool?>() ?? true)
                {
                    var title = o["title"]?.ToString() ?? "ALPR HIT";
                    var subtitle = o["subtitle"]?.ToString() ?? string.Empty;
                    var text = o["text"]?.ToString() ?? string.Empty;
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", title, subtitle, text);
                }

                if (o["sound"]?.Value<bool?>() ?? false)
                    NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1, "Beep_Red", "DLC_HEIST_HACKING_SNAKE_SOUNDS", true);

                var cfg = AlprState.Config;
                if (!int.TryParse(o["entityId"]?.ToString(), out var handle)) return;
                if (!AlprScanner.TryGetRecent(handle, out var target) || !target.Vehicle || !target.Vehicle.Exists()) return;

                var color = ParseColor(o["color"]?.ToString(), Color.Red);
                var now = Game.GameTime;

                if (cfg.HighlightHits && (o["highlight"]?.Value<bool?>() ?? true))
                {
                    var label = o["label"]?.ToString();
                    if (string.IsNullOrWhiteSpace(label)) label = "ALPR HIT";
                    var plate = o["plate"]?.ToString();
                    if (string.IsNullOrWhiteSpace(plate)) plate = target.Plate;
                    var pulse = string.Equals(o["priority"]?.ToString(), "CRITICAL", StringComparison.OrdinalIgnoreCase);

                    // One highlight per vehicle: a repeat or escalated alert refreshes it.
                    var existing = Highlights.Find(x => x.Vehicle == target.Vehicle);
                    if (existing == null)
                    {
                        existing = new Highlight { Vehicle = target.Vehicle };
                        Highlights.Add(existing);
                    }

                    existing.Until = now + 15000u;
                    existing.Color = color;
                    existing.Label = label;
                    existing.Plate = plate;
                    existing.Pulse = pulse;
                }

                if (cfg.BlipHits && (o["blip"]?.Value<bool?>() ?? true))
                {
                    var plateText = o["plate"]?.ToString();
                    if (string.IsNullOrWhiteSpace(plateText)) plateText = target.Plate;
                    AddBlip(cfg, target.Vehicle, color, plateText,
                        o["blipRoute"]?.Value<bool?>() ?? false,
                        o["blipFlash"]?.Value<bool?>() ?? false,
                        now + (uint)cfg.BlipSeconds * 1000u);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ALPR] Failed to handle alpr_alert: {ex.Message}");
            }
        }

        private static void AddBlip(AlprConfig cfg, Vehicle vehicle, Color color, string plate, bool route, bool flash, uint until)
        {
            HitBlip entry = null;
            foreach (var existing in Blips)
                if (existing.Blip && existing.Blip.Exists() && existing.Blip.Entity == vehicle)
                {
                    // A repeat or escalated hit refreshes the blip and upgrades its styling.
                    existing.Until = until;
                    entry = existing;
                    break;
                }

            if (entry == null)
            {
                entry = new HitBlip { Blip = new Blip(vehicle), Until = until };
                Blips.Add(entry);
            }

            var blip = entry.Blip;

            blip.Color = color;
            blip.Scale = cfg.BlipScale;
            if (cfg.BlipShowPlate && !string.IsNullOrWhiteSpace(plate)) blip.Name = "ALPR: " + plate;
            if (route && !entry.Routed)
            {
                blip.RouteColor = color;
                blip.IsRouteEnabled = true;
                entry.Routed = true;
            }

            if (flash) NativeFunction.Natives.SET_BLIP_FLASHES((uint)blip.Handle, true);
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
