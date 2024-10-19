using System;
using LSPD_First_Response.Mod.API;
using Rage;
using static ReportsPlus.Utils.RefreshUtils;
using static ReportsPlus.Utils.AnimationUtils;
using static ReportsPlus.Utils.Utils;

namespace ReportsPlus.Utils
{
    public class DataCollection
    {
        public static GameFiber DataCollectionFiber;
        public static GameFiber KeyCollectionFiber;
        public static GameFiber TrafficStopCollectionFiber;

        private static string
            lastPulledOverPlate = "";

        public static void KeyPressDetectionFiber()
        {
            while (Main.IsOnDuty)
            {
                if (Game.IsKeyDown(AnimationBind) && CheckRequirements()) PlayAnimation();

                GameFiber.Yield();
            }
        }

        public static void TrafficStopDataCollectionFiber()
        {
            while (Main.IsOnDuty)
            {
                CheckForTrafficStop();
                GameFiber.Yield();
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
            GameFiber.Wait(random.Next(500, 800));
            RefreshStreet();
            GameFiber.Wait(random.Next(500, 800));
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

                if (stoppedCar.LicensePlate == lastPulledOverPlate) return;

                lastPulledOverPlate = stoppedCar.LicensePlate;

                if (!pulledDriver.IsPersistent ||
                    Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) != pulledDriver)
                {
                    IsPerformingPullover = false;
                    return;
                }

                Game.LogTrivial("ReportsPlusListener: Found pulled over vehicle, Driver name: " + driverName +
                                " Plate: " + stoppedCar.LicensePlate);
                GetterUtils.CreateTrafficStopObj(stoppedCar);
            }
            catch (Exception e)
            {
                Game.LogTrivial(e.ToString());
                Game.LogTrivial("ReportsPlusListener: Error caught.");
            }
            finally
            {
                IsPerformingPullover = false;
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