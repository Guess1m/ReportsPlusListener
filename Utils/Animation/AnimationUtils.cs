using System;
using System.IO;
using System.Xml.Serialization;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Data;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.Data.MenuProcessing;

namespace ReportsPlus.Utils.Animation
{
    public static class AnimationUtils
    {
        private static bool _isAnimationActive;
        private static GameFiber _animationFiber;

        [XmlAttribute("IntroDict")] public static string StartDict { get; } = "veh@busted_std";
        [XmlAttribute("IntroName")] public static string StartName { get; } = "issue_ticket_cop";

        private static bool CheckRequirements()
        {
            return LocalPlayer.Exists() && LocalPlayer.IsAlive &&
                   LocalPlayer.IsValid() && LocalPlayer.IsOnFoot &&
                   !LocalPlayer.IsRagdoll &&
                   !LocalPlayer.IsReloading && !LocalPlayer.IsFalling && !LocalPlayer.IsInAir &&
                   !LocalPlayer.IsJumping && !LocalPlayer.IsInWater && !LocalPlayer.IsGettingIntoVehicle;
        }

        private static bool IsPlayerWithinDistanceOfPed(Ped targetPed, float maxDistance)
        {
            if (targetPed == null || !targetPed.Exists())
                return false;

            var playerPosition = Game.LocalPlayer.Character.Position;
            var vehPos = targetPed.Position;

            var distance = Vector3.Distance(playerPosition, vehPos);

            return distance <= maxDistance;
        }

        private static bool IsPlayerWithinDistanceOfVeh(Vehicle targetVehicle, float maxDistance)
        {
            if (targetVehicle == null || !targetVehicle.Exists())
                return false;

            var playerPosition = Game.LocalPlayer.Character.Position;
            var vehPos = targetVehicle.Position;

            var distance = Vector3.Distance(playerPosition, vehPos);

            return distance <= maxDistance;
        }

        public static void PlayAnimation()
        {
            if (!CheckRequirements())
            {
                Game.LogTrivial("ReportsPlusListener: CheckRequirements failed.");
                return;
            }

            if (!DataCollection.CitationSignalFound)
            {
                Game.LogTrivial("ReportsPlusListener: CitationSignal Not Found.");
                return;
            }

            if (DataCollection.CitationSignalType != null)
                switch (DataCollection.CitationSignalType)
                {
                    case "2":
                        var nearbyPed = LocalPlayer.GetNearbyPeds(1)[0];
                        if (nearbyPed == null || !nearbyPed.Exists())
                        {
                            Game.LogTrivial("ReportsPlusListener: No nearby ped found or ped does not exist.");
                            return;
                        }

                        if (!IsPlayerWithinDistanceOfPed(nearbyPed, 2.7f))
                        {
                            Game.LogTrivial(
                                $"ReportsPlusListener: Player is not within 2.7 units of the ped: [{DataCollection.CitationSignalName}]. Distance: {nearbyPed.Position.DistanceTo(LocalPlayer.Position):F2} units.");
                            Game.DisplaySubtitle(
                                $"~r~Move Closer to The Ped, Distance: ~y~{nearbyPed.Position.DistanceTo(LocalPlayer.Position):F2} ~r~units." +
                                "\n~w~Press ~y~" +
                                Misc.DiscardBind + " ~w~To Discard Citation");
                            return;
                        }

                        var pedName = Functions.GetPersonaForPed(nearbyPed).FullName.ToLower();
                        if (!pedName.Equals(DataCollection.CitationSignalName.ToLower()))
                        {
                            Game.LogTrivial("ReportsPlusListener: ped name: " + pedName +
                                            " is invalid. Citation for: " +
                                            DataCollection.CitationSignalName.ToLower());
                            Game.DisplaySubtitle("~r~Cannot Give Citation, Citation is For: ~y~" +
                                                 DataCollection.CitationSignalName + "\n~w~Press ~y~" +
                                                 Misc.DiscardBind + " ~w~To Discard Citation");
                            return;
                        }

                        break;
                    case "3":
                        var nearbyVeh = LocalPlayer.GetNearbyVehicles(1)[0];
                        if (nearbyVeh == null || !nearbyVeh.Exists())
                        {
                            Game.LogTrivial("ReportsPlusListener: No nearby veh found or ped does not exist.");
                            return;
                        }

                        if (!IsPlayerWithinDistanceOfVeh(nearbyVeh, 2f))
                        {
                            Game.LogTrivial(
                                $"ReportsPlusListener: Player is not within 2 units of the veh: [{DataCollection.CitationSignalPlate}]. Distance: {nearbyVeh.Position.DistanceTo(LocalPlayer.Position):F2} units.");
                            Game.DisplaySubtitle(
                                $"~w~Move Closer to The Vehicle, Distance: ~y~{nearbyVeh.Position.DistanceTo(LocalPlayer.Position):F2} ~w~units." +
                                "\n~w~Press ~y~" +
                                Misc.DiscardBind + " ~w~To Discard Citation");
                            return;
                        }

                        var vehiclePlate = nearbyVeh.LicensePlate.ToLower();
                        if (!vehiclePlate.Equals(DataCollection.CitationSignalPlate.ToLower()))
                        {
                            Game.LogTrivial("ReportsPlusListener: vehicle plate: " + vehiclePlate +
                                            " is invalid. Citation for: " +
                                            DataCollection.CitationSignalPlate.ToLower());

                            Game.DisplaySubtitle("~r~Cannot Give Citation, Citation is For: ~y~" +
                                                 DataCollection.CitationSignalPlate + "\n~w~Press ~y~" +
                                                 Misc.DiscardBind + " ~w~To Discard Citation");
                            return;
                        }

                        break;
                    default:
                        Game.LogTrivial("ReportsPlusListener: non-printed so returning");
                        return;
                }

            if (!(_animationFiber is { IsAlive: true }))
            {
                _animationFiber = new GameFiber(AnimationFiber);
                _animationFiber.Start();
            }

            _isAnimationActive = true;
        }

