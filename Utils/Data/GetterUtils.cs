using System;
using System.IO;
using Rage;
using Rage.Native;
using StopThePed.API;
using static ReportsPlus.Main;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils
{
    public class GetterUtils
    {
        private static readonly int ExpiredProb = 25;
        private static readonly int NoneProb = 15;
        private static readonly int ValidProb = 60;

        public static string GetRegistration(Vehicle car)
        {
            switch (StopThePed.API.Functions.getVehicleRegistrationStatus(car))
            {
                case STPVehicleStatus.Expired:
                    return "Expired";
                case STPVehicleStatus.None:
                    return "None";
                case STPVehicleStatus.Valid:
                    return "Valid";
            }

            return "";
        }

        public static string GetInsuranceInfo(Vehicle car)
        {
            switch (StopThePed.API.Functions.getVehicleInsuranceStatus(car))
            {
                case STPVehicleStatus.Expired:
                    return "Expired";
                case STPVehicleStatus.None:
                    return "None";
                case STPVehicleStatus.Valid:
                    return "Valid";
            }

            return "";
        }

        public static string GetRandomVehicleStatus(int expiredChance, int noneChance, int validChance)
        {
            var totalChance = expiredChance + noneChance + validChance;

            if (totalChance != 100) throw new ArgumentException("The sum of the chances must equal 100.");

            var randomValue = new Random().Next(1, 101); // Generates a random number between 1 and 100

            if (randomValue <= expiredChance) return "Expired";

            if (randomValue <= expiredChance + noneChance) return "None";

            return "Valid";
        }

        public static string GetWorldCarData(Vehicle car)
        {
            var driver = car.Driver.Exists()
                ? Functions.GetPersonaForPed(car.Driver).FullName
                : "";
            var color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(car) != -1
                ? ""
                : $"{car.PrimaryColor.R}-{car.PrimaryColor.G}-{car.PrimaryColor.B}";
            string insurance;
            string registration;
            if (HasStopThePed)
            {
                insurance = GetInsuranceInfo(car);
                registration = GetRegistration(car);
            }
            else
            {
                insurance = GetRandomVehicleStatus(ExpiredProb, NoneProb, ValidProb);
                registration = GetRandomVehicleStatus(ExpiredProb, NoneProb, ValidProb);
            }

            return
                $"licensePlate={car.LicensePlate}&model={car.Model.Name}&isStolen={car.IsStolen}&isPolice={car.IsPoliceVehicle}&owner={Functions.GetVehicleOwnerName(car)}&driver={driver}&registration={insurance}&insurance={registration}&color={color}";
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

        public static string GetPedData(Ped ped)
        {
            var persona = Functions.GetPersonaForPed(ped);
            var birthday = $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}";
            var fullName = persona.FullName;
            string address;
            var licenseNum = Utils.GenerateLicenseNumber();
            var pedModel = GetPedModel(ped);

            if (!Utils.PedAddresses.ContainsKey(fullName))
            {
                address = GetRandomAddress();
                Utils.PedAddresses.Add(fullName, address);
            }
            else
            {
                address = Utils.PedAddresses[fullName];
            }

            return
                $"name={persona.FullName}&licenseNumber={licenseNum}&pedModel={pedModel}&birthday={birthday}&gender={persona.Gender}&address={address}&isWanted={persona.Wanted}&licenseStatus={persona.ELicenseState}&relationshipGroup={ped.RelationshipGroup.Name}";
        }

        public static string GetPedModel(Ped ped)
        {
            return ped.Model.Name;
        }

        public static string GetPedCurrentZoneName()
        {
            return Functions.GetZoneAtPosition(Game.LocalPlayer.Character.Position).RealAreaName;
        }

        public static void CreateTrafficStopObj(Vehicle vehicle)
        {
            if (vehicle.Exists())
            {
                var color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(vehicle) != -1
                    ? ""
                    : $"{vehicle.PrimaryColor.R}-{vehicle.PrimaryColor.G}-{vehicle.PrimaryColor.B}";
                var owner = Functions.GetVehicleOwnerName(vehicle);
                var plate = vehicle.LicensePlate;
                var model = vehicle.Model.Name;
                var isStolen = vehicle.IsStolen;
                var isPolice = vehicle.IsPoliceVehicle;
                var registeration = GetRegistration(vehicle);
                var insurance = GetInsuranceInfo(vehicle);
                var street = World.GetStreetName(LocalPlayer.Position);
                var area = GetPedCurrentZoneName();

                var oldFile = File.ReadAllText($"{FileDataFolder}/trafficStop.data");
                if (oldFile.Contains(plate)) return;

                var vehicleData =
                    $"licensePlate={plate}&model={model}&isStolen={isStolen}&isPolice={isPolice}&owner={owner}&registration={registeration}&insurance={insurance}&color={color}&street={street}&area={area}";

                File.WriteAllText($"{FileDataFolder}/trafficStop.data", $"{vehicleData}");
                Game.LogTrivial("ReportsPlusListener: Traffic stop added.");
            }
        }

        public static void CreatePedObj(Ped ped)
        {
            if (ped.Exists())
            {
                var data = GetPedData(ped);
                var oldFile = File.ReadAllText($"{FileDataFolder}/worldPeds.data");
                if (oldFile.Contains(Functions.GetPersonaForPed(ped).FullName)) return;

                var addComma = oldFile.Length > 0 ? "," : "";

                File.WriteAllText($"{FileDataFolder}/worldPeds.data", $"{oldFile}{addComma}{data}");
            }
        }
    }
}