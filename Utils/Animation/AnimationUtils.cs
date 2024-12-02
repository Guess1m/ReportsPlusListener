using System.Xml.Serialization;
using Rage;
using static ReportsPlus.Main;

namespace ReportsPlus.Utils.Animation
{
    public static class AnimationUtils
    {
        private static bool _isAnimationActive;
        [XmlAttribute("IntroDict")] public static string StartDict { get; } = "veh@busted_std";
        [XmlAttribute("IntroName")] public static string StartName { get; } = "issue_ticket_cop";

        internal static bool CheckRequirements()
        {
            return LocalPlayer.Exists() && LocalPlayer.IsAlive &&
                   LocalPlayer.IsValid() && LocalPlayer.IsOnFoot && !LocalPlayer.IsRagdoll &&
                   !LocalPlayer.IsReloading && !LocalPlayer.IsFalling && !LocalPlayer.IsInAir &&
                   !LocalPlayer.IsJumping && !LocalPlayer.IsInWater && !LocalPlayer.IsGettingIntoVehicle;
        }

        public static void PlayAnimation()
        {
            if (!CheckRequirements())
                return;

            if (_isAnimationActive)
            {
                Game.LogTrivial("ReportsPlusListener: Stopping current animation.");
                LocalPlayer.Tasks.Clear();
                _isAnimationActive = false;
            }
            else
            {
                Game.LogTrivial("ReportsPlusListener: Playing animation: " + StartName);
                LocalPlayer.Tasks.PlayAnimation(new AnimationDictionary(StartDict), StartName, 5f, AnimationFlags.None);
                _isAnimationActive = true;
            }
        }
    }
}