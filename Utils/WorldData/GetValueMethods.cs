using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using StopThePed.API;

namespace ReportsPlus.Utils.WorldData
{
    /// <summary>
    ///     Getters for reg/ins data
    /// </summary>
    public static class GetValueMethods
    {
        public static string GetInsExpPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Insurance?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty;
        }

        public static string GetRegExpPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Registration?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty;
        }

        public static string GetRegistrationPr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Registration == null ? string.Empty : vehicleData.Registration.Status.ToString();
        }

        public static string GetInsurancePr(Vehicle car)
        {
            var vehicleData = car.GetVehicleData();
            return vehicleData?.Insurance == null ? string.Empty : vehicleData.Insurance.Status.ToString();
        }

        public static string GetRegistrationStp(Vehicle car)
        {
            return car == null ? string.Empty : Functions.getVehicleRegistrationStatus(car).ToString();
        }

        public static string GetInsuranceStp(Vehicle car)
        {
            return car == null ? string.Empty : Functions.getVehicleInsuranceStatus(car).ToString();
        }
    }
}