using System.Linq;
using System.Text;
using LSPD_First_Response.Mod.API;
using Rage;

namespace ReportsPlus.Utils
{
    public static class Misc
    {
        private static bool _usingPrFunctions;
        private static bool _usingCi;

        private static bool IsPluginInstalled(string pluginName)
        {
            var plugins = Functions.GetAllUserPlugins();
            var isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Game.LogTrivial($"ReportsPlusListener: Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }

        public static StringBuilder RunPluginChecks()
        {
            Game.LogTrivial("ReportsPlus: Running Plugin Checks..");
            var missingPluginsMessageBuilder = new StringBuilder();

            _usingCi = IsPluginInstalled("CalloutInterface");
            var hasPolicingRedefined = IsPluginInstalled("PolicingRedefined");
            var hasCommonDataFramework = IsPluginInstalled("CommonDataFramework");
            _usingPrFunctions = hasPolicingRedefined && hasCommonDataFramework;

            Game.LogTrivial("ReportsPlus: UsingCI: " + _usingCi);
            Game.LogTrivial("ReportsPlus: hasPolicingRedefined: " + hasPolicingRedefined);
            Game.LogTrivial("ReportsPlus: hasCommonDataFramework: " + hasCommonDataFramework);
            Game.LogTrivial("ReportsPlus: UsingPRFunctions: " + _usingPrFunctions);

            if (_usingCi)
            {
                EventUtils.EstablishCiEvent();
                Game.LogTrivial("ReportsPlus: Found Callout Interface");
            }
            else
            {
                Game.LogTrivial("ReportsPlus: CalloutInterface not found. Required for Callout Functions.");
                missingPluginsMessageBuilder.Append("~r~CalloutInterface Not Found\n~o~- Required for Callout Functions.\n");
            }

            if (hasPolicingRedefined && hasCommonDataFramework)
            {
                EventUtils.EstablishEventsPr();
                Game.LogTrivial("ReportsPlus: Found Policing Redefined and Common Data Framework");
            }
            else
            {
                Game.LogTrivial("ReportsPlus: Policing Redefined/CDF not found");
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
                Game.LogTrivial("ReportsPlusListener: Error fetching model for ped: " + ped);
                return "";
            }
        }

        public static void CleanupFiber(GameFiber fiber)
        {
            if (!(fiber is { IsAlive: true })) return;
            fiber.Abort();
        }
    }
}