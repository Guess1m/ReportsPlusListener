using System;
using System.IO;
using System.Xml.Serialization;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using ReportsPlus.Utils.Data;
using static ReportsPlus.Main;

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
                   LocalPlayer.IsValid() && Functions.IsPlayerPerformingPullover() && LocalPlayer.IsOnFoot &&
                   !LocalPlayer.IsRagdoll &&
                   !LocalPlayer.IsReloading && !LocalPlayer.IsFalling && !LocalPlayer.IsInAir &&
                   !LocalPlayer.IsJumping && !LocalPlayer.IsInWater && !LocalPlayer.IsGettingIntoVehicle;
        }

        private static bool IsPlayerWithinDistanceOfVeh(Vehicle targetVeh, float maxDistance)
        {
            if (targetVeh == null || !targetVeh.Exists())
                return false;

            var playerPosition = Game.LocalPlayer.Character.Position;
            var vehPos = targetVeh.Position;

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

            var nearbyVeh = LocalPlayer.GetNearbyVehicles(1)[0];

            if (nearbyVeh == null || !nearbyVeh.Exists())
            {
                Game.LogTrivial("ReportsPlusListener: No nearby vehicle found or vehicle does not exist.");
                return;
            }

            if (DataCollection.currentStoppedVehicle == null)
            {
                Game.LogTrivial("ReportsPlusListener: currentStoppedVehicle is null.");
                return;
            }

            if (nearbyVeh != DataCollection.currentStoppedVehicle)
            {
                Game.LogTrivial("ReportsPlusListener: Nearby vehicle does not match the current stopped vehicle.");
                return;
            }

            if (!DataCollection.citationSignalFound)
            {
                Game.LogTrivial("ReportsPlusListener: Citation signal not found.");
                return;
            }

            if (!IsPlayerWithinDistanceOfVeh(nearbyVeh, 3f))
            {
                Game.LogTrivial(
                    $"ReportsPlusListener: Player is not within 3 units of the vehicle. Distance: {nearbyVeh.Position.DistanceTo(LocalPlayer.Position):F2} units.");
                return;
            }

            if (_animationFiber == null || !_animationFiber.IsAlive)
            {
                _animationFiber = new GameFiber(AnimationFiber);
                _animationFiber.Start();
            }

            _isAnimationActive = true;
        }

        // Thank You, Lenny <3
        private static void AnimationFiber()
        {
            while (true)
            {
                GameFiber.Yield();
                if (!_isAnimationActive) continue;

                Game.LogTrivial("ReportsPlusListener: Playing animation: " + StartName);
                Game.DisplaySubtitle("Handing Citation..");
                LocalPlayer.Tasks.PlayAnimation(StartDict, StartName, 0.7f, AnimationFlags.None);
                GameFiber.Wait(1600);
                NativeFunction.Natives.x28004F88151E03E0(Game.LocalPlayer.Character, StartName,
                    StartDict, 0.5f);
                Game.LogTrivial("ReportsPlusListener: Animation finished");

                if (!_isAnimationActive) continue;

                if (Functions.IsPlayerPerformingPullover())
                {
                    Game.DisplaySubtitle("~g~Handed Citation, Return to Vehicle.");
                    Game.LogTrivial("ReportsPlusListener: Handed Citation");

                    var random = new Random();
                    var randomNumber = random.Next(1000, 2001);
                    GameFiber.Wait(randomNumber);

                    try
                    {
                        Functions.ForceEndCurrentPullover();
                    }
                    catch (Exception e)
                    {
                        Game.LogTrivial("ReportsPlusListener: EndTrafficStop failed: " + e.Message);
                    }

                    Game.LogTrivial("ReportsPlusListener: Pullover Ended!");
                }

                _isAnimationActive = false;
                DataCollection.citationSignalFound = false;
                if (File.Exists(DataCollection.CitationSignalFilePath))
                {
                    Game.LogTrivial("ReportsPlusListener: Deleting citation signal file.");
                    File.Delete(DataCollection.CitationSignalFilePath);
                }

                Game.LogTrivial("ReportsPlusListener: citationSignalFound & _isAnimationActive set false");
            }
        }
    }
}