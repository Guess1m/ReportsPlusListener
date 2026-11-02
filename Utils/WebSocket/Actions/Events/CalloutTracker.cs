using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils.WebSocket.Actions.Events
{
    /// <summary>
    ///     Sends every LSPDFR callout to the MDT, with or without Callout Interface (CI).
    ///     <para>
    ///         <b>How it works:</b> LSPDFR itself raises <see cref="LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed" />
    ///         for every callout from every pack, CI or not. The event only carries an <see cref="LHandle" />, and LSPDFR's
    ///         public API has no "callout from handle" call. But <c>LHandle</c> wraps the live <see cref="Callout" />
    ///         instance in an <c>internal Object</c> property, and that is what CI's
    ///         <c>GetCalloutFromHandle</c> unwraps. We unwrap it ourselves with reflection, so CI is optional.
    ///     </para>
    ///     <para>
    ///         Resolution order (each step only runs if the one before failed):
    ///         <list type="number">
    ///             <item>LHandle reflection (<see cref="ResolveCallout" />), with no dependency at all.</item>
    ///             <item>CI's <c>GetCalloutFromHandle</c>, only when CI is installed (<see cref="CalloutInterfaceBridge" />).</item>
    ///             <item>Name only, from <c>Functions.GetCalloutName</c>. The MDT still gets a callout, just without a location.</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         Priority and description come from the callout's own <c>[CalloutInterface(...)]</c> attribute. Callout packs
    ///         ship <c>CalloutInterfaceAPI.dll</c> next to themselves, so the attribute exists even when CI isn't installed.
    ///         It is read by type name, so this class never needs <c>CalloutInterfaceAPI</c> to load.
    ///         Postal: CI if installed, else CommonDataFramework if installed, else blank.
    ///     </para>
    ///     <para>
    ///         Every step is guarded. This handler runs on LSPDFR's callout fiber, and an exception there could
    ///         break the callout flow for the player.
    ///     </para>
    /// </summary>
    public static class CalloutTracker
    {
        private const string CiAttributeTypeName = "CalloutInterfaceAPI.CalloutInterfaceAttribute";
        private const int MinIdentifier = 10000;
        private const int MaxIdentifierExclusive = 100000;
        private const int RecentIdentifierLimit = 256;

        private static readonly Random Rng = new Random();
        private static readonly Queue<int> RecentIdentifiers = new Queue<int>();
        private static readonly HashSet<int> RecentIdentifierSet = new HashSet<int>();

        /// <summary>GTA text formatting tokens such as <c>~r~</c>, <c>~n~</c>, <c>~h~</c>, <c>~BLIP_INFO_ICON~</c>.</summary>
        private static readonly Regex FormattingToken = new Regex(@"~[A-Za-z0-9_]{1,32}~", RegexOptions.Compiled);

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private static bool _subscribed;
        private static bool _ciInstalled;
        private static bool _cdfInstalled;

        /// <summary>
        ///     Last handle sent, so a duplicate event for the same callout doesn't create a second MDT entry. Time-bound,
        ///     so even if LSPDFR ever reused handle objects, a later real callout is never swallowed.
        /// </summary>
        private static WeakReference _lastHandle;

        private static uint _lastHandleAt;
        private const uint DuplicateWindowMs = 5000;

        // Cached LHandle.Object accessor (internal, so reflection). null + _handleAccessorResolved = not found.
        private static PropertyInfo _handleObjectProperty;
        private static FieldInfo _handleObjectField;
        private static bool _handleAccessorResolved;

        /// <summary>
        ///     Subscribes to LSPDFR's callout event. Idempotent: safe to call on every on-duty toggle.
        /// </summary>
        public static void Establish(bool ciInstalled, bool cdfInstalled)
        {
            _ciInstalled = ciInstalled;
            _cdfInstalled = cdfInstalled;
            if (_subscribed) return;

            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed += OnCalloutDisplayed;
            _subscribed = true;
            Logger.LogInfo($"[Callouts] Tracking LSPDFR callouts (CalloutInterface: {(ciInstalled ? "installed" : "not installed")}, " +
                           $"postal source: {(ciInstalled ? "CalloutInterface" : cdfInstalled ? "CommonDataFramework" : "none")}).");
        }

        public static void Cleanup()
        {
            if (!_subscribed) return;
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed -= OnCalloutDisplayed;
            _subscribed = false;
            _lastHandle = null;
        }

        private static void OnCalloutDisplayed(LHandle handle)
        {
            try
            {
                if (handle == null) return;
                var now = Game.GameTime;
                if (_lastHandle != null && ReferenceEquals(_lastHandle.Target, handle) && now - _lastHandleAt < DuplicateWindowMs)
                {
                    Logger.LogInfo("[Callouts] Ignoring duplicate display event for the same callout.");
                    return;
                }

                _lastHandle = new WeakReference(handle);
                _lastHandleAt = now;

                var payload = BuildPayload(handle);
                if (payload == null) return;

                EventManager.SendCalloutUpdate(payload);
                Logger.LogInfo($"[Callouts] Sent callout {payload["Identifier"]} '{payload["Name"]}'.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Callouts] Failed to process displayed callout: {ex}");
            }
        }

        private static JObject BuildPayload(LHandle handle)
        {
            var callout = ResolveCallout(handle, out var source);

            string name = null, description = null, priority = null, message = null;
            var position = Vector3.Zero;

            if (callout != null)
            {
                message = Safe(() => callout.CalloutMessage);
                position = SafeValue(() => callout.CalloutPosition, Vector3.Zero);

                ReadCiMetadata(callout, out var ciName, out priority, out description);
                // Name preference: CI display name > LSPDFR friendly name > registered name.
                name = FirstNonBlank(ciName, Safe(() => callout.FriendlyName), ReadInfoAttributeName(callout));
            }

            if (IsBlank(name)) name = Safe(() => Functions.GetCalloutName(handle));
            if (IsBlank(name)) name = callout?.GetType().Name;
            if (IsBlank(name)) name = "Unknown Callout";

            if (callout == null)
                Logger.LogWarning($"[Callouts] Could not resolve the callout object for '{name}'. Sending it without location details.");
            else
                Logger.LogInfo($"[Callouts] Resolved '{name}' via {source}.");

            var hasPosition = IsUsablePosition(position);
            string street = string.Empty, area = string.Empty, county = string.Empty, postal = string.Empty;
            if (hasPosition)
            {
                street = Safe(() => World.GetStreetName(World.GetStreetHash(position))) ?? string.Empty;
                try
                {
                    var zone = Functions.GetZoneAtPosition(position);
                    if (zone != null)
                    {
                        area = zone.RealAreaName ?? string.Empty;
                        county = zone.County.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Callouts] Zone lookup failed: {ex.Message}");
                }

                postal = ResolvePostal(position);
            }

            var now = DateTime.Now;
            return new JObject
            {
                ["Identifier"] = NextIdentifier(),
                ["Status"] = "New",
                ["Name"] = Clean(name),
                ["Description"] = Clean(description),
                ["Message"] = Clean(message),
                ["Priority"] = Clean(priority),
                ["Street"] = street,
                ["Area"] = area,
                ["County"] = county,
                ["Postal"] = postal ?? string.Empty,
                ["Comments"] = string.Empty,
                ["StartTime"] = now.ToString("h:mm:ss tt"),
                ["StartDate"] = now.ToString("yyyy-MM-dd"),
                ["X"] = hasPosition ? position.X : 0f,
                ["Y"] = hasPosition ? position.Y : 0f
            };
        }

        // ------------------------------------------------------------------ callout resolution

        /// <summary>
        ///     Unwraps the <see cref="Callout" /> behind an LSPDFR handle. Tries the internal <c>LHandle.Object</c>
        ///     first (directly, then one level of wrapping, in case a future LSPDFR wraps it), then CI.
        /// </summary>
        private static Callout ResolveCallout(LHandle handle, out string source)
        {
            source = "none";
            try
            {
                var inner = GetHandleObject(handle);
                if (inner is Callout direct)
                {
                    source = "LSPDFR handle";
                    return direct;
                }

                var nested = FindCalloutMember(inner);
                if (nested != null)
                {
                    source = "LSPDFR handle (nested)";
                    return nested;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Callouts] LHandle reflection failed: {ex.Message}");
            }

            if (_ciInstalled)
                try
                {
                    // A missing/broken CI assembly throws when the bridge method is JIT-compiled, i.e. here.
                    var viaCi = CalloutInterfaceBridge.TryGetCallout(handle);
                    if (viaCi != null)
                    {
                        source = "CalloutInterface";
                        return viaCi;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Callouts] CalloutInterface unavailable: {ex.Message}");
                }

            return null;
        }

        private static object GetHandleObject(LHandle handle)
        {
            if (!_handleAccessorResolved)
            {
                _handleAccessorResolved = true;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var type = typeof(LHandle);
                _handleObjectProperty = type.GetProperty("Object", flags);
                if (_handleObjectProperty == null || _handleObjectProperty.GetIndexParameters().Length != 0)
                {
                    _handleObjectProperty = null;
                    // The property may be renamed or obfuscated later. Its backing field is the only object-typed field.
                    foreach (var f in type.GetFields(flags))
                        if (f.FieldType == typeof(object))
                        {
                            _handleObjectField = f;
                            break;
                        }
                }

                if (_handleObjectProperty == null && _handleObjectField == null)
                    Logger.LogWarning("[Callouts] LHandle has no object accessor in this LSPDFR version.");
            }

            if (_handleObjectProperty != null) return _handleObjectProperty.GetValue(handle, null);
            return _handleObjectField?.GetValue(handle);
        }

        /// <summary>One level search of an object's fields for a <see cref="Callout" /> (wrapper fallback).</summary>
        private static Callout FindCalloutMember(object holder)
        {
            if (holder == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = holder.GetType(); t != null && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(Callout).IsAssignableFrom(f.FieldType) && f.FieldType != typeof(object)) continue;
                    try
                    {
                        if (f.GetValue(holder) is Callout c) return c;
                    }
                    catch
                    {
                        // unreadable field; keep looking
                    }
                }

            return null;
        }

        // ------------------------------------------------------------------ metadata

        /// <summary>
        ///     Reads Name / Priority / Description from the callout's <c>[CalloutInterface]</c> attribute, by type name,
        ///     so neither CI nor CalloutInterfaceAPI has to be loadable. Looks at <c>ScriptInfo</c> first (what LSPDFR
        ///     registered), then the class's own attributes.
        /// </summary>
        private static void ReadCiMetadata(Callout callout, out string name, out string priority, out string description)
        {
            name = priority = description = null;
            var attr = FindCiAttribute(Safe<object>(() => callout.ScriptInfo));
            if (attr == null)
                try
                {
                    foreach (var a in callout.GetType().GetCustomAttributes(true))
                        if ((attr = FindCiAttribute(a)) != null)
                            break;
                }
                catch (Exception ex)
                {
                    // An attribute whose assembly can't load (e.g. missing CalloutInterfaceAPI.dll) throws here.
                    Logger.LogWarning($"[Callouts] Could not read callout attributes: {ex.Message}");
                }

            if (attr == null) return;
            name = ReadStringProperty(attr, "Name");
            priority = ReadStringProperty(attr, "Priority");
            description = ReadStringProperty(attr, "Description");
        }

        private static object FindCiAttribute(object candidate)
        {
            if (candidate == null) return null;
            for (var t = candidate.GetType(); t != null; t = t.BaseType)
                if (t.FullName == CiAttributeTypeName)
                    return candidate;
            return null;
        }

        /// <summary>The <c>[CalloutInfo("Name", ...)]</c> registered name, used when no friendlier name exists.</summary>
        private static string ReadInfoAttributeName(Callout callout)
        {
            var info = Safe<object>(() => callout.ScriptInfo);
            if (info is CalloutInfoAttribute cia) return Safe(() => cia.Name);
            try
            {
                foreach (var a in callout.GetType().GetCustomAttributes(typeof(CalloutInfoAttribute), true))
                    if (a is CalloutInfoAttribute found)
                        return Safe(() => found.Name);
            }
            catch
            {
                // ignore; caller has other fallbacks
            }

            return null;
        }

        private static string ReadStringProperty(object target, string property)
        {
            try
            {
                var p = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
                return p?.GetValue(target, null) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string ResolvePostal(Vector3 position)
        {
            // Each bridge call is wrapped: a missing assembly throws at JIT time, before the callee's own try runs.
            if (_ciInstalled)
                try
                {
                    var ci = CalloutInterfaceBridge.TryGetPostal(position);
                    if (!IsBlank(ci)) return ci.Trim();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Callouts] CalloutInterface postal unavailable: {ex.Message}");
                }

            if (_cdfInstalled)
                try
                {
                    var cdf = CommonDataFrameworkPostal.TryGetPostal(position);
                    if (!IsBlank(cdf)) return cdf.Trim();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Callouts] CommonDataFramework postal unavailable: {ex.Message}");
                }

            return string.Empty;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        ///     Random 5-digit id, as before, but never repeating one of the last few hundred this session.
        ///     Two live callouts with the same id would merge into one on the MDT.
        /// </summary>
        private static int NextIdentifier()
        {
            int id;
            var guard = 0;
            do
            {
                id = Rng.Next(MinIdentifier, MaxIdentifierExclusive);
            } while (RecentIdentifierSet.Contains(id) && ++guard < 50);

            RecentIdentifiers.Enqueue(id);
            RecentIdentifierSet.Add(id);
            while (RecentIdentifiers.Count > RecentIdentifierLimit) RecentIdentifierSet.Remove(RecentIdentifiers.Dequeue());
            return id;
        }

        /// <summary>Callout packs often have no position set yet, or a junk one. (0,0) also means "no marker" on the MDT map.</summary>
        private static bool IsUsablePosition(Vector3 p)
        {
            return !(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y))
                   && (Math.Abs(p.X) > 0.01f || Math.Abs(p.Y) > 0.01f);
        }

        /// <summary>Strips GTA formatting tokens (~r~, ~n~, ...) that callout packs put in their text.</summary>
        private static string Clean(string s)
        {
            if (IsBlank(s)) return string.Empty;
            return Whitespace.Replace(FormattingToken.Replace(s, " "), " ").Trim();
        }

        private static bool IsBlank(string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        private static string FirstNonBlank(params string[] values)
        {
            foreach (var v in values)
                if (!IsBlank(v))
                    return v;
            return null;
        }

        private static T Safe<T>(Func<T> f) where T : class
        {
            try
            {
                return f();
            }
            catch
            {
                return null;
            }
        }

        private static T SafeValue<T>(Func<T> f, T fallback) where T : struct
        {
            try
            {
                return f();
            }
            catch
            {
                return fallback;
            }
        }
    }

    /// <summary>
    ///     Everything that touches CalloutInterface types. It is a separate class with non-inlined methods, so the JIT only
    ///     loads CalloutInterface.dll when CI is installed and one of these is actually called.
    /// </summary>
    internal static class CalloutInterfaceBridge
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Callout TryGetCallout(LHandle handle)
        {
            try
            {
                return CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Callouts] CalloutInterface could not resolve the callout: {ex.Message}");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TryGetPostal(Vector3 position)
        {
            try
            {
                return CalloutInterface.API.Functions.GetPostalCode(position);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Callouts] CalloutInterface postal lookup failed: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>CommonDataFramework postal lookup, isolated for the same JIT reason as <see cref="CalloutInterfaceBridge" />.</summary>
    internal static class CommonDataFrameworkPostal
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TryGetPostal(Vector3 position)
        {
            try
            {
                return CommonDataFramework.Modules.Postals.PostalCodeController.GetPostalCode(position);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Callouts] CommonDataFramework postal lookup failed: {ex.Message}");
                return null;
            }
        }
    }
}
