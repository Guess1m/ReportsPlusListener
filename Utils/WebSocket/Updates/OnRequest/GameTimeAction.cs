using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.OnRequest{
    public class GameTimeAction : IWebSocketAction{
        public string Name         => "gametime";
        public bool   IsContinuous => false;

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            string timeString;
            try
            {
                timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                timeString = "";
            }

            var timeData = new JObject
            {
                ["time"] = timeString
            };

            client.Send(Name, timeData);
        }
    }
}