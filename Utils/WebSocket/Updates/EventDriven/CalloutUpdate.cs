using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.EventDriven
{
    public abstract class CalloutUpdate : IWebSocketAction
    {
        public string Name => "calloutupdate";
        public bool IsContinuous => false;

        public void Execute(IncomingRequest request)
        {
            Logger.LogWarning("CalloutUpdate.Execute called, but this action is driven by game events.");
        }

        public static void SendCalloutData(JObject data)
        {
            Main.Client.Send("calloutupdate", data);
        }
    }
}