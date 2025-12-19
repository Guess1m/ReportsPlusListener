using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    // TODO: Must have a collection of predefined actions on the Application side and check against that to give functionality
    public static class CustomActionRegistry{
        // Key = Action Name (from Web), Value = The Config
        private static readonly Dictionary<string, CustomActionConfig> Actions = new Dictionary<string, CustomActionConfig>();

        public static void LoadCustomActions()
        {
            Actions.Clear();
            var path = "Plugins/LSPDFR/ReportsPlus/Actions/";

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var serializer = new XmlSerializer(typeof(CustomActionConfig));
            foreach (var file in Directory.GetFiles(path, "*.xml"))
                using (var stream = new FileStream(file, FileMode.Open))
                {
                    var config = (CustomActionConfig)serializer.Deserialize(stream);
                    if (config != null)
                    {
                        Actions[config.Name.ToLower()] = config; // Store as lowercase for easy lookup
                        Logger.LogInfo($"Registered Custom Action: {config.Name} -> {config.Target}");
                    }
                }
        }

        public static bool TryGetAction(string name, out CustomActionConfig action)
        {
            return Actions.TryGetValue(name.ToLower(), out action);
        }
    }
}