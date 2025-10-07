using System.Globalization;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
    public class GameTimeAction : IRequestAction{
        public string Name => "gametime";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            var timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            var timeData   = new JObject { ["time"] = timeString };
            client.Send(Name, timeData);
        }
    }
}