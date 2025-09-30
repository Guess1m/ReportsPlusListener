using Newtonsoft.Json.Linq;
using ReportsPlus.Logging;
using ReportsPlus.Messages;
using ReportsPlus.WebSocket;

namespace ReportsPlus.Updates
{
    public abstract class CalloutUpdate : IWebSocketAction
    {
        public string Name => "calloutupdate";

        public void Execute(IncomingRequest request)
        {
            Logger.LogWarning("CalloutUpdate.Execute called, but this action is driven by game events.");
        }

        public static void SendCalloutData(JObject data)
        {
            GameClientSocket.Send("calloutupdate", data);
        }
    }
}