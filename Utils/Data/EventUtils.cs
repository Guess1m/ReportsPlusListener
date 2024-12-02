using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CalloutInterfaceAPI;
using LSPD_First_Response.Mod.API;
using Rage;
using Events = LSPD_First_Response.Mod.API.Events;
using Functions = LSPD_First_Response.Mod.API.Functions;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.GetterUtils;
using static ReportsPlus.Utils.Data.RefreshUtils;

namespace ReportsPlus.Utils.Data
{
    public static class EventUtils
    {
        public static void EstablishEventsStp()
        {
            StopThePed.API.Events.askIdEvent += AskForIdEvent;
            StopThePed.API.Events.pedArrestedEvent += ArrestPedEvent;
            StopThePed.API.Events.patDownPedEvent += PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += DriversLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += PassengerLicenseEvent;
            StopThePed.API.Events.stopPedEvent += StopPedEvent;
        }

        public static void EstablishCiEvent()
        {
            Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;

            void EventsOnCalloutDisplayed(LHandle handle)
            {
                Game.LogTrivial("ReportsPlusListener: Displaying Callout");
                var callout = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
                var calloutId = Utils.GenerateCalloutId();

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

        public static void AskForIdEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        public static void ArrestPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }

        public static void PatDownPedEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        public static void DriversLicenseEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        public static void PassengerLicenseEvent(Vehicle vehicle)
        {
            var passengers = vehicle.Passengers;
            for (var i = 0; i < passengers.Length; i++) UpdateCurrentId(passengers[i]);
        }

        public static void StopPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }
    }
}