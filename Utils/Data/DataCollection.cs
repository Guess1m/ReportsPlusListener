using System;
using System.Drawing;
using System.IO;
using LSPD_First_Response.Mod.API;
using Rage;
using RAGENativeUI.Elements;
using ReportsPlus.Utils.Animation;
using static ReportsPlus.Utils.Menu.MenuProcessing;
using static ReportsPlus.Utils.Data.UpdateUtils;
using static ReportsPlus.Utils.Misc;
using ALPRUtils = ReportsPlus.Utils.ALPR.ALPRUtils;

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
        private static string _lastPulledOverPlate = "";

        public static readonly string CitationSignalFilePath =
            Path.Combine(Path.GetTempPath(), "ReportsPlusSignalFile.txt");

        public static void KeyCollection()
        {
            if (Game.IsKeyDown(AnimationBind)) AnimationUtils.PlayAnimation();

            if (Game.IsKeyDown(DiscardBind)) AnimationUtils.RunDiscardCitation();

            if (Game.IsKeyDown(ALPRMenuBind)) ALPRUtils.ToggleAlpr();

            GameFiber.Yield();
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

                if (stoppedCar.LicensePlate == _lastPulledOverPlate) return;

                _lastPulledOverPlate = stoppedCar.LicensePlate;

                if (!pulledDriver.IsPersistent ||
                    Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) != pulledDriver)
                {
                    IsPerformingPullover = false;
                    return;
                }

                Game.LogTrivial("ReportsPlusListener: Found pulled over vehicle, Driver name: " + driverName +
                                " Plate: " + stoppedCar.LicensePlate);

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
                        continue;
                    }

                    string type = string.Empty, name = string.Empty, plate = string.Empty;

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
                        }
                    }

                    if (!string.IsNullOrEmpty(type))
                    {
                        CitationSignalFound = true;

                        switch (type)
                        {
                            case "2": // Printed citation
                                Game.LogTrivial("ReportsPlusListener: Received PrintedCitation for: " + name);
                                CitationSignalName = name;
                                CitationSignalPlate = null;
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                                    "~w~ReportsPlus",
                                    "~g~Created Citation",
                                    "~y~Citation For: ~b~" + name +
                                    "\n~w~Menu Keybind: ~y~" + MainMenuBind);
                                givecitdesc = "Citation for " + name;
                                break;

                            case "3": // Parking citation
                                Game.LogTrivial("ReportsPlusListener: Received ParkingCitation for: " + plate);
                                CitationSignalName = null;
                                CitationSignalPlate = plate;
                                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                                    "~w~ReportsPlus",
                                    "~g~Created Parking Citation",
                                    "~y~Citation For: ~b~" + plate +
                                    "\n~w~Menu Keybind: ~y~" + MainMenuBind);
                                givecitdesc = "Citation for " + plate;
                                break;

                            default: // Non-printed or invalid type
                                Game.LogTrivial("ReportsPlusListener: Received Non-Printed Citation");
                                CitationSignalFound = false;
                                break;
                        }

                        CitationSignalType = type;
                    }
                    else
                    {
                        Game.LogTrivial("ReportsPlusListener: Missing or invalid type");
                        CitationSignalFound = false;
                        CitationSignalName = null;
                        CitationSignalType = null;
                        CitationSignalPlate = null;
                    }
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"ReportsPlusListener: Error reading file - {ex}");
                    continue;
                }

                GameFiber.Wait(1000);

                var giveCitationMenuButton = new UIMenuItem("Give Citation", givecitdesc)
                {
                    ForeColor = Color.FromArgb(34, 139, 34),
                    HighlightedForeColor = Color.FromArgb(34, 139, 34)
                };
                giveCitationMenuButton.Activated += (sender, args) => { AnimationUtils.PlayAnimation(); };
                MainMenu.AddItem(giveCitationMenuButton);

                var discardCitationMenuButton = new UIMenuItem("Discard Citation")
                {
                    ForeColor = Color.FromArgb(226, 82, 47),
                    HighlightedForeColor = Color.FromArgb(226, 82, 47)
                };
                discardCitationMenuButton.Activated += (sender, args) => { AnimationUtils.RunDiscardCitation(); };
                MainMenu.AddItem(discardCitationMenuButton);
            }
        }

        private static Vehicle GetStoppedCar(Vehicle playerCar)
        {
            return (Vehicle)World.GetClosestEntity(
                playerCar.GetOffsetPosition(Vector3.RelativeFront * 8f), 8f,
                GetEntitiesFlags.ConsiderGroundVehicles | GetEntitiesFlags.ConsiderBoats |
                GetEntitiesFlags.ExcludeEmptyVehicles |
                GetEntitiesFlags.ExcludeEmergencyVehicles);
        }

        private static bool IsValidStoppedCar(Vehicle stoppedCar, Vehicle playerCar)
        {
            return stoppedCar.IsValid() && stoppedCar != playerCar && stoppedCar.Speed <= 0.2f;
        }
    }
}