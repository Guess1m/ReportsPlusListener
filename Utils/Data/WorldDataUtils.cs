using System;
using System.IO;
using CommonDataFramework.Modules.PedDatabase;
using Rage;
using Rage.Native;
using static ReportsPlus.Utils.Data.GetValueMethods;
using static ReportsPlus.Main;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils.Data
{
    public static class WorldDataUtils
    {
        public static string GetPedCurrentZoneName()
        {
            return Functions.GetZoneAtPosition(Game.LocalPlayer.Character.Position).RealAreaName;
        }

        public static string GetWorldCarData(Vehicle car)
        {
            var driver = car.Driver.Exists()
                ? Functions.GetPersonaForPed(car.Driver).FullName
                : "";
            var color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(car) != -1
                ? ""
                : $"{car.PrimaryColor.R}-{car.PrimaryColor.G}-{car.PrimaryColor.B}";

            var insurance = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb,
                MathUtils.ValidProb, MathUtils.RevokedProb);
            var registration = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb,
                MathUtils.ValidProb, MathUtils.RevokedProb);
            var stolen = car.IsStolen.ToString();
            var ownerAddress = MathUtils.GetRandomAddress();
            var owner = Functions.GetVehicleOwnerName(car);
            var vin = MathUtils.GenerateVin();
            var gender = MathUtils.Rand.Next(2) == 1 ? "Male" : "Female";
            var regexp = "";
            var insexp = "";

            var setValid = MathUtils.ShouldSetValid();

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                insurance = GetInsurancePr(car, setValid);
                registration = GetRegistrationPr(car, setValid);
                stolen = GetStolenPr(car, setValid);
                ownerAddress = GetOwnerAddressPr(car);
                owner = GetOwnerPr(car);
                vin = GetVinPr(car);
                regexp = GetRegExpPr(car);
                insexp = GetInsExpPr(car);
                gender = GetOwnerGenderPr(car);
            }
            else if (HasStopThePed)
            {
                insurance = GetInsuranceStp(car, setValid);
                registration = GetRegistrationStp(car, setValid);
            }
            else
            {
                registration = GetRegistrationBg(registration, setValid);
                insurance = GetInsuranceBg(insurance, setValid);
            }

            switch (registration.ToLower())
            {
                case "valid":
                    regexp = MathUtils.GenerateValidLicenseExpirationDate();
                    break;
                case "expired":
                    regexp = MathUtils.GenerateExpiredLicenseExpirationDate(3);
                    break;
                case "none":
                case "revoked":
                    regexp = "";
                    break;
            }

            switch (insurance.ToLower())
            {
                case "valid":
                    insexp = MathUtils.GenerateValidLicenseExpirationDate();
                    break;
                case "expired":
                    insexp = MathUtils.GenerateExpiredLicenseExpirationDate(3);
                    break;
                case "none":
                case "revoked":
                    insexp = "";
                    break;
            }

            var ownerModel = MathUtils.GenerateModelForPed(gender);

            return
                $"licenseplate={car.LicensePlate}&model={car.Model.Name}&regexp={regexp}&insexp={insexp}&vin={vin}&isstolen={stolen}&ispolice={car.IsPoliceVehicle}&owner={owner}&ownermodel={ownerModel}&ownergender={gender}&owneraddress={ownerAddress}&driver={driver}&registration={registration}&insurance={insurance}&color={color}&timescanned={DateTime.Now:o}";
        }

        public static void CreateVehicleObj(Vehicle vehicle)
        {
            if (!vehicle.Exists()) return;

            var oldFile = File.ReadAllText($"{FileDataFolder}/worldCars.data");
            if (oldFile.Contains(vehicle.LicensePlate)) return;

            var data = GetWorldCarData(vehicle);
            if (data == null) return;

            var delimiter = oldFile.Length > 0 ? "|" : "";

            File.WriteAllText($"{FileDataFolder}/worldCars.data", $"{oldFile}{delimiter}{data}");
        }

        public static string GetPedDataPr(Ped ped)
        {
            if (ped == null) return null;

            var pedData = ped.GetPedData();
            if (pedData == null) return null;

            var address = MathUtils.GetPedAddress(ped);
            string licenseNum;

            if (!Misc.PedLicenseNumbers.ContainsKey(pedData.FullName))
            {
                licenseNum = MathUtils.GenerateLicenseNumber();
                Misc.PedLicenseNumbers[pedData.FullName] = licenseNum;
            }
            else
            {
                licenseNum = Misc.PedLicenseNumbers[pedData.FullName];
            }

            return
                $"name={pedData.FullName}" +
                $"&licensenumber={licenseNum}" +
                $"&pedmodel={Misc.FindPedModel(ped)}" +
                $"&birthday={pedData.Birthday.Month}/{pedData.Birthday.Day}/{pedData.Birthday.Year}" +
                $"&gender={pedData.Gender.ToString() ?? ""}" +
                $"&address={address}" +
                $"&iswanted={pedData.Wanted.ToString()}" +
                $"&licensestatus={pedData.DriversLicenseState.ToString() ?? ""}" +
                $"&licenseexpiration={pedData.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? ""}" +
                $"&weaponpermittype={pedData.WeaponPermit?.PermitType.ToString() ?? ""}" +
                $"&weaponpermitstatus={pedData.WeaponPermit?.Status.ToString() ?? ""}" +
                $"&weaponpermitexpiration={pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" +
                $"&fishpermitstatus={pedData.FishingPermit?.Status.ToString() ?? ""}" +
                $"&timesstopped={pedData.TimesStopped.ToString()}" +
                $"&fishpermitexpiration={pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" +
                $"&huntpermitstatus={pedData.HuntingPermit?.Status.ToString() ?? ""}" +
                $"&huntpermitexpiration={pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" +
                $"&isonparole={pedData.IsOnParole.ToString()}" +
                $"&isonprobation={pedData.IsOnProbation.ToString()}";
        }

        public static string GetPedData(Ped ped)
        {
            if (ped == null)
                return null;

            var persona = Functions.GetPersonaForPed(ped);
            if (persona == null) return null;

            string address;
            string licenseNum;
            var licenseStatus = persona.ELicenseState.ToString();
            var licenseExp = "";

            switch (licenseStatus.ToLower())
            {
                case "valid":
                    licenseExp = MathUtils.GenerateValidLicenseExpirationDate();
                    break;
                case "expired":
                    licenseExp = MathUtils.GenerateExpiredLicenseExpirationDate(3);
                    break;
                case "none":
                case "suspended":
                case "unlicensed":
                    licenseExp = "";
                    break;
            }

            if (!Misc.PedLicenseNumbers.ContainsKey(persona.FullName))
            {
                licenseNum = MathUtils.GenerateLicenseNumber();
                Misc.PedLicenseNumbers.Add(persona.FullName, licenseNum);
            }
            else
            {
                licenseNum = Misc.PedLicenseNumbers[persona.FullName];
            }

            if (!Misc.PedAddresses.ContainsKey(persona.FullName))
            {
                address = MathUtils.GetRandomAddress();

                Misc.PedAddresses.Add(persona.FullName, address);
            }
            else
            {
                address = Misc.PedAddresses[persona.FullName];
            }

            return
                $"name={persona.FullName}" +
                $"&licensenumber={licenseNum}" +
                $"&pedmodel={Misc.FindPedModel(ped)}" +
                $"&birthday={persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}" +
                $"&gender={persona.Gender.ToString()}" +
                $"&address={address}" +
                $"&iswanted={persona.Wanted.ToString()}" +
                $"&licensestatus={licenseStatus}" +
                $"&licenseexpiration={licenseExp}" +
                $"&timesstopped={persona.TimesStopped.ToString()}" +
                $"&isonparole={MathUtils.CheckProbability(30).ToString()}" +
                $"&isonprobation={MathUtils.CheckProbability(25).ToString()}";
        }

        public static void CreateTrafficStopObj(Vehicle vehicle)
        {
            if (!vehicle.Exists()) return;
            var color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(vehicle) != -1
                ? ""
                : $"{vehicle.PrimaryColor.R}-{vehicle.PrimaryColor.G}-{vehicle.PrimaryColor.B}";
            var owner = Functions.GetVehicleOwnerName(vehicle);
            var plate = vehicle.LicensePlate;
            var model = vehicle.Model.Name;
            var isStolen = vehicle.IsStolen.ToString();
            var isPolice = vehicle.IsPoliceVehicle;
            var insurance = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb,
                MathUtils.ValidProb, MathUtils.RevokedProb);
            var registration = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb,
                MathUtils.ValidProb, MathUtils.RevokedProb);
            var street = World.GetStreetName(LocalPlayer.Position);
            var area = GetPedCurrentZoneName();

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                insurance = GetInsurancePr(vehicle);
                registration = GetRegistrationPr(vehicle);
                isStolen = GetStolenPr(vehicle);
                owner = GetOwnerPr(vehicle);
            }
            else if (HasStopThePed)
            {
                insurance = GetInsuranceStp(vehicle);
                registration = GetRegistrationStp(vehicle);
            }

            var oldFile = File.ReadAllText($"{FileDataFolder}/trafficStop.data");
            if (oldFile.Contains(plate)) return;
            var vehicleData =
                $"licenseplate={plate}&model={model}&isstolen={isStolen}&ispolice={isPolice}&owner={owner}&registration={registration}&insurance={insurance}&color={color}&street={street}&area={area}";

            File.WriteAllText($"{FileDataFolder}/trafficStop.data", $"{vehicleData}");
            Game.LogTrivial("ReportsPlusListener: TrafficStop DataFile Created");
        }

        public static void CreatePedObj(Ped ped)
        {
            if (!ped.Exists()) return;

            var oldFile = File.ReadAllText($"{FileDataFolder}/worldPeds.data");
            if (oldFile.Contains(Functions.GetPersonaForPed(ped).FullName)) return;

            string data;
            if (HasPolicingRedefined && HasCommonDataFramework)
                data = GetPedDataPr(ped);
            else
                data = GetPedData(ped);

            if (data == null) return;

            var delimiter = oldFile.Length > 0 ? "|" : "";

            File.WriteAllText($"{FileDataFolder}/worldPeds.data", $"{oldFile}{delimiter}{data}");
        }
    }
}