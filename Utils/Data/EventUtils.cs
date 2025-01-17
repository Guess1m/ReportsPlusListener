using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CalloutInterfaceAPI;
using LSPD_First_Response.Mod.API;
using PolicingRedefined.API;
using Rage;
using Events = LSPD_First_Response.Mod.API.Events;
using Functions = LSPD_First_Response.Mod.API.Functions;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.GetterUtils;
using static ReportsPlus.Utils.Data.UpdateUtils;

namespace ReportsPlus.Utils.Data
{
    public static class EventUtils
    {
        public static void EstablishEventsBaseGame()
        {
            Events.OnPedPresentedId += BASE_AskIDEvent;
            Events.OnPedArrested += BASE_ArrestPedEvent;
            Events.OnPedFrisked += BASE_PatDownPedEvent;
            Events.OnPedStopped += BASE_StopPedEvent;
        }

        public static void EstablishEventsStp()
        {
            StopThePed.API.Events.askIdEvent += STP_AskIDEvent;
            StopThePed.API.Events.pedArrestedEvent += STP_ArrestPedEvent;
            StopThePed.API.Events.patDownPedEvent += STP_PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += STP_HandLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += STP_PassengerHandLicenseEvent;
            StopThePed.API.Events.stopPedEvent += STP_StopPedEvent;
        }

        public static void EstablishEventsPr()
        {
            EventsAPI.OnIdentificationGiven += PR_OnIdentificationGiven;
            EventsAPI.OnPedArrested += PR_OnPedArrested;
            EventsAPI.OnPedPatDown += PR_OnPedPatDown;
            EventsAPI.OnDriverIdentificationGiven += PR_OnDriverIdentificationGiven;
            EventsAPI.OnOccupantIdentificationGiven += PR_OnOccupantIdentificationGiven;
            EventsAPI.OnPedStopped += PR_OnPedStopped;
        }

        private static void BASE_StopPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }

        private static void BASE_PatDownPedEvent(Ped suspect, Ped friskingofficer)
        {
            CreatePedObj(suspect);
            UpdateCurrentId(suspect);
        }

        private static void BASE_ArrestPedEvent(Ped suspect, Ped arrestingofficer)
        {
            CreatePedObj(suspect);
        }

        private static void BASE_AskIDEvent(Ped ped, LHandle pullover, LHandle pedinteraction)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void PR_OnPedStopped(Ped ped)
        {
            CreatePedObj(ped);
        }

        private static void PR_OnOccupantIdentificationGiven(Ped ped)
        {
            UpdateCurrentId(ped);
        }

        private static void PR_OnDriverIdentificationGiven(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void PR_OnPedPatDown(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void PR_OnPedArrested(Ped ped, Ped officer, bool frontcuffs)
        {
            CreatePedObj(ped);
        }

        private static void PR_OnIdentificationGiven(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        public static void EstablishCiEvent()
        {
            Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;

            void EventsOnCalloutDisplayed(LHandle handle)
            {
                Game.LogTrivial("ReportsPlusListener: Displaying Callout");
                var callout = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
                var calloutId = MathUtils.GenerateCalloutId();

                var agency = Functions.GetCurrentAgencyScriptName();
                var priority = "default";
                var description = "";
                var calMessage = "";
                var name = callout.FriendlyName;

                if (callout.ScriptInfo is CalloutInterfaceAttribute calloutInterfaceInfo)
                {
                    agency = calloutInterfaceInfo.Agency.Length > 0 ? calloutInterfaceInfo.Agency : agency;
                    priority = calloutInterfaceInfo.Priority.Length > 0 ? calloutInterfaceInfo.Priority : "default";
                    description = calloutInterfaceInfo.Description;
                    calMessage = callout.CalloutMessage;
                    name = calloutInterfaceInfo.Name;
                }

                var street = World.GetStreetName(World.GetStreetHash(callout.CalloutPosition));
                var zone = Functions.GetZoneAtPosition(callout.CalloutPosition);
                var currentTime = DateTime.Now.ToString("h:mm:ss tt");
                var currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                var cleanCalloutMessage = Regex.Replace(callout.CalloutMessage, @"~.*?~", "").Trim();

                CalloutDoc = new XDocument(new XElement("Callouts"));
                var calloutElement = new XElement("Callout",
                    new XElement("Number", calloutId),
                    new XElement("Name", name),
                    new XElement("Type", cleanCalloutMessage),
                    new XElement("Description", description),
                    new XElement("Message", calMessage),
                    new XElement("Priority", priority),
                    new XElement("Street", street),
                    new XElement("Area", zone.RealAreaName),
                    new XElement("County", zone.County),
                    new XElement("StartTime", currentTime),
                    new XElement("StartDate", currentDate)
                );

                CalloutDoc.Root?.Add(calloutElement);
                CalloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                Game.LogTrivial($"ReportsPlusListener: Callout {calloutId} data updated and displayed.");
            }
        }

        private static void STP_StopPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }

        private static void STP_PassengerHandLicenseEvent(Vehicle vehicle)
        {
            var passengers = vehicle.Passengers;
            foreach (var t in passengers)
                UpdateCurrentId(t);
        }

        private static void STP_AskIDEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void STP_PatDownPedEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void STP_ArrestPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }

        private static void STP_HandLicenseEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }
    }
}