using ReportsPlus.Messages;

namespace ReportsPlus.Updates
{
    public interface IWebSocketAction
    {
        string Name { get; }
        void Execute(IncomingRequest request);
    }
}