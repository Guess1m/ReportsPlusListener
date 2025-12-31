using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using Rage;

namespace ReportsPlus.Utils.WorldData{
    public static class PedDataHelper{
        /**
         * Generates a JObject containing comprehensive data for a given Ped entity.
         * Validates the entity existence before processing.
         *
         * @param ped The Rage.Ped entity to process.
         * @return A JObject containing the ped's data, or null if the ped is invalid or data is missing.
         */
        public static JObject GeneratePedData(Ped ped)
        {
            if (!ped || !ped.Exists()) return null;

            var pedData = ped.GetPedData();
            if (pedData == null) return null;

            // Pass the handle if the ped exists, otherwise null
            return GeneratePedDataFromObject(pedData, (int)ped.Handle.Value, ped);
        }

        /**
         * Generates a JObject from a raw PedData object.
         * Used for generating data for entities that may not be physically present (e.g., vehicle owners).
         *
         * @param pedData The data object containing civilian information.
         * @param entityHandle The optional entity handle ID.
         * @param physicalPed The optional physical Ped entity (used for calculating dynamic flags like 'isPolice').
         * @return A JObject constructed from the provided data.
         */
        public static JObject GeneratePedDataFromObject(PedData pedData, int? entityHandle, Ped physicalPed = null)
        {
            if (pedData == null) return null;

            var pedJson = new JObject
            {
                // -- Identification --
                ["entityId"] = entityHandle ?? -1,
                ["identification"] = new JObject
                {
                    ["name"]                = pedData.FullName ?? string.Empty,
                    ["address"]             = physicalPed != null ? Misc.Misc.GetPedAddress(physicalPed) ?? string.Empty : string.Empty,
                    ["pedModel"]            = physicalPed != null ? Misc.Misc.FindPedModel(physicalPed) ?? string.Empty : string.Empty,
                    ["birthday"]            = pedData.Birthday.Month.ToString("D2") + "/" + pedData.Birthday.Day.ToString("D2") + "/" + pedData.Birthday.Year,
                    ["gender"]              = pedData.Gender.ToString() ?? string.Empty,
                    ["height"]              = string.Empty,
                    ["weight"]              = string.Empty,
                    ["eyeColor"]            = string.Empty,
                    ["hairColor"]           = string.Empty,
                    ["knownAliases"]        = string.Empty,
                    ["ethnicity"]           = string.Empty,
                    ["distinguishingMarks"] = string.Empty,
                    ["citizenshipStatus"]   = string.Empty,
                    ["maritalStatus"]       = string.Empty,
                    ["disabilityStatus"]    = string.Empty,
                    ["isPolice"]            = physicalPed != null && physicalPed.RelationshipGroup == "COP" ? "true" : "false"
                },

                // -- Criminal History --
                ["judicialStatus"] = new JObject
                {
                    ["isWanted"] = pedData.Wanted.ToString() ?? string.Empty,
                    ["warrantInfo"] = new JObject
                    {
                        ["warrantNumber"] = string.Empty,
                        ["dateIssued"]    = string.Empty,
                        ["issuingAgency"] = string.Empty,
                        ["warrantCharge"] = string.Empty,
                        ["bailAmount"]    = string.Empty
                    },
                    ["paroleInfo"] = new JObject
                    {
                        ["isOnParole"]         = pedData.IsOnParole.ToString() ?? string.Empty,
                        ["paroleStartDate"]    = string.Empty,
                        ["paroleEndDate"]      = string.Empty,
                        ["paroleRestrictions"] = string.Empty,
                        ["paroleAgency"]       = string.Empty,
                        ["paroleOfficer"]      = string.Empty,
                        ["paroleOfficerEmail"] = string.Empty
                    },
                    ["probationInfo"] = new JObject
                    {
                        ["isOnProbation"]         = pedData.IsOnProbation.ToString() ?? string.Empty,
                        ["probationStartDate"]    = string.Empty,
                        ["probationEndDate"]      = string.Empty,
                        ["probationRestrictions"] = string.Empty,
                        ["probationAgency"]       = string.Empty,
                        ["probationOfficer"]      = string.Empty,
                        ["probationOfficerEmail"] = string.Empty
                    },
                    ["timesStopped"]           = pedData.TimesStopped.ToString() ?? string.Empty,
                    ["restrainingOrderActive"] = string.Empty
                },

                // -- Licenses and Permits --
                ["licensesAndPermits"] = new JObject
                {
                    ["driversLicense"] = new JObject
                    {
                        ["status"]        = pedData.DriversLicenseState.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["dlclass"]       = string.Empty
                    },
                    ["weaponPermit"] = new JObject
                    {
                        ["type"]          = pedData.WeaponPermit?.PermitType.ToString() ?? string.Empty,
                        ["status"]        = pedData.WeaponPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["wpclass"]       = string.Empty
                    },
                    ["fishingPermit"] = new JObject
                    {
                        ["status"]        = pedData.FishingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["huntingPermit"] = new JObject
                    {
                        ["status"]        = pedData.HuntingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["boatingPermit"] = new JObject
                    {
                        ["status"]        = string.Empty,
                        ["expiration"]    = string.Empty,
                        ["licenseNumber"] = string.Empty
                    }
                }
            };
            return pedJson;
        }
    }
}