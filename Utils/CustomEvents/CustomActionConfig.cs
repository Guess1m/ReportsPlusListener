using System.Xml.Serialization;

namespace ReportsPlus.Utils.CustomEvents{
    [XmlRoot("Action")]
    public class CustomActionConfig{
        public string Name     { get; set; } // The command name sent from Web Client
        public string Assembly { get; set; } // The DLL Name (e.g., "UltimateBackup")
        public string Target   { get; set; } // The Class.Method (e.g., "UltimateBackup.API.Functions.CallCode3")
    }
}