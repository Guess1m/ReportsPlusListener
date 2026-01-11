using System;
using CalloutInterfaceAPI;
using CommonDataFramework.Modules.PedDatabase;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using PolicingRedefined.API;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WorldData;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils.WebSocket.Actions.Events{
    public static class EventUtils{
        #region Base Game Events

        public static void EstablishEventsBaseGame()
        {
            LSPD_First_Response.Mod.API.Events.OnPedPresentedId += BASE_AskIDEvent;
            LSPD_First_Response.Mod.API.Events.OnPedFrisked     += BASE_PatDownPedEvent;
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
            LSPD_First_Response.Mod.API.Events.OnPedFrisked     -= BASE_PatDownPedEvent;
        }

        #endregion

        #region StopThePed Events

        public static void EstablishEventsStp()
        {
            StopThePed.API.Events.askIdEvent            += STP_AskIDEvent;
            StopThePed.API.Events.patDownPedEvent       += STP_PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += STP_HandLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent   += STP_PassengerHandLicenseEvent;
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
            StopThePed.API.Events.askIdEvent            -= STP_AskIDEvent;
            StopThePed.API.Events.patDownPedEvent       -= STP_PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent -= STP_HandLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent   -= STP_PassengerHandLicenseEvent;
        }

        #endregion

        #region Policing Redefined Events

        public static void EstablishEventsPr()
        {
            EventsAPI.OnIdentificationGiven         += PR_OnIdentificationGiven;
            EventsAPI.OnPedPatDown                  += PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven   += PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven += PR_OnOccupantIdentificationGiven;
            EventsAPI.OnDeadPedSearched             += PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch       += PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch   += PR_OnVehicleCheck;
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

        // TODO: Implement
        private static void PR_OnVehicleCheck(Vehicle vehicle, bool isVinCheck)
        {
            Logger.LogWarning("PR_OnVehicleCheck NOT IMPLEMENTED YET");
        }

        // TODO: Implement
        private static void PR_OnPedCheck(Ped ped)
        {
            Logger.LogWarning("PR_OnPedCheck NOT IMPLEMENTED YET");
        }

        public static void CleanupEventsPr()
        {
            EventsAPI.OnIdentificationGiven         -= PR_OnIdentificationGiven;
            EventsAPI.OnPedPatDown                  -= PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven   -= PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven -= PR_OnOccupantIdentificationGiven;
            EventsAPI.OnDeadPedSearched             -= PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch       -= PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch   -= PR_OnVehicleCheck;
        }

        #endregion

        #region Callout Interface

        public static void EstablishCiEvent()
        {
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;
        }

        private static void EventsOnCalloutDisplayed(LHandle handle)
        {
            Logger.LogInfo("Running EventsOnCalloutDisplayed");
            var callout    = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
            var identifier = new Random().Next(10000, 100000);

            var priority    = string.Empty;
            var description = string.Empty;
            var message     = string.Empty;
            var name        = callout.FriendlyName;

            const string status = "New";

            var comments = string.Empty;

            if (callout.ScriptInfo is CalloutInterfaceAttribute calloutInterfaceInfo)
            {
                priority    = calloutInterfaceInfo.Priority.Length > 0 ? calloutInterfaceInfo.Priority : string.Empty;
                description = calloutInterfaceInfo.Description.Length > 0 ? calloutInterfaceInfo.Description : string.Empty;
                message     = callout.CalloutMessage.Length > 0 ? callout.CalloutMessage : string.Empty;
                name        = calloutInterfaceInfo.Name.Length > 0 ? calloutInterfaceInfo.Name : callout.FriendlyName;
            }

            var street      = World.GetStreetName(World.GetStreetHash(callout.CalloutPosition));
            var zone        = Functions.GetZoneAtPosition(callout.CalloutPosition);
            var currentTime = DateTime.Now.ToString("h:mm:ss tt");
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            var calloutData = new JObject
            {
                ["Identifier"]  = identifier,
                ["Status"]      = status,
                ["Name"]        = name,
                ["Description"] = description,
                ["Message"]     = message,
                ["Priority"]    = priority,
                ["Street"]      = street,
                ["Area"]        = zone.RealAreaName,
                ["County"]      = zone.County.ToString(),
                ["Postal"]      = CalloutInterface.API.Functions.GetPostalCode(callout.CalloutPosition),
                ["Comments"]    = comments,
                ["StartTime"]   = currentTime,
                ["StartDate"]   = currentDate,
                ["X"]           = callout.CalloutPosition.X,
                ["Y"]           = callout.CalloutPosition.Y
            };

            EventManager.SendCalloutUpdate(calloutData);
            Logger.LogInfo($"Callout {identifier} Updated");
        }

        public static void CleanupCiEvent()
        {
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed -= EventsOnCalloutDisplayed;
        }

        #endregion
    }
}