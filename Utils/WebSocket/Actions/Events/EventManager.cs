using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public static class EventManager{
        private static GameClientSocket _client;

        /// <summary>
        ///     Assigns the active communication socket to the manager for event dispatching.
        /// </summary>
        /// <param name="client">The <see cref="GameClientSocket" /> to use for sending updates.</param>
        public static void SetClient(GameClientSocket client)
        {
            _client = client;
        }

        /// <summary>
        ///     Validates the connection state and dispatches a JSON payload to the client.
        /// </summary>
        /// <param name="type">The event type identifier.</param>
        /// <param name="data">The <see cref="JObject" /> payload to transmit.</param>
        private static void SendUpdate(string type, JObject data)
        {
            if (_client is { IsConnected: true })
                _client.Send(type, data);
            else
                Logger.LogWarning($"EventManager: Cannot send '{type}' update, client is not connected.");
        }

        /// <summary>
        ///     Dispatches a callout update event to the client.
        /// </summary>
        /// <param name="data">The callout metadata payload.</param>
        public static void SendCalloutUpdate(JObject data)
        {
            SendUpdate("calloutUpdate", data);
        }

        /// <summary>
        ///     Dispatches a ped identification update event to the client.
        /// </summary>
        /// <param name="data">The ped identification payload.</param>
        public static void SendIDUpdate(JObject data)
        {
            SendUpdate("pedIdScan", data);
        }
    }
}