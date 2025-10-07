using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket{
    public static class EventManager{
        private static GameClientSocket _client;

        public static void SetClient(GameClientSocket client)
        {
            _client = client;
        }

        public static void SendUpdate(string type, JObject data)
        {
            if (_client is { IsConnected: true })
                _client.Send(type, data);
            else
                Logger.LogWarning($"EventManager: Cannot send '{type}' update, client is not connected.");
        }
    }
}