using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Messages
{
    public class IncomingRequest
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IncomingRequest" /> class by parsing a JSON object.
        /// </summary>
        /// <param name="jsonObject">The raw JSON data received from the socket.</param>
        public IncomingRequest(JObject jsonObject)
        {
            Type = jsonObject["type"]?.ToString() ?? "error";
            Data = jsonObject["data"];
            Args = jsonObject["args"]?.ToString() ?? "error";
            Sender = jsonObject["sender"]?.ToString() ?? "error";

            HasErrors = Type == "error" || Data == null || Sender == "error";

            if (HasErrors)
                Logger.LogError("Error, null value(s) in request: " + ToString());
        }

        private bool HasErrors { get; }
        public string Type { get; }
        public JToken Data { get; }
        public string Args { get; }
        private string Sender { get; }

        /// <summary>
        ///     Returns a string representation of the request components for logging purposes.
        /// </summary>
        /// <returns>A formatted string containing Type, Data, Args, and Sender.</returns>
        public sealed override string ToString()
        {
            return $"Type: [{Type}], Data: [{Data}], Args: [{Args}], Sender: [{Sender}]";
        }
    }
}