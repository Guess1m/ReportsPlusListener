namespace ReportsPlus.Utils.WebSocket.Messages{
    public interface IRequestAction{
        string Name { get; }
        void   Execute(GameClientSocket client, IncomingRequest request);
    }
}