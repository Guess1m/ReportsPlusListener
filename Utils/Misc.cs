using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CommonDataFramework.Modules.PedDatabase;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils{
    public static class Misc{
        public static  bool UsingPrFunctions;
        private static bool _usingCi;

        private static readonly ThreadLocal<Random> RandThreadLocal = new ThreadLocal<Random>(() => new Random());
        private static          Random              Rand => RandThreadLocal.Value;

        private static bool IsPluginInstalled(string pluginName)
        {
            var plugins     = Functions.GetAllUserPlugins();
            var isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Logger.LogInfo($"ReportsPlusListener: Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }

        public static StringBuilder RunPluginChecks()
        {
            Logger.LogInfo("Running Plugin Checks..");
            var missingPluginsMessageBuilder = new StringBuilder();

            _usingCi = IsPluginInstalled("CalloutInterface");
            var hasPolicingRedefined   = IsPluginInstalled("PolicingRedefined");
            var hasCommonDataFramework = IsPluginInstalled("CommonDataFramework");
            UsingPrFunctions = hasPolicingRedefined && hasCommonDataFramework;

            Logger.LogInfo("UsingCI: " + _usingCi);
            Logger.LogInfo("hasPolicingRedefined: " + hasPolicingRedefined);
            Logger.LogInfo("hasCommonDataFramework: " + hasCommonDataFramework);
            Logger.LogInfo("UsingPRFunctions: " + UsingPrFunctions);

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

        public static void CleanupPluginEvents()
        {
            Logger.LogInfo("Cleaning up plugin event subscriptions...");
            if (_usingCi)
            {
                // YOU MUST IMPLEMENT THIS
                EventUtils.CleanupCiEvent();
                Logger.LogInfo("CI Events Cleaned up.");
            }

            if (UsingPrFunctions)
            {
                // YOU MUST IMPLEMENT THIS
                EventUtils.CleanupEventsPr();
                Logger.LogInfo("PR Events Cleaned up.");
            }
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

        public static string GetPedAddress(Ped ped)
        {
            if (ped == null) return null;
            if (ped.GetPedData() == null) return null;

            var addressBuilder = new StringBuilder();
            addressBuilder.Append(ped.GetPedData().Address.AddressPostal.Number).Append(" ");
            addressBuilder.Append(ped.GetPedData().Address.StreetName).Append(", ");
            addressBuilder.Append(ped.GetPedData().Address.Zone.RealAreaName).Append(", ");
            addressBuilder.Append(Regex.Replace(ped.GetPedData().Address.Zone.County.ToString(), "(?<!^)([A-Z])", " $1"));

            return addressBuilder.ToString();
        }

        public static string GenerateValidLicenseExpirationDate()
        {
            var maxYears       = 4;
            var currentDate    = DateTime.Now;
            var expirationDate = currentDate.AddYears(maxYears).AddDays(Rand.Next(0, 365));
            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static string GenerateExpiredLicenseExpirationDate(int maxYears)
        {
            var maxYearsAgo = maxYears;
            var currentDate = DateTime.Now;

            long minDaysAgo    = 1;
            var  maxDaysAgo    = maxYearsAgo * 365L + maxYearsAgo / 4;
            long randomDaysAgo = Rand.Next((int)minDaysAgo, (int)maxDaysAgo + 1);

            var expirationDate = currentDate.AddDays(-randomDaysAgo);

            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static void CleanupFiber(GameFiber fiber)
        {
            if (!(fiber is { IsAlive: true })) return;
            fiber.Abort();
            Logger.LogInfo($"Fiber {fiber} was cleaned up.");
        }
    }
}