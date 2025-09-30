using System;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates.OnRequest
{
    public class GameTimeAction : IWebSocketAction
    {
        public string Name => "gametime";
        public bool IsContinuous => false;

        public void Execute(IncomingRequest request)
        {
            var currentTime = DateTime.Now.ToString("h:mm tt");
            GameClientSocket.Send(Name, currentTime);
        }
    }
}