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
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;
        }

        private static void PR_OnPedStopped(Ped ped)
        {
            Logger.LogWarning("PR_OnPedStopped NOT IMPLEMENTED YET");
        }

        private static void PR_OnOccupantIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR PR_OnOccupantIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void PR_OnDriverIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR_OnDriverIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void PR_OnPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR_OnPedPatDown for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void PR_OnPedArrested(Ped ped, Ped officer, bool frontcuffs)
        {
            Logger.LogWarning("PR_OnPedArrested NOT IMPLEMENTED YET");
        }

        private static void PR_OnIdentificationGiven(Ped ped, EGivenIdentification identification)
        {
            Logger.LogInfo("Running PR_OnIdentificationGiven for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void PR_OnDeadPedPatDown(Ped ped)
        {
            Logger.LogInfo("Running PR_OnDeadPedPatDown for " + ped.GetPedData().FullName);
            EventManager.SendIDUpdate(PedDataHelper.GeneratePedData(ped));
        }

        private static void PR_OnVehicleCheck(Vehicle vehicle, bool isVinCheck)
        {
            Logger.LogWarning("PR_OnVehicleCheck NOT IMPLEMENTED YET");
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
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed -= EventsOnCalloutDisplayed;
        }
    }
}