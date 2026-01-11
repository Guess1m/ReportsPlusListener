using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using Rage;
using Rage.Native;

namespace ReportsPlus.Utils.WorldData{
    public static class VehicleDataHelper{
        /// <summary>
        ///     Generates a detailed JSON representation of a vehicle using Policing Redefined/CDF data structures.
        /// </summary>
        /// <param name="vehicle">The vehicle entity to process.</param>
        /// <returns>A <see cref="JObject" /> containing technical specs, ownership (linked via CDF), and legal status.</returns>
        public static JObject GenerateVehicleDataPR(Vehicle vehicle)
        {
            if (!vehicle || !vehicle.Exists()) return null;

            // Retrieve custom vehicle data from cdf
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
                ownerJson = PedDataHelper.GeneratePedDataFromObjectPR(vehData.Owner, null);
            }

            var vehicleJson = new JObject
            {
                ["entityId"] = (int)vehicle.Handle.Value,
                ["basics"] = new JObject
                {
                    ["plate"]            = vehicle.LicensePlate ?? string.Empty,
                    ["type"]             = string.Empty,
                    ["inspectionStatus"] = string.Empty,
                    ["model"]            = vehicle.Model.Name ?? string.Empty,
                    ["make"]             = Game.GetLocalizedString(NativeFunction.Natives.xF7AF4F159FF99F97<string>(vehicle.Model.Hash)) ?? string.Empty,
                    ["colorSpecific"]    = vehData.PrimaryColor ?? string.Empty,
                    ["color"]            = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(vehicle) != -1 ? string.Empty : $"{vehicle.PrimaryColor.R}-{vehicle.PrimaryColor.G}-{vehicle.PrimaryColor.B}",
                    ["vin"]              = vehData.Vin.ToString() ?? string.Empty,
                    ["isPolice"]         = vehicle.IsPoliceVehicle ? "true" : "false",
                    ["driver"]           = driverName
                },
                ["ownership"] = new JObject
                {
                    ["ownerName"] = ownerName,
                    ["ownerData"] = ownerJson ?? new JObject()
                },
                ["registration"] = new JObject
                {
                    ["status"]             = GetValueMethods.GetRegistrationPr(vehicle) ?? string.Empty,
                    ["expiration"]         = GetValueMethods.GetRegExpPr(vehicle) ?? string.Empty,
                    ["registrationNumber"] = string.Empty,
                    ["class"]              = string.Empty
                },
                ["insurance"] = new JObject
                {
                    ["status"]       = GetValueMethods.GetInsurancePr(vehicle) ?? string.Empty,
                    ["expiration"]   = GetValueMethods.GetInsExpPr(vehicle) ?? string.Empty,
                    ["coverage"]     = string.Empty,
                    ["policyNumber"] = string.Empty,
                    ["provider"]     = string.Empty
                },
                ["legalStatus"] = new JObject
                {
                    ["isStolen"] = vehData.IsStolen ? "true" : "false",
                    ["impounds"] = new JObject
                    {
                        ["count"]   = string.Empty,
                        ["history"] = string.Empty
                    },
                    ["flags"] = string.Empty,
                    ["stolenInfo"] = new JObject
                    {
                        ["dateReported"]    = string.Empty,
                        ["reportingAgency"] = string.Empty,
                        ["caseNumber"]      = string.Empty,
                        ["notes"]           = string.Empty
                    }
                }
            };
            return vehicleJson;
        }

        /// <summary>
        ///     Generates a detailed JSON representation of a vehicle using standard LSPDFR and Stop The Ped data.
        /// </summary>
        /// <param name="vehicle">The vehicle entity to process.</param>
        /// <returns>A <see cref="JObject" /> containing basic vehicle info, owner name, and status flags.</returns>
        public static JObject GenerateVehicleData(Vehicle vehicle)
        {
            if (!vehicle || !vehicle.Exists()) return null;

            // Determine driver name safely
            var driverName = string.Empty;
            if (vehicle.Driver && vehicle.Driver.Exists())
            {
                var driverPersona                     = Functions.GetPersonaForPed(vehicle.Driver);
                if (driverPersona != null) driverName = driverPersona.FullName ?? string.Empty;
            }

            var reg                                                                = string.Empty;
            if (Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.StopThePed) reg = GetValueMethods.GetRegistrationStp(vehicle);
            var ins                                                                = string.Empty;
            if (Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.StopThePed) ins = GetValueMethods.GetInsuranceStp(vehicle);

            var ownerNameStr = Functions.GetVehicleOwnerName(vehicle) ?? string.Empty;

            var vehicleJson = new JObject
            {
                ["entityId"] = (int)vehicle.Handle.Value,
                ["basics"] = new JObject
                {
                    ["plate"]            = vehicle.LicensePlate ?? string.Empty,
                    ["type"]             = string.Empty,
                    ["inspectionStatus"] = string.Empty,
                    ["model"]            = vehicle.Model.Name ?? string.Empty,
                    ["make"]             = Game.GetLocalizedString(NativeFunction.Natives.xF7AF4F159FF99F97<string>(vehicle.Model.Hash)) ?? string.Empty,
                    ["colorSpecific"]    = string.Empty, //TODO: Empty for now need to make converter to actual color string
                    ["color"]            = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(vehicle) != -1 ? string.Empty : $"{vehicle.PrimaryColor.R}-{vehicle.PrimaryColor.G}-{vehicle.PrimaryColor.B}",
                    ["vin"]              = string.Empty,
                    ["isPolice"]         = vehicle.IsPoliceVehicle ? "true" : "false",
                    ["driver"]           = driverName
                },
                ["ownership"] = new JObject
                {
                    ["ownerName"] = ownerNameStr,
                    ["ownerData"] = new JObject
                    {
                        ["entityId"] = -1,
                        ["identification"] = new JObject
                        {
                            ["name"] = ownerNameStr
                        }
                    }
                },
                ["registration"] = new JObject
                {
                    ["status"]             = reg,
                    ["expiration"]         = string.Empty,
                    ["registrationNumber"] = string.Empty,
                    ["class"]              = string.Empty
                },
                ["insurance"] = new JObject
                {
                    ["status"]       = ins,
                    ["expiration"]   = string.Empty,
                    ["coverage"]     = string.Empty,
                    ["policyNumber"] = string.Empty,
                    ["provider"]     = string.Empty
                },
                ["legalStatus"] = new JObject
                {
                    ["isStolen"] = vehicle.IsStolen.ToString() ?? string.Empty,
                    ["impounds"] = new JObject
                    {
                        ["count"]   = string.Empty,
                        ["history"] = string.Empty
                    },
                    ["flags"] = string.Empty,
                    ["stolenInfo"] = new JObject
                    {
                        ["dateReported"]    = string.Empty,
                        ["reportingAgency"] = string.Empty,
                        ["caseNumber"]      = string.Empty,
                        ["notes"]           = string.Empty
                    }
                }
            };
            return vehicleJson;
        }
    }
}