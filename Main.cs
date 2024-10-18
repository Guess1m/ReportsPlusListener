using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CalloutInterfaceAPI;
using LSPD_First_Response.Engine.Scripting;
using LSPD_First_Response.Engine.Scripting.Entities;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;
using StopThePed.API;
using Events = LSPD_First_Response.Mod.API.Events;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        /*
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the C# implementation and ideas for adding the GTA V integration.
         */

        // Vars
        private static readonly string FileDataFolder = "ReportsPlus\\data";
        private static XDocument CurrentIDDoc;
        private static XDocument _calloutDoc;
        private GameFiber DataCollectionFiber;
        internal static bool IsOnDuty;
        internal static Ped LocalPlayer => Game.LocalPlayer.Character;
        private static readonly Dictionary<LHandle, string> CalloutIds = new Dictionary<LHandle, string>();

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

        private static readonly Dictionary<string, string> PedAddresses = new Dictionary<string, string>();

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
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
                    Game.DisplayNotification(
                        "~r~ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    Game.LogTrivial("ReportsPlus requires CalloutInterface.dll and StopThePed.dll to be installed.");
                    return;
                }

                CalloutIds.Clear();
                if (!Directory.Exists(FileDataFolder))
                    Directory.CreateDirectory(FileDataFolder);

                CreateFiles();

                GameFiber.StartNew(StartDataCollectionFiber);
                EstablishEvents();
                CalloutEvent();
                RefreshPeds();
                RefreshVehs();
                RefreshStreet();

                GameFiber.StartNew(delegate
                {
                    GameFiber.Wait(6000);
                    CreateTrafficStopFiber();
                });

                Game.DisplayNotification("~g~ReportsPlus Listener Loaded Successfully.");
                Game.LogTrivial("ReportsPlus Listener Loaded Successfully.");
            }
        }


        // Events
        private void EstablishEvents()
        {
            StopThePed.API.Events.askIdEvent += AskForIdEvent;
            StopThePed.API.Events.pedArrestedEvent += ArrestPedEvent;
            StopThePed.API.Events.patDownPedEvent += PatDownPedEvent;
            StopThePed.API.Events.askDriverLicenseEvent += DriversLicenseEvent;
            StopThePed.API.Events.askPassengerIdEvent += PassengerLicenseEvent;
            StopThePed.API.Events.stopPedEvent += StopPedEvent;
        }

        private void CreateTrafficStopFiber()
        {
            Game.LogTrivial("ReportsPlus TrafficStopFiber has been started");

            bool isPerformingPullover = false;

            while (true)
            {
                GameFiber.Yield();
                if (Functions.IsPlayerPerformingPullover())
                {
                    Game.LogTrivial("Check 1: performing pullover");
                    if (!isPerformingPullover)
                    {
                        Game.LogTrivial("Check 2: not already performing pullover");
                        GameFiber.StartNew(delegate
                        {
                            try
                            {
                                //Safety checks
                                if (!Functions.IsPlayerPerformingPullover())
                                {
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 3: player is performing pullover");

                                if (!Game.LocalPlayer.Character.IsInAnyVehicle(false))
                                {
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 4: Player is in vehicle");

                                Vehicle playerCar = Game.LocalPlayer.Character.CurrentVehicle;
                                Vehicle stoppedCar = (Vehicle)World.GetClosestEntity(
                                    playerCar.GetOffsetPosition(Vector3.RelativeFront * 8f), 8f,
                                    (GetEntitiesFlags.ConsiderGroundVehicles | GetEntitiesFlags.ConsiderBoats |
                                     GetEntitiesFlags.ExcludeEmptyVehicles |
                                     GetEntitiesFlags.ExcludeEmergencyVehicles));
                                if (stoppedCar == null)
                                {
                                    Game.DisplayNotification(
                                        "Unable to detect the pulled over vehicle. Make sure you're behind the vehicle and try again.");
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 5: stopped car isn't null");

                                if (!stoppedCar.IsValid() || (stoppedCar == playerCar))
                                {
                                    Game.DisplayNotification(
                                        "Unable to detect the pulled over vehicle. Make sure you're behind the vehicle and try again.");
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 6: stopped car is valid and isn't the playerCar");

                                if (stoppedCar.Speed > 0.2f)
                                {
                                    Game.DisplayNotification("The vehicle must be stopped before they can mimic you.");
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 7: stopped car isn't moving");

                                Ped pulledDriver = stoppedCar.Driver;
                                if (!pulledDriver.IsPersistent ||
                                    Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) != pulledDriver)
                                {
                                    Game.DisplayNotification(
                                        "Unable to detect the pulled over vehicle. Make sure you're in front of the vehicle and try again.");
                                    isPerformingPullover = false;
                                    return;
                                }

                                Game.LogTrivial("Check 8: stopped driver is persistent and pulloversuspect matches");

                                Game.LogTrivial("Found pulled over vehicle! Model: " + stoppedCar.Model +
                                                " Driver name: " + pulledDriver.RelationshipGroup.Name);
                                Game.DisplayNotification("Found pulled over vehicle! Model: " + stoppedCar.Model +
                                                         " Driver name: " + pulledDriver.RelationshipGroup.Name);
                            }
                            catch (Exception e)
                            {
                                Game.LogTrivial(e.ToString());
                                Game.LogTrivial("Error handled.");
                            }
                            finally
                            {
                                isPerformingPullover = false;
                            }
                        });
                    }
                }
            }
        }


        private static void CalloutEvent()
        {
            Events.OnCalloutDisplayed += EventsOnCalloutDisplayed;

            void EventsOnCalloutDisplayed(LHandle handle)
            {
                Game.LogTrivial("ReportsPlus: Displaying Callout");
                Callout callout = CalloutInterface.API.Functions.GetCalloutFromHandle(handle);
                string calloutId = GenerateCalloutId();

                string agency = Functions.GetCurrentAgencyScriptName();
                string priority = "default";
                string description = "";
                string calMessage = "";
                string name = callout.FriendlyName; //todo find implementation for

                if (callout.ScriptInfo is CalloutInterfaceAttribute calloutInterfaceInfo)
                {
                    agency = calloutInterfaceInfo.Agency.Length > 0 ? calloutInterfaceInfo.Agency : agency;
                    priority = calloutInterfaceInfo.Priority.Length > 0 ? calloutInterfaceInfo.Priority : "default";
                    description = calloutInterfaceInfo.Description;
                    calMessage = callout.CalloutMessage;
                    name = calloutInterfaceInfo.Name;
                }

                string street = World.GetStreetName(World.GetStreetHash(callout.CalloutPosition));
                var zone = Functions.GetZoneAtPosition(callout.CalloutPosition);
                string currentTime = DateTime.Now.ToString("h:mm:ss tt");
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                string cleanCalloutMessage = Regex.Replace(callout.CalloutMessage, @"~.*?~", "").Trim();

                _calloutDoc = new XDocument(new XElement("Callouts"));
                var calloutElement = new XElement("Callout",
                    new XElement("Number", calloutId),
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

                _calloutDoc.Root?.Add(calloutElement);
                _calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
                Game.LogTrivial($"ReportsPlus: Callout {calloutId} data updated and displayed.");
            }
        }

        private static void AskForIdEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void ArrestPedEvent(Ped ped)
        {
            CreatePedObj(ped);
        }

        private static void PatDownPedEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void DriversLicenseEvent(Ped ped)
        {
            CreatePedObj(ped);
            UpdateCurrentId(ped);
        }

        private static void PassengerLicenseEvent(Vehicle vehicle)
        {
            Ped[] passengers = vehicle.Passengers;
            for (int i = 0; i < passengers.Length; i++)
            {
                UpdateCurrentId(passengers[i]);
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
                if (oldFile.Contains(Functions.GetPersonaForPed(ped).FullName)) return;

                string addComma = oldFile.Length > 0 ? "," : "";

                File.WriteAllText($"{FileDataFolder}/worldPeds.data", $"{oldFile}{addComma}{data}");
            }
        }

        private static void StartDataCollectionFiber()
        {
            while (IsOnDuty)
            {
                // todo add interval config
                Random random = new Random();
                RefreshPeds();
                GameFiber.Wait(random.Next(3000, 6000));
                RefreshStreet();
                GameFiber.Wait(random.Next(3000, 6000));
                RefreshVehs();
                GameFiber.Wait(random.Next(3000, 6000));
            }
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (DataCollectionFiber != null && DataCollectionFiber.IsAlive)
                DataCollectionFiber.Abort();

            CurrentIDDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            _calloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
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
            var plugins = Functions.GetAllUserPlugins();
            bool isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Game.LogTrivial($"Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }


        // Refreshers
        private static void UpdateCurrentId(Ped ped)
        {
            if (!ped.Exists())
                return;

            var persona = Functions.GetPersonaForPed(ped);
            var fullName = persona.FullName;
            string pedModel = GetPedModel(ped);

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
                new XElement("PedModel", pedModel),
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

        private static void RefreshStreet()
        {
            if (!LocalPlayer.Exists())
            {
                Game.LogTrivial("ReportsPlus: Failed to update location data; Invalid LocalPlayer");
                return;
            }

            String currentStreet = World.GetStreetName(LocalPlayer.Position);
            String currentZone = GetPedCurrentZoneName();

            File.WriteAllText($"{FileDataFolder}/location.data", currentStreet + ", " + currentZone);

            Game.LogTrivial("ReportsPlus: Updated location data file");
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
            string driver = car.Driver.Exists()
                ? Functions.GetPersonaForPed(car.Driver).FullName
                : "";
            string color = NativeFunction.Natives.GET_VEHICLE_LIVERY<int>(car) != -1
                ? ""
                : $"{car.PrimaryColor.R}-{car.PrimaryColor.G}-{car.PrimaryColor.B}";
            return
                $"licensePlate={car.LicensePlate}&model={car.Model.Name}&isStolen={car.IsStolen}&isPolice={car.IsPoliceVehicle}&owner={Functions.GetVehicleOwnerName(car)}&driver={driver}&registration={GetRegistration(car)}&insurance={GetInsuranceInfo(car)}&color={color}";
        }

        private static string GetPedData(Ped ped)
        {
            Persona persona = Functions.GetPersonaForPed(ped);
            string birthday = $"{persona.Birthday.Month}/{persona.Birthday.Day}/{persona.Birthday.Year}";
            string fullName = persona.FullName;
            string address;
            string licenseNum = GenerateLicenseNumber();
            string pedModel = GetPedModel(ped);

            if (!PedAddresses.ContainsKey(fullName))
            {
                address = GetRandomAddress();
                PedAddresses.Add(fullName, address);
            }
            else
            {
                address = PedAddresses[fullName];
            }

            return
                $"name={persona.FullName}&licenseNumber={licenseNum}&pedModel={pedModel}&birthday={birthday}&gender={persona.Gender}&address={address}&isWanted={persona.Wanted}&licenseStatus={persona.ELicenseState}&relationshipGroup={ped.RelationshipGroup.Name}";
        }

        private static string GetPedModel(Ped ped)
        {
            return ped.Model.Name;
        }

        private static string GetPedCurrentZoneName()
        {
            return Functions.GetZoneAtPosition(Game.LocalPlayer.Character.Position).RealAreaName;
        }


        // Utils
        private static string GenerateLicenseNumber()
        {
            StringBuilder licenseNum = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] randomNumber = new byte[1];
                for (int i = 0; i < 10; i++)
                {
                    rng.GetBytes(randomNumber);
                    int digit = randomNumber[0] % 10;
                    licenseNum.Append(digit);
                }
            }

            return licenseNum.ToString();
        }

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