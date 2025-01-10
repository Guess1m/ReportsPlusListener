using System.IO;
using System.Linq;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;

namespace ReportsPlus.Utils
{
    public static class ConfigUtils
    {
        public static int RefreshDelay;

        public static bool IsPluginInstalled(string pluginName)
        {
            var plugins = Functions.GetAllUserPlugins();
            var isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Game.LogTrivial($"ReportsPlusListener: Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }

        public static void LoadSettings()
        {
            Game.LogTrivial("ReportsPlusListener: Loading Settings..");
            var iniFile = new InitializationFile("plugins/LSPDFR/ReportsPlus.ini");
            iniFile.Create();

            if (!iniFile.DoesKeyExist("Settings", "DataRefreshInterval"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: DataRefreshInterval Config setting didn't exist, creating");
                iniFile.Write("Settings", "DataRefreshInterval", 13000);
            }

            if (!iniFile.DoesKeyExist("Keybinds", "GiveTicket"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: GiveTicket Config setting didn't exist, creating");
                iniFile.Write("Keybinds", "GiveTicket", Keys.U);
            }

            RefreshDelay = iniFile.ReadInt32("Settings", "DataRefreshInterval", 13000);
            Utils.AnimationBind = iniFile.ReadEnum("Keybinds", "GiveTicket", Keys.U);

            Game.LogTrivial("ReportsPlusListener {CONFIG}: GiveTicket Keybind- '" + Utils.AnimationBind + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: DataRefreshInterval- '" + RefreshDelay + "'");
        }

        public static void CreateFiles()
        {
            var dataFolder = "ReportsPlus\\data";

            if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

            string[] filesToCreate =
                { "callout.xml", "currentID.xml", "worldCars.data", "worldPeds.data", "trafficStop.data" };

            foreach (var fileName in filesToCreate)
            {
                var filePath = Path.Combine(dataFolder, fileName);
                if (!File.Exists(filePath))
                    using (File.Create(filePath))
                    {
                    }
            }
        }
    }
}