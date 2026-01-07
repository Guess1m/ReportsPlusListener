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

        // keybinding for input-lock
        [ConfigOption("Keybindings", "InputLockKey", "Key to toggle input lock (e.g. F9). Must be capitalized.")]
        public Keys InputLockKey { get; set; } = Keys.F9;

        [ConfigOption("Keybindings", "ReconnectKey", "Key to reconnect (e.g. F10). Must be capitalized.")]
        public Keys ReconnectKey { get; set; } = Keys.F10;

        // keybinding for give parking citation
        [ConfigOption("Keybindings", "GiveParkingCitationKey", "Key to give out a parking citation (e.g. F3). Must be capitalized.")]
        public Keys GiveParkingCitationKey { get; set; } = Keys.F3;

        // keybinding for discarding vehicle citation
        [ConfigOption("Keybindings", "DiscardParkingCitationKey", "Key to discard a parking citation (e.g. Delete). Must be capitalized.")]
        public Keys DiscardParkingCitationKey { get; set; } = Keys.Delete;

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