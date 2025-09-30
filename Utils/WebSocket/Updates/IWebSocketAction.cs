using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Updates
{
    public interface IWebSocketAction
    {
        string Name { get; }
        bool IsContinuous { get; }
        void Execute(IncomingRequest request);
    }
}