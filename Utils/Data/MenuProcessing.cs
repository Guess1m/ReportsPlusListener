using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;
using ReportsPlus.Utils.Data.ALPR;
using static ReportsPlus.Utils.ConfigUtils;

namespace ReportsPlus.Utils.Data
{
    public static class MenuProcessing
    {
        //TODO: !important reset def btn
        //TODO: !important add sub menu for license plate display

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

            var openPlateSettingsMenuButton =
                new UIMenuItem("Plate Display Settings", "Configure onscreen license plate display")
                {
                    ForeColor = Color.FromArgb(121, 163, 224),
                    HighlightedForeColor = Color.FromArgb(121, 163, 224)
                };

            var alprMenu = new UIMenu("ALPR", "ALPR Menu");
            var successfulScanProbability = new UIMenuNumericScrollerItem<int>("Success Percentage",
                "(RECOMMENDED) Percentage of a plate being scanned successfully. This is needed since STP/PR have very high rates for flags on vehicles.",
                1, 100, 1)
            {
                Value = ALPRSuccessfulScanProbability
            };
            var rescanPlateInterval = new UIMenuNumericScrollerItem<int>("Rescan Interval",
                "Interval for a plate being scanned again (sec)", 10, 200, 5)
            {
                Value = ReScanPlateInterval / 1000
            };
            var alprUpdateDelay = new UIMenuNumericScrollerItem<int>("ALPR Update Delay",
                "Interval for ALPR to update (ms). Lower values *can* impact fps (1000ms = 1sec)", 0, 1500,
                50)
            {
                Value = ALPRUpdateDelay
            };

            var scanRadius = new UIMenuNumericScrollerItem<float>("Scan Radius",
                "Radius of the scanners (distance)", 7, 20, 1)
            {
                Value = ScanRadius
            };
            var maxScanAngle = new UIMenuNumericScrollerItem<float>("Max Scan Angle",
                "Maximum angle of the alpr scanners", 20, 55, 1)
            {
                Value = MaxScanAngle
            };
            var alprBlipDisplayTime = new UIMenuNumericScrollerItem<int>("ALPR Blip Duration",
                "Duration of the ALPR blip (sec)", 5, 60, 5)
            {
                Value = BlipDisplayTime / 1000
            };
            var showDebugLines = new UIMenuCheckboxItem("Show Debug", ShowAlprDebug, "Display debugging lines")
            {
                Style = UIMenuCheckboxStyle.Cross,
                ForeColor = Color.FromArgb(185, 185, 185),
                HighlightedForeColor = Color.FromArgb(185, 185, 185)
            };

            var alprType = new UIMenuListScrollerItem<ALPRUtils.AlprSetupType>("ALPR Type", "Set the type of ALPR",
                new[] { ALPRUtils.AlprSetupType.All, ALPRUtils.AlprSetupType.Front, ALPRUtils.AlprSetupType.Rear })
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
                IniFile.Write("ALPRSettings", "ALPRUpdateDelay", alprUpdateDelay.Value);
                ALPRUpdateDelay = alprUpdateDelay.Value;
                IniFile.Write("ALPRSettings", "ShowAlprDebug", showDebugLines.Checked);
                ShowAlprDebug = showDebugLines.Checked;
                IniFile.Write("ALPRSettings", "ALPRType", alprType.SelectedItem);
                AlprSetupType = alprType.SelectedItem;

                ALPRUtils.ToggleAlpr(alprType.SelectedItem);
                ALPRButton.Enabled = true;
            };
            alprMenu.AddItems(ALPRButton, alprType, successfulScanProbability, rescanPlateInterval, scanRadius,
                maxScanAngle,
                alprBlipDisplayTime, alprUpdateDelay, showDebugLines, openPlateSettingsMenuButton);

            var plateDisplayMenu = new UIMenu("Plate Display Menu", "Plate Display Settings");
            var enablePlateDisplay =
                new UIMenuCheckboxItem("Show Plate Display", ShowAlprDebug, "Toggle in-game plate display")
                {
                    Style = UIMenuCheckboxStyle.Cross,
                    ForeColor = Color.FromArgb(185, 185, 185),
                    HighlightedForeColor = Color.FromArgb(185, 185, 185)
                };

            var plateDisplayX = new UIMenuNumericScrollerItem<float>("Plate Display X",
                "Horizontal position of plate display", 0f, 3000f, 10f)
            {
                Value = LicensePlateDisplay.BackgroundPositionX
            };

