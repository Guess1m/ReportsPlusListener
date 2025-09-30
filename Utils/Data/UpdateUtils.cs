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

            var fullName = HasPolicingRedefined && HasCommonDataFramework ? GetValueMethods.GetFullNamePr(ped) : Functions.GetPersonaForPed(ped).FullName;

            var pedData = GetPedDataFromWorldPeds(fullName);

            if (pedData == null)
            {
                Game.LogTrivial($"ReportsPlusListener: Could not find ped '{fullName}' in worldPeds.data. currentID.xml will not be updated.");
                return;
            }

            pedData.TryGetValue("name", out var name);
            pedData.TryGetValue("birthday", out var birthday);
            pedData.TryGetValue("gender", out var gender);
            pedData.TryGetValue("address", out var address);
            pedData.TryGetValue("pedmodel", out var pedModel);
            pedData.TryGetValue("licensenumber", out var licenseNumber);
            pedData.TryGetValue("licenseexpiration", out var licenseExp);
            pedData.TryGetValue("height", out var height);
            pedData.TryGetValue("weight", out var weight);

            var newEntry = new XElement("ID", new XElement("Name", name ?? "N/A"), new XElement("Birthday", birthday ?? "N/A"), new XElement("Gender", gender ?? "N/A"), new XElement("Address", address ?? "N/A"), new XElement("PedModel", pedModel ?? "N/A"), new XElement("LicenseNumber", licenseNumber ?? "N/A"), new XElement("Expiration", licenseExp ?? "N/A"), new XElement("Height", height ?? "N/A"), new XElement("Weight", weight ?? "N/A"));

            var newDoc = new XDocument(new XElement("IDs"));
            newDoc.Root?.Add(newEntry);

            newDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            Game.LogTrivial($"ReportsPlusListener: Updated CurrentID DataFile from worldPeds.data for {name}");
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

            Game.LogTrivial($"ReportsPlusListener: Added [{newEntries.Count}] new vehicles to worldCars.data, vehicles no longer in world: [{MathUtils.RemoveOldPlates("worldCars.data", 600000)}] removed");
        }

        public static void RefreshPeds()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to refresh ped data; Invalid LocalPlayer");
                return;
            }

            var filePath = $"{FileDataFolder}/worldPeds.data";
            if (!File.Exists(filePath)) return;

            var fileContent = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(fileContent)) return;

            var pedsInFile = fileContent.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(entry =>
            {
                var match = Regex.Match(entry, "name=([^&]+)");
                return match.Success ? new { Name = match.Groups[1].Value, Entry = entry } : null;
            }).Where(p => p != null && !string.IsNullOrEmpty(p.Name)).GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Entry, StringComparer.OrdinalIgnoreCase);

            var nearbyPedNames = LocalPlayer.GetNearbyPeds(15).Where(p => p != null && p.Exists()).Select(p => HasPolicingRedefined && HasCommonDataFramework ? GetValueMethods.GetFullNamePr(p) : Functions.GetPersonaForPed(p).FullName).Where(name => !string.IsNullOrEmpty(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var removedCount = 0;

            foreach (var pedNameInFile in pedsInFile.Keys.ToList())
                if (!nearbyPedNames.Contains(pedNameInFile))
                {
                    pedsInFile.Remove(pedNameInFile);
                    removedCount++;
                }

            if (removedCount > 0)
            {
                var newContent = string.Join("|", pedsInFile.Values);
                File.WriteAllText(filePath, newContent);
                Game.LogTrivial($"ReportsPlusListener: Refreshed worldPeds.data. Peds no longer in world: [{removedCount}] removed.");
            }
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
            var currentCounty = MathUtils.ParseCountyString(Functions.GetZoneAtPosition(LocalPlayer.Position).County.ToString());

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