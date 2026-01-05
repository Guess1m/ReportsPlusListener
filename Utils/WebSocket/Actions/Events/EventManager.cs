using System;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public static class EventManager{
        private static GameClientSocket _client;

        public static void SetClient(GameClientSocket client)
        {
            _client = client;
        }

        private static void SendUpdate(string type, JObject data)
        {
            if (_client is { IsConnected: true })
                _client.Send(type, data);
            else
                Logger.LogWarning($"EventManager: Cannot send '{type}' update, client is not connected.");
        }

        public static void SendCalloutUpdate(JObject data)
        {
            SendUpdate("calloutUpdate", data);
        }

        public static void SendIDUpdate(JObject data)
        {
            SendUpdate("pedIdScan", data);
        }

        public static void SendDynamicEvent(DynamicEvent dynamicEvent)
        {
            try
            {
                var payload = JObject.FromObject(dynamicEvent.GetPayload());

                payload["topic"] = dynamicEvent.Topic;

                SendUpdate("dynamic_event", payload);

                Logger.LogInfo($"Sent Dynamic Event: {dynamicEvent.Topic}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send dynamic event {dynamicEvent.Topic}: {ex.Message}");
            }
        }
    }
}