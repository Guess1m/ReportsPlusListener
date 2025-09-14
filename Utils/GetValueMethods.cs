using CommonDataFramework.Modules;
using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using Rage;
using StopThePed.API;

namespace ReportsPlus.Utils.Data
{
    public static class GetValueMethods
    {
        /// Policing Redefined Methods
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

        public static string GetOwnerGenderPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.Gender.ToString();
        }

        public static string GetOwnerAddressPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.Address.ToString();
        }

        public static string GetOwnerDobPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : $"{car.GetVehicleData().Owner.Birthday.Month}/{car.GetVehicleData().Owner.Birthday.Day}/{car.GetVehicleData().Owner.Birthday.Year}";
        }

        public static string GetOwnerLicenseStatePr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.DriversLicenseState.ToString();
        }

        public static string GetOwnerIsWantedPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Owner.Wanted.ToString();
        }

        public static string GetGenderPr(Ped ped)
        {
            return ped.GetPedData() == null ? "" : ped.GetPedData().Gender.ToString();
        }

        public static string GetFullNamePr(Ped ped)
        {
            return ped.GetPedData() == null ? "" : ped.GetPedData().FullName;
        }

        public static string GetStolenPr(Vehicle car, bool setValid = false)
        {
            if (setValid) car.GetVehicleData().IsStolen = false;

            return car.GetVehicleData() == null ? "" : car.GetVehicleData().IsStolen.ToString();
        }

        public static string GetRegistrationPr(Vehicle car, bool setValid = false)
        {
            if (setValid) car.GetVehicleData().Registration.Status = EDocumentStatus.Valid;

            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Registration.Status.ToString();
        }

        public static string GetInsurancePr(Vehicle car, bool setValid = false)
        {
            if (setValid) car.GetVehicleData().Insurance.Status = EDocumentStatus.Valid;

            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Insurance.Status.ToString();
        }

        public static string GetMakePr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Make;
        }

        public static string GetModelPr(Vehicle car)
        {
            return car.GetVehicleData() == null ? "" : car.GetVehicleData().Model;
        }

        public static string GetLicenseExpiration(Ped ped)
        {
            return ped.GetPedData() == null ? "" : ped.GetPedData().DriversLicenseExpiration?.ToString("MM-dd-yyyy");
        }

        /// Stop The Ped Methods
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

        /// Base Game Methods
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