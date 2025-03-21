using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;

namespace ReportsPlus.Utils
{
    public static class Misc
    {
        public static readonly List<string> LosSantosAddresses = new List<string>
        {
            "Abattoir Avenue",
            "Abe Milton Parkway",
            "Ace Jones Drive",
            "Adam's Apple Boulevard",
            "Aguja Street",
            "Alta Place",
            "Alta Street",
            "Amarillo Vista",
            "Amarillo Way",
            "Americano Way",
            "Atlee Street",
            "Autopia Parkway",
            "Banham Canyon Drive",
            "Barbareno Road",
            "Bay City Avenue",
            "Bay City Incline",
            "Baytree Canyon Road",
            "Boulevard Del Perro",
            "Bridge Street",
            "Brouge Avenue",
            "Buccaneer Way",
            "Buen Vino Road",
            "Caesars Place",
            "Calais Avenue",
            "Capital Boulevard",
            "Carcer Way",
            "Carson Avenue",
            "Chum Street",
            "Chupacabra Street",
            "Clinton Avenue",
            "Cockingend Drive",
            "Conquistador Street",
            "Cortes Street",
            "Cougar Avenue",
            "Covenant Avenue",
            "Cox Way",
            "Crusade Road",
            "Davis Avenue",
            "Decker Street",
            "Didion Drive",
            "Dorset Drive",
            "Dorset Place",
            "Dry Dock Street",
            "Dunstable Drive",
            "Dunstable Lane",
            "Dutch London Street",
            "Eastbourne Way",
            "East Galileo Avenue",
            "East Mirror Drive",
            "Eclipse Boulevard",
            "Edwood Way",
            "Elgin Avenue",
            "El Burro Boulevard",
            "El Rancho Boulevard",
            "Equality Way",
            "Exceptionalists Way",
            "Fantastic Place",
            "Fenwell Place",
            "Forum Drive",
            "Fudge Lane",
            "Galileo Road",
            "Gentry Lane",
            "Ginger Street",
            "Glory Way",
            "Goma Street",
            "Greenwich Parkway",
            "Greenwich Place",
            "Greenwich Way",
            "Grove Street",
            "Hanger Way",
            "Hangman Avenue",
            "Hardy Way",
            "Hawick Avenue",
            "Heritage Way",
            "Hillcrest Avenue",
            "Hillcrest Ridge Access Road",
            "Imagination Court",
            "Industry Passage",
            "Ineseno Road",
            "Integrity Way",
            "Invention Court",
            "Innocence Boulevard",
            "Jamestown Street",
            "Kimble Hill Drive",
            "Kortz Drive",
            "Labor Place",
            "Laguna Place",
            "Lake Vinewood Drive",
            "Las Lagunas Boulevard",
            "Liberty Street",
            "Lindsay Circus",
            "Little Bighorn Avenue",
            "Low Power Street",
            "Macdonald Street",
            "Mad Wayne Thunder Drive",
            "Magellan Avenue",
            "Marathon Avenue",
            "Marlowe Drive",
            "Melanoma Street",
            "Meteor Street",
            "Milton Road",
            "Mirror Park Boulevard",
            "Mirror Place",
            "Morningwood Boulevard",
            "Mount Haan Drive",
            "Mount Haan Road",
            "Mount Vinewood Drive",
            "Movie Star Way",
            "Mutiny Road",
            "New Empire Way",
            "Nikola Avenue",
            "Nikola Place",
            "Normandy Drive",
            "North Archer Avenue",
            "North Conker Avenue",
            "North Sheldon Avenue",
            "North Rockford Drive",
            "Occupation Avenue",
            "Orchardville Avenue",
            "Palomino Avenue",
            "Peaceful Street",
            "Perth Street",
            "Picture Perfect Drive",
            "Plaice Place",
            "Playa Vista",
            "Popular Street",
            "Portola Drive",
            "Power Street",
            "Prosperity Street",
            "Prosperity Street Promenade",
            "Red Desert Avenue",
            "Richman Street",
            "Rockford Drive",
            "Roy Lowenstein Boulevard",
            "Rub Street",
            "San Andreas Avenue",
            "Sandcastle Way",
            "San Vitus Boulevard",
            "Senora Road",
            "Shank Street",
            "Signal Street",
            "Sinner Street",
            "Sinners Passage",
            "South Arsenal Street",
            "South Boulevard Del Perro",
            "South Mo Milton Drive",
            "South Rockford Drive",
            "South Shambles Street",
            "Spanish Avenue",
            "Steele Way",
            "Strangeways Drive",
            "Strawberry Avenue",
            "Supply Street",
            "Sustancia Road",
            "Swiss Street",
            "Tackle Street",
            "Tangerine Street",
            "Tongva Drive",
            "Tower Way",
            "Tug Street",
            "Utopia Gardens",
            "Vespucci Boulevard",
            "Vinewood Boulevard",
            "Vinewood Park Drive",
            "Vitus Street",
            "Voodoo Place",
            "West Eclipse Boulevard",
            "West Galileo Avenue",
            "West Mirror Drive",
            "Whispymound Drive",
            "Wild Oats Drive",
            "York Street",
            "Zancudo Barranca"
        };

