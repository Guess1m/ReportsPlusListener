using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using StopThePed.API;

namespace ReportsPlus.Utils.Data
{
    public static class GetValueMethods
    {
        // Policing Redefined Methods
        public static string GetInsExpPr(Vehicle car)
        {
            if (car.GetVehicleData() == null) return "";
            return car.GetVehicleData().Insurance.ExpirationDate?.ToString("MM-dd-yyyy") ?? "";
        }

        public static string GetRegExpPr(Vehicle car)
        {
            if (car.GetVehicleData() == null) return "";
            return car.GetVehicleData().Registration.ExpirationDate?.ToString("MM-dd-yyyy") ?? "";
        }

        public static string GetVinPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Vin.ToString();
        }

        public static string GetOwnerPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.FullName;
        }

        public static string GetOwnerAddressPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.Address.ToString();
        }

        public static string GetStolenPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().IsStolen.ToString();
        }

        public static string GetRegistrationPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Registration.Status.ToString();
        }

        public static string GetInsurancePr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Insurance.Status.ToString();
        }

        public static string GetGenderPr(Ped ped)
        {
            return ped.GetPedData() == null ? "" : ped.GetPedData().Gender.ToString();
        }

        public static string GetFullNamePr(Ped ped)
        {
            return ped.GetPedData() == null ? "" : ped.GetPedData().FullName;
        }

        // Stop The Ped Methods
        public static string GetRegistrationStp(Vehicle car)
        {
            return car == null ? "" : Functions.getVehicleRegistrationStatus(car).ToString();
        }

        public static string GetInsuranceStp(Vehicle car)
        {
            return car == null ? "" : Functions.getVehicleInsuranceStatus(car).ToString();
        }
    }
}