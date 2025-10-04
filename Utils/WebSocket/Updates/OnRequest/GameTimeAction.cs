using System;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.OnRequest
{
    public class GameTimeAction : IWebSocketAction
    {
        public string Name => "gametime";
        public bool IsContinuous => false;

        public void Execute(IncomingRequest request)
        {
            var timeData = new JObject
            {
                ["time"] = DateTime.Now.ToString("h:mm tt")
            };

            Main.Client.Send(Name, timeData);
        }
    }
}