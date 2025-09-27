using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Updates;
using RPWebSocketPlugin.Messages;
using WebSocketSharp;

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
            ClientSocket.OnOpen += (sender, e) => { Game.LogTrivial("[STATUS] Connected successfully."); };
            ClientSocket.OnMessage += (client, e) =>
            {
                try
                {
                    OnMessageReceived?.Invoke(new IncomingRequest(JObject.Parse(e.Data).ToString()));
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"[ERROR] Could not parse message from server: {ex.Message}");
                }
            };

            ClientSocket.OnError += (sender, e) => { Game.LogTrivial($"[ERROR] WebSocket error: {e.Message}"); };

            ClientSocket.OnClose += (sender, e) => { Game.LogTrivial($"[STATUS] Disconnected. Code: {e.Code}, Reason: {e.Reason}"); };
        }

        public static void Connect()
        {
            Game.LogTrivial($"[STATUS] Connecting to {ClientSocket.Url}...");
            ClientSocket.Connect();
        }

        public static void Send(string type, string data)
        {
            if (!IsConnected)
            {
                Game.LogTrivial("[ERROR] Cannot send message: not connected.");
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
                Game.LogTrivial($"[ERROR] Failed to send message: {ex.Message}");
            }
        }

        public static void Send(string type, string data, string args = "")
        {
            if (!IsConnected)
            {
                Game.LogTrivial("[ERROR] Cannot send message: not connected.");
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
                Game.LogTrivial($"[ERROR] Failed to send message: {ex.Message}");
            }
        }

        public static void Send(string type, JObject data)
        {
            if (!IsConnected)
            {
                Game.LogTrivial("[ERROR] Cannot send message: not connected.");
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
                Game.LogTrivial($"[ERROR] Failed to send message: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (ClientSocket != null && IsConnected) ClientSocket.Close(CloseStatusCode.Normal);
        }
    }
}