            var plateDisplayY = new UIMenuNumericScrollerItem<float>("Plate Display Y",
                "Vertical position of plate display", 0f, 2000f, 10f)
            {
                Value = LicensePlateDisplay.BackgroundPositionY
            };

            var plateSpacing = new UIMenuNumericScrollerItem<float>("Plate Spacing",
                "Space between front/rear plates", 5f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateSpacing
            };

            var labelSize = new UIMenuNumericScrollerItem<float>("Label Size",
                "Text size for front/rear labels", 8f, 30f, 1f)
            {
                Value = LicensePlateDisplay.LabelFontSize
            };

            var labelOffset = new UIMenuNumericScrollerItem<float>("Label Offset",
                "Vertical offset for labels", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.LabelVerticalOffset
            };

            var licensePlateVerticalOffset = new UIMenuNumericScrollerItem<float>("Plate Offset",
                "Vertical offset for plates", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.LicensePlateVerticalOffset
            };

            var plateNumberFontSize = new UIMenuNumericScrollerItem<float>("Plate Number Size",
                "Font size for plate numbers", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateTextFontSize
            };

            var plateNumberOffset = new UIMenuNumericScrollerItem<float>("Plate Number Offset",
                "Vertical offset for plate numbers", 0f, 50f, 1f)
            {
                Value = LicensePlateDisplay.PlateTextVerticalOffset
            };

            var plateTextColor = new UIMenuListScrollerItem<Color>("Plate Text Color",
                "Set the color of the plate numbers",
                LicensePlateDisplay.ColorPresets.Keys.ToArray())
            {
                Formatter = color => LicensePlateDisplay.ColorPresets.TryGetValue(color, out var name)
                    ? name
                    : $"Custom ({color.R},{color.G},{color.B})",

                SelectedItem = LicensePlateDisplay.ColorPresets.Keys.FirstOrDefault(c =>
                    c.A == LicensePlateDisplay.PlateTextColor.A &&
                    c.R == LicensePlateDisplay.PlateTextColor.R &&
                    c.G == LicensePlateDisplay.PlateTextColor.G &&
                    c.B == LicensePlateDisplay.PlateTextColor.B)
            };

            if (!LicensePlateDisplay.AvailableColors.Contains(plateTextColor.SelectedItem))
                plateTextColor.SelectedItem = Color.Black;
            var bgScale = new UIMenuNumericScrollerItem<float>("Background Scale",
                "Size of background image", 0.1f, 3.0f, 0.01f)
            {
                Value = LicensePlateDisplay.BackgroundScale,
                Formatter = v => $"{v * 100}%"
            };

            var plateWidth = new UIMenuNumericScrollerItem<float>("Plate Width",
                "Width of license plates", 50f, 300f, 1f)
            {
                Value = LicensePlateDisplay.TargetPlateWidth
            };

            var plateHeight = new UIMenuNumericScrollerItem<float>("Plate Height",
                "Height of license plates", 20f, 100f, 1f)
            {
                Value = LicensePlateDisplay.TargetPlateHeight
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

                var selectedColor = LicensePlateDisplay.AvailableColors.Contains(plateTextColor.SelectedItem)
                    ? plateTextColor.SelectedItem
                    : Color.Black;
                IniFile.Write("PlateDisplay", "PlateTextColor",
                    $"{selectedColor.A},{selectedColor.R},{selectedColor.G},{selectedColor.B}");
                LicensePlateDisplay.PlateTextColor = selectedColor;
            };

            plateDisplayMenu.AddItems(enablePlateDisplay, plateDisplayX, plateDisplayY,
                plateSpacing, labelSize, labelOffset, licensePlateVerticalOffset, plateNumberOffset,
                plateNumberFontSize, plateTextColor, bgScale, plateWidth, plateHeight, savePlateDisplaySettings);

            var openALPRMenuButton = new UIMenuItem("ALPR Menu", "Open the ALPR settings menu");
            MainMenu.AddItems(openALPRMenuButton);
            MainMenu.BindMenuToItem(alprMenu, openALPRMenuButton);

            alprMenu.BindMenuToItem(plateDisplayMenu, openPlateSettingsMenuButton);

            MainMenuPool.Add(MainMenu, alprMenu, plateDisplayMenu);
        }

        public static void ProcessMenus()
        {
            while (Main.IsOnDuty)
            {
                GameFiber.Yield();

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