using System;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Cleanup;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WebSocket.Updates;
using WebSocketSharp;
using Logger = ReportsPlus.Utils.Logging.Logger;

namespace ReportsPlus.Utils.WebSocket{
    public class GameClientSocket{
        private readonly WebSocketSharp.WebSocket _clientSocket;

        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();

        private GameFiber _senderFiber;

        public GameClientSocket(string hostname = "localhost", int port = 6969)
        {
            var url = $"ws://{hostname}:{port}/ws/game-client";
            _clientSocket = new WebSocketSharp.WebSocket(url);
            SetupEvents();

            OnMessageReceived += message => HandleServerMessage(this, message);
        }

        public bool IsConnected => _clientSocket is { ReadyState: WebSocketState.Open };

        private static void HandleServerMessage(GameClientSocket client, IncomingRequest message)
        {
            ActionRegistry.HandleRequest(client, message);
            ActionRegistry.HandleKeybinding(message);
        }

        public event Action<IncomingRequest> OnMessageReceived;

        private void SetupEvents()
        {
            _clientSocket.OnOpen += (sender, e) =>
            {
                Logger.LogInfo("Connected successfully.");
                _senderFiber = GameFiber.StartNew(SenderLoop, "ReportsPlus-SenderFiber");

                CleanupRegistry.Register(() => Misc.CleanupFiber(_senderFiber));
            };

            _clientSocket.OnMessage += (client, e) =>
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

            _clientSocket.OnError += (sender, e) => { Logger.LogError($"WebSocket error: {e.Message}"); };
            _clientSocket.OnClose += (sender, e) => { Logger.LogError($"Disconnected. Code: {e.Code}, Reason: {e.Reason}"); };
        }

        private void SenderLoop()
        {
            Logger.LogDebug("SenderLoop has started.");
            try
            {
                while (IsConnected)
                    if (_sendQueue.TryDequeue(out var message))
                        _clientSocket.Send(message);
                    else
                        GameFiber.Sleep(10);
            }
            catch (Exception ex)
            {
                Logger.LogError($"An exception occurred in the SenderLoop: {ex.Message}");
            }
            finally
            {
                Logger.LogWarning("SenderLoop has ended.");
            }
        }

        public void Connect()
        {
            Logger.LogDebug($"Connecting to {_clientSocket.Url}...");
            _clientSocket.Connect();
        }

        public void Send(string type, JToken data, string args = "")
        {
            if (!IsConnected)
            {
                Logger.LogWarning("Cannot send message: not connected.");
                return;
            }

            try
            {
                var messageObject = new JObject
                {
                    ["type"]   = type,
                    ["data"]   = data,
                    ["sender"] = _clientSocket.Url.ToString()
                };

                if (!string.IsNullOrEmpty(args)) messageObject["args"] = args;

                var messageJson = messageObject.ToString(Formatting.None);

                _sendQueue.Enqueue(messageJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to serialize or enqueue message: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_senderFiber is { IsAlive: true })
            {
                _senderFiber.Abort();
                _senderFiber = null;
            }

            if (_clientSocket != null && IsConnected) _clientSocket.Close(CloseStatusCode.Normal);
        }
    }
}