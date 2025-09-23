using CommonDataFramework.Modules;
using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using StopThePed.API;

namespace ReportsPlus.Utils.Data
{
    public static class GetValueMethods
    {
        public static string GetInsExpPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Insurance?.ExpirationDate?.ToString("MM-dd-yyyy") ?? "";
        }

        public static string GetRegExpPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Registration?.ExpirationDate?.ToString("MM-dd-yyyy") ?? "";
        }

        public static string GetVinPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Vin?.ToString() ?? "";
        }

        public static string GetOwnerPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Owner?.FullName ?? "";
        }

        public static string GetOwnerGenderPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Owner?.Gender.ToString() ?? "";
        }

        public static string GetOwnerAddressPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Owner?.Address?.ToString() ?? "";
        }

        public static string GetOwnerDobPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            if (vehicleData?.Owner?.Birthday == null) return "";
            var birthday = vehicleData.Owner.Birthday;
            return $"{birthday.Month}/{birthday.Day}/{birthday.Year}";
        }

        public static string GetOwnerLicenseStatePr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Owner?.DriversLicenseState.ToString() ?? "";
        }

        public static string GetOwnerIsWantedPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Owner?.Wanted.ToString() ?? "";
        }

        public static string GetGenderPr(Ped ped)
        {
            var pedData = ped.GetPedData();
            return pedData?.Gender.ToString() ?? "";
        }

        public static string GetFullNamePr(Ped ped)
        {
            var pedData = ped.GetPedData();
            return pedData?.FullName ?? "";
        }

        public static string GetStolenPr(Vehicle car, bool setValid = false)
        {
            var vehicleData = car.GetVehicleData();
            if (vehicleData == null) return "";
            if (setValid) vehicleData.IsStolen = false;
            return vehicleData.IsStolen.ToString();
        }

        public static string GetRegistrationPr(Vehicle car, bool setValid = false)
        {
            var vehicleData = car.GetVehicleData();
            if (vehicleData?.Registration == null) return "";
            if (setValid) vehicleData.Registration.Status = EDocumentStatus.Valid;
            return vehicleData.Registration.Status.ToString();
        }

        public static string GetInsurancePr(Vehicle car, bool setValid = false)
        {
            var vehicleData = car.GetVehicleData();
            if (vehicleData?.Insurance == null) return "";
            if (setValid) vehicleData.Insurance.Status = EDocumentStatus.Valid;
            return vehicleData.Insurance.Status.ToString();
        }

        public static string GetMakePr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Make ?? "";
        }

        public static string GetModelPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Model ?? "";
        }

        public static string GetLicenseExpiration(Ped ped)
        {
            var pedData = ped.GetPedData();
            return pedData?.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? "";
        }

        public static string GetRegistrationStp(Vehicle car, bool setValid = false)
        {
            if (setValid) Functions.setVehicleRegistrationStatus(car, STPVehicleStatus.Valid);
            return car == null ? "" : Functions.getVehicleRegistrationStatus(car).ToString();
        }

        public static string GetInsuranceStp(Vehicle car, bool setValid = false)
        {
            if (setValid) Functions.setVehicleInsuranceStatus(car, STPVehicleStatus.Valid);
            return car == null ? "" : Functions.getVehicleInsuranceStatus(car).ToString();
        }

        public static string GetRegistrationBg(string reg, bool setValid = false)
        {
            return setValid ? "Valid" : reg;
        }

        public static string GetInsuranceBg(string ins, bool setValid = false)
        {
            return setValid ? "Valid" : ins;
        }
    }
}