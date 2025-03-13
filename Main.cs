using System.IO;
using System.Text;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils;
using ReportsPlus.Utils.Data;
using ReportsPlus.Utils.Data.ALPR;
using static ReportsPlus.Utils.Data.EventUtils;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        /*
         UPDATE: Update Version
        */
        private const string Version = "v1.5.0-alpha";
        public const string FileDataFolder = "ReportsPlus\\data";

        internal static bool IsOnDuty;

        public static XDocument CurrentIdDoc;
        public static XDocument CalloutDoc;

        public static bool HasStopThePed;
        public static bool HasPolicingRedefined;
        private static bool _hasCalloutInterface;
        public static bool HasCommonDataFramework;

        internal static Ped LocalPlayer => Game.LocalPlayer.Character;

        /*
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the ideas for adding the GTA V integration.
         */

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlusListener Plugin Initialized. Version: " + Version);
            ConfigUtils.LoadSettings();

            //TODO: !important add actual functionality when alpr is turned on (put in new thread?)
            LicensePlateDisplay.InitializeLicensePlateDisplay();

            Game.RawFrameRender += LicensePlateDisplay.OnFrameRender;
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

            var checksOutcome = RunPluginChecks();

            MenuProcessing.InitializeMenu();

            GameFiber.StartNew(() =>
            {
                DataCollection.TrafficStopCollectionFiber =
                    GameFiber.StartNew(DataCollection.TrafficStopCollection, "CombinedDataCollection");
                MenuProcessing.MenuProcessingFiber =
                    GameFiber.StartNew(MenuProcessing.ProcessMenus, "ReportsPlusMenuProcessing");
                DataCollection.WorldDataCollectionFiber =
                    GameFiber.StartNew(DataCollection.WorldDataCollection, "DataCollection");
                DataCollection.SignalFileCheckFiber =
                    GameFiber.StartNew(DataCollection.SignalFileCheck, "SignalFileCheck");

                Game.DisplayNotification("commonmenu", "mp_alerttriangle", "~w~ReportsPlusListener",
                    "~g~Version: " + Version + " Loaded!",
                    "~w~Menu Keybind: ~y~" + MenuProcessing.MainMenuBind + "\n" + checksOutcome);

                Game.LogTrivial("ReportsPlusListener: " + Version + ", Loaded Successfully");
            }, "ReportsPlusListener");
        }

        private StringBuilder RunPluginChecks()
        {
            var builder = new StringBuilder();

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
                builder.Append("~r~CalloutInterface Not Found\n~o~- Required for Callout Functions.\n");
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

                    builder.Append("~r~StopThePed/PR Not Found\n~o~- Using base game functions.");
                }
            }

            return builder;
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (DataCollection.TrafficStopCollectionFiber != null &&
                DataCollection.TrafficStopCollectionFiber.IsAlive)
                DataCollection.TrafficStopCollectionFiber.Abort();

            if (ALPRUtils.AlprFiber != null &&
                ALPRUtils.AlprFiber.IsAlive)
                ALPRUtils.AlprFiber.Abort();

            if (DataCollection.WorldDataCollectionFiber != null &&
                DataCollection.WorldDataCollectionFiber.IsAlive)
                DataCollection.WorldDataCollectionFiber.Abort();

            if (DataCollection.SignalFileCheckFiber != null &&
                DataCollection.SignalFileCheckFiber.IsAlive)
                DataCollection.SignalFileCheckFiber.Abort();

            if (MenuProcessing.MenuProcessingFiber != null &&
                MenuProcessing.MenuProcessingFiber.IsAlive)
                MenuProcessing.MenuProcessingFiber.Abort();

            CurrentIdDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            CalloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener: Cleaned Up.");
        }
    }
}