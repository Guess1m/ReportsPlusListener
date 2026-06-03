using Rage;
using Rage.Native;

namespace ReportsPlus.Utils.WorldData
{
    /// <summary>
    ///     Maps a vehicle's native GTA class index to the canonical class identifiers
    ///     the Java MDT uses for BOLO reason targeting (see
    ///     <c>ManageBoloSettings.getKnownVehicleClasses()</c>).
    /// </summary>
    public static class VehicleClassUtil
    {
        // GET_VEHICLE_CLASS (0x29439776AAA00A62) -> 0..22, index-aligned with GTA classes.
        private static readonly string[] ClassNames =
        {
            "COMPACT", // 0  Compacts
            "SEDAN", // 1  Sedans
            "SUV", // 2  SUVs
            "COUPE", // 3  Coupes
            "MUSCLE", // 4  Muscle
            "SPORTS_CLASSIC", // 5  Sports Classics
            "SPORTS", // 6  Sports
            "SUPER", // 7  Super
            "MOTORCYCLE", // 8  Motorcycles
            "OFFROAD", // 9  Off-road
            "INDUSTRIAL", // 10 Industrial
            "UTILITY", // 11 Utility
            "VAN", // 12 Vans
            "CYCLE", // 13 Cycles
            "BOAT", // 14 Boats
            "HELICOPTER", // 15 Helicopters
            "PLANE", // 16 Planes
            "SERVICE", // 17 Service
            "EMERGENCY", // 18 Emergency
            "MILITARY", // 19 Military
            "COMMERCIAL", // 20 Commercial
            "TRAIN", // 21 Trains
            "OPEN_WHEEL" // 22 Open Wheel
        };

        /// <summary>
        ///     Returns the canonical class name for a vehicle, or an empty string when
        ///     unavailable.
        /// </summary>
        public static string GetClassName(Vehicle vehicle)
        {
            if (!vehicle || !vehicle.Exists()) return string.Empty;

            try
            {
                var index = NativeFunction.Natives.GET_VEHICLE_CLASS<int>(vehicle);
                if (index >= 0 && index < ClassNames.Length) return ClassNames[index];
            }
            catch
            {
                // Native unavailable / invalid handle - fall through to empty.
            }

            return string.Empty;
        }
    }
}
