using System;
using System.Collections.Generic;
using System.IO;
using CommonDataFramework.Modules.PedDatabase;
using Rage;
using Rage.Native;
using static ReportsPlus.Utils.GetValueMethods;
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
            var driver = car.Driver.Exists() ? Functions.GetPersonaForPed(car.Driver).FullName : "";
            if (HasPolicingRedefined && HasCommonDataFramework) driver = GetFullNamePr(car.Driver);

            var color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(car) != -1 ? "" : $"{car.PrimaryColor.R}-{car.PrimaryColor.G}-{car.PrimaryColor.B}";

            var insurance = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb, MathUtils.ValidProb, MathUtils.RevokedProb);
            var registration = MathUtils.GetRandomVehicleStatus(MathUtils.ExpiredProb, MathUtils.NoneProb, MathUtils.ValidProb, MathUtils.RevokedProb);
            var stolen = car.IsStolen.ToString();
            var ownerAddress = MathUtils.GetRandomAddress();
            var ownerdob = MathUtils.GenerateDob(35, 55);
            var ownerIsWanted = MathUtils.GenerateIsWanted(10).ToString();
            var ownerLicenseNumber = MathUtils.GenerateLicenseNumber();
            var ownerLicenseState = MathUtils.GetRandomLicenseStatus(10, 5, 65, 20);
            var owner = Functions.GetVehicleOwnerName(car);
            var vin = MathUtils.GenerateVin();
            var gender = MathUtils.Rand.Next(2) == 1 ? "Male" : "Female";
            var model = car.Model.Name ?? "Unknown";
            var make = Game.GetLocalizedString(NativeFunction.Natives.xF7AF4F159FF99F97<string>(car.Model.Hash)) ?? "Unknown";
            var regexp = "";
            var insexp = "";
            var insuranceCoverage = MathUtils.GenerateRandomCoverage();
            var ownerModel = MathUtils.GenerateModelForPed(gender);

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                insurance = GetInsurancePr(car);
                registration = GetRegistrationPr(car);
                stolen = GetStolenPr(car);
                make = GetMakePr(car);
                model = GetModelPr(car);
                ownerdob = GetOwnerDobPr(car);
                ownerIsWanted = GetOwnerIsWantedPr(car);
                ownerLicenseState = GetOwnerLicenseStatePr(car);
                ownerAddress = GetOwnerAddressPr(car);
                owner = GetOwnerPr(car);
                vin = GetVinPr(car);
                regexp = GetRegExpPr(car);
                insexp = GetInsExpPr(car);
                gender = GetOwnerGenderPr(car);
                ownerModel = GetOwnerModelPr(car);
            }
            else if (HasStopThePed)
            {
                insurance = GetInsuranceStp(car);
                registration = GetRegistrationStp(car);
            }
            else
            {
                registration = GetRegistrationBg(registration);
                insurance = GetInsuranceBg(insurance);
            }

            return $"licenseplate={car.LicensePlate}&model={model}&make={make}&regexp={regexp}&insexp={insexp}&coverage={insuranceCoverage}&vin={vin}&isstolen={stolen}&ispolice={car.IsPoliceVehicle}&owner={owner}&ownermodel={ownerModel}&ownergender={gender}&owneraddress={ownerAddress}&ownerdob={ownerdob}&owneriswanted={ownerIsWanted}&ownerlicensenumber={ownerLicenseNumber}&ownerlicensestate={ownerLicenseState}&driver={driver}&registration={registration}&insurance={insurance}&color={color}&timescanned={DateTime.Now:o}";
        }

        private static void CreateVehicleObj(Vehicle vehicle)
        {
            if (!vehicle.Exists()) return;

            var oldFile = File.ReadAllText($"{FileDataFolder}/worldCars.data");
            if (oldFile.Contains(vehicle.LicensePlate)) return;

            var data = GetWorldCarData(vehicle);
            if (data == null) return;

            var delimiter = oldFile.Length > 0 ? "|" : "";

            File.WriteAllText($"{FileDataFolder}/worldCars.data", $"{oldFile}{delimiter}{data}");
        }

        private static string GetPedDataPr(Ped ped)
        {
            if (ped == null) return null;

            var pedData = ped.GetPedData();
            if (pedData == null) return null;

            var address = MathUtils.GetPedAddress(ped);
            var isPolice = ped.RelationshipGroup == "COP" ? "true" : "false";
            var gender = pedData.Gender.ToString() ?? "";

            if (!Misc.PedExpirations.TryGetValue(pedData.FullName, out var licenseExp))
            {
                licenseExp = pedData.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? "";
                Misc.PedExpirations.Add(pedData.FullName, licenseExp);
            }

            if (!Misc.PedLicenseNumbers.TryGetValue(pedData.FullName, out var licenseNum))
            {
                licenseNum = MathUtils.GenerateLicenseNumber();
                Misc.PedLicenseNumbers[pedData.FullName] = licenseNum;
            }

            if (!Misc.PedHeights.TryGetValue(pedData.FullName, out var height) || !Misc.PedWeights.TryGetValue(pedData.FullName, out var weight))
            {
                var heightAndWeight = MathUtils.GenerateHeightAndWeight(gender);
                height = heightAndWeight[0];
                weight = heightAndWeight[1];
                Misc.PedHeights[pedData.FullName] = height;
                Misc.PedWeights[pedData.FullName] = weight;
            }

            return $"name={pedData.FullName}" + $"&licensenumber={licenseNum}" + $"&pedmodel={Misc.FindPedModel(ped)}" + $"&birthday={pedData.Birthday.Month}/{pedData.Birthday.Day}/{pedData.Birthday.Year}" + $"&gender={gender}" + $"&height={height}" + $"&weight={weight}" + $"&ispolice={isPolice}" + $"&address={address}" + $"&iswanted={pedData.Wanted.ToString()}" + $"&licensestatus={pedData.DriversLicenseState.ToString() ?? ""}" + $"&licenseexpiration={licenseExp}" + $"&weaponpermittype={pedData.WeaponPermit?.PermitType.ToString() ?? ""}" + $"&weaponpermitstatus={pedData.WeaponPermit?.Status.ToString() ?? ""}" + $"&weaponpermitexpiration={pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" + $"&fishpermitstatus={pedData.FishingPermit?.Status.ToString() ?? ""}" + $"&timesstopped={pedData.TimesStopped.ToString()}" + $"&fishpermitexpiration={pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" + $"&huntpermitstatus={pedData.HuntingPermit?.Status.ToString() ?? ""}" + $"&huntpermitexpiration={pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? ""}" + $"&isonparole={pedData.IsOnParole.ToString()}" + $"&isonprobation={pedData.IsOnProbation.ToString()}";
        }

        private static string GetPedData(Ped ped)
        {
            if (ped == null)
                return null;

            var persona = Functions.GetPersonaForPed(ped);
            if (persona == null) return null;

            var isPolice = ped.RelationshipGroup == "COP" ? "true" : "false";
            var gender = persona.Gender.ToString();

            if (!Misc.PedExpirations.TryGetValue(persona.FullName, out var licenseExp))
            {
                var licenseStatus = persona.ELicenseState.ToString();
                licenseExp = licenseStatus.ToLower() switch
                {
                    "valid" => MathUtils.GenerateValidLicenseExpirationDate(),
                    "suspended" => MathUtils.GenerateValidLicenseExpirationDate(),
                    "expired" => MathUtils.GenerateExpiredLicenseExpirationDate(3),
                    _ => "N/A"
                };

                Misc.PedExpirations.Add(persona.FullName, licenseExp);
            }

            if (!Misc.PedLicenseNumbers.TryGetValue(persona.FullName, out var licenseNum))
            {
                licenseNum = MathUtils.GenerateLicenseNumber();
                Misc.PedLicenseNumbers.Add(persona.FullName, licenseNum);
            }

            if (!Misc.PedAddresses.TryGetValue(persona.FullName, out var address))
            {
                address = MathUtils.GetRandomAddress();
                Misc.PedAddresses.Add(persona.FullName, address);
            }

            if (!Misc.PedHeights.TryGetValue(persona.FullName, out var height) || !Misc.PedWeights.TryGetValue(persona.FullName, out var weight))
            {
                var heightAndWeight = MathUtils.GenerateHeightAndWeight(gender);
                height = heightAndWeight[0];
                weight = heightAndWeight[1];
                Misc.PedHeights[persona.FullName] = height;
                Misc.PedWeights[persona.FullName] = weight;
            }

            return $"name={persona.FullName}" + $"&licensenumber={licenseNum}" + $"&pedmodel={Misc.FindPedModel(ped)}" + $"&birthday={persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}" + $"&gender={gender}" + $"&height={height}" + $"&weight={weight}" + $"&address={address}" + $"&ispolice={isPolice}" + $"&iswanted={persona.Wanted.ToString()}" + $"&licensestatus={persona.ELicenseState.ToString()}" + $"&licenseexpiration={licenseExp}" + $"&timesstopped={persona.TimesStopped.ToString()}" + $"&isonparole={MathUtils.CheckProbability(30).ToString()}" + $"&isonprobation={MathUtils.CheckProbability(25).ToString()}";
        }

        public static void CreateTrafficStopObj(Vehicle vehicle)
        {
            if (!vehicle.Exists()) return;

            var plate = vehicle.LicensePlate;
            var vehicleData = GetVehicleDataFromWorldCars(plate);

            if (vehicleData == null)
            {
                CreateVehicleObj(vehicle);
                vehicleData = GetVehicleDataFromWorldCars(plate);
                if (vehicleData == null) return;
            }

            vehicleData.TryGetValue("model", out var model);
            vehicleData.TryGetValue("isstolen", out var isStolen);
            vehicleData.TryGetValue("owner", out var owner);
            vehicleData.TryGetValue("registration", out var registration);
            vehicleData.TryGetValue("insurance", out var insurance);
            vehicleData.TryGetValue("color", out var color);

            var street = World.GetStreetName(Game.LocalPlayer.Character.Position);
            var area = GetPedCurrentZoneName();

            var trafficStopFile = $"{FileDataFolder}/trafficStop.data";
            if (File.ReadAllText(trafficStopFile).Contains(plate)) return;

            var vehicleDataEntry = $"licenseplate={plate}&model={model}&isstolen={isStolen}" + $"&owner={owner}&registration={registration}&insurance={insurance}&color={color}" + $"&street={street}&area={area}";

            File.WriteAllText(trafficStopFile, vehicleDataEntry);
            Game.LogTrivial($"ReportsPlusListener: TrafficStop DataFile Created; [{vehicleDataEntry}]");
        }

        public static Dictionary<string, string> GetVehicleDataFromWorldCars(string plate)
        {
            var filePath = $"{FileDataFolder}/worldCars.data";
            if (!File.Exists(filePath)) return null;

            foreach (var entry in File.ReadAllText(filePath).Split('|'))
            {
                var data = ParseEntry(entry);
                if (data.TryGetValue("licenseplate", out var existingPlate) && existingPlate == plate)
                    return data;
            }

            return null;
        }

        // parse helper
        public static Dictionary<string, string> ParseEntry(string entry)
        {
            var dict = new Dictionary<string, string>();
            foreach (var pair in entry.Split('&'))
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                    dict[keyValue[0]] = Uri.UnescapeDataString(keyValue[1]);
            }

            return dict;
        }

        // parse from worldPeds
        public static Dictionary<string, string> GetPedDataFromWorldPeds(string fullName)
        {
            var filePath = $"{FileDataFolder}/worldPeds.data";
            if (!File.Exists(filePath)) return null;

            var fileContent = File.ReadAllText(filePath);
            var entries = fileContent.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                var data = ParseEntry(entry);
                if (data.TryGetValue("name", out var existingName) && existingName.Equals(fullName, StringComparison.OrdinalIgnoreCase)) return data;
            }

            return null;
        }

        public static void CreatePedObj(Ped ped)
        {
            if (!ped.Exists()) return;

            var persona = Functions.GetPersonaForPed(ped);
            var fullName = HasPolicingRedefined && HasCommonDataFramework ? GetFullNamePr(ped) : persona.FullName;

            if (string.IsNullOrEmpty(fullName))
            {
                Game.LogTrivial("ReportsPlusListener: Attempted to create a ped object for a ped with no name. Aborting.");
                return;
            }

            if (GetPedDataFromWorldPeds(fullName) != null) return;

            string data;
            if (HasPolicingRedefined && HasCommonDataFramework)
                data = GetPedDataPr(ped);
            else
                data = GetPedData(ped);

            if (string.IsNullOrEmpty(data)) return;

            var filePath = $"{FileDataFolder}/worldPeds.data";
            var fileInfo = new FileInfo(filePath);
            var delimiter = fileInfo.Exists && fileInfo.Length > 0 ? "|" : "";

            File.AppendAllText(filePath, $"{delimiter}{data}");
            Game.LogTrivial($"ReportsPlusListener: Added new ped '{fullName}' to worldPeds.data.");
        }
    }
}