using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using Rage;

namespace ReportsPlus.Utils{
    public static class PedDataHelper{
        public static JObject GeneratePedData(Ped ped)
        {
            if (!ped || !ped.Exists()) return null;

            var pedData = ped.GetPedData();
            if (pedData == null) return null;

            // TODO: These must be added back
            // var height                 = pedData.Height.ToString() ?? null;
            // var weight                 = pedData.Weight.ToString() ?? null;

            var licenseStatus = pedData.DriversLicenseState.ToString() ?? null;

            string licenseExp = null;
            switch (licenseStatus.ToLower())
            {
                case "valid":
                case "suspended":
                    licenseExp = Misc.GenerateValidLicenseExpirationDate();
                    break;
                case "expired":
                    licenseExp = Misc.GenerateExpiredLicenseExpirationDate(3);
                    break;
            }

            var licenseExpiration      = licenseExp ?? string.Empty;
            var pedName                = pedData.FullName ?? string.Empty;
            var pedModel               = Misc.FindPedModel(ped) ?? string.Empty;
            var pedDob                 = pedData.Birthday.Month + "/" + pedData.Birthday.Day + "/" + pedData.Birthday.Year;
            var gender                 = pedData.Gender.ToString() ?? string.Empty;
            var isPolice               = ped.RelationshipGroup == "COP" ? "true" : "false";
            var address                = Misc.GetPedAddress(ped) ?? string.Empty;
            var isWanted               = pedData.Wanted.ToString() ?? string.Empty;
            var weaponPermitType       = pedData.WeaponPermit?.PermitType.ToString() ?? string.Empty;
            var weaponPermitStatus     = pedData.WeaponPermit?.Status.ToString() ?? string.Empty;
            var weaponPermitExpiration = pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty;
            var fishPermitStatus       = pedData.FishingPermit?.Status.ToString() ?? string.Empty;
            var timesStopped           = pedData.TimesStopped.ToString() ?? string.Empty;
            var fishPermitExpiration   = pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty;
            var huntPermitStatus       = pedData.HuntingPermit?.Status.ToString() ?? string.Empty;
            var huntPermitExpiration   = pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty;
            var isOnParole             = pedData.IsOnParole.ToString() ?? string.Empty;
            var isOnProbation          = pedData.IsOnProbation.ToString() ?? string.Empty;

            var pedJson = new JObject
            {
                ["entityId"]               = (int)ped.Handle.Value,
                ["name"]                   = pedName,
                ["pedModel"]               = pedModel,
                ["birthday"]               = pedDob,
                ["gender"]                 = gender,
                ["isPolice"]               = isPolice,
                ["address"]                = address,
                ["isWanted"]               = isWanted,
                ["licenseStatus"]          = licenseStatus,
                ["licenseExpiration"]      = licenseExpiration,
                ["weaponPermitType"]       = weaponPermitType,
                ["weaponPermitStatus"]     = weaponPermitStatus,
                ["weaponPermitExpiration"] = weaponPermitExpiration,
                ["fishPermitStatus"]       = fishPermitStatus,
                ["timesStopped"]           = timesStopped,
                ["fishPermitExpiration"]   = fishPermitExpiration,
                ["huntPermitStatus"]       = huntPermitStatus,
                ["huntPermitExpiration"]   = huntPermitExpiration,
                ["isOnParole"]             = isOnParole,
                ["isOnProbation"]          = isOnProbation
            };

            return pedJson;
        }
    }
}