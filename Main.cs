using System.IO;
using System.Xml.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils;
using static ReportsPlus.Utils.EventUtils;
using static ReportsPlus.Utils.RefreshUtils;
using Functions = LSPD_First_Response.Mod.API.Functions;

namespace ReportsPlus
{
    public class Main : Plugin
    {
        public static readonly string FileDataFolder = "ReportsPlus\\data";
        internal static bool IsOnDuty;
        public static XDocument CurrentIdDoc;
        public static XDocument CalloutDoc;
        public static bool HasStopThePed;
        public bool HasCalloutInterface;

        internal static Ped LocalPlayer => Game.LocalPlayer.Character;
        /*
         * Thank you @HeyPalu, Creator of ExternalPoliceComputer, for the C# implementation and ideas for adding the GTA V integration.
         */

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Game.LogTrivial("ReportsPlusListener Plugin initialized.");

            ConfigUtils.LoadSettings();
        }

        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            IsOnDuty = onDuty;
            Game.LogTrivial("ReportsPlusListener: IsOnDuty State Changed: '" + IsOnDuty + "'");
            if (onDuty)
            {
                Utils.Utils.CalloutIds.Clear();
                if (!Directory.Exists(FileDataFolder))
                    Directory.CreateDirectory(FileDataFolder);

                ConfigUtils.CreateFiles();

                DataCollection.DataCollectionFiber = GameFiber.StartNew(DataCollection.StartDataCollectionFiber);
                DataCollection.KeyCollectionFiber = GameFiber.StartNew(DataCollection.KeyPressDetectionFiber);
                DataCollection.TrafficStopCollectionFiber =
                    GameFiber.StartNew(DataCollection.TrafficStopDataCollectionFiber);

                RunPluginChecks();

                RefreshPeds();
                RefreshVehs();
                RefreshStreet();

                Game.DisplayNotification("~g~ReportsPlus Listener Loaded Successfully.");
                Game.LogTrivial("ReportsPlusListener Loaded Successfully.");
            }
        }

        private void RunPluginChecks()
        {
            HasCalloutInterface = ConfigUtils.IsPluginInstalled("CalloutInterface");
            HasStopThePed = ConfigUtils.IsPluginInstalled("StopThePed");
            if (HasStopThePed)
            {
                EstablishEventsStp();
                Game.LogTrivial("ReportsPlusListener: Found StopThePed");
            }
            else
            {
                Game.LogTrivial("ReportsPlusListener: StopThePed not found. Required for ID Functions.");
                Game.DisplayNotification("~r~ReportsPlusListener: StopThePed not found. Required for ID Functions.");
            }

            if (HasCalloutInterface)
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
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            if (DataCollection.DataCollectionFiber != null && DataCollection.DataCollectionFiber.IsAlive)
                DataCollection.DataCollectionFiber.Abort();

            if (DataCollection.KeyCollectionFiber != null && DataCollection.KeyCollectionFiber.IsAlive)
                DataCollection.KeyCollectionFiber.Abort();

            if (DataCollection.TrafficStopCollectionFiber != null && DataCollection.TrafficStopCollectionFiber.IsAlive)
                DataCollection.TrafficStopCollectionFiber.Abort();

            CurrentIdDoc.Save(Path.Combine(FileDataFolder, "currentID.xml"));
            CalloutDoc.Save(Path.Combine(FileDataFolder, "callout.xml"));
            Game.LogTrivial("ReportsPlusListener: cleaned up.");
        }
    }
}