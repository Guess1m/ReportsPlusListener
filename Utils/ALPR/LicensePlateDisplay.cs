using System;
using System.Collections.Generic;
using System.Drawing;
using Rage;
using Graphics = Rage.Graphics;

namespace ReportsPlus.Utils.ALPR
{
    public static class LicensePlateDisplay
    {
        private const string PlateImagePath = Main.FileResourcesFolder + "LicensePlate.png";
        private const string BackgroundImgPath = Main.FileResourcesFolder + "ALPRBackground.png";
        public static bool EnablePlateDisplay;

        public static float BackgroundPositionX;
        public static float BackgroundPositionY;
        public static float TargetPlateHeight;
        public static float PlateSpacing;

        public static float BackgroundScale;
        public static float TargetPlateWidth;
        public static bool EnableDisplayOnFoot;

        public static float LabelFontSize;
        public static float LabelVerticalOffset;
        public static float LicensePlateVerticalOffset;

        public static float PlateTextFontSize;
        public static float PlateTextVerticalOffset;
        public static string FrontPlateText = "";
        public static string RearPlateText = "";
        public static Texture PlateImage;
        public static Texture BackgroundImg;
        public static Color PlateTextColor;

        public static readonly Dictionary<Color, string> ColorPresets = new Dictionary<Color, string>
        {
            { Color.Black, "Black" },
            { Color.FromArgb(43, 49, 127), "Police Blue" },
            { Color.Red, "Red" },
            { Color.Yellow, "Yellow" },
            { Color.White, "White" }
        };

        public static readonly Color[] AvailableColors = { Color.Black, Color.FromArgb(43, 49, 127), Color.Red, Color.Yellow, Color.White };

        public static void InitializeLicensePlateDisplay()
        {
            try
            {
                PlateImage = Game.CreateTextureFromFile(PlateImagePath);
                BackgroundImg = Game.CreateTextureFromFile(BackgroundImgPath);
            }
            catch (Exception)
            {
                PlateImage = null;
                BackgroundImg = null;
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~r~Error Loading Images", "~o~Failed to load license plate/background image");
                Game.LogTrivial("ReportsPlusListener [ERROR]: Failed to load license plate images or background image!");
            }
        }

        public static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (PlateImage == null || BackgroundImg == null) return;

            if (!EnableDisplayOnFoot && !Main.CachedIsInVehicle) return;

            var bgWidth = BackgroundImg.Size.Width * BackgroundScale;
            var bgHeight = BackgroundImg.Size.Height * BackgroundScale;

            e.Graphics.DrawTexture(BackgroundImg, BackgroundPositionX, BackgroundPositionY, bgWidth, bgHeight);

            CalculatePositions(bgWidth, bgHeight, out var frontPos, out var rearPos);

            DrawPlate(e.Graphics, frontPos, TargetPlateWidth, TargetPlateHeight, "FRONT", FrontPlateText);
            DrawPlate(e.Graphics, rearPos, TargetPlateWidth, TargetPlateHeight, "REAR", RearPlateText);
        }

        private static void CalculatePositions(float bgWidth, float bgHeight, out PointF frontPos, out PointF rearPos)
        {
            var totalWidth = TargetPlateWidth * 2 + PlateSpacing;
            var startX = (bgWidth - totalWidth) / 2;

            var startY = (bgHeight - TargetPlateHeight) / 2 + LicensePlateVerticalOffset;

            frontPos = new PointF(BackgroundPositionX + startX, BackgroundPositionY + startY);

            rearPos = new PointF(frontPos.X + TargetPlateWidth + PlateSpacing, frontPos.Y);
        }

        private static void DrawPlate(Graphics g, PointF position, float width, float height, string plateTypeLabel, string plateText)
        {
            g.DrawTexture(PlateImage, position.X, position.Y, width, height);

            DrawCenteredText(g, plateTypeLabel, LabelFontSize, new PointF(position.X, position.Y - LabelVerticalOffset), width, Color.White);

            DrawCenteredText(g, plateText, PlateTextFontSize, new PointF(position.X, position.Y + PlateTextVerticalOffset), width, PlateTextColor);
        }

        private static void DrawCenteredText(Graphics g, string text, float fontSize, PointF basePosition, float plateWidth, Color color)
        {
            var textSize = Graphics.MeasureText(text, "HouseScript", fontSize);
            g.DrawText(text, "HouseScript", fontSize, new PointF(basePosition.X + plateWidth / 2 - textSize.Width / 2, basePosition.Y), color);
        }
    }
}