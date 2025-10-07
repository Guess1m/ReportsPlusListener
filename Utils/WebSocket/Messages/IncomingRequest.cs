using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Messages{
    public class IncomingRequest{
        public IncomingRequest(JObject jsonObject)
        {
            Type   = jsonObject["type"]?.ToString() ?? "error";
            Data   = jsonObject["data"];
            Args   = jsonObject["args"]?.ToString() ?? "error";
            Sender = jsonObject["sender"]?.ToString() ?? "error";

            HasErrors = Type == "error" || Data == null || Sender == "error";

            if (HasErrors)
                Logger.LogError("Error, null value(s) in request: " + ToString());
            else
                Logger.LogInfo("IncomingRequest: " + ToString());
        }

        private bool   HasErrors { get; }
        public  string Type      { get; }
        public  JToken Data      { get; }
        public  string Args      { get; }
        private string Sender    { get; }

        public sealed override string ToString()
        {
            return $"Type: [{Type}], Data: [{Data}], Args: [{Args}], Sender: [{Sender}]";
        }
    }
}