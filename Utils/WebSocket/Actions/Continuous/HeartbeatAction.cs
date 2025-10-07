using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;
using Logger = ReportsPlus.Utils.Logging.Logger;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public class HeartbeatAction : IRequestAction{
        //TODO: test this
        public string Name => "heartbeat";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            Logger.LogDebug("Heartbeat received");
            var now = DateTime.UtcNow;

            client.Send(Name, new JObject { ["time"] = now.ToString(CultureInfo.InvariantCulture) });
        }
    }
}