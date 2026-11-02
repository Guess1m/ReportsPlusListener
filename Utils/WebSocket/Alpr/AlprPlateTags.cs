using System;
using System.Collections.Generic;
using System.Drawing;
using Rage;
using Rage.Native;

namespace ReportsPlus.Utils.WebSocket.Alpr
{
    /// <summary>
    ///     In-world ALPR plate tags: a small panel anchored next to the plate that faces the
    ///     officer, with a leader line down to it:
    ///     <code>
    ///         ┌──────────────┐  accent bar (camera color, or the hit's priority color)
    ///         │ [ 12ABC345 ] │  plate chip
    ///         │ 24 m | 38 mph│  distance from the officer | target speed
    ///         │ STOLEN VEH.. │  alert label (hits only)
    ///         └──────┬───────┘
    ///                • plate
    ///     </code>
    ///     <list type="bullet">
    ///         <item>Hit vehicles (from <c>alpr_alert</c>, "Highlight hit vehicles") get a tag for the
    ///             highlight duration, scan zones or not. Critical hits pulse.</item>
    ///         <item>Every other read gets a tag for <see cref="ReadTagMs" />, only while scan zones are
    ///             shown and the department enabled plate tags.</item>
    ///     </list>
    ///     Drawn with the same native text/rect calls as the status overlay, from the per-frame ALPR
    ///     fiber. Capped at <see cref="MaxTags" /> nearest vehicles, hidden in the pause menu or with
    ///     the HUD off, and allocation-free per frame. Units follow the game's metric setting.
    /// </summary>
    internal static class AlprPlateTags
    {
        private const uint ReadTagMs = 5000;
        private const uint FadeMs = 800;
        private const float MaxDistance = 90f;
        private const int MaxTags = 8;
        private const float BaseAspect = 16f / 9f;

        private const int FontChalet = 0;
        private const int FontCondensed = 4;

        private static readonly Color Panel = Color.FromArgb(14, 17, 23);
        private static readonly Color PlateChip = Color.FromArgb(236, 239, 244);
        private static readonly Color PlateInk = Color.FromArgb(22, 36, 78);
        private static readonly Color Meta = Color.FromArgb(205, 210, 220);

        private static readonly List<Tag> Candidates = new List<Tag>();
        private static readonly HashSet<uint> Added = new HashSet<uint>();
        private static readonly Dictionary<uint, PlateGeometry> Geometry = new Dictionary<uint, PlateGeometry>();
        private static readonly Comparison<Tag> ByDistance = (a, b) => a.Distance.CompareTo(b.Distance);

        private struct Tag
        {
            public Vehicle Vehicle;
            public string Plate;
            public Color Accent;
            public string Label;
            public bool Pulse;
            public float Alpha;
            public float Distance;
        }

        /// <summary>Where the plates sit on a model, in vehicle-local space.</summary>
        private struct PlateGeometry
        {
            public float FrontY;
            public float RearY;
            public float Z;

            /// <summary>Motorcycles: only a rear plate, so the tag never jumps to the front.</summary>
            public bool RearOnly;
        }

        public static void Draw(AlprConfig cfg, bool scanZones, uint now, List<AlprOverlay.Highlight> highlights)
        {
            var readTags = scanZones && cfg.PlateTags;
            if (highlights.Count == 0 && !readTags) return;
            if (NativeFunction.CallByHash<bool>(0xB0034A223497FFCB) // IS_PAUSE_MENU_ACTIVE
                || NativeFunction.CallByHash<bool>(0xA86478C6958735C5)) // IS_HUD_HIDDEN
                return;

            var player = Main.LPC;
            if (!player || !player.Exists()) return;
            var own = player.CurrentVehicle;
            var origin = own && own.Exists() ? own.Position : player.Position;

            Candidates.Clear();
            Added.Clear();

            // Hits first so they win over a plain read tag for the same vehicle.
            foreach (var h in highlights)
                Add(h.Vehicle, h.Plate, h.Color, h.Label, h.Pulse, Fade(h.Until - now), origin);

            if (readTags)
                foreach (var t in AlprScanner.RecentTargets)
                {
                    var age = now - t.ReadAt;
                    if (age >= ReadTagMs || t.Camera == null) continue;
                    Add(t.Vehicle, t.Plate, t.Camera.Color, null, false, Fade(ReadTagMs - age), origin);
                }

            if (Candidates.Count == 0) return;
            Candidates.Sort(ByDistance);

            var metric = NativeFunction.CallByHash<bool>(0xD3D15555431AB793); // SHOULD_USE_METRIC_MEASUREMENTS
            var aspect = NativeFunction.CallByHash<float>(0xF1307EF624A80D87, false); // GET_ASPECT_RATIO
            var widthFix = aspect > 0.1f ? BaseAspect / aspect : 1f;

            // Farthest first so nearer tags draw on top.
            for (var i = Math.Min(Candidates.Count, MaxTags) - 1; i >= 0; i--)
                DrawTag(Candidates[i], player.Position, now, metric, widthFix);
        }

        private static void Add(Vehicle vehicle, string plate, Color accent, string label, bool pulse, float alpha, Vector3 origin)
        {
            if (alpha <= 0f || !vehicle || !vehicle.Exists()) return;
            if (!Added.Add(vehicle.Handle.Value)) return;
            var distance = Vector3.Distance(origin, vehicle.Position);
            if (distance > MaxDistance) return;
            Candidates.Add(new Tag
            {
                Vehicle = vehicle, Plate = string.IsNullOrWhiteSpace(plate) ? "--------" : plate, Accent = accent,
                Label = label, Pulse = pulse, Alpha = alpha, Distance = distance
            });
        }

