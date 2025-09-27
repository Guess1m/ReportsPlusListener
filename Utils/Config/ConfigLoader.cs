using System.Reflection;
using Rage;

namespace ReportsPlus.Utils.Config
{
    public static class ConfigLoader
    {
        private const string IniFilePath = "plugins/LSPDFR/ReportsPlus.ini";

        public static AppSettings LoadSettings()
        {
            Game.LogTrivial("ReportsPlus: Loading settings...");
            var manager = new ConfigManager(IniFilePath);
            var settings = new AppSettings();

            foreach (var prop in typeof(AppSettings).GetProperties())
            {
                var attr = prop.GetCustomAttribute<ConfigOptionAttribute>();
                if (attr == null) continue;

                var defaultValue = prop.GetValue(settings);

                var getValueMethod = typeof(ConfigManager).GetMethod(nameof(ConfigManager.GetValue)).MakeGenericMethod(prop.PropertyType);

                var loadedValue = getValueMethod.Invoke(manager, new[] { attr.Section, attr.Key, defaultValue, attr.Comment });

                prop.SetValue(settings, loadedValue);
            }

            Game.LogTrivial(settings.ToString());
            Game.LogTrivial("ReportsPlus: Settings loaded successfully.");
            return settings;
        }
    }
}