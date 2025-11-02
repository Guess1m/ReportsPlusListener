using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
    public class PlayerLocationAction : IRequestAction{
        public string Name => "playerLocation";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            var playerPosition = Main.LPC.Position;
            var locationData = new JObject
            {
                ["x"] = playerPosition.X,
                ["y"] = playerPosition.Y
            };
            client.Send("playerLocation", locationData);
        }
    }
}