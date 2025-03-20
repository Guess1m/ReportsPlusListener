using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;
using ReportsPlus.Utils.Data;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.ALPR.LicensePlateDisplay;
using static ReportsPlus.Utils.Data.MathUtils;
using static ReportsPlus.Utils.Misc;
using ALPRUtils = ReportsPlus.Utils.ALPR.ALPRUtils;

namespace ReportsPlus.Utils
{
    public static class ConfigUtils
    {
        public static InitializationFile IniFile;
        public static int ALPRUpdateDelay;
        public static int RefreshDelay;
        public static int ReScanPlateInterval;
        public static int BlipDisplayTime;
        public static bool ShowAlprDebug;
        public static ALPRUtils.AlprSetupType AlprSetupType;
        public static float ScanRadius;
        public static float MaxScanAngle;
        public static int ALPRSuccessfulScanProbability;

        public static void LoadSettings()
        {
            Game.LogTrivial("ReportsPlusListener: Loading Settings..");
            IniFile = new InitializationFile("plugins/LSPDFR/ReportsPlus.ini");
            IniFile.Create();

            if (!IniFile.DoesKeyExist("Settings", "MenuKey"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: MenuKey Config setting didn't exist, creating");
                IniFile.Write("Settings", "MenuKey", Keys.F7);
            }

            if (!IniFile.DoesKeyExist("Settings", "ALPRKey"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ALPRKey Config setting didn't exist, creating");
                IniFile.Write("Settings", "ALPRKey", Keys.None);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "ALPRType"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ALPRType Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "ALPRType", ALPRUtils.AlprSetupType.Front);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "PlateTextColor"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: PlateTextColor Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "PlateTextColor", Color.FromArgb(43, 49, 127));
            }

            if (!IniFile.DoesKeyExist("Settings", "DataRefreshInterval"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: DataRefreshInterval Config setting didn't exist, creating");
                IniFile.Write("Settings", "DataRefreshInterval", 13000);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "RescanPlateInterval"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: RescanPlateInterval Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "RescanPlateInterval", 60000);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "ALPRSuccessfulScanProbability"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ALPRSuccessfulScanProbability Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "ALPRSuccessfulScanProbability", 20);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "ScanRadius"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ScanRadius Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "ScanRadius", 13f);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "MaxScanAngle"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: MaxScanAngle Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "MaxScanAngle", 40f);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "ShowAlprDebug"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ShowAlprDebug Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "ShowAlprDebug", false);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "EnablePlateDisplay"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: EnablePlateDisplay Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "EnablePlateDisplay", true);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "BlipDisplayTime"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: BlipDisplayTime Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "BlipDisplayTime", 15000);
            }

            if (!IniFile.DoesKeyExist("ALPRSettings", "ALPRUpdateDelay"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ALPRUpdateDelay Config setting didn't exist, creating");
                IniFile.Write("ALPRSettings", "ALPRUpdateDelay", 400);
            }

            if (!IniFile.DoesKeyExist("Keybinds", "GiveTicket"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: GiveTicket Config setting didn't exist, creating");
                IniFile.Write("Keybinds", "GiveTicket", Keys.None);
            }

            if (!IniFile.DoesKeyExist("Keybinds", "DiscardCitation"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: DiscardCitation Config setting didn't exist, creating");
                IniFile.Write("Keybinds", "DiscardCitation", Keys.None);
            }

            if (!IniFile.DoesKeyExist("Probabilities", "ExpiredProbability"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: ExpiredProbability Config setting didn't exist, creating");
                IniFile.Write("Probabilities", "ExpiredProbability", 20);
            }

            if (!IniFile.DoesKeyExist("Probabilities", "NoneProbability"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: NoneProbability Config setting didn't exist, creating");
                IniFile.Write("Probabilities", "NoneProbability", 10);
            }

            if (!IniFile.DoesKeyExist("Probabilities", "RevokedProbability"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: RevokedProbability Config setting didn't exist, creating");
                IniFile.Write("Probabilities", "RevokedProbability", 5);
            }

            if (!IniFile.DoesKeyExist("Probabilities", "ValidProbability"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: ValidProbability Config setting didn't exist, creating");
                IniFile.Write("Probabilities", "ValidProbability", 65);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "BackgroundPositionX"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: BackgroundPositionX Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "BackgroundPositionX", 330f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "BackgroundPositionY"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: BackgroundPositionY Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "BackgroundPositionY", 900f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "TargetPlateHeight"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: TargetPlateHeight Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "TargetPlateHeight", 50f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "PlateSpacing"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: PlateSpacing Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "PlateSpacing", 10f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "LabelFontSize"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: LabelFontSize Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "LabelFontSize", 11);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "LabelVerticalOffset"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: LabelVerticalOffset Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "LabelVerticalOffset", 19f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "LicensePlateVerticalOffset"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: LicensePlateVerticalOffset Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "LicensePlateVerticalOffset", 5f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "PlateTextVerticalOffset"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: PlateTextVerticalOffset Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "PlateTextVerticalOffset", 15f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "PlateTextFontSize"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: PlateTextFontSize Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "PlateTextFontSize", 20f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "BackgroundScale"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: BackgroundScale Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "BackgroundScale", 1.04f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "TargetPlateWidth"))
            {
                Game.LogTrivial("ReportsPlusListener {CONFIG}: TargetPlateWidth Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "TargetPlateWidth", 100f);
            }

            if (!IniFile.DoesKeyExist("PlateDisplay", "TargetPlateHeight"))
            {
                Game.LogTrivial(
                    "ReportsPlusListener {CONFIG}: TargetPlateHeight Config setting didn't exist, creating");
                IniFile.Write("PlateDisplay", "TargetPlateHeight", 50f);
            }

            MenuProcessing.MainMenuBind = IniFile.ReadEnum("Settings", "MenuKey", Keys.F7);
            MenuProcessing.ALPRMenuBind = IniFile.ReadEnum("Settings", "ALPRKey", Keys.None);
            AlprSetupType = IniFile.ReadEnum("ALPRSettings", "ALPRType", ALPRUtils.AlprSetupType.Front);
            RefreshDelay = IniFile.ReadInt32("Settings", "DataRefreshInterval", 13000);
            ALPRSuccessfulScanProbability = IniFile.ReadInt32("ALPRSettings", "ALPRSuccessfulScanProbability", 20);
            ReScanPlateInterval = IniFile.ReadInt32("ALPRSettings", "RescanPlateInterval", 60000);
            BlipDisplayTime = IniFile.ReadInt32("ALPRSettings", "BlipDisplayTime", 15000);
            ScanRadius = IniFile.ReadSingle("ALPRSettings", "ScanRadius", 15f);
            MaxScanAngle = IniFile.ReadSingle("ALPRSettings", "MaxScanAngle", 40f);
            ShowAlprDebug = IniFile.ReadBoolean("ALPRSettings", "ShowAlprDebug");
            ALPRUpdateDelay = IniFile.ReadInt32("ALPRSettings", "ALPRUpdateDelay", 400);
            AnimationBind = IniFile.ReadEnum("Keybinds", "GiveTicket", Keys.None);
            DiscardBind = IniFile.ReadEnum("Keybinds", "DiscardCitation", Keys.None);
            ExpiredProb = IniFile.ReadInt32("Probabilities", "ExpiredProbability", 20);
            NoneProb = IniFile.ReadInt32("Probabilities", "NoneProbability", 10);
            RevokedProb = IniFile.ReadInt32("Probabilities", "RevokedProbability", 5);
            ValidProb = IniFile.ReadInt32("Probabilities", "ValidProbability", 65);

            BackgroundPositionX = IniFile.ReadSingle("PlateDisplay", "BackgroundPositionX", 330f);
            BackgroundPositionY = IniFile.ReadSingle("PlateDisplay", "BackgroundPositionY", 900f);
            TargetPlateHeight = IniFile.ReadSingle("PlateDisplay", "TargetPlateHeight", 50f);
            PlateSpacing = IniFile.ReadSingle("PlateDisplay", "PlateSpacing", 10f);
            LabelFontSize = IniFile.ReadInt32("PlateDisplay", "LabelFontSize", 11);
            LabelVerticalOffset = IniFile.ReadSingle("PlateDisplay", "LabelVerticalOffset", 19);
            LicensePlateVerticalOffset = IniFile.ReadSingle("PlateDisplay", "LicensePlateVerticalOffset", 5);
            PlateTextVerticalOffset = IniFile.ReadSingle("PlateDisplay", "PlateTextVerticalOffset", 15);
            PlateTextFontSize = IniFile.ReadSingle("PlateDisplay", "PlateTextFontSize", 20);
            BackgroundScale = IniFile.ReadSingle("PlateDisplay", "BackgroundScale", 1.04f);
            TargetPlateWidth = IniFile.ReadSingle("PlateDisplay", "TargetPlateWidth", 100f);
            EnablePlateDisplay = IniFile.ReadBoolean("PlateDisplay", "EnablePlateDisplay", true);

            var colorParts = IniFile.ReadString("PlateDisplay", "PlateTextColor", "255,43,49,127").Split(',')
                .Select(int.Parse).ToArray();
            var tempColor = Color.FromArgb(colorParts[0], colorParts[1], colorParts[2], colorParts[3]);
            PlateTextColor = AvailableColors.FirstOrDefault(c =>
                c.A == tempColor.A &&
                c.R == tempColor.R &&
                c.G == tempColor.G &&
                c.B == tempColor.B);
            if (PlateTextColor == Color.Empty)
                PlateTextColor = Color.Black;

            Game.LogTrivial("ReportsPlusListener {CONFIG}: MainMenu Keybind- '" + MenuProcessing.MainMenuBind + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: ToggleALPR Keybind- '" + MenuProcessing.ALPRMenuBind + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: ALPRSetupType- '" + AlprSetupType + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: GiveTicket Keybind- '" + AnimationBind + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: Discard Citation Keybind- '" + DiscardBind + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: ALPRSuccessfulScanProbability- '" +
                            ALPRSuccessfulScanProbability + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: ALPRUpdateDelay- '" + ALPRUpdateDelay + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: ScanRadius- '" + ScanRadius + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: MaxScanAngle- '" + MaxScanAngle + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: ShowAlprDebug- '" + ShowAlprDebug + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: DataRefreshInterval- '" + RefreshDelay + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: RescanPlateInterval- '" + ReScanPlateInterval + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: BlipDisplayTime- '" + BlipDisplayTime + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: ExpiredProbability- '" + ExpiredProb + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: NoneProbability- '" + NoneProb + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: RevokedProbability- '" + RevokedProb + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: ValidProbability- '" + ValidProb + "'");

            Game.LogTrivial("ReportsPlusListener {CONFIG}: BackgroundPositionX- '" + BackgroundPositionX + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: BackgroundPositionY- '" + BackgroundPositionY + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: TargetPlateHeight- '" + TargetPlateHeight + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: PlateSpacing- '" + PlateSpacing + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: LabelFontSize- '" + LabelFontSize + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: LabelVerticalOffset- '" + LabelVerticalOffset + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: LicensePlateVerticalOffset- '" + LicensePlateVerticalOffset +
                            "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: PlateTextVerticalOffset- '" + PlateTextVerticalOffset + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: PlateTextFontSize- '" + PlateTextFontSize + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: PlateTextColor- '" + PlateTextColor + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: BackgroundScale- '" + BackgroundScale + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: TargetPlateWidth- '" + TargetPlateWidth + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: TargetPlateHeight- '" + TargetPlateHeight + "'");
            Game.LogTrivial("ReportsPlusListener {CONFIG}: EnablePlateDisplay- '" + EnablePlateDisplay + "'");
        }

        public static bool IsPluginInstalled(string pluginName)
        {
            var plugins = Functions.GetAllUserPlugins();
            var isInstalled = plugins.Any(x => x.GetName().Name.Equals(pluginName));
            Game.LogTrivial($"ReportsPlusListener: Plugin '{pluginName}' is installed: {isInstalled}");

            return isInstalled;
        }

        public static void CreateFiles()
        {
            if (!Directory.Exists(FileDataFolder)) Directory.CreateDirectory(FileDataFolder);

            string[] filesToCreate =
                { "callout.xml", "currentID.xml", "worldCars.data", "worldPeds.data", "trafficStop.data", "alpr.data" };

            foreach (var fileName in filesToCreate)
                try
                {
                    var filePath = Path.Combine(FileDataFolder, fileName);
                    if (!File.Exists(filePath))
                        File.Create(filePath);
                }
                catch (Exception)
                {
                    Game.LogTrivial($"ReportsPlusListener: Exception Creating file: {fileName}");
                }
        }
    }
}