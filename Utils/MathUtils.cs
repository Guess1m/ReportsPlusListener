using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CommonDataFramework.Modules.PedDatabase;
using Rage;
using ReportsPlus.Utils.Menu;
using static ReportsPlus.Utils.ConfigUtils;

namespace ReportsPlus.Utils
{
    public static class MathUtils
    {
        public static int ExpiredProb = 20;
        public static int NoneProb = 10;
        public static int RevokedProb = 5;
        public static int ValidProb = 65;

        private static readonly ThreadLocal<Random> RandThreadLocal = new ThreadLocal<Random>(() => new Random());

        public static Random Rand => RandThreadLocal.Value;

        public static string GenerateModelForPed(string gender)
        {
            var maleModels = new List<string>
            {
                "[ig_zimbor][0][0]", "[mp_m_weed_01][0][0]", "[s_m_m_bouncer_01][0][0]", "[s_m_m_postal_02][0][0]",
                "[s_m_y_waretech_01][0][0]", "[a_m_m_eastsa_01][0][0]"
            };
            var femaleModels = new List<string>
            {
                "[a_f_m_bevhills_02][0][0]", "[a_f_y_femaleagent][0][0]", "[a_f_y_soucent_02][0][0]",
                "[csb_mrs_r][0][0]", "[mp_f_counterfeit_01][0][0]", "[mp_f_cardesign_01][0][0]"
            };

            return gender.ToLower().Equals("male")
                ? maleModels[Rand.Next(maleModels.Count)]
                : femaleModels[Rand.Next(femaleModels.Count)];
        }

        public static bool CheckProbability(int probability)
        {
            return Rand.Next(0, 100) < probability;
        }

        public static string GenerateLicenseNumber()
        {
            var licenseNum = new StringBuilder();
            using (var rng = new RNGCryptoServiceProvider())
            {
                var randomNumber = new byte[1];
                for (var i = 0; i < 10; i++)
                {
                    rng.GetBytes(randomNumber);
                    var digit = randomNumber[0] % 10;
                    licenseNum.Append(digit);
                }
            }

            return licenseNum.ToString();
        }

        public static string GenerateCalloutId()
        {
            return Rand.Next(10000, 100000).ToString();
        }

        public static string GenerateVin()
        {
            var vinBuilder = new StringBuilder();
            var characters = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

            for (var i = 0; i < 17; i++)
            {
                var nextChar = characters[Rand.Next(characters.Length)];
                vinBuilder.Append(nextChar);
            }

            return vinBuilder.ToString();
        }

        public static string GetRandomAddress()
        {
            var chosenList = Rand.Next(2) == 0 ? Misc.LosSantosAddresses : Misc.BlaineCountyAddresses;
            var index = Rand.Next(chosenList.Count);
            var addressNumber = Rand.Next(1000).ToString().PadLeft(3, '0');
            var address = $"{addressNumber} {chosenList[index]}";

            while (Misc.PedAddresses.ContainsValue(address))
            {
                index = Rand.Next(chosenList.Count);
                addressNumber = Rand.Next(1000).ToString().PadLeft(3, '0');
                address = $"{addressNumber} {chosenList[index]}";
            }

            return address;
        }

