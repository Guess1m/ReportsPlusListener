using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;
using WebSocketSharp;
using Logger = ReportsPlus.Utils.Logging.Logger;

namespace ReportsPlus.Utils.WebSocket{
    public class GameClientSocket{
        private readonly ConcurrentQueue<string> _sendQueue  = new ConcurrentQueue<string>();
        private readonly object                  _socketLock = new object();
        private readonly string                  _url;

        private WebSocketSharp.WebSocket _clientSocket;
        private Task                     _senderTask;
        private CancellationTokenSource  _shutdownTokenSource;

        public GameClientSocket(string hostname, int port)
        {
            _url = $"ws://{hostname}:{port}/ws/game-client";
        }

        public bool IsConnected
        {
            get
            {
                lock (_socketLock)
                {
                    return _clientSocket is { ReadyState: WebSocketState.Open };
                }
            }
        }

        public event Action<IncomingRequest> OnMessageReceived;
        public event Action                  OnConnected;
        public event Action                  OnDisconnected;

        /// <summary>
        ///     Initializes the sender loop and attempts a single connection to the server.
        /// </summary>
        public void Start()
        {
            if (_shutdownTokenSource != null && !_shutdownTokenSource.IsCancellationRequested) return;

            _shutdownTokenSource = new CancellationTokenSource();
            var token = _shutdownTokenSource.Token;

            Logger.LogInfo("Starting background socket tasks...");

            _senderTask = Task.Run(() => SenderLoop(token), token);

            // Initial connection attempt
            Task.Run(AttemptConnection);
        }

        /// <summary>
        ///     Attempts to connect to the websocket server.
        ///     Safe to call if already connected (will return early) or if previous connection failed.
        /// </summary>
        public void AttemptConnection()
        {
            lock (_socketLock)
            {
                if (_clientSocket is { ReadyState: WebSocketState.Open })
                {
                    Logger.LogWarning("AttemptConnection called, but socket is already open.");
                    return;
                }
            }

            try
            {
                InitializeSocket();
                Logger.LogInfo($"Attempting to connect to {_url}...");

                lock (_socketLock)
                {
                    _clientSocket?.Connect();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Connection attempt failed: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        private void InitializeSocket()
        {
            lock (_socketLock)
            {
                // Ensure previous socket is closed before creating a new one
                if (_clientSocket != null)
                {
                    _clientSocket.OnOpen    -= OnSocketOpen;
                    _clientSocket.OnMessage -= OnSocketMessage;
                    _clientSocket.OnError   -= OnSocketError;
                    _clientSocket.OnClose   -= OnSocketClose;

                    if (_clientSocket.ReadyState == WebSocketState.Open)
                        _clientSocket.Close();
                }

                _clientSocket = new WebSocketSharp.WebSocket(_url);

                _clientSocket.OnOpen    += OnSocketOpen;
                _clientSocket.OnMessage += OnSocketMessage;
                _clientSocket.OnError   += OnSocketError;
                _clientSocket.OnClose   += OnSocketClose;
            }
        }

        private void OnSocketOpen(object sender, EventArgs e)
        {
            Logger.LogInfo("Connected successfully.");
            OnConnected?.Invoke();
        }

        private void OnSocketMessage(object sender, MessageEventArgs e)
        {
            try
            {
                var jsonObject = JObject.Parse(e.Data);
                OnMessageReceived?.Invoke(new IncomingRequest(jsonObject));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Parse error: {ex.Message}");
            }
        }

        private void OnSocketError(object sender, ErrorEventArgs e)
        {
            Logger.LogError($"WebSocket error: {e.Message}");
        }

        private void OnSocketClose(object sender, CloseEventArgs e)
        {
            Logger.LogError($"Disconnected. Code: {e.Code}, Reason: {e.Reason}");
            OnDisconnected?.Invoke();
        }

        private async Task SenderLoop(CancellationToken token)
        {
            Logger.LogInfo("Background SenderLoop started.");
            while (!token.IsCancellationRequested)
                if (IsConnected && _sendQueue.TryDequeue(out var message))
                    lock (_socketLock)
                    {
                        _clientSocket.Send(message);
                    }
                else
                    try
                    {
                        await Task.Delay(10, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

            Logger.LogWarning("Background SenderLoop stopped.");
        }

        public void Send(string type, JToken data, string args = "")
        {
            try
            {
                var messageObject = new JObject
                {
                    ["type"]   = type,
                    ["data"]   = data,
                    ["sender"] = _url
                };

                if (!string.IsNullOrEmpty(args)) messageObject["args"] = args;

                var messageJson = messageObject.ToString(Formatting.None);
                _sendQueue.Enqueue(messageJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Serialization error: {ex.Message}");
            }
        }

        public void Stop()
        {
            Logger.LogInfo("Stopping GameClientSocket...");
            _shutdownTokenSource?.Cancel();

            lock (_socketLock)
            {
                if (_clientSocket != null)
                {
                    _clientSocket.Close(CloseStatusCode.Normal);
                    _clientSocket = null;
                }
            }

            _shutdownTokenSource?.Dispose();
            _shutdownTokenSource = null;
        }
    }
}