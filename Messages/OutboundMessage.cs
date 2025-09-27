using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.WebSocket;
using RPWebSocketPlugin.WebSocket.Messages;

namespace ReportsPlus.Messages
{
    public class OutboundMessage : IMessage
    {
        public OutboundMessage(string type, string args, string data)
        {
            Type = type ?? "error";
            Args = args ?? "error";
            Data = data ?? "error";
            Sender = GameClientSocket.ClientSocket.Url.ToString(); //BUG: this may be accessing wrong socket

            if (type == null || data == null || args == null)
                Game.LogTrivial("Error, null value(s) in OutboundMessage: " + ToString());
            else
                Game.LogTrivial("OutboundMessage: " + ToString());
        }

        public string Type { get; } // "request"
        public string Args { get; } // "24hr"
        public string Data { get; } // "gametime"
        public string Sender { get; } // "http://localhost:6969/ws/game-client"

        public string ToJson()
        {
            var messageObject = new JObject
            {
                ["type"] = Type,
                ["args"] = Args,
                ["data"] = Data,
                ["sender"] = Sender
            };
            return messageObject.ToString(Formatting.None);
        }

        public sealed override string ToString()
        {
            return $"Type: [{Type}], Args: [{Args}], Data: [{Data}], Sender: [{Sender}]";
        }
    }
}