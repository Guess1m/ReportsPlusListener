namespace ReportsPlus.Utils.WebSocket.Messages
{
    // Interface for all messages
    public interface IMessage
    {
        string Type { get; }
        string Args { get; }
        string Data { get; }
        string Sender { get; }
    }
}