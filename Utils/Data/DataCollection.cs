using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using PolicingRedefined.API;
using PolicingRedefined.Interaction.Assets.PedAttributes;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;
using ReportsPlus.Utils.ALPR;
using ReportsPlus.Utils.Animation;
using ReportsPlus.Utils.Menu;
using static ReportsPlus.Utils.Menu.MenuProcessing;
using static ReportsPlus.Utils.Data.UpdateUtils;
using static ReportsPlus.Utils.Misc;

namespace ReportsPlus.Utils.Data
{
    public static class DataCollection
    {
        public static GameFiber TrafficStopCollectionFiber;
        public static GameFiber KeyCollectionFiber;
        public static GameFiber WorldDataCollectionFiber;
        public static GameFiber SignalFileCheckFiber;
        public static GameFiber ActivePulloverCheckFiber;
        public static bool CitationSignalFound;
        public static string CitationSignalName;
        public static string CitationSignalPlate;
        public static string CitationSignalType;
        public static string CitationSignalCharges;
        public static string CitationSignalFine;
        public static string CitationSignalArrestable;
        private static string _lastPulledOverPlate = "";
        private static bool _giveTicketKeyWasPressed;
        private static bool _discardTicketKeyWasPressed;
        private static bool _alprKeyWasPressed;

        public static readonly string CitationSignalFilePath = Path.Combine(Path.GetTempPath(), "ReportsPlusSignalFile.txt");

        public static void KeyCollection()
        {
            while (Main.IsOnDuty)
            {
                GameFiber.Yield();

                if (AnimationBind != Keys.None)
                {
                    if (Game.IsKeyDown(AnimationBind) && !TabView.IsAnyPauseMenuVisible && !UIMenu.IsAnyMenuVisible)
                    {
                        if (!_giveTicketKeyWasPressed)
                        {
                            AnimationUtils.PlayAnimation();
                            _giveTicketKeyWasPressed = true;
                        }
                    }
                    else
                    {
                        _giveTicketKeyWasPressed = false;
                    }
                }

                if (ALPRMenuBind != Keys.None)
                {
                    if (Game.IsKeyDown(ALPRMenuBind))
                    {
                        if (!_alprKeyWasPressed)
                        {
                            ALPRUtils.ToggleAlpr();
                            _alprKeyWasPressed = true;
                        }
                    }
                    else
                    {
                        _alprKeyWasPressed = false;
                    }
                }

                if (DiscardBind == Keys.None) continue;
                if (Game.IsKeyDown(DiscardBind))
                {
                    if (_discardTicketKeyWasPressed) continue;
                    AnimationUtils.RunDiscardCitation();
                    _discardTicketKeyWasPressed = true;
                }
                else
                {
                    _discardTicketKeyWasPressed = false;
                }
            }
        }

        public static void TrafficStopCollection()
        {
            while (Main.IsOnDuty)
            {
                if (Functions.IsPlayerPerformingPullover() && !IsPerformingPullover)
                {
                    if (ActivePulloverCheckFiber is { IsAlive: true }) ActivePulloverCheckFiber.Abort();

                    ActivePulloverCheckFiber = GameFiber.StartNew(() =>
                    {
                        IsPerformingPullover = true;
                        CheckPullover();
                        IsPerformingPullover = false;
                    }, "ReportsPlus-PulloverCheck");

                    GameFiber.Wait(1000);
                }

                GameFiber.Yield();
            }
        }

        private static void CheckPullover()
        {
            try
            {
                if (!Functions.IsPlayerPerformingPullover())
                {
                    IsPerformingPullover = false;
                    return;
                }

                if (!Game.LocalPlayer.Character.IsInAnyVehicle(false))
                {
                    IsPerformingPullover = false;
                    return;
                }

                var playerCar = Game.LocalPlayer.Character.CurrentVehicle;
                var stoppedCar = GetStoppedCar(playerCar);

                if (stoppedCar == null || !IsValidStoppedCar(stoppedCar, playerCar))
                {
                    IsPerformingPullover = false;
                    return;
                }

                var pulledDriver = stoppedCar.Driver;
                var driverName = pulledDriver.Exists() ? Functions.GetPersonaForPed(pulledDriver).FullName : "";
                if (Main.HasPolicingRedefined && Main.HasCommonDataFramework) driverName = GetValueMethods.GetFullNamePr(pulledDriver);

                if (stoppedCar.LicensePlate == _lastPulledOverPlate) return;

                _lastPulledOverPlate = stoppedCar.LicensePlate;

                if (!pulledDriver.IsPersistent || Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) != pulledDriver)
                {
                    IsPerformingPullover = false;
                    return;
                }

                Game.LogTrivial("ReportsPlusListener: Found pulled over vehicle, Driver name: " + driverName + "; Plate: " + stoppedCar.LicensePlate);
                if (Main.HasPolicingRedefined && Main.HasCommonDataFramework) Game.LogTrivial("ReportsPlusListener: Found pulled over vehicle, Driver name: " + driverName + "; " + GetValueMethods.GetOwnerType(stoppedCar) + " ; Plate: " + stoppedCar.LicensePlate);

                WorldDataUtils.CreateTrafficStopObj(stoppedCar);
            }
            catch (Exception e)
            {
                Game.LogTrivial("ReportsPlusListener ERROR: " + e);
            }
            finally
            {
                IsPerformingPullover = false;
            }
        }

