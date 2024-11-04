using System.IO;
using System.Linq;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;

namespace ReportsPlus.Utils
{
    public class ConfigUtils
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
            RefreshDelay = iniFile.ReadInt32("Settings", "DataRefreshInterval", 5000);
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