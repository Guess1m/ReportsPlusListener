using Newtonsoft.Json.Linq;
using Rage;
using RPWebSocketPlugin.WebSocket.Messages;

namespace RPWebSocketPlugin.Messages
{
    public class IncomingRequest : IMessage
    {
        // Handles incoming messages
        public IncomingRequest(string json)
        {
            var jsonObject = JObject.Parse(json);

            Type = jsonObject["type"]?.ToString() ?? "error";
            Data = jsonObject["data"]?.ToString() ?? "error";
            Args = jsonObject["args"]?.ToString() ?? "error";
            Sender = jsonObject["sender"]?.ToString() ?? "error";

            HasErrors = Type == "error" || Data == "error" || Sender == "error";

            Game.LogTrivial(HasErrors ? "Error, null value(s) in request: " + ToString() : "IncomingRequest: " + ToString());
        }

        private bool HasErrors { get; }
        public string Type { get; }
        public string Data { get; }
        public string Args { get; }
        public string Sender { get; }

        public sealed override string ToString()
        {
            return $"Type: [{Type}], Data: [{Data}], Args: [{Args}], Sender: [{Sender}]";
        }
    }
}