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
        /* 
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the C# implementation and ideas for adding the GTA V integration.
        */

        // Vars
        private static readonly string FileDataFolder = "ReportsPlus\\data";
        private static XDocument currentIDDoc;
        private static XDocument calloutDoc;
        private GameFiber dataCollectionFiber;
        internal static bool IsOnDuty;
        internal static Ped LocalPlayer => Game.LocalPlayer.Character;
        private static Dictionary<LHandle, string> calloutIds = new Dictionary<LHandle, string>();
        private static readonly List<string> LosSantosAddresses = new List<string>
{
    "Abattoir Avenue",
    "Abe Milton Parkway",
    "Ace Jones Drive",
    "Adam's Apple Boulevard",
    "Aguja Street",
    "Alta Place",
    "Alta Street",
    "Amarillo Vista",
    "Amarillo Way",
    "Americano Way",
    "Atlee Street",
    "Autopia Parkway",
    "Banham Canyon Drive",
    "Barbareno Road",
    "Bay City Avenue",
    "Bay City Incline",
    "Baytree Canyon Road",
    "Boulevard Del Perro",
    "Bridge Street",
    "Brouge Avenue",
    "Buccaneer Way",
    "Buen Vino Road",
    "Caesars Place",
    "Calais Avenue",
    "Capital Boulevard",
    "Carcer Way",
    "Carson Avenue",
    "Chum Street",
    "Chupacabra Street",
    "Clinton Avenue",
    "Cockingend Drive",
    "Conquistador Street",
    "Cortes Street",
    "Cougar Avenue",
    "Covenant Avenue",
    "Cox Way",
    "Crusade Road",
    "Davis Avenue",
    "Decker Street",
    "Didion Drive",
    "Dorset Drive",
    "Dorset Place",
    "Dry Dock Street",
    "Dunstable Drive",
    "Dunstable Lane",
    "Dutch London Street",
    "Eastbourne Way",
    "East Galileo Avenue",
    "East Mirror Drive",
    "Eclipse Boulevard",
    "Edwood Way",
    "Elgin Avenue",
    "El Burro Boulevard",
    "El Rancho Boulevard",
    "Equality Way",
    "Exceptionalists Way",
    "Fantastic Place",
    "Fenwell Place",
    "Forum Drive",
    "Fudge Lane",
    "Galileo Road",
    "Gentry Lane",
    "Ginger Street",
    "Glory Way",
    "Goma Street",
    "Greenwich Parkway",
    "Greenwich Place",
    "Greenwich Way",
    "Grove Street",
    "Hanger Way",
    "Hangman Avenue",
    "Hardy Way",
    "Hawick Avenue",
    "Heritage Way",
    "Hillcrest Avenue",
    "Hillcrest Ridge Access Road",
    "Imagination Court",
    "Industry Passage",
    "Ineseno Road",
    "Integrity Way",
    "Invention Court",
    "Innocence Boulevard",
    "Jamestown Street",
    "Kimble Hill Drive",
    "Kortz Drive",
    "Labor Place",
    "Laguna Place",
    "Lake Vinewood Drive",
    "Las Lagunas Boulevard",
    "Liberty Street",
    "Lindsay Circus",
    "Little Bighorn Avenue",
    "Low Power Street",
    "Macdonald Street",
    "Mad Wayne Thunder Drive",
    "Magellan Avenue",
    "Marathon Avenue",
    "Marlowe Drive",
    "Melanoma Street",
    "Meteor Street",
    "Milton Road",
    "Mirror Park Boulevard",
    "Mirror Place",
    "Morningwood Boulevard",
    "Mount Haan Drive",
    "Mount Haan Road",
    "Mount Vinewood Drive",
    "Movie Star Way",
    "Mutiny Road",
    "New Empire Way",
    "Nikola Avenue",
    "Nikola Place",
    "Normandy Drive",
    "North Archer Avenue",
    "North Conker Avenue",
    "North Sheldon Avenue",
    "North Rockford Drive",
    "Occupation Avenue",
    "Orchardville Avenue",
    "Palomino Avenue",
    "Peaceful Street",
    "Perth Street",
    "Picture Perfect Drive",
    "Plaice Place",
    "Playa Vista",
    "Popular Street",
    "Portola Drive",
    "Power Street",
    "Prosperity Street",
    "Prosperity Street Promenade",
    "Red Desert Avenue",
    "Richman Street",
    "Rockford Drive",
    "Roy Lowenstein Boulevard",
    "Rub Street",
    "San Andreas Avenue",
    "Sandcastle Way",
    "San Vitus Boulevard",
    "Senora Road",
    "Shank Street",
    "Signal Street",
    "Sinner Street",
    "Sinners Passage",
    "South Arsenal Street",
    "South Boulevard Del Perro",
    "South Mo Milton Drive",
    "South Rockford Drive",
    "South Shambles Street",
    "Spanish Avenue",
    "Steele Way",
    "Strangeways Drive",
    "Strawberry Avenue",
    "Supply Street",
    "Sustancia Road",
    "Swiss Street",
    "Tackle Street",
    "Tangerine Street",
    "Tongva Drive",
    "Tower Way",
    "Tug Street",
    "Utopia Gardens",
    "Vespucci Boulevard",
    "Vinewood Boulevard",
    "Vinewood Park Drive",
    "Vitus Street",
    "Voodoo Place",
    "West Eclipse Boulevard",
    "West Galileo Avenue",
    "West Mirror Drive",
    "Whispymound Drive",
    "Wild Oats Drive",
    "York Street",
    "Zancudo Barranca"
};
        private static readonly List<string> BlaineCountyAddresses = new List<string>
{
    "Algonquin Boulevard",
    "Alhambra Drive",
    "Armadillo Avenue",
    "Baytree Canyon Road",
    "Calafia Road",
    "Cascabel Avenue",
    "Cassidy Trail",
    "Cat-Claw Avenue",
    "Chianski Passage",
    "Cholla Road",
    "Cholla Springs Avenue",
    "Duluoz Avenue",
    "East Joshua Road",
    "Fort Zancudo Approach Road",
    "Galileo Road",
    "Grapeseed Avenue",
    "Grapeseed Main Street",
    "Joad Lane",
    "Joshua Road",
    "Lesbos Lane",
    "Lolita Avenue",
    "Marina Drive",
    "Meringue Lane",
    "Mount Haan Road",
    "Mountain View Drive",
    "Niland Avenue",
    "North Calafia Way",
    "Nowhere Road",
    "O'Neil Way",
    "Paleto Boulevard",
    "Panorama Drive",
    "Procopio Drive",
    "Procopio Promenade",
    "Pyrite Avenue",
    "Raton Pass",
    "Route 68 Approach",
    "Seaview Road",
    "Senora Way",
    "Smoke Tree Road",
    "Union Road",
    "Zancudo Avenue",
    "Zancudo Road",
    "Zancudo Trail"
};


        private static Dictionary<string, string> PedAddresses = new Dictionary<string, string>();

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
                    Game.DisplayNotification("~r~ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    Game.LogTrivial("ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    return;
                }

                calloutIds.Clear();
                if (!Directory.Exists(FileDataFolder))
                    Directory.CreateDirectory(FileDataFolder);

                CreateFiles();

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

                string cleanCalloutMessage = Regex.Replace(callout.CalloutMessage, @"~.*?~", "").Trim();

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
                Random random = new Random();
                RefreshPeds();
                GameFiber.Wait(random.Next(4000, 5000));

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

            calloutDoc = XDocument.Load(Path.Combine(FileDataFolder, "callout.xml"));

            XElement calloutElement = calloutDoc.Descendants("Callout")
                                                .FirstOrDefault(c => c.Element("ID")?.Value == calloutId);

            if (calloutElement != null)
            {
                XElement elementToUpdate = calloutElement.Element(key);
                if (elementToUpdate != null)
                {
                    elementToUpdate.Value = value;
                    Game.LogTrivial($"ReportsPlus: Updated {key} for callout ID {calloutId} to {value}");

                    calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                }
                else
                {
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
            var fullName = persona.FullName;

            if (!PedAddresses.ContainsKey(fullName))
            {
                PedAddresses[fullName] = GetRandomAddress();
            }

            int index = ped.IsInAnyVehicle(false) ? ped.SeatIndex + 2 : 0;

            XElement newEntry = new XElement("ID",
                new XElement("Name", fullName),
                new XElement("Birthday", $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}"),
                new XElement("Gender", persona.Gender),
                new XElement("Address", PedAddresses[fullName]),
                new XElement("Index", index)
            );

            XDocument newDoc = new XDocument(new XElement("IDs"));
            newDoc.Root.Add(newEntry);

            newDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
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
            string[] pedsList = new string[allPeds.Length];

            for (int i = 0; i < allPeds.Length; i++)
            {
                Ped ped = allPeds[i];
                if (ped.Exists())
                {
                    pedsList[Array.IndexOf(allPeds, ped)] = GetPedData(ped);
                }
            }

            File.WriteAllText($"{FileDataFolder}/worldPeds.data", string.Join(",", pedsList));

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
            string fullName = persona.FullName;
            string address;

            if (!PedAddresses.ContainsKey(fullName))
            {
                address = GetRandomAddress();
                PedAddresses.Add(fullName, address);
            }
            else
            {
                address = PedAddresses[fullName];
            }


            return $"name={persona.FullName}&birthday={birthday}&gender={persona.Gender}&address={address}&isWanted={persona.Wanted}&licenseStatus={persona.ELicenseState}&relationshipGroup={ped.RelationshipGroup.Name}";
        }



        // Utils
        private void CreateFiles()
        {
            string dataFolder = "ReportsPlus\\data";

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            string[] filesToCreate = { "callout.xml", "currentID.xml", "worldCars.data", "worldPeds.data" };

            foreach (var fileName in filesToCreate)
            {
                string filePath = Path.Combine(dataFolder, fileName);
                if (!File.Exists(filePath))
                {
                    using (File.Create(filePath))
                    {
                        // Create an empty file
                    }
                }
            }
        }
        private int CalculateAge(DateTime birthday)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - birthday.Year;
            if (birthday > today.AddYears(-age)) age--;
            return age;
        }
        private static string GetRandomAddress()
        {
            Random random = new Random();
            List<string> chosenList = random.Next(2) == 0 ? LosSantosAddresses : BlaineCountyAddresses;
            int index = random.Next(chosenList.Count);
            string addressNumber = random.Next(1000).ToString().PadLeft(3, '0');
            string address = $"{addressNumber} {chosenList[index]}";

            while (PedAddresses.ContainsValue(address))
            {
                index = random.Next(chosenList.Count);
                addressNumber = random.Next(1000).ToString().PadLeft(3, '0');
                address = $"{addressNumber} {chosenList[index]}";
            }

            return address;
        }
    }
}