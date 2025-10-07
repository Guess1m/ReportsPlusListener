using System.Text;
using INIUtility;

namespace ReportsPlus.Utils.Config{
    public sealed class ReportsPlusSettings{
        // Register Settings

        [ConfigOption("Settings", "ClientAddress", "The network address for the websocket server (e.g., localhost, 127.0.0.1).")]
        public static string ClientAddress { get; set; } = "localhost";

        [ConfigOption("Settings", "ClientPort", "Port to use for websocket server (must be 1-65535).")]
        public static int ClientPort { get; set; } = 6969;

        [ConfigOption("Intervals", "ContinuousUpdateInterval", "Interval (ms) for sending continuous updates to server (e.g. PlayerLocation, PoliceVehiclesLocation).")]
        public static int ContinuousUpdateInterval { get; set; } = 5000;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- ReportsPlus Loaded Settings ---");
            foreach (var prop in GetType().GetProperties())
                if (prop.GetCustomAttributes(typeof(ConfigOptionAttribute), false).Length > 0)
                    sb.AppendLine($" {prop.Name}: '{prop.GetValue(this)}'");

            sb.AppendLine("-----------------------------------");
            return sb.ToString();
        }
    }
}