        public static readonly List<string> BlaineCountyAddresses = new List<string>
        {
            "Algonquin Boulevard",
            "Alhambra Drive",
            "Armadillo Avenue",
            "Baytree Canyon Road",
            "Calafia Road",
            "Cascabel Avenue",
            "Cassidy Trail",
            "Cat-Claw Avenue",
            "Chianski Passage",
            "Cholla Road",
            "Cholla Springs Avenue",
            "Duluoz Avenue",
            "East Joshua Road",
            "Fort Zancudo Approach Road",
            "Galileo Road",
            "Grapeseed Avenue",
            "Grapeseed Main Street",
            "Joad Lane",
            "Joshua Road",
            "Lesbos Lane",
            "Lolita Avenue",
            "Marina Drive",
            "Meringue Lane",
            "Mount Haan Road",
            "Mountain View Drive",
            "Niland Avenue",
            "North Calafia Way",
            "Nowhere Road",
            "O'Neil Way",
            "Paleto Boulevard",
            "Panorama Drive",
            "Procopio Drive",
            "Procopio Promenade",
            "Pyrite Avenue",
            "Raton Pass",
            "Route 68 Approach",
            "Seaview Road",
            "Senora Way",
            "Smoke Tree Road",
            "Union Road",
            "Zancudo Avenue",
            "Zancudo Road",
            "Zancudo Trail"
        };

        public static readonly Dictionary<string, string> PedAddresses = new Dictionary<string, string>();

        public static readonly Dictionary<string, string> PedLicenseNumbers = new Dictionary<string, string>();

        public static readonly Dictionary<LHandle, string> CalloutIds = new Dictionary<LHandle, string>();

        public static bool IsPerformingPullover = false;

        public static Keys AnimationBind;
        public static Keys DiscardBind;

        internal static string FindPedModel(Ped ped)
        {
            try
            {
                if (ped == null || !ped.IsValid()) return "";
                ped.GetVariation(0, out var drawable, out var texture);
                return $"[{ped.Model.Name.ToLower()}][{drawable}][{texture}]";
            }
            catch
            {
                Game.LogTrivial("ReportsPlusListener: Error fetching model for ped: " + ped);
                return "";
            }
        }

        public static void CopyImageResourcesIfMissing()
        {
            try
            {
                var targetDir = Path.Combine(Main.FileResourcesFolder);

                var embeddedImages = new Dictionary<string, string>
                {
                    { "ALPRBackground.png", "ReportsPlus.Resources.images.ALPRBackground.png" },
                    { "LicensePlate.png", "ReportsPlus.Resources.images.LicensePlate.png" }
                };

                var assembly = Assembly.GetExecutingAssembly();
                foreach (var image in embeddedImages)
                {
                    var destPath = Path.Combine(targetDir, image.Key);

                    if (File.Exists(destPath)) continue;
                    using var stream = assembly.GetManifestResourceStream(image.Value);
                    if (stream == null) continue;

                    using var fileStream = File.Create(destPath);
                    stream.CopyTo(fileStream);

                    Game.LogTrivial("ReportsPlusListener: Copied Resource Image: " + image.Key + " to " + destPath);
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"Error copying images: {ex.Message}");
            }
        }

        public static void CleanupFiber(GameFiber fiber)
        {
            if (!(fiber is { IsAlive: true })) return;
            fiber.Abort();
        }
    }
}