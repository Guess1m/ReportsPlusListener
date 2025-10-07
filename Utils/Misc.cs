using System.Linq;
using System.Text;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils{
    public static class Misc{
        private static bool _usingPrFunctions;
        private static bool _usingCi;

        private static bool IsPluginInstalled(string pluginName)
        {
            var plugins     = Functions.GetAllUserPlugins();
            var isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Logger.LogDebug($"ReportsPlusListener: Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }

        public static StringBuilder RunPluginChecks()
        {
            Logger.LogInfo("Running Plugin Checks..");
            var missingPluginsMessageBuilder = new StringBuilder();

            _usingCi = IsPluginInstalled("CalloutInterface");
            var hasPolicingRedefined   = IsPluginInstalled("PolicingRedefined");
            var hasCommonDataFramework = IsPluginInstalled("CommonDataFramework");
            _usingPrFunctions = hasPolicingRedefined && hasCommonDataFramework;

            Logger.LogDebug("UsingCI: " + _usingCi);
            Logger.LogDebug("hasPolicingRedefined: " + hasPolicingRedefined);
            Logger.LogDebug("hasCommonDataFramework: " + hasCommonDataFramework);
            Logger.LogDebug("UsingPRFunctions: " + _usingPrFunctions);

            if (_usingCi)
            {
                Logger.LogInfo("CalloutInterface found. Establishing Events..");
                EventUtils.EstablishCiEvent();
            }
            else
            {
                Logger.LogWarning("CalloutInterface not found. Required for Callout Functions.");
                missingPluginsMessageBuilder.Append("~r~CalloutInterface Not Found\n~o~- Required for Callout Functions.\n");
            }

            if (hasPolicingRedefined && hasCommonDataFramework)
            {
                Logger.LogInfo("Policing Redefined and Common Data Framework found. Establishing Events..");
                EventUtils.EstablishEventsPr();
            }
            else
            {
                Logger.LogWarning("Policing Redefined/CDF not found");
                missingPluginsMessageBuilder.Append("~r~PR Not Found\n~o~- Using base game functions.");
            }

            return missingPluginsMessageBuilder;
        }

        internal static string FindPedModel(Ped ped)
        {
            try
            {
                if (ped == null || !ped.IsValid()) return "";
                ped.GetVariation(0, out var drawable, out var texture);
                return $"[{ped.Model.Name.ToLower()}][{drawable}][{texture}]";
            }
            catch
            {
                Logger.LogError("ReportsPlusListener: Error fetching model for ped: " + ped);
                return "";
            }
        }

        public static void CleanupFiber(GameFiber fiber)
        {
            if (!(fiber is { IsAlive: true })) return;
            fiber.Abort();
            Logger.LogDebug($"Fiber {fiber} was cleaned up.");
        }
    }
}