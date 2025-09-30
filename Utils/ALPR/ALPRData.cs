using Rage;

namespace ReportsPlus.Utils.ALPR
{
    public static partial class ALPRUtils
    {
        public enum AlprSetupType
        {
            All,
            Rear,
            Front
        }

        private struct VehicleData
        {
            public Vehicle Vehicle;
            public string LicensePlateLower;
            public bool IsPolice;
            public Vector3 Position;
        }

        private class ScannerConfig
        {
            public ScannerConfig(Vector3 position, Vector3 forward, float radius, PlateScanLocation scanLocation, ScannerPositionType positionType)
            {
                Position = position;
                Forward = forward;
                Radius = radius;
                ScanLocation = scanLocation;
                ScannerPositionType = positionType;
                Vehicle = Main.LocalPlayer.CurrentVehicle;
            }

            public Vector3 Position { get; }
            public Vector3 Forward { get; }
            public float Radius { get; }
            public PlateScanLocation ScanLocation { get; }
            public ScannerPositionType ScannerPositionType { get; }
            public Vehicle Vehicle { get; }
        }

        private enum PlateScanLocation
        {
            FrontLeft,
            FrontRight,
            RearLeft,
            RearRight
        }

        private enum ScannerPositionType
        {
            Front,
            Rear
        }

        private enum VehiclePlateType
        {
            Front,
            Rear
        }
    }
}