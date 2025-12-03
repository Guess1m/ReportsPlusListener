namespace ReportsPlus.Utils.WebSocket.Actions.Continuous{
    public interface IContinuousAction{
        void Execute(GameClientSocket client);
    }
}