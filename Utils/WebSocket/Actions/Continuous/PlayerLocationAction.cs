using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Actions.Interfaces;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public class PlayerLocationAction : IContinuousAction{
        public void Execute(GameClientSocket client)
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