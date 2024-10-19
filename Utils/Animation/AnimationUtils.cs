using System.Xml.Serialization;
using Rage;
using static ReportsPlus.Main;

namespace ReportsPlus.Utils
{
    public class AnimationUtils
    {
        internal static bool IsAnimationActive;
        [XmlAttribute("IntroDict")] public static string StartDict { get; } = "veh@busted_std";
        [XmlAttribute("IntroName")] public static string StartName { get; } = "issue_ticket_cop";

        internal static bool CheckRequirements()
        {
            return LocalPlayer.Exists() && LocalPlayer.IsAlive &&
                   LocalPlayer.IsValid() && LocalPlayer.IsOnFoot && !LocalPlayer.IsRagdoll &&
                   !LocalPlayer.IsReloading && !LocalPlayer.IsFalling && !LocalPlayer.IsInAir &&
                   !LocalPlayer.IsJumping && !LocalPlayer.IsInWater && !LocalPlayer.IsGettingIntoVehicle;
        }

        internal static void EndAction()
        {
            LocalPlayer.Tasks.Clear();
            GameFiber.Wait(1);
            IsAnimationActive = false;
        }

        public static void PlayAnimation()
        {
            if (!CheckRequirements())
                return;

            if (IsAnimationActive)
            {
                Game.LogTrivial("ReportsPlusListener: Stopping current animation.");
                LocalPlayer.Tasks.Clear();
                IsAnimationActive = false;
            }
            else
            {
                Game.LogTrivial("ReportsPlusListener: Playing animation: " + StartName);
                LocalPlayer.Tasks.PlayAnimation(new AnimationDictionary(StartDict), StartName, 5f, AnimationFlags.None);
                IsAnimationActive = true;
            }
        }
    }

    public enum AnimationStage
    {
        Start,
        Main,
        End,
        None
    }
}