        private static float Fade(uint remainingMs)
        {
            if (remainingMs > int.MaxValue) return 0f; // wrapped: already expired
            return remainingMs >= FadeMs ? 1f : remainingMs / (float)FadeMs;
        }

        private static void DrawTag(Tag tag, Vector3 eye, uint now, bool metric, float k)
        {
            var anchor = PlatePosition(tag.Vehicle, eye);
            if (!NativeFunction.Natives.GET_SCREEN_COORD_FROM_WORLD_COORD<bool>(anchor.X, anchor.Y, anchor.Z, out float sx, out float sy))
                return;

            var s = MathHelper.Clamp(1.12f - tag.Distance / 110f, 0.72f, 1.05f);
            var a = tag.Alpha;

            var plateScale = 0.40f * s;
            var metaScale = 0.30f * s;
            var plateTextH = CharHeight(plateScale, FontChalet);
            var metaH = CharHeight(metaScale, FontCondensed);

            var padX = 0.0045f * s * k;
            var padY = 0.0045f * s;
            var gap = 0.003f * s;
            var accentH = 0.0022f * s;
            var width = 0.084f * s * k;
            var chipH = plateTextH + 0.005f * s;
            var hasLabel = !string.IsNullOrEmpty(tag.Label);

            var height = accentH + padY + chipH + gap + metaH + padY;
            if (hasLabel) height += metaH + gap;

            var lift = 0.024f * s;
            var bottom = sy - lift;
            var top = bottom - height;

            var accentAlpha = tag.Pulse ? 0.55f + 0.45f * (float)Math.Sin(now / 140.0) : 1f;
            var accent = Alpha(255 * a * accentAlpha);

            // Leader line and plate dot.
            Rect(sx, sy - lift / 2f, 0.0008f * k, lift, tag.Accent, accent);
            Rect(sx, sy, 0.0034f * k, 0.006f * s, tag.Accent, accent);

            // Panel + accent bar.
            Rect(sx, top + height / 2f, width, height, Panel, Alpha(205 * a));
            Rect(sx, top + accentH / 2f, width, accentH, tag.Accent, accent);

            // Plate chip.
            var y = top + accentH + padY;
            Rect(sx, y + chipH / 2f, width - padX * 2f, chipH, PlateChip, Alpha(245 * a));
            Text(tag.Plate, sx, y + (chipH - plateTextH) / 2f - 0.002f * s, plateScale, FontChalet, PlateInk, Alpha(255 * a));
            y += chipH + gap;

            // Distance | speed.
            var speed = tag.Vehicle.Speed;
            var meta = metric
                ? $"{tag.Distance:0} m  |  {speed * 3.6f:0} km/h"
                : $"{tag.Distance * 3.28084f:0} ft  |  {speed * 2.236936f:0} mph";
            Text(meta, sx, y - 0.002f * s, metaScale, FontCondensed, Meta, Alpha(255 * a));

            if (!hasLabel) return;
            y += metaH + gap;
            Text(tag.Label.ToUpperInvariant(), sx, y - 0.002f * s, metaScale, FontCondensed, tag.Accent, Alpha(255 * a));
        }

        /// <summary>World position of the plate on the end of the vehicle facing <paramref name="eye" />.</summary>
        private static Vector3 PlatePosition(Vehicle vehicle, Vector3 eye)
        {
            var model = vehicle.Model;
            if (!Geometry.TryGetValue(model.Hash, out var g))
            {
                model.GetDimensions(out var min, out var max);
                bool rearOnly;
                try
                {
                    rearOnly = model.IsBike;
                }
                catch
                {
                    rearOnly = false;
                }

                g = new PlateGeometry { FrontY = max.Y, RearY = min.Y, Z = min.Z + 0.45f, RearOnly = rearOnly };
                Geometry[model.Hash] = g;
            }

            var facingFront = !g.RearOnly && Vector3.Dot(eye - vehicle.Position, vehicle.ForwardVector) >= 0f;
            return vehicle.GetOffsetPosition(new Vector3(0f, facingFront ? g.FrontY : g.RearY, g.Z));
        }

        private static float CharHeight(float scale, int font)
        {
            return NativeFunction.CallByHash<float>(0xDB88A37483346780, scale, font); // GET_RENDERED_CHARACTER_HEIGHT
        }

        private static int Alpha(float value)
        {
            return value <= 0f ? 0 : value >= 255f ? 255 : (int)value;
        }

        private static void Rect(float cx, float cy, float w, float h, Color c, int alpha)
        {
            NativeFunction.CallByHash<int>(0x3A618A217E5154F0, cx, cy, w, h, (int)c.R, (int)c.G, (int)c.B, alpha, false); // DRAW_RECT
        }

        private static void Text(string text, float x, float y, float scale, int font, Color c, int alpha)
        {
            NativeFunction.CallByHash<int>(0x66E0276CC5F6B9DA, font); // SET_TEXT_FONT
            NativeFunction.CallByHash<int>(0x07C837F9A01C34C9, 0.0f, scale); // SET_TEXT_SCALE
            NativeFunction.CallByHash<int>(0xBE6B23FFA53FB442, (int)c.R, (int)c.G, (int)c.B, alpha); // SET_TEXT_COLOUR
            NativeFunction.CallByHash<int>(0xC02F4DBFB51D988B, true); // SET_TEXT_CENTRE
            NativeFunction.CallByHash<int>(0x25FBB336DF1804CB, "STRING"); // BEGIN_TEXT_COMMAND_DISPLAY_TEXT
            NativeFunction.CallByHash<int>(0x6C188BE134E074AA, text); // ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            NativeFunction.CallByHash<int>(0xCD015E5BB0D96A57, x, y, 0); // END_TEXT_COMMAND_DISPLAY_TEXT
        }
    }
}