        public static void RunDiscardCitation()
        {
            if (!CheckRequirements()) return;

            if (!DataCollection.CitationSignalFound) return;

            if (DataCollection.CitationSignalType == null) return;

            Game.LogTrivial("ReportsPlusListener: DiscardCitation Running");

            switch (DataCollection.CitationSignalType)
            {
                case "2":
                    Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlus",
                        "~r~Citation Discarded",
                        "~y~Citation for: ~b~" + DataCollection.CitationSignalName + " ~y~Has Been Discarded");
                    break;
                case "3":
                    Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlus",
                        "~r~Citation Discarded",
                        "~y~Citation for: ~b~" + DataCollection.CitationSignalPlate + " ~y~Has Been Discarded");
                    break;
            }

            DiscardCitation();
        }

        private static void DiscardCitation()
        {
            DataCollection.CitationSignalFound = false;
            if (File.Exists(DataCollection.CitationSignalFilePath)) File.Delete(DataCollection.CitationSignalFilePath);

            DataCollection.CitationSignalName = null;
            DataCollection.CitationSignalType = null;
            DataCollection.CitationSignalPlate = null;

            Game.LogTrivial("ReportsPlusListener: Deleted CitationSignal File / Clearing Data");

            // Remove second-to-last item if it exists
            MainMenu.RefreshIndex();
            if (MainMenu.MenuItems.Count > 0) MainMenu.MenuItems.RemoveAt(MainMenu.MenuItems.Count - 1);

            MainMenu.RefreshIndex();

            // Remove new last item if it exists
            if (MainMenu.MenuItems.Count > 0) MainMenu.MenuItems.RemoveAt(MainMenu.MenuItems.Count - 1);
            MainMenu.RefreshIndex();
        }

        private static void AnimationFiber()
        {
            while (true)
            {
                GameFiber.Yield();
                GameFiber.Wait(100);
                if (!_isAnimationActive) continue;

                Game.LogTrivial("ReportsPlusListener: Playing Animation: " + StartName);
                LocalPlayer.Tasks.PlayAnimation(StartDict, StartName, 0.7f, AnimationFlags.None);
                GameFiber.Wait(1600);
                NativeFunction.Natives.x28004F88151E03E0(Game.LocalPlayer.Character, StartName,
                    StartDict, 0.5f);
                Game.LogTrivial("ReportsPlusListener: Animation Finished");
                _isAnimationActive = false;

                if (DataCollection.CitationSignalType != null)
                    switch (DataCollection.CitationSignalType)
                    {
                        case "2":
                            Game.DisplaySubtitle("~g~Handed Citation to ~w~" + DataCollection.CitationSignalName);
                            Game.LogTrivial("ReportsPlusListener: Handed Citation to " +
                                            DataCollection.CitationSignalName);
                            break;
                        case "3":
                            Game.DisplaySubtitle("~g~Placed Citation on ~w~" + DataCollection.CitationSignalPlate);
                            Game.LogTrivial("ReportsPlusListener: Placed Parking Citation on " +
                                            DataCollection.CitationSignalPlate);
                            break;
                    }

                DiscardCitation();

                if (!Functions.IsPlayerPerformingPullover()) continue;
                Game.DisplaySubtitle("~g~Handed Citation, Return to Vehicle.");
                Game.LogTrivial("ReportsPlusListener: Handed Citation, Ending Traffic Stop");

                Game.LogTrivial("ReportsPlusListener: Enter Vehicle Fiber Started");
                while (true)
                {
                    if (!LocalPlayer.IsGettingIntoVehicle)
                    {
                        Game.LogTrivial("ReportsPlusListener: Player not getting into vehicle, waiting one second");
                        GameFiber.Wait(1000);
                        continue;
                    }

                    var randomNumber = MathUtils.Rand.Next(4000, 7001);
                    Game.LogTrivial($"ReportsPlusListener: Player getting into vehicle, sleeping {randomNumber}ms");
                    GameFiber.Wait(randomNumber);
                    try
                    {
                        Functions.ForceEndCurrentPullover();
                    }
                    catch (Exception e)
                    {
                        Game.LogTrivial("ReportsPlusListener: EndTrafficStop Failed: " + e.Message);
                    }

                    Game.LogTrivial("ReportsPlusListener: Pullover Ended!");
                    break;
                }
            }
        }

        //TODO: add animation for ped receiving ticket
    }
}