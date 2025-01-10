using System;
using System.IO;
using LSPD_First_Response.Mod.API;
using Rage;
using static ReportsPlus.Utils.Data.RefreshUtils;
using static ReportsPlus.Utils.Animation.AnimationUtils;
using static ReportsPlus.Utils.Utils;

namespace ReportsPlus.Utils.Data
{
    public static class DataCollection
    {
        public static GameFiber CombinedDataCollectionFiber;
        public static GameFiber DataCollectionFiber;
        public static GameFiber SignalFileCheckFiber;
        public static Vehicle currentStoppedVehicle;
        public static bool citationSignalFound;
        private static string _lastPulledOverPlate = "";
        public static readonly String CitationSignalFilePath = Path.Combine(Path.GetTempPath(), "ReportsPlusSignalFile.txt");

        public static void StartCombinedDataCollectionFiber()
        {
            while (Main.IsOnDuty)
            {
                if (Game.IsKeyDown(AnimationBind)) PlayAnimation();

                CheckForTrafficStop();
            }
        }

        public static void StartDataCollectionFiber()
        {
            while (Main.IsOnDuty) RefreshDataWithRandomDelay();
        }

        private static void RefreshDataWithRandomDelay()
        {
            var random = new Random();
            var delay = random.Next(ConfigUtils.RefreshDelay, ConfigUtils.RefreshDelay + 1500);
            GameFiber.Wait(delay);
            RefreshPeds();
            GameFiber.Wait(random.Next(1200, 2300));
            RefreshStreet();
            GameFiber.Wait(random.Next(1200, 2300));
            RefreshVehs();
        }

        private static void CheckForTrafficStop()
        {
            if (Functions.IsPlayerPerformingPullover() && !IsPerformingPullover) GameFiber.StartNew(CheckPullover);

            GameFiber.Yield();
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

                GetterUtils.CreateTrafficStopObj(stoppedCar);

                currentStoppedVehicle = stoppedCar;
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

        public static void StartSignalFileCheckFiber()
        {
            while (Main.IsOnDuty)
            {
                GameFiber.Wait(5000);
                if (citationSignalFound) continue;

                if (!File.Exists(CitationSignalFilePath)) continue;
                Game.LogTrivial("ReportsPlusListener: Citation Signal file found");

                citationSignalFound = true;
                GameFiber.Wait(1000);
                Game.DisplaySubtitle("~w~ReportsPlus: Give Citation Keybind: ~y~" + AnimationBind);

                File.Delete(CitationSignalFilePath);
                Game.LogTrivial("ReportsPlusListener: Signal file removed.");
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