using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WebSocket.Updates;
using WebSocketSharp;
using Logger = ReportsPlus.Utils.Logging.Logger;

namespace ReportsPlus.Utils.WebSocket
{
    public class GameClientSocket
    {
        public WebSocketSharp.WebSocket ClientSocket;

        public GameClientSocket(string hostname = "localhost", int port = 6969)
        {
            var url = $"ws://{hostname}:{port}/ws/game-client";
            ClientSocket = new WebSocketSharp.WebSocket(url);
            SetupEvents();

            OnMessageReceived += HandleServerMessage;
        }

        public bool IsConnected => ClientSocket is { ReadyState: WebSocketState.Open };

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
                    var jsonObject = JObject.Parse(e.Data);
                    OnMessageReceived?.Invoke(new IncomingRequest(jsonObject));
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Could not parse message from server: {ex.Message}");
                }
            };

            ClientSocket.OnError += (sender, e) => { Logger.LogError($"WebSocket error: {e.Message}"); };

            ClientSocket.OnClose += (sender, e) => { Logger.LogError($"Disconnected. Code: {e.Code}, Reason: {e.Reason}"); };
        }

        public void Connect()
        {
            Logger.LogDebug($"Connecting to {ClientSocket.Url}...");
            ClientSocket.Connect();
        }

        public void Send(string type, JToken data, string args = "")
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
                    ["data"] = data,
                    // Add the sender here, where it belongs
                    ["sender"] = ClientSocket.Url.ToString()
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

        public void Disconnect()
        {
            if (ClientSocket != null && IsConnected) ClientSocket.Close(CloseStatusCode.Normal);
        }
    }
}