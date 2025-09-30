using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using PolicingRedefined.API;
using PolicingRedefined.Interaction.Assets.PedAttributes;
using Rage;
using StopThePed.API;

namespace ReportsPlus.Utils
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

        public static string GetOwnerType(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.OwnerType.ToString() ?? "";
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

        public static string GetOwnerModelPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            var owner = vehicleData?.Owner?.Holder;
            return Misc.FindPedModel(owner).ToLower() ?? "";
        }

        public static string GetFullNamePr(Ped ped)
        {
            if (ped == null || !ped.Exists()) return "";
            var pedData = ped.GetPedData();
            return pedData?.FullName ?? "";
        }

        public static string GetStolenPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.IsStolen.ToString() ?? "";
        }

        public static string GetRegistrationPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Registration?.Status.ToString() ?? "";
        }

        public static string GetInsurancePr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Insurance?.Status.ToString() ?? "";
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

        public static void PRGiveCitation(Ped targetPed, string CitationSignalCharges, int fineAmount, bool isArrestable)
        {
            var citation = new Citation(targetPed, CitationSignalCharges, fineAmount, isArrestable);
            PedAPI.GiveCitationToPed(targetPed, citation);
        }

        public static string GetRegistrationStp(Vehicle car)
        {
            return car == null ? "" : Functions.getVehicleRegistrationStatus(car).ToString();
        }

        public static string GetInsuranceStp(Vehicle car)
        {
            return car == null ? "" : Functions.getVehicleInsuranceStatus(car).ToString();
        }

        public static string GetRegistrationBg(string reg)
        {
            return reg;
        }

        public static string GetInsuranceBg(string ins)
        {
            return ins;
        }
    }
}