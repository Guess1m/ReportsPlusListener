using System.IO;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils;
using ReportsPlus.Utils.Data;
using static ReportsPlus.Utils.Data.EventUtils;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        /*
         UPDATE: Update Version
         */
        private static readonly string Version = "v1.4.0-alpha";
        public static readonly string FileDataFolder = "ReportsPlus\\data";

        internal static bool IsOnDuty;

        public static XDocument CurrentIdDoc;
        public static XDocument CalloutDoc;

        public static bool HasStopThePed;
        public static bool HasPolicingRedefined;
        private static bool _hasCalloutInterface;
        public static bool HasCommonDataFramework;

        internal static Ped LocalPlayer => Game.LocalPlayer.Character;

        /*
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the C# implementation and ideas for adding the GTA V integration.
         */

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlusListener Plugin initialized. Version: " + Version);

            ConfigUtils.LoadSettings();
        }

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            IsOnDuty = onDuty;
            Game.LogTrivial("ReportsPlusListener: IsOnDuty State Changed: '" + IsOnDuty + "'");
            if (!onDuty) return;
            Utils.Utils.CalloutIds.Clear();
            if (!Directory.Exists(FileDataFolder))
                Directory.CreateDirectory(FileDataFolder);

            ConfigUtils.CreateFiles();

            if (File.Exists(DataCollection.CitationSignalFilePath))
            {
                Game.LogTrivial("ReportsPlusListener: Found old citationSignalFile, Deleting");
                File.Delete(DataCollection.CitationSignalFilePath);
            }

            DataCollection.CombinedDataCollectionFiber =
                GameFiber.StartNew(DataCollection.StartCombinedDataCollectionFiber);

            DataCollection.DataCollectionFiber = GameFiber.StartNew(DataCollection.StartDataCollectionFiber);

            DataCollection.SignalFileCheckFiber = GameFiber.StartNew(DataCollection.StartSignalFileCheckFiber);

            RunPluginChecks();

            Game.DisplayNotification("~g~ReportsPlus-" + Version + " Loaded!" +
                                     "\n~b~Citation Keybind: ~y~" + Utils.Utils.AnimationBind);
            Game.LogTrivial("ReportsPlusListener: " + Version + ", Loaded Successfully Animation Keybind: " +
                            Utils.Utils.AnimationBind);
        }

        private void RunPluginChecks()
        {
            _hasCalloutInterface = ConfigUtils.IsPluginInstalled("CalloutInterface");
            HasStopThePed = ConfigUtils.IsPluginInstalled("StopThePed");
            HasPolicingRedefined = ConfigUtils.IsPluginInstalled("PolicingRedefined");
            HasCommonDataFramework = ConfigUtils.IsPluginInstalled("CommonDataFramework");

            if (_hasCalloutInterface)
            {
                EstablishCiEvent();
                Game.LogTrivial("ReportsPlusListener: Found Callout Interface");
            }
            else
            {
                Game.LogTrivial("ReportsPlusListener: CalloutInterface not found. Required for Callout Functions.");
                Game.DisplayNotification(
                    "~r~ReportsPlusListener: CalloutInterface not found. Required for Callout Functions.");
            }

            if (HasPolicingRedefined && HasCommonDataFramework)
            {
                EstablishEventsPr();
                Game.LogTrivial("ReportsPlusListener: Found Policing Redefined and Common Data Framework");
                HasStopThePed = false;
            }
            else
            {
                Game.LogTrivial("ReportsPlusListener: Policing Redefined/CDF not found, checking for STP");
                if (HasStopThePed)
                {
                    EstablishEventsStp();
                    Game.LogTrivial("ReportsPlusListener: Found StopThePed");
                }
                else
                {
                    Game.LogTrivial("ReportsPlusListener: StopThePed/PR not found.\nUsing base game functions.");
                    EstablishEventsBaseGame();
                    Game.DisplayNotification(
                        "~r~ReportsPlusListener: StopThePed/PR not found.");
                }
            }
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (DataCollection.CombinedDataCollectionFiber != null &&
                DataCollection.CombinedDataCollectionFiber.IsAlive)
                DataCollection.CombinedDataCollectionFiber.Abort();

            if (DataCollection.DataCollectionFiber != null &&
                DataCollection.DataCollectionFiber.IsAlive)
                DataCollection.DataCollectionFiber.Abort();

            if (DataCollection.SignalFileCheckFiber != null &&
                DataCollection.SignalFileCheckFiber.IsAlive)
                DataCollection.SignalFileCheckFiber.Abort();

            CurrentIdDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            CalloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener: cleaned up.");
        }
    }
}