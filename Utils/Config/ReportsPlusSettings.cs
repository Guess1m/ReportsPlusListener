using System.Text;
using System.Windows.Forms;
using INIUtility;

namespace ReportsPlus.Utils.Config{
    public sealed class ReportsPlusSettings{
        // Register Settings
        [ConfigOption("Settings", "ClientAddress", "The network address for the websocket server (e.g., localhost, 127.0.0.1).")]
        public string ClientAddress { get; set; } = "localhost";

        [ConfigOption("Settings", "ClientPort", "Port to use for websocket server (must be 1-65535).")]
        public int ClientPort { get; set; } = 6969;

        [ConfigOption("Intervals", "ContinuousUpdateInterval", "Interval (ms) for sending continuous updates to server (e.g. PlayerLocation, PoliceVehiclesLocation).")]
        public int ContinuousUpdateInterval { get; set; } = 15000;

        // keybinding for menu
        [ConfigOption("Keybindings", "MenuKey", "Key to toggle menu (e.g. F11). Must be capitalized.")]
        public Keys MenuKey { get; set; } = Keys.F11;

        // keybinding for input-lock
        [ConfigOption("Keybindings", "InputLockKey", "Key to toggle input lock (e.g. F9). Must be capitalized.")]
        public Keys InputLockKey { get; set; } = Keys.None;

        // keybinding for give parking citation
        [ConfigOption("Keybindings", "GiveCitationKey", "Key to give out a citation (e.g. F3). Must be capitalized. This isn't used when you are using PR.")]
        public Keys GiveCitationKey { get; set; } = Keys.F3;

        // keybinding for discarding vehicle citation
        [ConfigOption("Keybindings", "DiscardCitationKey", "Key to discard a citation (e.g. Delete). Must be capitalized. Always used for parking citations. Not used for printed when you are using PR.")]
        public Keys DiscardCitationKey { get; set; } = Keys.Delete;

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