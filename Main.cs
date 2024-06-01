using LSPD_First_Response.Engine.Scripting;
using LSPD_First_Response.Engine.Scripting.Entities;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using StopThePed.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        // Vars
        private static readonly string FileDataFolder = "ReportsPlus\\data";
        private static XDocument currentIDDoc;
        private static XDocument calloutDoc;
        private GameFiber dataCollectionFiber;
        internal static bool IsOnDuty;
        internal static Ped LocalPlayer => Game.LocalPlayer.Character;
        private static Dictionary<LHandle, string> calloutIds = new Dictionary<LHandle, string>();

        // TODO: Delete all the previous config files on startup.

        // Startup
        public override void Initialize()
        {
            LSPD_First_Response.Mod.API.Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlus Listener Plugin initialized.");
        }
        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            IsOnDuty = onDuty;
            if (onDuty)
            {
                bool pluginsInstalled = CheckPlugins();
                if (!pluginsInstalled)
                {
                    // Check for required plugins
                    Game.DisplayNotification("~r~ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    Game.LogTrivial("ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    return; // Exit initialization if plugins are missing
                }

                calloutIds.Clear();
                if (!Directory.Exists(FileDataFolder))
                    Directory.CreateDirectory(FileDataFolder);

                currentIDDoc = new XDocument(new XElement("IDs"));
                //LoadCurrentIDDocument();

                GameFiber.StartNew(Int);
                EstablishEvents();
                CalloutEvent();
                RefreshPeds();
                RefreshVehs();
                Game.DisplayNotification("~g~ReportsPlus Listener Loaded Successfully.");
                Game.LogTrivial("ReportsPlus Listener Loaded Successfully.");
            }
        }



        // Loaders
        private void LoadCurrentIDDocument()
        {
            string filePath = Path.Combine(FileDataFolder, "currentID.xml");
            if (File.Exists(filePath))
            {
                currentIDDoc = XDocument.Load(filePath);
            }
        }
        private void LoadCalloutDocument()
        {
            string filePath = Path.Combine(FileDataFolder, "callout.xml");
            if (File.Exists(filePath))
            {
                calloutDoc = XDocument.Load(filePath);
            }
            else
            {
                calloutDoc = new XDocument(new XElement("Callouts"));
                calloutDoc.Save(filePath);
            }
        }


        // Events
        private void EstablishEvents()
        {
            StopThePed.API.Events.askIdEvent += AskForIDEvent;
            StopThePed.API.Events.pedArrestedEvent += ArrestPedEvent;
            StopThePed.API.Events.patDownPedEvent += PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += DriversLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += PassengerLicenseEvent;
            StopThePed.API.Events.stopPedEvent += StopPedEvent;
        }
        private static void CalloutEvent()
        {
            LSPD_First_Response.Mod.API.Events.OnCalloutDisplayed += Events_OnCalloutDisplayed;

            void Events_OnCalloutDisplayed(LHandle handle)
            {
                Game.LogTrivial("ReportsPlus: Displaying Callout");
                Callout callout = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
                string calloutId = GenerateCalloutId();

                string agency = LSPD_First_Response.Mod.API.Functions.GetCurrentAgencyScriptName();
                string priority = "default";
                string description = "";
                string name = callout.FriendlyName;

                if (callout.ScriptInfo is CalloutInterfaceAPI.CalloutInterfaceAttribute calloutInterfaceInfo)
                {
                    agency = calloutInterfaceInfo.Agency.Length > 0 ? calloutInterfaceInfo.Agency : agency;
                    priority = calloutInterfaceInfo.Priority.Length > 0 ? calloutInterfaceInfo.Priority : "default";
                    description = calloutInterfaceInfo.Description;
                    name = calloutInterfaceInfo.Name;
                }

                string street = World.GetStreetName(World.GetStreetHash(callout.CalloutPosition));
                WorldZone zone = LSPD_First_Response.Mod.API.Functions.GetZoneAtPosition(callout.CalloutPosition);
                string currentTime = DateTime.Now.ToString("h:mm:ss tt");
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                // Remove ~ and anything inside it from callout.CalloutMessage
                string cleanCalloutMessage = Regex.Replace(callout.CalloutMessage, @"~.*?~", "").Trim();

                // Clear existing callouts before adding new one
                calloutDoc = new XDocument(new XElement("Callouts"));
                XElement calloutElement = new XElement("Callout",
                    new XElement("Number", calloutId),
                    new XElement("Type", cleanCalloutMessage),
                    new XElement("Description", description),
                    new XElement("Priority", priority),
                    new XElement("Street", street),
                    new XElement("Area", zone.RealAreaName),
                    new XElement("County", zone.County),
                    new XElement("StartTime", currentTime),
                    new XElement("StartDate", currentDate)
                );

                calloutDoc.Root.Add(calloutElement);
                calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                Game.LogTrivial($"ReportsPlus: Callout {calloutId} data updated and displayed.");
            }
        }
        private static void AskForIDEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentID(ped);
        }
        private static void ArrestPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }
        private static void PatDownPedEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentID(ped);
        }
        private static void DriversLicenseEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentID(ped);
        }
        private static void PassengerLicenseEvent(Vehicle vehicle)
        {
            Ped[] passengers = vehicle.Passengers;
            for (int i = 0; i < passengers.Length; i++)
            {
                UpdateCurrentID(passengers[i]);
            }
        }
        private static void StopPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }


        // Utils
        private static string GenerateCalloutId()
        {
            return new Random().Next(10000, 100000).ToString();
        }
        private static void CreatePedObj(Ped ped)
        {
            if (ped.Exists())
            {
                string data = GetPedData(ped);
                string oldFile = File.ReadAllText($"{FileDataFolder}/worldPeds.data");
                if (oldFile.Contains(LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(ped).FullName)) return;

                string addComma = oldFile.Length > 0 ? "," : "";

                File.WriteAllText($"{FileDataFolder}/worldPeds.data", $"{oldFile}{addComma}{data}");
            }
        }
        private static void Int()
        {
            while (IsOnDuty)
            {
                RefreshPeds();
                RefreshVehs();
                GameFiber.Wait(15000);
            }
        }
        public override void Finally()
        {
            LSPD_First_Response.Mod.API.Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (dataCollectionFiber != null && dataCollectionFiber.IsAlive)
                dataCollectionFiber.Abort();

            currentIDDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener cleaned up.");
        }
        private bool CheckPlugins()
        {
            bool hasCalloutInterface = IsPluginInstalled("CalloutInterface");
            bool hasStopThePed = IsPluginInstalled("StopThePed");

            return hasCalloutInterface && hasStopThePed;
        }
        private bool IsPluginInstalled(string pluginName)
        {
            var plugins = LSPD_First_Response.Mod.API.Functions.GetAllUserPlugins();
            bool isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Game.LogTrivial($"Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }


        // Refreshers
        internal static void UpdateCalloutData(string calloutId, string key, string value)
        {

            // Load the XML file if not already loaded or if it might have changed
            calloutDoc = XDocument.Load(Path.Combine(FileDataFolder, "callout.xml"));

            // Find the callout with the specified ID
            XElement calloutElement = calloutDoc.Descendants("Callout")
                                                .FirstOrDefault(c => c.Element("ID")?.Value == calloutId);

            if (calloutElement != null)
            {
                // Find the element to update
                XElement elementToUpdate = calloutElement.Element(key);
                if (elementToUpdate != null)
                {
                    // Update the element's value
                    elementToUpdate.Value = value;
                    Game.LogTrivial($"ReportsPlus: Updated {key} for callout ID {calloutId} to {value}");

                    // Save the changes back to the XML file
                    calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                }
                else
                {
                    // Key does not exist, so add it
                    calloutElement.Add(new XElement(key, value));
                    Game.LogTrivial($"ReportsPlus: Added {key} for callout ID {calloutId} with value {value}");
                    calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                }
            }
            else
            {
                Game.LogTrivial("ReportsPlus: No callout found with the specified ID");
            }
        }
        private static void UpdateCurrentID(Ped ped)
        {

            if (!ped.Exists())
                return;

            var persona = LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(ped);
            var existingEntry = currentIDDoc.Descendants("ID").FirstOrDefault(e => e.Element("Name")?.Value == persona.FullName);
            if (existingEntry != null)
                return;

            int index = ped.IsInAnyVehicle(false) ? ped.SeatIndex + 2 : 0;
            XElement newEntry = new XElement("ID",
                new XElement("Name", persona.FullName),
                new XElement("Birthday", $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}"),
                new XElement("Gender", persona.Gender),
                new XElement("Index", index)
            );

            currentIDDoc.Root.Add(newEntry);
            currentIDDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            Game.LogTrivial("ReportsPlus: Updated currentID data file");
        }
        private static void RefreshVehs()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlus: Failed to update worldCars.data; Invalid LocalPlayer");
                return;
            }
            Vehicle[] allCars = LocalPlayer.GetNearbyVehicles(15);
            string[] carsList = new string[allCars.Length];

            for (int i = 0; i < allCars.Length; i++)
            {
                Vehicle car = allCars[i];
                if (car.Exists())
                {
                    carsList[Array.IndexOf(allCars, car)] = GetWorldCarData(car);
                }
            }
            File.WriteAllText($"{FileDataFolder}/worldCars.data", string.Join(",", carsList));
            Game.LogTrivial("ReportsPlus: Updated veh data file");
        }
        private static void RefreshPeds()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlus: Failed to update ped data; Invalid LocalPlayer");
                return;
            }
            Ped[] allPeds = LocalPlayer.GetNearbyPeds(15);
            string[] persList = new string[allPeds.Length];

            for (int i = 0; i < allPeds.Length; i++)
            {
                Ped ped = allPeds[i];
                if (ped.Exists())
                {
                    persList[Array.IndexOf(allPeds, ped)] = GetPedData(ped);
                }
            }

            File.WriteAllText($"{FileDataFolder}/worldPeds.data", string.Join(",", persList));

            Game.LogTrivial("ReportsPlus: Updated ped data file");
        }


        // Get Info
        private static string GetRegistration(Vehicle car)
        {
            switch (StopThePed.API.Functions.getVehicleRegistrationStatus(car))
            {
                case STPVehicleStatus.Expired:
                    return "Expired";
                case STPVehicleStatus.None:
                    return "None";
                case STPVehicleStatus.Valid:
                    return "Valid";
            }
            return "";
        }
        private static string GetInsuranceInfo(Vehicle car)
        {
            switch (StopThePed.API.Functions.getVehicleInsuranceStatus(car))
            {
                case STPVehicleStatus.Expired:
                    return "Expired";
                case STPVehicleStatus.None:
                    return "None";
                case STPVehicleStatus.Valid:
                    return "Valid";
            }
            return "";
        }

        private static string GetWorldCarData(Vehicle car)
        {
            string driver = car.Driver.Exists() ? LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(car.Driver).FullName : "";
            string color = Rage.Native.NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(car) != -1 ? "" : $"{car.PrimaryColor.R}-{car.PrimaryColor.G}-{car.PrimaryColor.B}";
            return $"licensePlate={car.LicensePlate}&model={car.Model.Name}&isStolen={car.IsStolen}&isPolice={car.IsPoliceVehicle}&owner={LSPD_First_Response.Mod.API.Functions.GetVehicleOwnerName(car)}&driver={driver}&registration={GetRegistration(car)}&insurance={GetInsuranceInfo(car)}&color={color}";
        }
        private static string GetPedData(Ped ped)
        {
            Persona persona = LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(ped);
            string birthday = $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}";
            return $"name={persona.FullName}&birthday={birthday}&gender={persona.Gender}&isWanted={persona.Wanted}&licenseStatus={persona.ELicenseState}&relationshipGroup={ped.RelationshipGroup.Name}";
        }

        private int CalculateAge(DateTime birthday)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - birthday.Year;
            if (birthday > today.AddYears(-age)) age--;
            return age;
        }

    }
}