using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.WebSocket;
using RPWebSocketPlugin.Messages;

namespace ReportsPlus.Updates
{
    public abstract class CalloutUpdate : IWebSocketAction
    {
        public string Name => "calloutupdate";

        public void Execute(IncomingRequest request)
        {
            Game.LogTrivial("[INFO] CalloutUpdate.Execute called, but this action is driven by game events.");
        }

        public static void SendCalloutData(JObject data)
        {
            GameClientSocket.Send("calloutupdate", data);
        }
    }
}