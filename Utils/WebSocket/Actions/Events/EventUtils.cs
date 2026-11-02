using System;
using CommonDataFramework.Modules.PedDatabase;
using CommonDataFramework.Modules.VehicleDatabase;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using PolicingRedefined.API;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WorldData;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils.WebSocket.Actions.Events
{
    public static class EventUtils
    {
        #region Base Game Events

        public static void EstablishEventsBaseGame()
        {
            LSPD_First_Response.Mod.API.Events.OnPedPresentedId += BASE_AskIDEvent;
            LSPD_First_Response.Mod.API.Events.OnPedFrisked += BASE_PatDownPedEvent;
        }

        private static void BASE_PatDownPedEvent(Ped suspect, Ped friskingofficer)
        {
            Logger.LogInfo("Running BASE_PatDownPedEvent for " + Functions.GetPersonaForPed(suspect).FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(suspect));
        }

        private static void BASE_AskIDEvent(Ped ped, LHandle pullover, LHandle pedinteraction)
        {
            Logger.LogInfo("Running BASE_AskIDEvent for " + Functions.GetPersonaForPed(ped).FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        public static void CleanupEventsBaseGame()
        {
            LSPD_First_Response.Mod.API.Events.OnPedPresentedId -= BASE_AskIDEvent;
            LSPD_First_Response.Mod.API.Events.OnPedFrisked -= BASE_PatDownPedEvent;
        }

        #endregion

        #region StopThePed Events

        public static void EstablishEventsStp()
        {
            StopThePed.API.Events.askIdEvent += STP_AskIDEvent;
            StopThePed.API.Events.patDownPedEvent += STP_PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += STP_HandLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += STP_PassengerHandLicenseEvent;
        }

        private static void STP_PassengerHandLicenseEvent(Vehicle vehicle)
        {
            var passengers = vehicle.Passengers;
            foreach (var p in passengers)
            {
                Logger.LogInfo("Running STP_PassengerHandLicenseEvent for " + Functions.GetPersonaForPed(p).FullName);
                EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(p));
            }
        }

        private static void STP_AskIDEvent(Ped ped)
        {
            Logger.LogInfo("Running STP_AskIDEvent for " + Functions.GetPersonaForPed(ped).FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void STP_PatDownPedEvent(Ped ped)
        {
            Logger.LogInfo("Running STP_PatDownPedEvent for " + Functions.GetPersonaForPed(ped).FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void STP_HandLicenseEvent(Ped ped)
        {
            Logger.LogInfo("Running STP_HandLicenseEvent for " + Functions.GetPersonaForPed(ped).FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        public static void CleanupEventsStp()
        {
            StopThePed.API.Events.askIdEvent -= STP_AskIDEvent;
            StopThePed.API.Events.patDownPedEvent -= STP_PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent -= STP_HandLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent -= STP_PassengerHandLicenseEvent;
        }

        #endregion

        #region Policing Redefined Events

        public static void EstablishEventsPr()
        {
            EventsAPI.OnIdentificationGiven += PR_OnIdentificationGiven;
            EventsAPI.OnPedPatDown += PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven += PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven += PR_OnOccupantIdentificationGiven;
            EventsAPI.OnDeadPedSearched += PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch += PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch += PR_OnVehicleCheck;
        }

        private static void PR_OnOccupantIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR PR_OnOccupantIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedDataPR(ped));
        }

        private static void PR_OnDriverIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR_OnDriverIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedDataPR(ped));
        }

        private static void PR_OnPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR_OnPedPatDown for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedDataPR(ped));
        }

        private static void PR_OnIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR_OnIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedDataPR(ped));
        }

        private static void PR_OnDeadPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR_OnDeadPedPatDown for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedDataPR(ped));
        }

        private static void PR_OnVehicleCheck(Vehicle vehicle, bool isVinCheck)
        {
            Logger.LogInfo("Running PR_OnVehicleCheck for " + vehicle.LicensePlate);

            if (!vehicle || !vehicle.Exists()) return;

            var vehicleData = VehicleDataHelper.GenerateVehicleDataPR(vehicle);
            if (vehicleData != null)
            {
                EventManager.SendVehicleDispatchCheck(vehicleData);
            }
        }

        private static void PR_OnPedCheck(Ped ped)
        {
            Logger.LogInfo("Running PR_OnPedCheck for " + ped.GetPedData().FullName);

            if (!ped || !ped.Exists()) return;

            var pedData = PedDataHelper.GeneratePedDataPR(ped);
            if (pedData != null)
            {
                EventManager.SendPedDispatchCheck(pedData);
            }
        }

        public static void CleanupEventsPr()
        {
            EventsAPI.OnIdentificationGiven -= PR_OnIdentificationGiven;
            EventsAPI.OnPedPatDown -= PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven -= PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven -= PR_OnOccupantIdentificationGiven;
            EventsAPI.OnDeadPedSearched -= PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch -= PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch -= PR_OnVehicleCheck;
        }

        #endregion
    }
}