using System;
using ReportsPlus.WebSocket;
using RPWebSocketPlugin.Messages;

namespace ReportsPlus.Updates
{
    public class GameTimeAction : IWebSocketAction
    {
        public string Name => "gametime";

        public void Execute(IncomingRequest request)
        {
            var currentTime = DateTime.Now.ToString("h:mm tt");
            GameClientSocket.Send(Name, currentTime);
        }
    }
}