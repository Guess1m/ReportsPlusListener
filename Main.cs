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
        private static readonly string Version = "v1.4.1-alpha";
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
            Game.LogTrivial("ReportsPlusListener Plugin Initialized. Version: " + Version);

            ConfigUtils.LoadSettings();
        }

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            IsOnDuty = onDuty;
            Game.LogTrivial("ReportsPlusListener: IsOnDuty State Changed: '" + IsOnDuty + "'");
            if (!onDuty) return;
            Utils.Utils.CalloutIds.Clear();
            Utils.Utils.PedAddresses.Clear();
            Utils.Utils.PedLicenseNumbers.Clear();
            if (!Directory.Exists(FileDataFolder))
                Directory.CreateDirectory(FileDataFolder);

            ConfigUtils.CreateFiles();

            if (File.Exists(DataCollection.CitationSignalFilePath))
            {
                Game.LogTrivial("ReportsPlusListener: Found Old CitationSignalFile, Deleting");
                File.Delete(DataCollection.CitationSignalFilePath);
            }

            RunPluginChecks();

            GameFiber.StartNew(() =>
            {
                DataCollection.TrafficStopCollectionFiber =
                    GameFiber.StartNew(DataCollection.TrafficStopCollection, "CombinedDataCollection");

                DataCollection.WorldDataCollectionFiber =
                    GameFiber.StartNew(DataCollection.WorldDataCollection, "DataCollection");

                DataCollection.SignalFileCheckFiber =
                    GameFiber.StartNew(DataCollection.SignalFileCheck, "SignalFileCheck");

                Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlusListener",
                    "~g~Version: " + Version + " Loaded!", "~w~Citation Keybind: ~y~" + Utils.Utils.AnimationBind);
                Game.LogTrivial("ReportsPlusListener: " + Version + ", Loaded Successfully Animation Keybind: " +
                                Utils.Utils.AnimationBind);
            }, "ReportsPlusListener");
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

                Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlusListener",
                    "~r~CalloutInterface Not Found", "Required for Callout Functions");
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
                    Game.LogTrivial("ReportsPlusListener: StopThePed/PR not found. Using base game functions.");
                    EstablishEventsBaseGame();

                    Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlusListener",
                        "~r~StopThePed/PR Not Found", "Using Base LSPDFR Functions");
                }
            }
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (DataCollection.TrafficStopCollectionFiber != null &&
                DataCollection.TrafficStopCollectionFiber.IsAlive)
                DataCollection.TrafficStopCollectionFiber.Abort();

            if (DataCollection.WorldDataCollectionFiber != null &&
                DataCollection.WorldDataCollectionFiber.IsAlive)
                DataCollection.WorldDataCollectionFiber.Abort();

            if (DataCollection.SignalFileCheckFiber != null &&
                DataCollection.SignalFileCheckFiber.IsAlive)
                DataCollection.SignalFileCheckFiber.Abort();

            CurrentIdDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            CalloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener: Cleaned Up.");
        }
    }
}