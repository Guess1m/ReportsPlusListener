using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using Rage;

namespace ReportsPlus.Utils.WorldData{
    public static class PedDataHelper{
        public static JObject GeneratePedData(Ped ped)
        {
            if (!ped || !ped.Exists()) return null;

            var pedData = ped.GetPedData();
            if (pedData == null) return null;

            var pedJson = new JObject
            {
                // -- Identification --
                ["entityId"] = (int)ped.Handle.Value,
                ["identification"] = new JObject
                {
                    ["name"]              = pedData.FullName ?? string.Empty,
                    ["address"]           = Misc.Misc.GetPedAddress(ped) ?? string.Empty,
                    ["pedModel"]          = Misc.Misc.FindPedModel(ped) ?? string.Empty,
                    ["birthday"]          = pedData.Birthday.Month + "/" + pedData.Birthday.Day + "/" + pedData.Birthday.Year,
                    ["gender"]            = pedData.Gender.ToString() ?? string.Empty,
                    ["height"]            = string.Empty, // Empty
                    ["weight"]            = string.Empty, // Empty
                    ["eyeColor"]          = string.Empty, // Empty
                    ["knownAliases"]      = string.Empty, // Empty
                    ["citizenshipStatus"] = string.Empty, // Empty
                    ["maritalStatus"]     = string.Empty, // Empty
                    ["disabilityStatus"]  = string.Empty, // Empty, often top-level for immediate view
                    ["isPolice"]          = ped.RelationshipGroup == "COP" ? "true" : "false"
                },

                // -- Criminal History --
                ["judicialStatus"] = new JObject
                {
                    ["isWanted"] = pedData.Wanted.ToString() ?? string.Empty,
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
                    ["restrainingOrderActive"] = string.Empty // Empty
                },

                // -- Licenses and Permits --
                ["licensesAndPermits"] = new JObject
                {
                    ["driversLicense"] = new JObject
                    {
                        ["status"]        = pedData.DriversLicenseState.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty, // Empty
                        ["dlclass"]       = string.Empty  // Empty
                    },
                    ["weaponPermit"] = new JObject
                    {
                        ["type"]          = pedData.WeaponPermit?.PermitType.ToString() ?? string.Empty,
                        ["status"]        = pedData.WeaponPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty, // Empty
                        ["wpclass"]       = string.Empty  // Empty
                    },
                    ["fishingPermit"] = new JObject
                    {
                        ["status"]        = pedData.FishingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty // Empty
                    },
                    ["huntingPermit"] = new JObject
                    {
                        ["status"]        = pedData.HuntingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"]    = pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty // Empty
                    },
                    ["boatingPermit"] = new JObject
                    {
                        ["status"]        = string.Empty, // Empty
                        ["expiration"]    = string.Empty, // Empty
                        ["licenseNumber"] = string.Empty  // Empty
                    }
                }
            };
            return pedJson;
        }
    }
}