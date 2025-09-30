using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.Continuous
{
    public class PlayerLocationAction : IWebSocketAction
    {
        public string Name => "playerLocation";
        public bool IsContinuous => true;

        public void Execute(IncomingRequest request)
        {
            var playerPosition = Main.LPC.Position;

            var locationData = new JObject
            {
                ["x"] = playerPosition.X,
                ["y"] = playerPosition.Y
            };

            GameClientSocket.Send(Name, locationData.ToString());
        }
    }
}