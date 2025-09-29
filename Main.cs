using System.IO;
using System.Text;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils;
using ReportsPlus.Utils.ALPR;
using ReportsPlus.Utils.Data;
using ReportsPlus.Utils.Menu;
using static ReportsPlus.Utils.Data.EventUtils;
using ALPRUtils = ReportsPlus.Utils.ALPR.ALPRUtils;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        /*
         UPDATE: Update Version
        */
        private const string Version = "v1.5.2-alpha";
        public const string FileDataFolder = "ReportsPlus/data";
        public const string FileResourcesFolder = "Plugins/lspdfr/ReportsPlus/";

        internal static bool IsOnDuty;
        public static bool CachedIsInVehicle;

        public static XDocument CurrentIdDoc;
        public static XDocument CalloutDoc;

        public static bool HasStopThePed;
        public static bool HasPolicingRedefined;
        public static bool HasCommonDataFramework;
        private static bool _hasCalloutInterface;

        private static GameFiber _primaryFiber;
        private static GameFiber _playerStateCheckFiber;

        internal static Ped LocalPlayer;

        /*
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the ideas for adding the GTA V integration.
         */

        //TODO: add check for nativeUI

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlusListener Plugin Initialized. Version: " + Version);
        }

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            IsOnDuty = onDuty;
            Game.LogTrivial("ReportsPlusListener: IsOnDuty State Changed: '" + IsOnDuty + "'");
            if (!onDuty)
            {
                RunFullCleanup();
                return;
            }

            LocalPlayer = Game.LocalPlayer.Character;

            Misc.CalloutIds?.Clear();
            Misc.PedAddresses?.Clear();
            Misc.PedHeights?.Clear();
            Misc.PedWeights?.Clear();
            Misc.PedExpirations?.Clear();
            Misc.PedLicenseNumbers?.Clear();

            ConfigUtils.LoadSettings();

            if (!Directory.Exists(FileResourcesFolder))
                Directory.CreateDirectory(FileResourcesFolder);

            Misc.CopyImageResourcesIfMissing();

            LicensePlateDisplay.InitializeLicensePlateDisplay();

            ConfigUtils.CreateFiles();

            if (File.Exists(DataCollection.CitationSignalFilePath))
            {
                Game.LogTrivial("ReportsPlusListener: Found Old CitationSignalFile, Deleting");
                File.Delete(DataCollection.CitationSignalFilePath);
            }

            var checksOutcome = RunPluginChecks();

            MenuProcessing.InitializeMenu();

            _primaryFiber = GameFiber.StartNew(() =>
            {
                _playerStateCheckFiber = GameFiber.StartNew(UpdatePlayerState, "ReportsPlus-UpdatePlayerState");
                DataCollection.TrafficStopCollectionFiber = GameFiber.StartNew(DataCollection.TrafficStopCollection, "ReportsPlus-TrafficStopCollection");
                DataCollection.KeyCollectionFiber = GameFiber.StartNew(DataCollection.KeyCollection, "ReportsPlus-KeyCollection");
                MenuProcessing.MenuProcessingFiber = GameFiber.StartNew(MenuProcessing.ProcessMenus, "ReportsPlus-MenuProcessing");
                DataCollection.WorldDataCollectionFiber = GameFiber.StartNew(DataCollection.WorldDataCollection, "ReportsPlus-DataCollection");
                DataCollection.SignalFileCheckFiber = GameFiber.StartNew(DataCollection.SignalFileCheck, "ReportsPlus-SignalFileCheck");

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlusListener", "By: ~y~Guess1m", "~g~Version: " + Version + " Loaded!" + "\n" + "~w~Menu Keybind: ~y~" + MenuProcessing.MainMenuBind + "\n" + checksOutcome);

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

            RunFullCleanup();
        }

        private static void UpdatePlayerState()
        {
            while (IsOnDuty)
            {
                GameFiber.Wait(2000);
                CachedIsInVehicle = Game.LocalPlayer.Character?.IsInAnyVehicle(false) ?? false;
            }
        }

        private static void RunFullCleanup()
        {
            Misc.CleanupFiber(DataCollection.TrafficStopCollectionFiber);
            Misc.CleanupFiber(DataCollection.KeyCollectionFiber);
            Misc.CleanupFiber(ALPRUtils.AlprFiber);
            Misc.CleanupFiber(DataCollection.WorldDataCollectionFiber);
            Misc.CleanupFiber(DataCollection.SignalFileCheckFiber);
            Misc.CleanupFiber(DataCollection.ActivePulloverCheckFiber);
            Misc.CleanupFiber(MenuProcessing.MenuProcessingFiber);
            Misc.CleanupFiber(_primaryFiber);
            Misc.CleanupFiber(_playerStateCheckFiber);

            ALPRUtils.CleanupAllBlips();

            Game.RawFrameRender -= LicensePlateDisplay.OnFrameRender;

            CurrentIdDoc?.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            CalloutDoc?.Save(Path.Combine(FileDataFolder, "callout.xml"));

            Misc.CalloutIds?.Clear();
            Misc.PedAddresses?.Clear();
            Misc.PedLicenseNumbers?.Clear();
            Misc.PedHeights?.Clear();
            Misc.PedWeights?.Clear();
            Misc.PedExpirations?.Clear();
            ALPRUtils.RecentlyScannedPlates?.Clear();

            Game.LogTrivial("ReportsPlusListener: Cleaned Up.");
        }
    }
}