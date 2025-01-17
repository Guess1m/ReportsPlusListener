using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CommonDataFramework.Modules.PedDatabase;
using Rage;

namespace ReportsPlus.Utils.Data
{
    public static class MathUtils
    {
        public const int ExpiredProb = 20;
        public const int NoneProb = 10;
        public const int RevokedProb = 5;
        public const int ValidProb = 65;

        public static HashSet<string> GetPermitTypeBasedOnChances(int chanceConcealed, int chanceOpen, int chanceBoth)
        {
            var totalChance = chanceConcealed + chanceOpen + chanceBoth;

            if (totalChance != 100)
            {
                chanceConcealed = 33;
                chanceOpen = 33;
                chanceBoth = 34;
            }

            var random = new Random();
            var roll = random.Next(1, 101);

            var result = new HashSet<string>();

            if (roll <= chanceConcealed)
                result.Add("concealed");
            else if (roll <= chanceConcealed + chanceOpen)
                result.Add("open");
            else if (roll <= chanceConcealed + chanceOpen + chanceBoth) result.Add("both");

            return result;
        }

        public static string GetRandomWeaponPermitType()
        {
            var random = new Random();

            const int ccwPermitProbability = 30;

            var randomValue = random.Next(0, 100);

            return randomValue < ccwPermitProbability ? "CcwPermit" : "FflPermit";
        }

        public static bool CalculateTrueFalseProbability(string percentage)
        {
            if (!int.TryParse(percentage, out var percentage1) || percentage1 < 0 || percentage1 > 100)
                percentage1 = 50;

            var random = new Random();
            return random.Next(100) < percentage1;
        }

        public static string CalculateLicenseStatus(int chanceValid, int chanceExpired, int chanceSuspended)
        {
            var totalChance = chanceValid + chanceSuspended + chanceExpired;

            if (totalChance != 100)
            {
                chanceValid = 55;
                chanceExpired = 22;
                chanceSuspended = 23;
            }

            var random = new Random();
            var roll = random.Next(1, 101);

            if (roll <= chanceValid) return "valid";

            return roll <= chanceValid + chanceSuspended ? "suspended" : "expired";
        }

        public static bool CheckProbability(int probability)
        {
            var random = new Random();
            return random.Next(0, 100) < probability;
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
            return new Random().Next(10000, 100000).ToString();
        }

        public static string GenerateVin()
        {
            var random = new Random();
            var vinBuilder = new StringBuilder();
            var characters = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

            for (var i = 0; i < 17; i++)
            {
                var nextChar = characters[random.Next(characters.Length)];
                vinBuilder.Append(nextChar);
            }

            return vinBuilder.ToString();
        }

        public static string GetRandomAddress()
        {
            var random = new Random();
            var chosenList = random.Next(2) == 0 ? Utils.LosSantosAddresses : Utils.BlaineCountyAddresses;
            var index = random.Next(chosenList.Count);
            var addressNumber = random.Next(1000).ToString().PadLeft(3, '0');
            var address = $"{addressNumber} {chosenList[index]}";

            while (Utils.PedAddresses.ContainsValue(address))
            {
                index = random.Next(chosenList.Count);
                addressNumber = random.Next(1000).ToString().PadLeft(3, '0');
                address = $"{addressNumber} {chosenList[index]}";
            }

            return address;
        }

        public static string GenerateValidLicenseExpirationDate()
        {
            var maxYears = 4;
            var currentDate = DateTime.Now;

            long minDaysAhead = 0;
            var maxDaysAhead = maxYears * 365L + maxYears / 4;
            var random = new Random();
            long randomDaysAhead = random.Next((int)minDaysAhead, (int)maxDaysAhead + 1);

            var expirationDate = currentDate.AddDays(randomDaysAhead);

            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static string GenerateExpiredLicenseExpirationDate(int maxYears)
        {
            var maxYearsAgo = maxYears;
            var currentDate = DateTime.Now;

            long minDaysAgo = 1;
            var maxDaysAgo = maxYearsAgo * 365L + maxYearsAgo / 4;
            var random = new Random();
            long randomDaysAgo = random.Next((int)minDaysAgo, (int)maxDaysAgo + 1);

            var expirationDate = currentDate.AddDays(-randomDaysAgo);

            return expirationDate.ToString("MM-dd-yyyy");
        }

        public static string GetRandomVehicleStatus(int expiredChance, int noneChance, int validChance,
            int revokedChance)
        {
            var totalChance = expiredChance + noneChance + validChance + revokedChance;

            if (totalChance != 100) throw new ArgumentException("The sum of the chances must equal 100.");

            var randomValue = new Random().Next(1, 101);

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
    }
}