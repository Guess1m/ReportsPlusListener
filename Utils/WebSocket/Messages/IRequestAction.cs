namespace ReportsPlus.Utils.WebSocket.Messages
{
    public interface IRequestAction
    {
        /// <summary>
        ///     Gets the unique identifier for the request action.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Executes the logic associated with the specific request action.
        /// </summary>
        /// <param name="client">The active game client socket for sending responses.</param>
        /// <param name="request">The incoming request containing data and arguments.</param>
        void Execute(GameClientSocket client, IncomingRequest request);
    }
}