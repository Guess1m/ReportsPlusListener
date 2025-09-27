using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.WebSocket;
using RPWebSocketPlugin.Messages;

namespace ReportsPlus.Updates
{
    public class PlayerLocationAction : IWebSocketAction
    {
        public string Name => "playerLocation";

        public void Execute(IncomingRequest request)
        {
            var playerPosition = Game.LocalPlayer.Character.Position;

            var locationData = new JObject
            {
                ["x"] = playerPosition.X,
                ["y"] = playerPosition.Y
            };

            GameClientSocket.Send(Name, locationData.ToString());
        }
    }
}