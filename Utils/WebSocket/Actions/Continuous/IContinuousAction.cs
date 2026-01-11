namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public interface IContinuousAction{
        /// <summary>
        ///     Defines the execution logic for a continuous tracking or synchronization task.
        /// </summary>
        /// <param name="client">The active <see cref="GameClientSocket" /> instance used for communication.</param>
        void Execute(GameClientSocket client);
    }
}