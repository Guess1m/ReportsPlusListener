using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Bolo;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    /// <summary>
    ///     Draws a static map blip at the last-seen location of every active BOLO,
    ///     mirroring the MDT's map circles. Independent of whether the flagged vehicle
    ///     is still loaded, so ambient BOLOs stay visible after their vehicle despawns.
    ///
    ///     <para>
    ///     Pure Rage blips (no CDF), so this runs in every integration mode. Gated by
    ///     the <see cref="BoloState.ShowBlips" /> setting synced from the MDT. Blips are
    ///     keyed by plate and reconciled against the active BOLO set each tick:
    ///     added when a located BOLO appears, removed when it expires/clears or the
    ///     feature is toggled off, and fully cleared on <see cref="CleanupBlips" />.
    ///     </para>
    /// </summary>
    public class BoloBlipAction : IContinuousAction
    {
        private const float BlipScale = 0.8f;

        private readonly Dictionary<string, Blip> _blips = new Dictionary<string, Blip>();

        public void Execute(GameClientSocket client)
        {
            try
            {
                var active = BoloState.Snapshot();
                if (_blips.Count > 0)
                {
                    var stale = _blips.Keys
                        .Where(plate => !BoloState.ShowBlips
                                        || !active.TryGetValue(plate, out var e)
                                        || e == null
                                        || !e.HasLocation)
                        .ToList();
                    foreach (var plate in stale)
                    {
                        SafeDeleteBlip(_blips[plate]);
                        _blips.Remove(plate);
                    }
                }

                if (!BoloState.ShowBlips) return;

                foreach (var kvp in active)
                {
                    var entry = kvp.Value;
                    if (entry == null || !entry.HasLocation) continue;
                    if (_blips.ContainsKey(kvp.Key)) continue;

                    var blip = CreateBlip(entry);
                    if (blip != null) _blips[kvp.Key] = blip;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BOLO] Blip reconcile failed: {ex.Message}");
            }
        }

        private static Blip CreateBlip(BoloEntry entry)
        {
            try
            {
                var blip = new Blip(new Vector3((float)entry.X, (float)entry.Y, 0f))
                {
                    Color = System.Drawing.Color.OrangeRed,
                    Scale = BlipScale,
                    Name = $"BOLO: {entry.Plate}"
                };
                return blip;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BOLO] Could not create blip for [{entry.Plate}]: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Deletes every blip this action created and forgets all tracking. Must be
        ///     called on plugin teardown / off-duty so blips never linger after a reload.
        /// </summary>
        public void CleanupBlips()
        {
            foreach (var blip in _blips.Values)
                SafeDeleteBlip(blip);
            _blips.Clear();
        }

        private static void SafeDeleteBlip(Blip blip)
        {
            if (blip == null) return;
            try
            {
                blip.Delete();
            }
            catch
            {
                // ignore - blip already gone
            }
        }
    }
}
