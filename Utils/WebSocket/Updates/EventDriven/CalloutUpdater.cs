using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Updates.EventDriven{
    public static class CalloutUpdater{
        public static void SendCalloutData(JObject data)
        {
            // Check if the client exists and is connected to avoid errors
            if (Main.Client != null && Main.Client.IsConnected)
                Main.Client.Send("calloutupdate", data);
            else
                Logger.LogWarning("CalloutUpdater: Cannot send data, client is not connected.");
        }
    }
}