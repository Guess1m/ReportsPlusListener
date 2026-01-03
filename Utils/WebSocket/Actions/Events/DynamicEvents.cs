using Rage;

namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public static class DynamicEvents{
        public class PanicButtonEvent : DynamicEvent{
            private readonly string _location;

            private readonly string _message;

            public PanicButtonEvent(string message)
            {
                _message  = message;
                _location = World.GetStreetName(Game.LocalPlayer.Character.Position);
            }

            public string Topic => "URGENT_ALERT";

            public object GetPayload()
            {
                return new
                {
                    message  = _message,
                    location = _location,
                    severity = "HIGH",                            // Triggers the red notification/sound in Java
                    officer  = "Officer " + Game.LocalPlayer.Name // Example data
                };
            }
        }
    }
}