        public static void WorldDataCollection()
        {
            while (Main.IsOnDuty)
            {
                GameFiber.Wait(ConfigUtils.RefreshDelay);
                RefreshPeds();
                GameFiber.Wait(MathUtils.Rand.Next(500, 1200));
                RefreshVehs();
                GameFiber.Wait(MathUtils.Rand.Next(500, 1200));
                RefreshGameData();
            }
        }

        public static void SignalFileCheck()
        {
            while (Main.IsOnDuty)
            {
                var givecitdesc = "";

                GameFiber.Wait(5000);
                if (CitationSignalFound) continue;

                if (!File.Exists(CitationSignalFilePath)) continue;

                Game.LogTrivial("ReportsPlusListener: CitationSignal File Found; " + CitationSignalFilePath);
                try
                {
                    var parts = File.ReadAllText(CitationSignalFilePath).Split('|');

                    if (string.IsNullOrEmpty(parts[0]))
                    {
                        Game.LogTrivial("ReportsPlusListener: CitationSignal is empty or invalid.");
                        if (File.Exists(CitationSignalFilePath)) File.Delete(CitationSignalFilePath);
                        continue;
                    }

                    string type = string.Empty, name = string.Empty, plate = string.Empty, charges = string.Empty, fine = string.Empty, arrestable = string.Empty;

                    foreach (var part in parts)
                    {
                        var keyValue = part.Split('=');
                        if (keyValue.Length != 2)
                        {
                            Game.LogTrivial($"ReportsPlusListener: Invalid key-value pair '{part}'");
                            continue;
                        }

                        var key = keyValue[0].Trim();
                        var value = keyValue[1].Trim();

                        switch (key)
                        {
                            case "name":
                                name = value;
                                break;
                            case "plate":
                                plate = value;
                                break;
                            case "type":
                                type = value;
                                break;
                            case "charges":
                                charges = value;
                                break;
                            case "fine":
                                fine = value;
                                break;
                            case "arrestable":
                                arrestable = value;
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(type))
                    {
                        switch (type)
                        {
                            case "2": // Printed citation
                                CitationSignalName = name;
                                CitationSignalPlate = null;
                                CitationSignalCharges = charges;
                                CitationSignalFine = fine;
                                CitationSignalArrestable = arrestable;
                                CitationSignalType = type;

                                if (Main.HasPolicingRedefined && Main.HasCommonDataFramework)
                                {
                                    Game.LogTrivial("ReportsPlusListener: Using PolicingRedefined to handle citation.");
                                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Created Printed Citation", "~y~Citation For: ~b~" + name);

                                    var targetPed = Main.LocalPlayer.GetNearbyPeds(4).FirstOrDefault(p => p != null && p.Exists() && GetValueMethods.GetFullNamePr(p).Equals(name, StringComparison.OrdinalIgnoreCase));
                                    if (targetPed != null && targetPed.Exists())
                                    {
                                        if (!int.TryParse(CitationSignalFine, out var fineAmount))
                                        {
                                            fineAmount = 0;
                                            Game.LogTrivial($"ReportsPlusListener: Could not parse fine '{CitationSignalFine}'. Defaulting to 0.");
                                        }

                                        if (!bool.TryParse(CitationSignalArrestable, out var isArrestable))
                                        {
                                            isArrestable = false;
                                            Game.LogTrivial($"ReportsPlusListener: Could not parse arrestable status '{CitationSignalArrestable}'. Defaulting to false.");
                                        }

                                        var citation = new Citation(targetPed, CitationSignalCharges, fineAmount, isArrestable);
                                        PedAPI.GiveCitationToPed(targetPed, citation);
                                        Game.LogTrivial($"ReportsPlusListener: Gave citation to {name} via PolicingRedefined API. Charges: {CitationSignalCharges}, Fine: {fineAmount}, Arrestable: {isArrestable}");
                                    }
                                    else
                                    {
                                        Game.LogTrivial($"ReportsPlusListener: Ped for PR citation '{name}' not found nearby.");
                                    }

                                    AnimationUtils.DiscardCitation();
                                }
                                else
                                {
                                    Game.LogTrivial("ReportsPlusListener: Using NativeFunctions to handle citation.");
                                    CitationSignalFound = true;
                                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Created Citation", "~y~Citation For: ~b~" + name + "\n~w~Menu Keybind: ~y~" + MainMenuBind);
                                    givecitdesc = "Citation for " + name;
                                    var giveCitationMenuButton = new UIMenuItem("Give Citation", givecitdesc)
                                    {
                                        ForeColor = Color.FromArgb(34, 139, 34),
                                        HighlightedForeColor = Color.FromArgb(34, 139, 34)
                                    };
                                    giveCitationMenuButton.Activated += (sender, args) => { AnimationUtils.PlayAnimation(); };
                                    MenuProcessing.MainMenu.AddItem(giveCitationMenuButton);
                                }

                                break;

                            case "3": // Parking citation
                                Game.LogTrivial("ReportsPlusListener: Received ParkingCitation for: " + plate);
                                CitationSignalFound = true;
                                CitationSignalName = null;
                                CitationSignalPlate = plate;
                                CitationSignalType = type;
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Created Parking Citation", "~y~Citation For: ~b~" + plate + "\n~w~Menu Keybind: ~y~" + MainMenuBind);
                                givecitdesc = "Citation for " + plate;

                                var giveParkingCitationMenuButton = new UIMenuItem("Give Citation", givecitdesc)
                                {
                                    ForeColor = Color.FromArgb(34, 139, 34),
                                    HighlightedForeColor = Color.FromArgb(34, 139, 34)
                                };
                                giveParkingCitationMenuButton.Activated += (sender, args) => { AnimationUtils.PlayAnimation(); };
                                MenuProcessing.MainMenu.AddItem(giveParkingCitationMenuButton);
                                break;

                            case "1":
                            default: // Non-printed or invalid type
                                Game.LogTrivial("ReportsPlusListener: Received Non-Printed or unknown citation type. Processing and deleting signal file.");
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Citation Logged", "~y~A non-printed citation has been processed.");

                                if (File.Exists(CitationSignalFilePath)) File.Delete(CitationSignalFilePath);
                                break;
                        }
                    }
                    else
                    {
                        Game.LogTrivial("ReportsPlusListener: Missing or invalid type in signal file. Deleting file.");
                        if (File.Exists(CitationSignalFilePath)) File.Delete(CitationSignalFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"ReportsPlusListener: Error reading signal file - {ex}. Deleting to prevent loop.");
                    if (File.Exists(CitationSignalFilePath)) File.Delete(CitationSignalFilePath);
                    continue;
                }

                if (!CitationSignalFound) continue;

                if (!Main.HasPolicingRedefined || !Main.HasCommonDataFramework)
                {
                    var discardCitationMenuButton = new UIMenuItem("Discard Citation")
                    {
                        ForeColor = Color.FromArgb(226, 82, 47),
                        HighlightedForeColor = Color.FromArgb(226, 82, 47)
                    };
                    discardCitationMenuButton.Activated += (sender, args) => { AnimationUtils.RunDiscardCitation(); };
                    MenuProcessing.MainMenu.AddItem(discardCitationMenuButton);
                }
            }
        }

        private static Vehicle GetStoppedCar(Vehicle playerCar)
        {
            return (Vehicle)World.GetClosestEntity(playerCar.GetOffsetPosition(Vector3.RelativeFront * 8f), 8f, GetEntitiesFlags.ConsiderGroundVehicles | GetEntitiesFlags.ConsiderBoats | GetEntitiesFlags.ExcludeEmptyVehicles | GetEntitiesFlags.ExcludeEmergencyVehicles);
        }

        private static bool IsValidStoppedCar(Vehicle stoppedCar, Vehicle playerCar)
        {
            return stoppedCar.IsValid() && stoppedCar != playerCar && stoppedCar.Speed <= 0.2f;
        }
    }
}