        public static string GenerateValidLicenseExpirationDate()
        {
            var maxYears = 4;
            var currentDate = DateTime.Now;
            var expirationDate = currentDate.AddYears(maxYears).AddDays(Rand.Next(0, 365));
            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static string GenerateExpiredLicenseExpirationDate(int maxYears)
        {
            var maxYearsAgo = maxYears;
            var currentDate = DateTime.Now;

            long minDaysAgo = 1;
            var maxDaysAgo = maxYearsAgo * 365L + maxYearsAgo / 4;
            long randomDaysAgo = Rand.Next((int)minDaysAgo, (int)maxDaysAgo + 1);

            var expirationDate = currentDate.AddDays(-randomDaysAgo);

            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static string GetRandomVehicleStatus(int expiredChance, int noneChance, int validChance,
            int revokedChance)
        {
            var totalChance = expiredChance + noneChance + validChance + revokedChance;

            if (totalChance != 100) throw new ArgumentException("The sum of the chances must equal 100.");

            var randomValue = Rand.Next(1, 101);

            if (randomValue <= expiredChance) return "Expired";
            if (randomValue <= expiredChance + noneChance) return "None";
            return randomValue <= expiredChance + noneChance + validChance ? "Valid" : "Revoked";
        }

        public static string ParseCountyString(string input)
        {
            return Regex.Replace(input, "(?<!^)([A-Z])", " $1");
        }

        public static string GetPedAddress(Ped ped)
        {
            if (ped == null) return GetRandomAddress();
            if (ped.GetPedData() == null) return GetRandomAddress();

            var addressBuilder = new StringBuilder();
            addressBuilder.Append(ped.GetPedData().Address.AddressPostal.Number).Append(" ");
            addressBuilder.Append(ped.GetPedData().Address.StreetName).Append(", ");
            addressBuilder.Append(ped.GetPedData().Address.Zone.RealAreaName).Append(", ");
            addressBuilder.Append(ParseCountyString(ped.GetPedData().Address.Zone.County.ToString()));

            return addressBuilder.ToString();
        }

        public static List<Dictionary<string, string>> ParseVehicleData(string input)
        {
            var vehicles = new List<Dictionary<string, string>>();

            var vehicleEntries = input.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in vehicleEntries)
            {
                var vehicleData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var fields = entry.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var field in fields)
                {
                    var parts = field.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    vehicleData[key] = value;
                }

                if (vehicleData.Count > 0) vehicles.Add(vehicleData);
            }

            return vehicles;
        }

        public static bool ShouldSetValid()
        {
            if (!MenuProcessing.ALPRActive) return false;
            if (ALPRSuccessfulScanProbability <= 100) return Rand.Next(0, 100) > ALPRSuccessfulScanProbability;
            Game.LogTrivial(
                "ReportsPlusListener: Error: successPercentage > 100; its: " + ALPRSuccessfulScanProbability);
            ALPRSuccessfulScanProbability = 20;

            return Rand.Next(0, 100) < ALPRSuccessfulScanProbability;
        }

        // TODO: check if pr is being used, if so use getVehicleData.licenseplate instead
        public static int RemoveOldPlates(string filePath, int interval)
        {
            var fullPath = $"{Main.FileDataFolder}/{filePath}";
            var fileContent = File.ReadAllText(fullPath);
            if (string.IsNullOrWhiteSpace(fileContent)) return 0;

            var existingPlates = World.GetAllVehicles()
                .Where(v => v.Exists())
                .Select(v => v.LicensePlate.Trim().ToLower())
                .ToHashSet();

            var vehicles = ParseVehicleData(fileContent).ToList();
            var now = DateTimeOffset.Now;
            var removed = 0;

            foreach (var vehicle in vehicles.ToList())
            {
                var isPresent = false;
                if (vehicle.TryGetValue("licenseplate", out var plate))
                {
                    plate = plate.Trim().ToLower();
                    isPresent = existingPlates.Contains(plate);
                }

                var isExpired = false;
                if (vehicle.TryGetValue("timescanned", out var timeScannedStr) &&
                    DateTimeOffset.TryParse(timeScannedStr, out var timeScanned))
                {
                    var timeDifference = now - timeScanned;
                    isExpired = timeDifference.TotalMilliseconds > interval || timeDifference.TotalMilliseconds < 0;
                }
                else
                {
                    isExpired = true;
                }

                if (isPresent && !isExpired) continue;
                vehicles.Remove(vehicle);
                removed++;
            }

            var newContent = string.Join("|", vehicles.Select(v =>
                string.Join("&", v.Select(kvp => $"{kvp.Key}={kvp.Value}"))));
            File.WriteAllText(fullPath, newContent);

            return removed;
        }
    }
}