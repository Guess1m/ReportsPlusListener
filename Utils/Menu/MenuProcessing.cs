using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;
using ReportsPlus.Utils.ALPR;
using ReportsPlus.Utils.Animation;
using static ReportsPlus.Utils.ConfigUtils;
using ALPRUtils = ReportsPlus.Utils.ALPR.ALPRUtils;

namespace ReportsPlus.Utils.Menu
{
    public static class MenuProcessing
    {
        public static UIMenuItem ALPRButton;
        public static GameFiber MenuProcessingFiber;
        public static Keys MainMenuBind;
        public static Keys ALPRMenuBind;

        private static readonly MenuPool MainMenuPool = new MenuPool();
        public static UIMenu MainMenu;

        public static bool ALPRActive { get; set; }

        public static void InitializeMenu()
        {
            MainMenu = new UIMenu("ReportsPlus", "Main Menu");

            var openPlateSettingsMenuButton = new UIMenuItem("Plate Display Settings", "Configure onscreen license plate display")
            {
                ForeColor = Color.FromArgb(121, 163, 224),
                HighlightedForeColor = Color.FromArgb(68, 95, 224)
            };

            var alprMenu = new UIMenu("ALPR", "ALPR Menu");
            var successfulScanProbability = new UIMenuNumericScrollerItem<int>("Scan Probability", "The chance (%) that a vehicle in range will be scanned by the ALPR.", 1, 100, 1)
            {
                Value = ALPRSuccessfulScanProbability
            };
            var rescanPlateInterval = new UIMenuNumericScrollerItem<int>("Rescan Interval", "Interval for a plate being scanned again (sec)", 10, 900, 10)
            {
                Value = ReScanPlateInterval / 1000
            };
            var alprUpdateDelay = new UIMenuNumericScrollerItem<int>("ALPR Update Delay", "Interval for ALPR to update (ms). Lower values *can* impact fps (1000ms = 1sec)", 0, 30000, 100)
            {
                Value = ALPRUpdateDelay
            };

            var scanRadius = new UIMenuNumericScrollerItem<float>("Scan Radius", "Radius of the scanners (distance)", 7, 20, 1)
            {
                Value = ScanRadius
            };
            var maxScanAngle = new UIMenuNumericScrollerItem<float>("Max Scan Angle", "Maximum angle of the alpr scanners", 20, 55, 1)
            {
                Value = MaxScanAngle
            };
            var alprBlipDisplayTime = new UIMenuNumericScrollerItem<int>("ALPR Blip Duration", "Duration of the ALPR blip (sec)", 5, 60, 5)
            {
                Value = BlipDisplayTime / 1000
            };
            var enableBlips = new UIMenuCheckboxItem("Enable Blips", BlipsEnabled, "Toggle blips for ALPR hits")
            {
                Style = UIMenuCheckboxStyle.Cross,
                Checked = BlipsEnabled
            };
            var showDebugLines = new UIMenuCheckboxItem("Show Debug", ShowAlprDebug, "Display debugging lines")
            {
                Style = UIMenuCheckboxStyle.Cross,
                ForeColor = Color.FromArgb(185, 185, 185),
                HighlightedForeColor = Color.FromArgb(40, 40, 40),
                Checked = ShowAlprDebug
            };

            var alprType = new UIMenuListScrollerItem<ALPRUtils.AlprSetupType>("ALPR Type", "Set the type of ALPR", new[] { ALPRUtils.AlprSetupType.All, ALPRUtils.AlprSetupType.Front, ALPRUtils.AlprSetupType.Rear })
            {
                SelectedItem = AlprSetupType
            };

            ALPRButton = new UIMenuItem("Toggle ALPR", "Toggle ALPR");
            ALPRButton.Activated += (sender, args) =>
            {
                ALPRButton.Enabled = false;
                IniFile.Write("ALPRSettings", "ALPRSuccessfulScanProbability", successfulScanProbability.Value);
                ALPRSuccessfulScanProbability = successfulScanProbability.Value;
                IniFile.Write("ALPRSettings", "RescanPlateInterval", rescanPlateInterval.Value * 1000);
                ReScanPlateInterval = rescanPlateInterval.Value * 1000;
                IniFile.Write("ALPRSettings", "ScanRadius", scanRadius.Value);
                ScanRadius = scanRadius.Value;
                IniFile.Write("ALPRSettings", "MaxScanAngle", maxScanAngle.Value);
                MaxScanAngle = maxScanAngle.Value;
                IniFile.Write("ALPRSettings", "BlipDisplayTime", alprBlipDisplayTime.Value * 1000);
                BlipDisplayTime = alprBlipDisplayTime.Value * 1000;
                IniFile.Write("ALPRSettings", "BlipsEnabled", enableBlips.Checked);
                BlipsEnabled = enableBlips.Checked;
                IniFile.Write("ALPRSettings", "ALPRUpdateDelay", alprUpdateDelay.Value);
                ALPRUpdateDelay = alprUpdateDelay.Value;
                IniFile.Write("ALPRSettings", "ShowAlprDebug", showDebugLines.Checked);
                ShowAlprDebug = showDebugLines.Checked;
                IniFile.Write("ALPRSettings", "ALPRType", alprType.SelectedItem);
                AlprSetupType = alprType.SelectedItem;

                ALPRUtils.ToggleAlpr();
                ALPRButton.Enabled = true;
            };
            alprMenu.AddItems(ALPRButton, alprType, successfulScanProbability, rescanPlateInterval, scanRadius, maxScanAngle, enableBlips, alprBlipDisplayTime, alprUpdateDelay, showDebugLines, openPlateSettingsMenuButton);

            var plateDisplayMenu = new UIMenu("Plate Display Menu", "Plate Display Settings");
            var enablePlateDisplay = new UIMenuCheckboxItem("Show Plate Display", LicensePlateDisplay.EnablePlateDisplay, "Toggle in-game plate display")
            {
                Style = UIMenuCheckboxStyle.Cross,
                ForeColor = Color.FromArgb(185, 185, 185),
                HighlightedForeColor = Color.FromArgb(40, 40, 40),
                Checked = LicensePlateDisplay.EnablePlateDisplay
            };

            var EnableDisplayOnFoot = new UIMenuCheckboxItem("Display On Foot", LicensePlateDisplay.EnableDisplayOnFoot, "Toggle whether to display plates when on foot")
            {
                Style = UIMenuCheckboxStyle.Cross,
                ForeColor = Color.FromArgb(185, 185, 185),
                HighlightedForeColor = Color.FromArgb(40, 40, 40),
                Checked = LicensePlateDisplay.EnableDisplayOnFoot
            };

            var plateDisplayX = new UIMenuNumericScrollerItem<float>("Plate Display X", "Horizontal position of plate display", 0f, 3000f, 10f)
            {
                Value = LicensePlateDisplay.BackgroundPositionX,
                ForeColor = Color.FromArgb(42, 157, 185),
                HighlightedForeColor = Color.FromArgb(20, 105, 185)
            };

            var plateDisplayY = new UIMenuNumericScrollerItem<float>("Plate Display Y", "Vertical position of plate display", 0f, 2000f, 10f)
            {
                Value = LicensePlateDisplay.BackgroundPositionY,
                ForeColor = Color.FromArgb(42, 157, 185),
                HighlightedForeColor = Color.FromArgb(20, 105, 185)
            };

            var plateSpacing = new UIMenuNumericScrollerItem<float>("Plate Spacing", "Space between front/rear plates", 5f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateSpacing,
                ForeColor = Color.FromArgb(52, 185, 128),
                HighlightedForeColor = Color.FromArgb(15, 150, 24)
            };

            var labelSize = new UIMenuNumericScrollerItem<float>("Label Size", "Text size for front/rear labels", 8f, 30f, 1f)
            {
                Value = LicensePlateDisplay.LabelFontSize,
                ForeColor = Color.FromArgb(185, 148, 80),
                HighlightedForeColor = Color.FromArgb(162, 104, 13)
            };

            var labelOffset = new UIMenuNumericScrollerItem<float>("Label Offset", "Vertical offset for labels", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.LabelVerticalOffset,
                ForeColor = Color.FromArgb(185, 148, 80),
                HighlightedForeColor = Color.FromArgb(162, 104, 13)
            };

            var licensePlateVerticalOffset = new UIMenuNumericScrollerItem<float>("Plate Offset", "Vertical offset for plates", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.LicensePlateVerticalOffset,
                ForeColor = Color.FromArgb(52, 185, 128),
                HighlightedForeColor = Color.FromArgb(15, 150, 24)
            };

            var plateNumberFontSize = new UIMenuNumericScrollerItem<float>("Plate Number Size", "Font size for plate numbers", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateTextFontSize,
                ForeColor = Color.FromArgb(181, 42, 185),
                HighlightedForeColor = Color.FromArgb(128, 0, 167)
            };

            var plateNumberOffset = new UIMenuNumericScrollerItem<float>("Plate Number Offset", "Vertical offset for plate numbers", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateTextVerticalOffset,
                ForeColor = Color.FromArgb(181, 42, 185),
                HighlightedForeColor = Color.FromArgb(128, 0, 167)
            };

            var plateTextColor = new UIMenuListScrollerItem<Color>("Plate Text Color", "Set the color of the plate numbers", LicensePlateDisplay.ColorPresets.Keys.ToArray())
            {
                Formatter = color => LicensePlateDisplay.ColorPresets.TryGetValue(color, out var name) ? name : $"Custom ({color.R},{color.G},{color.B})",

                SelectedItem = LicensePlateDisplay.ColorPresets.Keys.FirstOrDefault(c => c.A == LicensePlateDisplay.PlateTextColor.A && c.R == LicensePlateDisplay.PlateTextColor.R && c.G == LicensePlateDisplay.PlateTextColor.G && c.B == LicensePlateDisplay.PlateTextColor.B),
                ForeColor = Color.FromArgb(181, 42, 185),
                HighlightedForeColor = Color.FromArgb(128, 0, 167)
            };

            if (!LicensePlateDisplay.AvailableColors.Contains(plateTextColor.SelectedItem))
                plateTextColor.SelectedItem = Color.Black;
            var bgScale = new UIMenuNumericScrollerItem<float>("Background Scale", "Size of background image", 0.1f, 3.0f, 0.01f)
            {
                Value = LicensePlateDisplay.BackgroundScale,
                Formatter = v => $"{v * 100}%",
                ForeColor = Color.FromArgb(185, 183, 33),
                HighlightedForeColor = Color.FromArgb(145, 127, 0)
            };

            var plateWidth = new UIMenuNumericScrollerItem<float>("Plate Width", "Width of license plates", 50f, 300f, 1f)
            {
                Value = LicensePlateDisplay.TargetPlateWidth,
                ForeColor = Color.FromArgb(52, 185, 128),
                HighlightedForeColor = Color.FromArgb(15, 150, 24)
            };

            var plateHeight = new UIMenuNumericScrollerItem<float>("Plate Height", "Height of license plates", 20f, 100f, 1f)
            {
                Value = LicensePlateDisplay.TargetPlateHeight,
                ForeColor = Color.FromArgb(52, 185, 128),
                HighlightedForeColor = Color.FromArgb(15, 150, 24)
            };

            var savePlateDisplaySettings = new UIMenuItem("Save", "Save Plate Display Settings")
            {
                ForeColor = Color.FromArgb(111, 224, 151),
                HighlightedForeColor = Color.FromArgb(23, 169, 45)
            };
            savePlateDisplaySettings.Activated += (sender, args) =>
            {
                IniFile.Write("PlateDisplay", "BackgroundPositionX", plateDisplayX.Value);
                LicensePlateDisplay.BackgroundPositionX = plateDisplayX.Value;
                IniFile.Write("PlateDisplay", "BackgroundPositionY", plateDisplayY.Value);
                LicensePlateDisplay.BackgroundPositionY = plateDisplayY.Value;
                IniFile.Write("PlateDisplay", "BackgroundScale", bgScale.Value);
                LicensePlateDisplay.BackgroundScale = bgScale.Value;
                IniFile.Write("PlateDisplay", "TargetPlateWidth", plateWidth.Value);
                LicensePlateDisplay.TargetPlateWidth = plateWidth.Value;
                IniFile.Write("PlateDisplay", "TargetPlateHeight", plateHeight.Value);
                LicensePlateDisplay.TargetPlateHeight = plateHeight.Value;
                IniFile.Write("PlateDisplay", "PlateSpacing", plateSpacing.Value);
                LicensePlateDisplay.PlateSpacing = plateSpacing.Value;
                IniFile.Write("PlateDisplay", "LabelFontSize", labelSize.Value);
                LicensePlateDisplay.LabelFontSize = labelSize.Value;
                IniFile.Write("PlateDisplay", "LabelVerticalOffset", labelOffset.Value);
                LicensePlateDisplay.LabelVerticalOffset = labelOffset.Value;
                IniFile.Write("PlateDisplay", "LicensePlateVerticalOffset", licensePlateVerticalOffset.Value);
                LicensePlateDisplay.LicensePlateVerticalOffset = licensePlateVerticalOffset.Value;
                IniFile.Write("PlateDisplay", "PlateTextVerticalOffset", plateNumberOffset.Value);
                LicensePlateDisplay.PlateTextVerticalOffset = plateNumberOffset.Value;
                IniFile.Write("PlateDisplay", "PlateTextFontSize", plateNumberFontSize.Value);
                LicensePlateDisplay.PlateTextFontSize = plateNumberFontSize.Value;
                IniFile.Write("PlateDisplay", "EnablePlateDisplay", enablePlateDisplay.Checked);
                LicensePlateDisplay.EnablePlateDisplay = enablePlateDisplay.Checked;
                IniFile.Write("PlateDisplay", "EnableDisplayOnFoot", EnableDisplayOnFoot.Checked);
                LicensePlateDisplay.EnableDisplayOnFoot = EnableDisplayOnFoot.Checked;

                var selectedColor = LicensePlateDisplay.AvailableColors.Contains(plateTextColor.SelectedItem) ? plateTextColor.SelectedItem : Color.Black;
                IniFile.Write("PlateDisplay", "PlateTextColor", $"{selectedColor.A},{selectedColor.R},{selectedColor.G},{selectedColor.B}");
                LicensePlateDisplay.PlateTextColor = selectedColor;
            };

            var resetDefaultsButton = new UIMenuItem("Reset Defaults", "Reset all settings to default values")
            {
                ForeColor = Color.FromArgb(224, 86, 86),
                HighlightedForeColor = Color.FromArgb(224, 50, 50)
            };

            resetDefaultsButton.Activated += (sender, args) =>
            {
                enablePlateDisplay.Checked = true;
                plateDisplayX.Value = 330f;
                plateDisplayY.Value = 900f;
                plateSpacing.Value = 10f;
                plateWidth.Value = 100f;
                plateHeight.Value = 50f;
                licensePlateVerticalOffset.Value = 5f;
                plateNumberOffset.Value = 15f;
                plateNumberFontSize.Value = 20f;
                labelSize.Value = 11f;
                labelOffset.Value = 19f;
                bgScale.Value = 1.04f;
                plateTextColor.SelectedItem = Color.FromArgb(43, 49, 127);

                IniFile.Write("PlateDisplay", "EnablePlateDisplay", true);
                IniFile.Write("PlateDisplay", "EnableDisplayOnFoot", true);
                IniFile.Write("PlateDisplay", "BackgroundPositionX", 330f);
                IniFile.Write("PlateDisplay", "BackgroundPositionY", 900f);
                IniFile.Write("PlateDisplay", "PlateSpacing", 10f);
                IniFile.Write("PlateDisplay", "TargetPlateWidth", 100f);
                IniFile.Write("PlateDisplay", "TargetPlateHeight", 50f);
                IniFile.Write("PlateDisplay", "LicensePlateVerticalOffset", 5f);
                IniFile.Write("PlateDisplay", "PlateTextVerticalOffset", 15f);
                IniFile.Write("PlateDisplay", "PlateTextFontSize", 20f);
                IniFile.Write("PlateDisplay", "LabelFontSize", 11);
                IniFile.Write("PlateDisplay", "LabelVerticalOffset", 19f);
                IniFile.Write("PlateDisplay", "BackgroundScale", 1.04f);
                IniFile.Write("PlateDisplay", "PlateTextColor", Color.FromArgb(43, 49, 127));

                LicensePlateDisplay.EnablePlateDisplay = true;
                LicensePlateDisplay.EnableDisplayOnFoot = true;
                LicensePlateDisplay.BackgroundPositionX = 330f;
                LicensePlateDisplay.BackgroundPositionY = 900f;
                LicensePlateDisplay.PlateSpacing = 10f;
                LicensePlateDisplay.TargetPlateWidth = 100f;
                LicensePlateDisplay.TargetPlateHeight = 50f;
                LicensePlateDisplay.LicensePlateVerticalOffset = 5f;
                LicensePlateDisplay.PlateTextVerticalOffset = 15f;
                LicensePlateDisplay.PlateTextFontSize = 20f;
                LicensePlateDisplay.LabelFontSize = 11;
                LicensePlateDisplay.LabelVerticalOffset = 19f;
                LicensePlateDisplay.BackgroundScale = 1.04f;
                LicensePlateDisplay.PlateTextColor = Color.FromArgb(43, 49, 127);

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Success", "Plate display settings reset to defaults!");
            };

            var placeholder = new UIMenuItem("", "") { Skipped = true, Enabled = false };

            plateDisplayMenu.AddItems(enablePlateDisplay, bgScale, plateDisplayX, plateDisplayY, labelSize, labelOffset, plateSpacing, plateWidth, plateHeight, licensePlateVerticalOffset, plateNumberOffset, plateNumberFontSize, plateTextColor, placeholder, EnableDisplayOnFoot, resetDefaultsButton, savePlateDisplaySettings);

            var openALPRMenuButton = new UIMenuItem("ALPR Menu", "Open the ALPR settings menu");

            if (Main.HasPolicingRedefined && Main.HasCommonDataFramework)
            {
                var discardCitationsButton = new UIMenuItem("Discard Citations", "Discards all pending citations.")
                {
                    ForeColor = Color.FromArgb(226, 82, 47),
                    HighlightedForeColor = Color.FromArgb(226, 82, 47)
                };
                discardCitationsButton.Activated += (sender, args) => { AnimationUtils.ClearAllCitations(); };
                MainMenu.AddItems(openALPRMenuButton, discardCitationsButton);
            }
            else
            {
                MainMenu.AddItems(openALPRMenuButton);
            }

            MainMenu.BindMenuToItem(alprMenu, openALPRMenuButton);

            alprMenu.BindMenuToItem(plateDisplayMenu, openPlateSettingsMenuButton);

            MainMenuPool.Add(MainMenu, alprMenu, plateDisplayMenu);
        }

        public static void ClearCitationButtons()
        {
            MainMenu.MenuItems.RemoveAll(item => item.Text == "Give Citation" || item.Text == "Discard Citation");
            MainMenu.RefreshIndex();
        }

        public static void ProcessMenus()
        {
            while (Main.IsOnDuty)
            {
                GameFiber.Yield();

                if (MainMenuBind == Keys.None) continue;

                MainMenuPool.ProcessMenus();

                if (!Game.IsKeyDown(MainMenuBind)) continue;

                if (MainMenu.Visible)
                {
                    MainMenu.Visible = false;
                }
                else if (!UIMenu.IsAnyMenuVisible && !TabView.IsAnyPauseMenuVisible)
                {
                    MainMenu.RefreshIndex();
                    MainMenu.Visible = true;
                }
            }
        }
    }
}