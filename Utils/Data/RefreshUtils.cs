using System;
using System.IO;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.GetterUtils;

namespace ReportsPlus.Utils.Data
{
    public static class RefreshUtils
    {
        public static void UpdateCurrentId(Ped ped)
        {
            if (!ped.Exists())
                return;

            var persona = Functions.GetPersonaForPed(ped);
            var fullName = persona.FullName;
            var pedModel = GetPedModel(ped);

            if (!Utils.PedAddresses.ContainsKey(fullName)) Utils.PedAddresses[fullName] = GetRandomAddress();

            var index = ped.IsInAnyVehicle(false) ? ped.SeatIndex + 2 : 0;

            var newEntry = new XElement("ID",
                new XElement("Name", fullName),
                new XElement("Birthday", $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}"),
                new XElement("Gender", persona.Gender),
                new XElement("Address", Utils.PedAddresses[fullName]),
                new XElement("PedModel", pedModel),
                new XElement("Index", index)
            );

            var newDoc = new XDocument(new XElement("IDs"));
            newDoc.Root.Add(newEntry);

            newDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            Game.LogTrivial("ReportsPlusListener: Updated currentID data file");
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

            for (var i = 0; i < allCars.Length; i++)
            {
                var car = allCars[i];
                if (car.Exists()) carsList[Array.IndexOf(allCars, car)] = GetWorldCarData(car);
            }

            File.WriteAllText($"{FileDataFolder}/worldCars.data", string.Join(",", carsList));
            Game.LogTrivial("ReportsPlusListener: Updated veh data file");
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
                if (ped.Exists())
                    pedsList[Array.IndexOf(allPeds, ped)] = GetPedData(ped);

            File.WriteAllText($"{FileDataFolder}/worldPeds.data", string.Join(",", pedsList));

            Game.LogTrivial("ReportsPlusListener: Updated ped data file");
        }

        public static void RefreshStreet()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: Failed to update location data; Invalid LocalPlayer");
                return;
            }

            var currentStreet = World.GetStreetName(LocalPlayer.Position);
            var currentZone = GetPedCurrentZoneName();

            File.WriteAllText($"{FileDataFolder}/location.data", currentStreet + ", " + currentZone);

            Game.LogTrivial("ReportsPlusListener: Updated location data file");
        }
    }
}