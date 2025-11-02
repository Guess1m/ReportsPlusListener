using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public class HeartbeatAction : IRequestAction{
        public string Name => "heartbeat";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            var now = DateTime.UtcNow;
            client.Send(Name, new JObject { ["time"] = now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) });
        }
    }
}