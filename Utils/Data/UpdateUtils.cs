using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.WorldDataUtils;

namespace ReportsPlus.Utils.Data
{
    public static class UpdateUtils
    {
        public static void UpdateCurrentId(Ped ped)
        {
            if (!ped.Exists())
                return;

            var persona = Functions.GetPersonaForPed(ped);
            var fullName = persona.FullName;
            var pedModel = Misc.FindPedModel(ped);
            var gender = persona.Gender.ToString();
            string address;
            string licenseExp;
            string weight;

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                fullName = GetValueMethods.GetFullNamePr(ped);
                gender = GetValueMethods.GetGenderPr(ped);
                address = MathUtils.GetPedAddress(ped);
                if (!Misc.PedExpirations.TryGetValue(fullName, out licenseExp))
                {
                    licenseExp = GetValueMethods.GetLicenseExpiration(ped);
                    Misc.PedExpirations.Add(fullName, licenseExp);
                }
            }
            else
            {
                address = Misc.PedAddresses[fullName];
                if (!Misc.PedExpirations.TryGetValue(fullName, out licenseExp))
                {
                    var licenseStatus = persona.ELicenseState.ToString();
                    licenseExp = licenseStatus.ToLower() switch
                    {
                        //TODO: !important find every  use here and update it to include suspended, unlicensed etc.
                        // only issue with STP / probably base
                        "valid" => MathUtils.GenerateValidLicenseExpirationDate(),
                        "expired" => MathUtils.GenerateExpiredLicenseExpirationDate(3),
                        _ => ""
                    };

                    Misc.PedExpirations.Add(fullName, licenseExp);
                }
            }

            if (!Misc.PedLicenseNumbers.ContainsKey(fullName))
                Misc.PedLicenseNumbers[fullName] = MathUtils.GenerateLicenseNumber();
            if (!Misc.PedAddresses.ContainsKey(fullName))
                Misc.PedAddresses[fullName] = MathUtils.GetRandomAddress();

            var licenseNumber = Misc.PedLicenseNumbers[fullName];

            if (!Misc.PedHeights.TryGetValue(fullName, out var height))
            {
                var heightAndWeight = MathUtils.GenerateHeightAndWeight(gender);
                height = heightAndWeight[0];
                weight = heightAndWeight[1];
                Misc.PedHeights.Add(fullName, height);
                Misc.PedWeights.Add(fullName, weight);
            }
            else
            {
                if (!Misc.PedWeights.TryGetValue(fullName, out weight))
                {
                    var heightAndWeight = MathUtils.GenerateHeightAndWeight(gender);
                    weight = heightAndWeight[1];
                    Misc.PedWeights.Add(fullName, weight);
                }
            }

            var newEntry = new XElement("ID",
                new XElement("Name", fullName),
                new XElement("Birthday", $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}"),
                new XElement("Gender", gender),
                new XElement("Address", address),
                new XElement("PedModel", pedModel),
                new XElement("LicenseNumber", licenseNumber),
                new XElement("Expiration", licenseExp),
                new XElement("Height", height),
                new XElement("Weight", weight)
            );

            var newDoc = new XDocument(new XElement("IDs"));
            newDoc.Root?.Add(newEntry);

            newDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            Game.LogTrivial("ReportsPlusListener: Updated CurrentID DataFile");
        }

        public static void RefreshVehs()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to update worldCars.data; Invalid LocalPlayer");
                return;
            }

            var filePath = $"{FileDataFolder}/worldCars.data";
            var existingPlates = new HashSet<string>();

            if (File.Exists(filePath))
            {
                var existingContent = File.ReadAllText(filePath);
                foreach (var entry in existingContent.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var plateMatch = Regex.Match(entry, "licenseplate=([^&]+)");
                    if (plateMatch.Success) existingPlates.Add(plateMatch.Groups[1].Value);
                }
            }

            var allCars = LocalPlayer.GetNearbyVehicles(13);
            var newEntries = new List<string>();

            foreach (var car in allCars)
            {
                if (!car.Exists()) continue;

                var plate = car.LicensePlate;
                if (existingPlates.Contains(plate)) continue;
                var data = GetWorldCarData(car);
                if (data == null) continue;
                newEntries.Add(data);
                existingPlates.Add(plate);
            }

            if (newEntries.Count > 0)
            {
                var delimiter = File.Exists(filePath) && new FileInfo(filePath).Length > 0 ? "|" : "";
                File.AppendAllText(filePath, $"{delimiter}{string.Join("|", newEntries)}");
            }

            Game.LogTrivial(
                $"ReportsPlusListener: Added [{newEntries.Count}] new vehicles to worldCars.data, vehicles no longer in world: [{MathUtils.RemoveOldPlates("worldCars.data", 600000)}] removed");
        }

        public static void RefreshPeds()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to update ped data; Invalid LocalPlayer");
                return;
            }

            var filePath = $"{FileDataFolder}/worldPeds.data";
            var existingPeds = new Dictionary<string, string>();

            if (File.Exists(filePath))
            {
                var existingContent = File.ReadAllText(filePath);
                foreach (var entry in existingContent.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var pedIdMatch = Regex.Match(entry, "pedid=([^&]+)");
                    if (!pedIdMatch.Success) continue;
                    var pedId = pedIdMatch.Groups[1].Value;
                    existingPeds[pedId] = entry;
                }
            }

            var allPeds = LocalPlayer.GetNearbyPeds(13);
            var newEntries = new List<string>();

            foreach (var ped in allPeds)
            {
                if (ped == null || !ped.Exists()) continue;

                var pedId = $"ped_{ped.Handle}";

                if (existingPeds.ContainsKey(pedId)) continue;

                string pedData;
                if (HasPolicingRedefined && HasCommonDataFramework)
                    pedData = GetPedDataPr(ped);
                else
                    pedData = GetPedData(ped);

                if (pedData == null) continue;

                pedData = $"pedid={pedId}&{pedData}";

                newEntries.Add(pedData);
                existingPeds[pedId] = pedData;
            }

            var removedCount = 0;
            var activePedIds = allPeds
                .Where(p => p != null && p.Exists())
                .Select(p => $"ped_{p.Handle}")
                .ToHashSet();

            foreach (var pedId in existingPeds.Keys.ToList().Where(pedId => !activePedIds.Contains(pedId)))
            {
                existingPeds.Remove(pedId);
                removedCount++;
            }

            var newContent = string.Join("|", existingPeds.Values);
            File.WriteAllText(filePath, newContent);

            Game.LogTrivial(
                $"ReportsPlusListener: Added [{newEntries.Count}] new peds to worldPeds.data, peds no longer in world: [{removedCount}] removed");
        }

        public static void RefreshGameData()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to update GameData; Invalid LocalPlayer");
                return;
            }

            var currentStreet = World.GetStreetName(LocalPlayer.Position);
            var currentZone = GetPedCurrentZoneName();
            var currentCounty =
                MathUtils.ParseCountyString(Functions.GetZoneAtPosition(Game.LocalPlayer.Character.Position).County
                    .ToString());

            var fullLocation = currentStreet + ", " + currentZone + ", " + currentCounty;

            string timeString;
            try
            {
                timeString = World.DateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                timeString = "Unknown";
            }

            var gameData = $"location={fullLocation}|time={timeString}";

            File.WriteAllText($"{FileDataFolder}/gameData.data", gameData);

            Game.LogTrivial("ReportsPlusListener: Updated GameData File");
        }
    }
}