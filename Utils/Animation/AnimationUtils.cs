using System.Xml.Serialization;
using LSPD_First_Response.Mod.API;
using Rage;
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

            if (!IsPlayerWithinDistanceOfVeh(nearbyVeh, 2.5f))
            {
                Game.LogTrivial(
                    $"ReportsPlusListener: Player is not within 2 units of the vehicle. Distance: {nearbyVeh.Position.DistanceTo(LocalPlayer.Position):F2} units.");
                return;
            }

            if (_animationFiber == null || !_animationFiber.IsAlive)
            {
                _animationFiber = new GameFiber(() =>
                {
                    while (true)
                    {
                        GameFiber.Yield();
                        if (!_isAnimationActive) continue;

                        Game.LogTrivial("ReportsPlusListener: Playing animation: " + StartName);
                        LocalPlayer.Tasks.PlayAnimation(new AnimationDictionary(StartDict), StartName, 5f,
                            AnimationFlags.None);

                        var startTime = Game.GameTime;
                        while (Game.GameTime - startTime < 10000 && _isAnimationActive) GameFiber.Yield();

                        if (!_isAnimationActive) continue;
                        Game.LogTrivial("ReportsPlusListener: Animation finished after 10 seconds.");

                        if (Functions.IsPlayerPerformingPullover())
                        {
                            Functions.ForceEndCurrentPullover();
                            Game.LogTrivial("ReportsPlusListener: Pullover Ended!");
                            Game.DisplayNotification("~g~Handed Citation!");
                            //TODO: finish implementation
                        }

                        _isAnimationActive = false;
                    }
                });
                _animationFiber.Start();
            }

            _isAnimationActive = true;
        }
    }
}