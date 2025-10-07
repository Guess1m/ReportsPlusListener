using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.Keybindings{
    public interface IKeybindingAction{
        string Name { get; }
        void   Execute(IncomingRequest request);
    }
}