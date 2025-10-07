using Newtonsoft.Json.Linq;

namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public static class CalloutHandler{
        public static void SendCalloutData(JObject data)
        {
            EventManager.SendUpdate("calloutUpdate", data);
        }
    }
}