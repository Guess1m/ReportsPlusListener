using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Messages;
using ReportsPlus.Updates;
using WebSocketSharp;
using Logger = ReportsPlus.Logging.Logger;

namespace ReportsPlus.WebSocket
{
    public class GameClientSocket
    {
        public static WebSocketSharp.WebSocket ClientSocket;

        public GameClientSocket(string hostname = "localhost", int port = 6969)
        {
            var url = $"ws://{hostname}:{port}/ws/game-client";
            ClientSocket = new WebSocketSharp.WebSocket(url);
            SetupEvents();

            OnMessageReceived += HandleServerMessage;
        }

        public static bool IsConnected => ClientSocket is { ReadyState: WebSocketState.Open };

        private static void HandleServerMessage(IncomingRequest message)
        {
            GameFiber.StartNew(() => { ActionRegistry.HandleRequest(message); });
        }

        public event Action<IncomingRequest> OnMessageReceived;

        private void SetupEvents()
        {
            ClientSocket.OnOpen += (sender, e) => { Logger.LogInfo("Connected successfully."); };
            ClientSocket.OnMessage += (client, e) =>
            {
                try
                {
                    OnMessageReceived?.Invoke(new IncomingRequest(JObject.Parse(e.Data).ToString()));
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not parse message from server: {ex.Message}");
                }
            };

            ClientSocket.OnError += (sender, e) => { Logger.LogError($"WebSocket error: {e.Message}"); };

            ClientSocket.OnClose += (sender, e) => { Logger.LogError($"Disconnected. Code: {e.Code}, Reason: {e.Reason}"); };
        }

        public static void Connect()
        {
            Logger.LogDebug($"Connecting to {ClientSocket.Url}...");
            ClientSocket.Connect();
        }

        public static void Send(string type, string data)
        {
            if (!IsConnected)
            {
                Logger.LogError("Cannot send message: not connected.");
                return;
            }

            try
            {
                var messageObject = new JObject { ["type"] = type, ["data"] = data };
                var messageJson = messageObject.ToString(Formatting.None);

                ClientSocket.Send(messageJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send message: {ex.Message}");
            }
        }

        public static void Send(string type, string data, string args = "")
        {
            if (!IsConnected)
            {
                Logger.LogError("Cannot send message: not connected.");
                return;
            }

            try
            {
                var messageObject = new JObject
                {
                    ["type"] = type,
                    ["data"] = data
                };

                if (!string.IsNullOrEmpty(args)) messageObject["args"] = args;

                var messageJson = messageObject.ToString(Formatting.None);

                ClientSocket.Send(messageJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send message: {ex.Message}");
            }
        }

        public static void Send(string type, JObject data)
        {
            if (!IsConnected)
            {
                Logger.LogError("Cannot send message: not connected.");
                return;
            }

            try
            {
                var messageObject = new JObject
                {
                    ["type"] = type,
                    ["data"] = data
                };
                var messageJson = messageObject.ToString(Formatting.None);

                ClientSocket.Send(messageJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send message: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (ClientSocket != null && IsConnected) ClientSocket.Close(CloseStatusCode.Normal);
        }
    }
}