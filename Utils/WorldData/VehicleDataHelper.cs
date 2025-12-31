using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using Newtonsoft.Json.Linq;
using Rage;
using Rage.Native;

// Assumed namespace for VehicleData

namespace ReportsPlus.Utils.WorldData{
    public static class VehicleDataHelper{
        /**
         * Generates a JObject containing detailed data for a given Vehicle entity.
         * Includes nested owner data using the CommonDataFramework direct link.
         *
         * @param vehicle The Rage.Vehicle entity to process.
         * @return A JObject containing the vehicle's data, or null if the vehicle is invalid.
         */
        public static JObject GenerateVehicleData(Vehicle vehicle)
        {
            if (!vehicle || !vehicle.Exists()) return null;

            // Retrieve custom vehicle data from the framework
            var vehData = vehicle.GetVehicleData();
            if (vehData == null) return null;

            // Determine driver name safely
            var driverName = string.Empty;
            if (vehicle.Driver && vehicle.Driver.Exists())
            {
                var driverData                     = vehicle.Driver.GetPedData();
                if (driverData != null) driverName = driverData.FullName ?? string.Empty;
            }

            // Determine Owner Data directly from CDF
            JObject ownerJson = null;
            var     ownerName = string.Empty;

            // Check if CDF has a linked owner object
            if (vehData.Owner != null)
            {
                ownerName = vehData.Owner.FullName ?? string.Empty;

                // Generate the full owner JSON using the helper, passing null for handle/phys-ped
                // because the owner might not be physically present.
                ownerJson = PedDataHelper.GeneratePedDataFromObject(vehData.Owner, null);
            }

            var vehicleJson = new JObject
            {
                ["entityId"] = (int)vehicle.Handle.Value,
                ["basics"] = new JObject
                {
                    ["plate"]    = vehicle.LicensePlate ?? string.Empty,
                    ["model"]    = vehicle.Model.Name ?? string.Empty,
                    ["make"]     = Game.GetLocalizedString(NativeFunction.Natives.xF7AF4F159FF99F97<string>(vehicle.Model.Hash)) ?? string.Empty,
                    ["color"]    = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(vehicle) != -1 ? string.Empty : $"{vehicle.PrimaryColor.R}-{vehicle.PrimaryColor.G}-{vehicle.PrimaryColor.B}",
                    ["vin"]      = string.Empty,
                    ["isPolice"] = vehicle.IsPoliceVehicle ? "true" : "false",
                    ["isStolen"] = vehicle.IsStolen ? "true" : "false",
                    ["driver"]   = driverName
                },
                ["ownership"] = new JObject
                {
                    ["ownerName"] = ownerName,
                    ["ownerData"] = ownerJson ?? new JObject()
                },
                ["registration"] = new JObject
                {
                    ["status"]     = GetValueMethods.GetRegistrationPr(vehicle) ?? string.Empty,
                    ["expiration"] = GetValueMethods.GetRegExpPr(vehicle) ?? string.Empty
                },
                ["insurance"] = new JObject
                {
                    ["status"]     = GetValueMethods.GetInsurancePr(vehicle) ?? string.Empty,
                    ["expiration"] = GetValueMethods.GetInsExpPr(vehicle) ?? string.Empty,
                    ["coverage"]   = string.Empty
                }
            };

            return vehicleJson;
        }
    }
}