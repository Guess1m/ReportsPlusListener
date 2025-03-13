using System;
using System.IO;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.GetterUtils;

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
            var pedModel = Utils.FindPedModel(ped);
            var gender = persona.Gender.ToString();

            if (!Utils.PedLicenseNumbers.ContainsKey(fullName))
                Utils.PedLicenseNumbers[fullName] = MathUtils.GetRandomAddress();
            if (!Utils.PedAddresses.ContainsKey(fullName)) Utils.PedAddresses[fullName] = MathUtils.GetRandomAddress();

            var address = Utils.PedAddresses[fullName];
            var licenseNumber = Utils.PedLicenseNumbers[fullName];

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                gender = GetValueMethods.GetGenderPr(ped);
                fullName = GetValueMethods.GetFullNamePr(ped);
                address = MathUtils.GetPedAddress(ped);
            }

            var newEntry = new XElement("ID",
                new XElement("Name", fullName),
                new XElement("Birthday", $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}"),
                new XElement("Gender", gender),
                new XElement("Address", address),
                new XElement("PedModel", pedModel),
                new XElement("LicenseNumber", licenseNumber)
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

            var allCars = LocalPlayer.GetNearbyVehicles(15);
            var carsList = new string[allCars.Length];

            foreach (var car in allCars)
                if (car.Exists())
                    carsList[Array.IndexOf(allCars, car)] = GetWorldCarData(car);

            File.WriteAllText($"{FileDataFolder}/worldCars.data", string.Join("|", carsList));
            Game.LogTrivial("ReportsPlusListener: Updated Vehicle Data");
        }

        public static void RefreshPeds()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to update ped data; Invalid LocalPlayer");
                return;
            }

            var allPeds = LocalPlayer.GetNearbyPeds(15);
            var pedsList = new string[allPeds.Length];

            foreach (var ped in allPeds)
                if (ped != null)
                {
                    if (!ped.Exists()) continue;

                    string pedData;
                    if (HasPolicingRedefined && HasCommonDataFramework)
                        pedData = GetPedDataPr(ped);
                    else
                        pedData = GetPedData(ped);

                    if (pedData == null) continue;

                    pedsList[Array.IndexOf(allPeds, ped)] = pedData;
                }

            File.WriteAllText($"{FileDataFolder}/worldPeds.data", string.Join("|", pedsList));

            Game.LogTrivial("ReportsPlusListener: Updated Pedestrian Data");
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
                timeString = World.DateTime.ToShortTimeString();
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