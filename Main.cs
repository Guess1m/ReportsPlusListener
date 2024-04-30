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
using System.Xml.Linq;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        private static readonly string DataPath = "ReportsPlus\\data";
        private static XDocument currentIDDoc;
        private static XDocument calloutDoc;
        private GameFiber dataCollectionFiber;
        internal static bool CurrentlyOnDuty;
        internal static Ped Player => Game.LocalPlayer.Character;
        private static Dictionary<LHandle, string> calloutIds = new Dictionary<LHandle, string>();


        public override void Initialize()
        {
            calloutIds.Clear();
            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            currentIDDoc = new XDocument(new XElement("IDs"));
            LoadCurrentIDDocument();
            LoadCalloutDocument();

            LSPD_First_Response.Mod.API.Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlusListener Plugin initialized.");
        }

        public override void Finally()
        {
            LSPD_First_Response.Mod.API.Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (dataCollectionFiber != null && dataCollectionFiber.IsAlive)
                dataCollectionFiber.Abort();

            currentIDDoc.Save(Path.Combine(DataPath, "currentID.xml"));
            calloutDoc.Save(Path.Combine(DataPath, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener cleaned up.");
        }

        private void LoadCurrentIDDocument()
        {
            string filePath = Path.Combine(DataPath, "currentID.xml");
            if (File.Exists(filePath))
            {
                currentIDDoc = XDocument.Load(filePath);
            }
        }
        private void LoadCalloutDocument()
        {
            string filePath = Path.Combine(DataPath, "callout.xml");
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

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            CurrentlyOnDuty = onDuty;
            if (onDuty)
            {
                GameFiber.StartNew(Interval);
                SetupEventHandlers();
                AddCalloutEventWithCI();
                UpdateWorldPeds();
                UpdateWorldCars();
                Game.DisplayNotification("ReportsPlusListener loaded successfully.");
            }
        }

        private void SetupEventHandlers()
        {
            StopThePed.API.Events.askIdEvent += Events_askIdEvent;
            StopThePed.API.Events.pedArrestedEvent += Events_pedArrestedEvent;
            StopThePed.API.Events.patDownPedEvent += Events_patDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += Events_askDriverLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += Events_askPassengerIdEvent;
            StopThePed.API.Events.stopPedEvent += Events_stopPedEvent;
        }

        private static void AddCalloutEventWithCI()
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

                // Clear existing callouts before adding new one
                calloutDoc.Root.Elements("Callout").Remove();

                XElement calloutElement = new XElement("Callout",
                    new XElement("Number", calloutId),
                    new XElement("Type", callout.CalloutMessage),
                    new XElement("Description", description),
                    new XElement("Priority", priority),
                    new XElement("Street", street),
                    new XElement("Area", zone.RealAreaName),
                    new XElement("County", zone.County),
                    new XElement("StartTime", currentTime),
                    new XElement("StartDate", currentDate)
                );

                calloutDoc.Root.Add(calloutElement);
                calloutDoc.Save(Path.Combine(DataPath, "callout.xml"));
                Game.LogTrivial($"ReportsPlus: Callout {calloutId} data updated and displayed.");
            }
        }

            private static string GenerateCalloutId()
        {
            return new Random().Next(10000, 100000).ToString();
        }

        internal static void UpdateCalloutData(string calloutId, string key, string value)
        {
            Game.LogTrivial("ReportsPlus: Update callout data");

            // Load the XML file if not already loaded or if it might have changed
            calloutDoc = XDocument.Load(Path.Combine(DataPath, "callout.xml"));

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
                    calloutDoc.Save(Path.Combine(DataPath, "callout.xml"));
                }
                else
                {
                    // Key does not exist, so add it
                    calloutElement.Add(new XElement(key, value));
                    Game.LogTrivial($"ReportsPlus: Added {key} for callout ID {calloutId} with value {value}");
                    calloutDoc.Save(Path.Combine(DataPath, "callout.xml"));
                }
            }
            else
            {
                Game.LogTrivial("ReportsPlus: No callout found with the specified ID");
            }
        }

        private static void UpdateCurrentID(Ped ped)
        {
            Game.LogTrivial("ReportsPlus: Update currentID.data");

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
            currentIDDoc.Save(Path.Combine(DataPath, "currentID.xml"));
            Game.LogTrivial("ReportsPlus: Updated currentID.data");
        }

        private static void AddWorldPed(Ped ped)
        {
            if (ped.Exists())
            {
                string data = GetWorldPedData(ped);
                string oldFile = File.ReadAllText($"{DataPath}/worldPeds.data");
                if (oldFile.Contains(LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(ped).FullName)) return;

                string addComma = oldFile.Length > 0 ? "," : "";

                File.WriteAllText($"{DataPath}/worldPeds.data", $"{oldFile}{addComma}{data}");
            }
        }

        // STP
        private static void Events_askIdEvent(Ped ped)
        {
            AddWorldPed(ped);
            UpdateCurrentID(ped);
        }

        private static void Events_pedArrestedEvent(Ped ped)
        {
            AddWorldPed(ped);
        }

        private static void Events_patDownPedEvent(Ped ped)
        {
            AddWorldPed(ped);
            UpdateCurrentID(ped);
        }

        private static void Events_askDriverLicenseEvent(Ped ped)
        {
            AddWorldPed(ped);
            UpdateCurrentID(ped);
        }

        private static void Events_askPassengerIdEvent(Vehicle vehicle)
        {
            Ped[] passengers = vehicle.Passengers;
            for (int i = 0; i < passengers.Length; i++)
            {
                UpdateCurrentID(passengers[i]);
            }
        }

        private static void Events_stopPedEvent(Ped ped)
        {
            AddWorldPed(ped);
        }


        private static void Interval()
        {
            while (CurrentlyOnDuty)
            {
                UpdateWorldPeds();
                UpdateWorldCars();
                GameFiber.Wait(15000);
            }
        }

        private static void UpdateWorldCars()
        {
            Game.LogTrivial("ReportsPlus: Update worldCars.data");
            if (!Player.Exists())
            {
                Game.LogTrivial("ReportsPlus: Failed to update worldCars.data; Invalid Player");
                return;
            }
            Vehicle[] allCars = Player.GetNearbyVehicles(15);
            string[] carsList = new string[allCars.Length];

            for (int i = 0; i < allCars.Length; i++)
            {
                Vehicle car = allCars[i];
                if (car.Exists())
                {
                    carsList[Array.IndexOf(allCars, car)] = GetWorldCarData(car);
                }
            }
            File.WriteAllText($"{DataPath}/worldCars.data", string.Join(",", carsList));
            Game.LogTrivial("ReportsPlus: Updated worldCars.data");
        }

        // update world data
        private static void UpdateWorldPeds()
        {
            Game.LogTrivial("ReportsPlus: Update worldPeds.data");
            if (!Player.Exists())
            {
                Game.LogTrivial("ReportsPlus: Failed to update worldPeds.data; Invalid Player");
                return;
            }
            Ped[] allPeds = Player.GetNearbyPeds(15);
            string[] persList = new string[allPeds.Length];

            for (int i = 0; i < allPeds.Length; i++)
            {
                Ped ped = allPeds[i];
                if (ped.Exists())
                {
                    persList[Array.IndexOf(allPeds, ped)] = GetWorldPedData(ped);
                }
            }

            File.WriteAllText($"{DataPath}/worldPeds.data", string.Join(",", persList));

            Game.LogTrivial("ReportsPlus: Updated worldPeds.data");
        }

        // world data
        // STP
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

        private static string GetInsurance(Vehicle car)
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
            return $"licensePlate={car.LicensePlate}&model={car.Model.Name}&isStolen={car.IsStolen}&isPolice={car.IsPoliceVehicle}&owner={LSPD_First_Response.Mod.API.Functions.GetVehicleOwnerName(car)}&driver={driver}&registration={GetRegistration(car)}&insurance={GetInsurance(car)}&color={color}";
        }



        private static string GetWorldPedData(Ped ped)
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