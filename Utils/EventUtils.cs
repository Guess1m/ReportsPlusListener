using System;
using CalloutInterfaceAPI;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using PolicingRedefined.API;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Events;
using Events = LSPD_First_Response.Mod.API.Events;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus.Utils{
    public static class EventUtils{
        public static void EstablishEventsPr()
        {
            EventsAPI.OnIdentificationGiven         += PR_OnIdentificationGiven;
            EventsAPI.OnPedArrested                 += PR_OnPedArrested;
            EventsAPI.OnPedPatDown                  += PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven   += PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven += PR_OnOccupantIdentificationGiven;
            EventsAPI.OnPedStopped                  += PR_OnPedStopped;
            EventsAPI.OnDeadPedSearched             += PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch       += PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch   += PR_OnVehicleCheck;
        }

        public static void EstablishCiEvent()
        {
            Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;
        }

        private static void PR_OnPedStopped(Ped ped)
        {
            Logger.LogInfo("Running PR OnPedStopped");
        }

        private static void PR_OnOccupantIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR OnOccupantIdentificationGiven");
        }

        private static void PR_OnDriverIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR OnDriverIdentificationGiven");
        }

        private static void PR_OnPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR OnPedPatDown");
        }

        private static void PR_OnPedArrested(Ped ped, Ped officer, bool frontcuffs)
        {
            Logger.LogInfo("Running PR OnPedArrested");
        }

        private static void PR_OnIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR OnIdentificationGiven");
        }

        private static void PR_OnDeadPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR OnDeadPedPatDown");
        }

        private static void PR_OnVehicleCheck(Vehicle vehicle, bool isVinCheck)
        {
            Logger.LogInfo("Updated Lookup File (vehicle); ");
        }

        private static void PR_OnPedCheck(Ped ped)
        {
            Logger.LogInfo("Updated Lookup File (ped); ");
        }

        // BUG: this will cause issues when calloutinterface isnt downloaded
        private static void EventsOnCalloutDisplayed(LHandle handle)
        {
            Logger.LogInfo("Running EventsOnCalloutDisplayed");
            var callout    = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
            var identifier = new Random().Next(10000, 100000);

            var priority    = "";
            var description = "";
            var message     = "";
            var name        = callout.FriendlyName;

            if (callout.ScriptInfo is CalloutInterfaceAttribute calloutInterfaceInfo)
            {
                priority    = calloutInterfaceInfo.Priority.Length > 0 ? calloutInterfaceInfo.Priority : "";
                description = calloutInterfaceInfo.Description.Length > 0 ? calloutInterfaceInfo.Description : "";
                message     = callout.CalloutMessage.Length > 0 ? callout.CalloutMessage : "";
                name        = calloutInterfaceInfo.Name.Length > 0 ? calloutInterfaceInfo.Name : callout.FriendlyName;
            }

            var street      = World.GetStreetName(World.GetStreetHash(callout.CalloutPosition));
            var zone        = Functions.GetZoneAtPosition(callout.CalloutPosition);
            var currentTime = DateTime.Now.ToString("h:mm:ss tt");
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            var calloutData = new JObject
            {
                ["Identifier"]  = identifier,
                ["Name"]        = name,
                ["Description"] = description,
                ["Message"]     = message,
                ["Priority"]    = priority,
                ["Street"]      = street,
                ["Area"]        = zone.RealAreaName,
                ["County"]      = zone.County.ToString(),
                ["Postal"]      = CalloutInterface.API.Functions.GetPostalCode(callout.CalloutPosition),
                ["StartTime"]   = currentTime,
                ["StartDate"]   = currentDate,
                ["x"]           = callout.CalloutPosition.X,
                ["y"]           = callout.CalloutPosition.Y
            };

            CalloutHandler.SendCalloutData(calloutData);

            Logger.LogInfo($"Callout {identifier} DataFile Updated");
        }

        public static void CleanupEventsPr()
        {
            EventsAPI.OnIdentificationGiven         -= PR_OnIdentificationGiven;
            EventsAPI.OnPedArrested                 -= PR_OnPedArrested;
            EventsAPI.OnPedPatDown                  -= PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven   -= PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven -= PR_OnOccupantIdentificationGiven;
            EventsAPI.OnPedStopped                  -= PR_OnPedStopped;
            EventsAPI.OnDeadPedSearched             -= PR_OnDeadPedPatDown;
            EventsAPI.OnPedRanThroughDispatch       -= PR_OnPedCheck;
            EventsAPI.OnVehicleRanThroughDispatch   -= PR_OnVehicleCheck;
        }

        public static void CleanupCiEvent()
        {
            Events.OnCalloutDisplayed -= EventsOnCalloutDisplayed;
        }
    }
}