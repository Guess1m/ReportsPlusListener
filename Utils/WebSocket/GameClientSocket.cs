using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.WebSocket.Messages;
using WebSocketSharp;
using Logger = ReportsPlus.Utils.Logging.Logger;

namespace ReportsPlus.Utils.WebSocket
{
    public class GameClientSocket
    {
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly object _socketLock = new object();
        private readonly string _url;
        private bool _silent;

        private WebSocketSharp.WebSocket _clientSocket;
        private Task _senderTask;
        private CancellationTokenSource _shutdownTokenSource;

        public GameClientSocket(string hostname, int port)
        {
            _url = $"ws://{hostname}:{port}/ws/game-client";
        }

        public bool IsConnected
        {
            get
            {
                var socket = _clientSocket;
                return socket is { ReadyState: WebSocketState.Open };
            }
        }

        public bool IsConnecting
        {
            get
            {
                var socket = _clientSocket;
                return socket is { ReadyState: WebSocketState.Connecting };
            }
        }

        public event Action<IncomingRequest> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;

        /// <summary>
        ///     Starts the background sender loop and initiates the first connection attempt.
        /// </summary>
        public void Start(bool silent = false)
        {
            _silent = silent;
            if (_shutdownTokenSource is { IsCancellationRequested: false }) return;

            _shutdownTokenSource = new CancellationTokenSource();
            var token = _shutdownTokenSource.Token;

            if (!_silent) Logger.LogInfo("Starting background socket tasks...");

            _senderTask = Task.Run(() => SenderLoop(token), token);
            Task.Run(AttemptConnection);
        }

        /// <summary>
        ///     Manually triggers a connection attempt if the socket is currently disconnected.
        /// </summary>
        private void AttemptConnection()
        {
            var socket = _clientSocket;
            if (socket is { ReadyState: WebSocketState.Open })
            {
                Logger.LogWarning("AttemptConnection called, but socket is already open.");
                return;
            }

            try
            {
                InitializeSocket();
                if (!_silent) Logger.LogInfo($"Attempting to connect to {_url}...");

                WebSocketSharp.WebSocket socketToConnect;
                lock (_socketLock)
                {
                    socketToConnect = _clientSocket;
                }

                socketToConnect?.ConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Connection attempt failed: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        ///     Disposes of the existing socket and initializes a new instance with event handlers.
        /// </summary>
        private void InitializeSocket()
        {
            lock (_socketLock)
            {
                if (_clientSocket != null)
                {
                    _clientSocket.OnOpen -= OnSocketOpen;
                    _clientSocket.OnMessage -= OnSocketMessage;
                    _clientSocket.OnError -= OnSocketError;
                    _clientSocket.OnClose -= OnSocketClose;

                    if (_clientSocket.ReadyState == WebSocketState.Open || _clientSocket.ReadyState == WebSocketState.Connecting)
                    {
                        _clientSocket.CloseAsync();
                    }
                }

                _clientSocket = new WebSocketSharp.WebSocket(_url);

                _clientSocket.OnOpen += OnSocketOpen;
                _clientSocket.OnMessage += OnSocketMessage;
                _clientSocket.OnError += OnSocketError;
                _clientSocket.OnClose += OnSocketClose;
            }
        }

        /// <summary>
        ///     Event handler for a successful socket connection.
        /// </summary>
        private void OnSocketOpen(object sender, EventArgs e)
        {
            Logger.LogInfo("Connected successfully.");
            OnConnected?.Invoke();
        }

        /// <summary>
        ///     Event handler for incoming raw messages; parses them into IncomingRequest objects.
        /// </summary>
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

        /// <summary>
        ///     Event handler for socket-level errors.
        /// </summary>
        private void OnSocketError(object sender, ErrorEventArgs e)
        {
            Logger.LogError($"WebSocket error: {e.Message}");
        }

        /// <summary>
        ///     Event handler for socket closure.
        /// </summary>
        private void OnSocketClose(object sender, CloseEventArgs e)
        {
            Logger.LogError($"Disconnected. Code: {e.Code}, Reason: {e.Reason}");
            OnDisconnected?.Invoke();
        }

        /// <summary>
        ///     An asynchronous loop that consumes the send queue and transmits data to the server.
        ///     Error handling to prevent the loop from crashing.
        /// </summary>
        /// <param name="token">Cancellation token to stop the loop.</param>
        private async Task SenderLoop(CancellationToken token)
        {
            if (!_silent) Logger.LogInfo("Background SenderLoop started.");
            while (!token.IsCancellationRequested)
            {
                if (IsConnected && _sendQueue.TryDequeue(out var message))
                {
                    try
                    {
                        WebSocketSharp.WebSocket socketToSend;
                        lock (_socketLock)
                        {
                            socketToSend = _clientSocket;
                        }

                        if (socketToSend is { ReadyState: WebSocketState.Open })
                        {
                            socketToSend.SendAsync(message, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"SenderLoop Error: Failed to send message. {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(10, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }

            if (!_silent) Logger.LogWarning("Background SenderLoop stopped.");
        }

        /// <summary>
        ///     Enqueues a structured message to be sent to the server.
        /// </summary>
        /// <param name="type">The message type identifier.</param>
        /// <param name="data">The JSON payload data.</param>
        /// <param name="args">Optional additional arguments.</param>
        public void Send(string type, JToken data, string args = "")
        {
            try
            {
                var messageObject = new JObject
                {
                    ["type"] = type,
                    ["data"] = data,
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

        /// <summary>
        ///     Stops the sender loop and closes the socket connection gracefully.
        /// </summary>
        public void Stop(bool silent = false)
        {
            if (!silent) Logger.LogInfo("Stopping GameClientSocket...");
            _shutdownTokenSource?.Cancel();

            WebSocketSharp.WebSocket socketToClose;
            lock (_socketLock)
            {
                socketToClose = _clientSocket;
                _clientSocket = null;
            }

            socketToClose?.CloseAsync();

            _shutdownTokenSource?.Dispose();
            _shutdownTokenSource = null;
        }
    }
}