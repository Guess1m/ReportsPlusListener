namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public interface DynamicEvent{
        // The Topic acts as the "Event Name" (What is subscribed to in client)
        string Topic { get; }

        // The data to send. 'object' so can return a Dictionary, JObject, etc.
        object GetPayload();
    }
}